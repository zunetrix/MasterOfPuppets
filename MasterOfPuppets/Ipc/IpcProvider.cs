using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

using TinyIpc.IO;
using TinyIpc.Messaging;

namespace MasterOfPuppets.Ipc;

internal partial class IpcProvider : IDisposable {
    private readonly TinyMessageBus MessageBus;
    private readonly Channel<(byte[] serialized, bool includeSelf)> _channel =
        Channel.CreateUnbounded<(byte[], bool)>(new UnboundedChannelOptions { SingleReader = true });
    private readonly CancellationTokenSource _cts = new();
    private readonly Dictionary<IpcMessageType, Action<IpcMessage>> _ipcHandlers = new();
    private readonly bool _initFailed;

    private Plugin Plugin { get; }

    public IpcProvider(Plugin plugin) {
        Plugin = plugin;

        RegisterHandlersFromType(typeof(IpcProvider), this);

        try {
            const long maxFileSize = 1 << 24;
            MessageBus = new TinyMessageBus(new TinyMemoryMappedFile("MasterOfPuppets.IPC", maxFileSize), true);
            MessageBus.MessageReceived += OnMessageReceived;

            Task.Run(() => ProcessMessageQueue(_cts.Token));
        } catch (Exception e) {
            DalamudApi.PluginLog.Error(e, "TinyIpc init failed.");
            _initFailed = true;
        }
    }

    public void Dispose() {
        _cts.Cancel();
        _channel.Writer.Complete();
        _cts.Dispose();
        MessageBus.MessageReceived -= OnMessageReceived;
    }

    private void RegisterHandlersFromType(Type type, object instance) {
        foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)) {
            var attr = method.GetCustomAttribute<IpcHandleAttribute>();
            if (attr == null) continue;
            var del = (Action<IpcMessage>)Delegate.CreateDelegate(typeof(Action<IpcMessage>), instance, method);
            _ipcHandlers[attr.MessageType] = del;
        }
    }

    private void OnMessageReceived(object sender, TinyMessageReceivedEventArgs e) {
        if (_initFailed) return;
        try {
            var message = e.Message.ToArray<byte>().Decompress().ProtoDeserialize<IpcMessage>();
            if (_ipcHandlers.TryGetValue(message.MessageType, out var handler))
                handler(message);
            else
                DalamudApi.PluginLog.Warning($"No handler for {message.MessageType}");
        } catch (Exception ex) {
            DalamudApi.PluginLog.Error(ex, "Error processing IPC message");
        }
    }

    private async Task ProcessMessageQueue(CancellationToken ct) {
        DalamudApi.PluginLog.Information("IPC message queue worker started");
        try {
            await foreach (var (data, includeSelf) in _channel.Reader.ReadAllAsync(ct)) {
                try {
                    await MessageBus.PublishAsync(data);
                    if (includeSelf)
                        OnMessageReceived(null, new TinyMessageReceivedEventArgs(data));
                } catch (Exception e) {
                    DalamudApi.PluginLog.Warning(e, "Error publishing IPC");
                }
            }
        } catch (OperationCanceledException) { }
        DalamudApi.PluginLog.Information("IPC message queue worker ended");
    }

    public void BroadCast(byte[] serialized, bool includeSelf = false) {
        if (_initFailed || !Plugin.Config.SyncClients) return;
        _channel.Writer.TryWrite((serialized, includeSelf));
    }
}
