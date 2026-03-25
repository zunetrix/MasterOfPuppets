using System;
using System.Threading;
using System.Threading.Tasks;

using XivIpc.Messaging;

namespace MasterOfPuppets.Ipc;

/// <summary>
/// IPC transport for Linux / Wine environments using the embedded XivIpc transport.
/// <para>
/// On Linux, the upstream TinyIpc Windows shared-memory backend is unavailable.
/// This transport uses an embedded copy of XivIpc which implements a serverless,
/// POSIX-compatible shared-memory bus using native Linux primitives (futex, shm_open, mmap).
/// </para>
/// <para>
/// <b>No additional setup is required.</b> This implementation operates completely
/// out-of-the-box in Wine/Proton and connects directly between plugin instances.
/// </para>
/// </summary>
internal sealed class LinuxIpcTransport : IIpcTransport {
    private readonly UnixSidecarTinyMessageBus? _bus;

    public event Action<byte[]>? MessageReceived;
    public bool IsAvailable { get; }

    /// <param name="channelName">Name of the IPC channel. Must match across all plugin instances.</param>
    public LinuxIpcTransport(string channelName = "MasterOfPuppets.IPC") {
        try {
            // Note: 1 << 24 (16MB) is the default payload size used in MOP
            _bus = new UnixSidecarTinyMessageBus(channelName, 1 << 24);
            _bus.MessageReceived += OnBusMessageReceived;
            IsAvailable = true;
        } catch (Exception ex) {
            DalamudApi.PluginLog.Error(ex, "LinuxIpcTransport: XivMessageBus init failed.");
        }
    }

    private void OnBusMessageReceived(object? sender, XivMessageReceivedEventArgs e) {
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
