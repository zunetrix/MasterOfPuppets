using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace MasterOfPuppets.Ipc;

/// <summary>
/// Core message bus: owns the async publish queue, the handler registry, and message dispatch.
/// Transport-agnostic - the actual wire is provided by <see cref="IIpcTransport"/>.
/// </summary>
internal sealed class IpcBus : IDisposable {
    private readonly IIpcTransport _transport;
    private readonly Channel<(byte[] data, bool includeSelf)> _channel =
        Channel.CreateUnbounded<(byte[], bool)>(new UnboundedChannelOptions { SingleReader = true });
    private readonly CancellationTokenSource _cts = new();
    private readonly Dictionary<IpcMessageType, Action<IpcMessage>> _handlers = new();

    public bool IsAvailable => _transport.IsAvailable;

    public IpcBus(IIpcTransport transport) {
        _transport = transport;
        _transport.MessageReceived += OnTransportMessageReceived;
        Task.Run(() => ProcessPublishQueue(_cts.Token));
    }

    /// <summary>
    /// Registers a handler for a specific message type.
    /// Only one handler per type is supported; later registrations overwrite earlier ones.
    /// </summary>
    public void RegisterHandler(IpcMessageType type, Action<IpcMessage> handler) {
        _handlers[type] = handler;
    }

    /// <summary>
    /// Enqueues a serialized payload for publishing. Fire-and-forget.
    /// </summary>
    /// <param name="serialized">The output of <see cref="IpcMessage.Serialize"/>.</param>
    /// <param name="includeSelf">When <c>true</c>, the message is also dispatched locally after publishing.</param>
    public void Broadcast(byte[] serialized, bool includeSelf = false) {
        if (!IsAvailable) return;
        _channel.Writer.TryWrite((serialized, includeSelf));
    }

    private void OnTransportMessageReceived(BinaryData payload) {
        try {
            var message = payload.ToArray().Decompress().ProtoDeserialize<IpcMessage>();
            Dispatch(message);
        } catch (Exception ex) {
            DalamudApi.PluginLog.Error(ex, "IpcBus: error processing incoming message");
        }
    }

    private void Dispatch(IpcMessage message) {
        if (_handlers.TryGetValue(message.MessageType, out var handler))
            handler(message);
        else
            DalamudApi.PluginLog.Warning($"IpcBus: no handler registered for {message.MessageType}");
    }

    private async Task ProcessPublishQueue(CancellationToken ct) {
        DalamudApi.PluginLog.Information("IpcBus: publish worker started");
        try {
            await foreach (var (data, includeSelf) in _channel.Reader.ReadAllAsync(ct)) {
                try {
                    await _transport.PublishAsync(new BinaryData(data), ct);
                    if (includeSelf) {
                        var message = data.Decompress().ProtoDeserialize<IpcMessage>();
                        Dispatch(message);
                    }
                } catch (Exception ex) {
                    DalamudApi.PluginLog.Warning(ex, "IpcBus: error publishing message");
                }
            }
        } catch (OperationCanceledException) { }
        DalamudApi.PluginLog.Information("IpcBus: publish worker stopped");
    }

    public void Dispose() {
        _cts.Cancel();
        _channel.Writer.Complete();
        _transport.MessageReceived -= OnTransportMessageReceived;
        _transport.Dispose();
        _cts.Dispose();
    }
}
