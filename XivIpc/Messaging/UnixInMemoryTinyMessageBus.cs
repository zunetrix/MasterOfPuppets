using System.Collections.Concurrent;

using XivIpc.Internal;

namespace XivIpc.Messaging;

internal sealed class UnixInMemoryTinyMessageBus : IXivMessageBus {
    private static readonly ConcurrentDictionary<string, ChannelState> Channels = new(StringComparer.Ordinal);

    private readonly string _channelName;
    private readonly int _maxPayloadBytes;
    private readonly Guid _instanceId = Guid.NewGuid();

    private readonly CancellationTokenSource _cts = new();
    private readonly ConcurrentQueue<byte[]> _pendingMessages = new();
    private readonly SemaphoreSlim _pendingSignal = new(0);
    private readonly Task _dispatchLoopTask;

    private readonly ChannelState _channelState;
    private bool _disposed;

    public UnixInMemoryTinyMessageBus(ChannelInfo channelInfo)
        : this(channelInfo.Name, checked(channelInfo.Size)) {
    }

    public UnixInMemoryTinyMessageBus(string channelName, int maxPayloadBytes) {
        if (string.IsNullOrWhiteSpace(channelName))
            throw new ArgumentException("Channel name must be provided.", nameof(channelName));

        if (maxPayloadBytes <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxPayloadBytes));

        _channelName = channelName;
        _maxPayloadBytes = maxPayloadBytes;

        _channelState = Channels.GetOrAdd(
            _channelName,
            static (name, state) => new ChannelState(name, state.MaxPayloadBytes),
            new ChannelSeed(maxPayloadBytes));

        _channelState.AddMember(this, maxPayloadBytes);

        TinyIpcLogger.Info(
            nameof(UnixInMemoryTinyMessageBus),
            "Initialized",
            "Initialized in-memory TinyMessageBus.",
            ("channel", _channelName),
            ("instanceId", _instanceId),
            ("maxPayloadBytes", _maxPayloadBytes),
            ("memberCount", _channelState.MemberCount));

