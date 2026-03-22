using System;
using System.Reflection;

namespace MasterOfPuppets.Ipc;

/// <summary>
/// Coordinates all IPC handler partials. Transport-agnostic - depends only on
/// <see cref="IpcBus"/> which accepts any <see cref="IIpcTransport"/> implementation.
/// </summary>
internal partial class IpcProvider : IDisposable {
    private readonly IpcBus _bus;

    private Plugin Plugin { get; }

    public IpcProvider(Plugin plugin, IIpcTransport transport) {
        Plugin = plugin;
        _bus = new IpcBus(transport);
        RegisterHandlersFromType(typeof(IpcProvider), this);
    }

    /// <summary>
    /// Scans all methods decorated with <see cref="IpcHandleAttribute"/> and registers them in the bus.
    /// </summary>
    private void RegisterHandlersFromType(Type type, object instance) {
        foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)) {
            var attr = method.GetCustomAttribute<IpcHandleAttribute>();
            if (attr == null) continue;
            var del = (Action<IpcMessage>)Delegate.CreateDelegate(typeof(Action<IpcMessage>), instance, method);
            _bus.RegisterHandler(attr.MessageType, del);
        }
    }

    /// <summary>
    /// Enqueues a serialized <see cref="IpcMessage"/> for broadcast.
    /// No-ops when sync is disabled or the transport is unavailable.
    /// </summary>
    public void BroadCast(byte[] serialized, bool includeSelf = false) {
        if (!Plugin.Config.SyncClients) return;
        _bus.Broadcast(serialized, includeSelf);
    }

    public void Dispose() => _bus.Dispose();
}
