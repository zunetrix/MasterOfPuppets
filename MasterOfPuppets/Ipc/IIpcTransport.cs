using System;
using System.Threading;
using System.Threading.Tasks;

namespace MasterOfPuppets.Ipc;

/// <summary>
/// Pluggable transport used by <see cref="IpcBus"/> to publish and receive raw serialized message payloads.
/// Implementations are responsible for the actual messaging mechanism (e.g. shared memory, named pipes, sockets).
/// </summary>
internal interface IIpcTransport : IDisposable {
    /// <summary>
    /// Raised when a raw payload is received from any peer (never from self - deduplication is transport-level).
    /// </summary>
    event Action<byte[]> MessageReceived;

    /// <summary>
    /// Whether the transport was successfully initialized and is ready to send/receive.
    /// When <c>false</c>, <see cref="PublishAsync"/> is a no-op.
    /// </summary>
    bool IsAvailable { get; }

    /// <summary>
    /// Publishes a raw payload to all other subscribers of this transport channel.
    /// </summary>
    Task PublishAsync(byte[] payload, CancellationToken ct = default);
}
