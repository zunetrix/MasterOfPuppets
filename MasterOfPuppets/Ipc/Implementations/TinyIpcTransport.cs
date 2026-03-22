using System;
using System.Threading;
using System.Threading.Tasks;

using TinyIpc.IO;
using TinyIpc.Messaging;

namespace MasterOfPuppets.Ipc;

/// <summary>
/// IPC transport backed by TinyIPC (Windows shared-memory / memory-mapped files).
/// Enables broadcasting serialized <see cref="IpcMessage"/> payloads across multiple
/// FFXIV/Dalamud process instances running on the same machine.
/// </summary>
internal sealed class TinyIpcTransport : IIpcTransport {
    private readonly TinyMessageBus? _bus;

    public event Action<byte[]>? MessageReceived;
    public bool IsAvailable { get; }

    /// <param name="channelName">Name of the shared memory channel (must match across all instances).</param>
    /// <param name="maxFileSize">Maximum size of the underlying memory-mapped file, in bytes.</param>
    public TinyIpcTransport(string channelName = "MasterOfPuppets.IPC", long maxFileSize = 1 << 24) {
        try {
            _bus = new TinyMessageBus(new TinyMemoryMappedFile(channelName, maxFileSize), true);
            _bus.MessageReceived += OnBusMessageReceived;
            IsAvailable = true;
        } catch (Exception ex) {
            DalamudApi.PluginLog.Error(ex, "TinyIPC init failed.");
        }
    }

    private void OnBusMessageReceived(object? sender, TinyMessageReceivedEventArgs e) {
        MessageReceived?.Invoke(e.Message);
    }

    public Task PublishAsync(byte[] payload, CancellationToken ct = default) {
        if (!IsAvailable || _bus is null) return Task.CompletedTask;
        return _bus.PublishAsync(payload);
    }

    public void Dispose() {
        if (_bus is null) return;
        _bus.MessageReceived -= OnBusMessageReceived;
        _bus.Dispose();
    }
}