        _dispatchLoopTask = Task.Run(() => DispatchLoopAsync(_cts.Token));
    }

    public event EventHandler<XivMessageReceivedEventArgs>? MessageReceived;

    public Task PublishAsync(byte[] message) {
        ArgumentNullException.ThrowIfNull(message);
        ThrowIfDisposed();

        if (message.Length > _maxPayloadBytes)
            throw new InvalidOperationException(
                $"Message length {message.Length} exceeds configured max {_maxPayloadBytes}.");

        if (TinyIpcLogger.IsEnabled(TinyIpcLogLevel.Debug)) {
            TinyIpcLogger.Debug(
                nameof(UnixInMemoryTinyMessageBus),
                "Publish",
                "Publishing message to in-memory bus.",
                ("channel", _channelName),
                ("instanceId", _instanceId),
                ("bytes", message.Length),
                ("payloadPreview", TinyIpcLogger.CreatePayloadPreview(message)));
        }

        _channelState.Publish(this, message);
        return Task.CompletedTask;
    }

    public void Dispose() {
        if (_disposed)
            return;

        _disposed = true;

        TinyIpcLogger.Info(
            nameof(UnixInMemoryTinyMessageBus),
            "Dispose",
            "Disposing in-memory TinyMessageBus.",
            ("channel", _channelName),
            ("instanceId", _instanceId));

        try {
            _cts.Cancel();
        } catch (Exception ex) {
            TinyIpcLogger.Warning(
                nameof(UnixInMemoryTinyMessageBus),
                "CancelFailed",
                "Failed to cancel in-memory bus tasks.",
                ex,
                ("channel", _channelName));
        }

        try {
            _pendingSignal.Release();
        } catch {
        }

        try {
            _channelState.RemoveMember(this);
            if (_channelState.MemberCount == 0)
                Channels.TryRemove(_channelName, out _);
        } catch (Exception ex) {
            TinyIpcLogger.Warning(
                nameof(UnixInMemoryTinyMessageBus),
                "RemoveMemberFailed",
                "Failed removing in-memory bus from channel state.",
                ex,
                ("channel", _channelName));
        }

        try {
            _dispatchLoopTask.Wait(TimeSpan.FromSeconds(2));
        } catch (Exception ex) {
            TinyIpcLogger.Warning(
                nameof(UnixInMemoryTinyMessageBus),
                "WaitFailed",
                "Timed out or failed waiting for dispatch loop.",
                ex,
                ("channel", _channelName));
        }

        try {
            _pendingSignal.Dispose();
        } catch (Exception ex) {
            TinyIpcLogger.Warning(
                nameof(UnixInMemoryTinyMessageBus),
                "SignalDisposeFailed",
                "Failed to dispose pending signal.",
                ex,
                ("channel", _channelName));
        }

        try {
            _cts.Dispose();
        } catch (Exception ex) {
            TinyIpcLogger.Warning(
                nameof(UnixInMemoryTinyMessageBus),
                "CtsDisposeFailed",
                "Failed to dispose cancellation source.",
                ex,
                ("channel", _channelName));
        }
    }

    public ValueTask DisposeAsync() {
        Dispose();
        return ValueTask.CompletedTask;
    }

    internal Guid InstanceId => _instanceId;

    internal int MaxPayloadBytes => _maxPayloadBytes;

    internal void EnqueueForDelivery(byte[] payload) {
        if (_disposed)
            return;

        _pendingMessages.Enqueue(payload);

        try {
            _pendingSignal.Release();
        } catch (ObjectDisposedException) {
        } catch (SemaphoreFullException) {
        }
    }

    private async Task DispatchLoopAsync(CancellationToken cancellationToken) {
        while (!cancellationToken.IsCancellationRequested) {
            try {
                await _pendingSignal.WaitAsync(cancellationToken).ConfigureAwait(false);
            } catch (OperationCanceledException) {
                break;
            }

            while (!_disposed && _pendingMessages.TryDequeue(out byte[]? payload)) {
                try {
                    MessageReceived?.Invoke(this, new XivMessageReceivedEventArgs(payload));
                } catch (Exception ex) {
                    TinyIpcLogger.Error(
                        nameof(UnixInMemoryTinyMessageBus),
                        "MessageHandlerFailed",
                        "A MessageReceived handler threw an exception.",
                        ex,
                        ("channel", _channelName),
                        ("bytes", payload.Length));
                }
            }
        }
    }

    private void ThrowIfDisposed() {
        if (_disposed)
            throw new ObjectDisposedException(nameof(UnixInMemoryTinyMessageBus));
    }

    private sealed class ChannelState {
        private readonly object _gate = new();
        private readonly HashSet<UnixInMemoryTinyMessageBus> _members = new();

        public ChannelState(string channelName, int maxPayloadBytes) {
            ChannelName = channelName;
            MaxPayloadBytes = maxPayloadBytes;
        }

        public string ChannelName { get; }

        public int MaxPayloadBytes { get; }

        public int MemberCount {
            get {
                lock (_gate)
                    return _members.Count;
            }
        }

        public void AddMember(UnixInMemoryTinyMessageBus member, int requestedMaxPayloadBytes) {
            lock (_gate) {
                if (requestedMaxPayloadBytes > MaxPayloadBytes) {
                    throw new InvalidOperationException(
                        $"Existing in-memory bus for channel '{ChannelName}' has payload capacity {MaxPayloadBytes} bytes, " +
                        $"which is smaller than requested {requestedMaxPayloadBytes} bytes.");
                }

                _members.Add(member);
            }
        }

        public void RemoveMember(UnixInMemoryTinyMessageBus member) {
            lock (_gate) {
                _members.Remove(member);
            }
        }

        public void Publish(UnixInMemoryTinyMessageBus sender, byte[] message) {
            UnixInMemoryTinyMessageBus[] recipients;

            lock (_gate) {
                recipients = new UnixInMemoryTinyMessageBus[_members.Count];
                _members.CopyTo(recipients);
            }

            foreach (UnixInMemoryTinyMessageBus recipient in recipients) {
                if (ReferenceEquals(recipient, sender))
                    continue;

                if (recipient._disposed)
                    continue;

                if (message.Length > recipient.MaxPayloadBytes) {
                    TinyIpcLogger.Warning(
                        nameof(UnixInMemoryTinyMessageBus),
                        "RecipientCapacityTooSmall",
                        "Skipping delivery to recipient because its configured max payload is smaller than the published message.",
                        null,
                        ("channel", ChannelName),
                        ("senderInstanceId", sender.InstanceId),
                        ("recipientInstanceId", recipient.InstanceId),
                        ("messageBytes", message.Length),
                        ("recipientMaxPayloadBytes", recipient.MaxPayloadBytes));

                    continue;
                }

                byte[] payloadCopy = message.Length == 0 ? Array.Empty<byte>() : (byte[])message.Clone();
                recipient.EnqueueForDelivery(payloadCopy);
            }
        }
    }

    private readonly record struct ChannelSeed(int MaxPayloadBytes);
}
