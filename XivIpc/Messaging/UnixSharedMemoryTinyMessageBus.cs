using System.Collections.Concurrent;
using System.Globalization;
using System.Runtime.CompilerServices;

using XivIpc.Internal;

namespace XivIpc.Messaging;

public sealed class UnixSharedMemoryTinyMessageBus : IXivMessageBus {
    private const uint BusMagic = 0x54494253;   // "TIBS"
    private const uint SlotMagic = 0x5449534C;  // "TISL"
    private const int BusVersion = 3;

    private const int HeaderMagicOffset = 0;        // uint
    private const int HeaderVersionOffset = 4;      // int
    private const int HeaderSlotCountOffset = 8;    // int
    private const int HeaderSlotPayloadOffset = 12; // int
    private const int HeaderHeadSeqOffset = 16;     // long
    private const int HeaderTailSeqOffset = 24;     // long
    private const int HeaderTtlMsOffset = 32;       // long
    private const int HeaderWakeSeqOffset = 40;     // int
    private const int HeaderReservedOffset = 44;    // int
    private const int HeaderGenerationOffset = 48;  // Guid (16 bytes)
    private const int HeaderSize = 64;

    private const int SlotMagicOffset = 0;          // uint
    private const int SlotPayloadLenOffset = 4;     // int
    private const int SlotSequenceOffset = 8;       // long
    private const int SlotTimestampMsOffset = 16;   // long
    private const int SlotSenderGuidOffset = 24;    // Guid (16 bytes)
    private const int SlotReservedOffset = 40;      // int
    private const int SlotHeaderSize = 44;

    private const int MinimumSlotCount = 4;
    private const int DefaultSlotCount = 64;
    private const long DefaultMessageTtlMs = 1_000;
    private const int FutexWaitTimeoutMs = 1_000;

    private const string MetadataMagic = "TINYIPC_BUS_META";
    private const int MetadataVersion = 2;

    private readonly string _channelName;
    private readonly int _requestedBufferBytes;
    private readonly RingSizing _sizing;
    private readonly UnixSharedFileLock _writerLock;
    private readonly UnixSharedMemoryRegion _region;
    private readonly string _metadataPath;

    private readonly CancellationTokenSource _cts = new();
    private readonly Guid _instanceId = Guid.NewGuid();
    private readonly ConcurrentQueue<byte[]> _pendingMessages = new();
    private readonly SemaphoreSlim _pendingSignal = new(0);
    private readonly Task _subscriberLoopTask;
    private readonly Task _dispatchLoopTask;

    private Guid _generationId;
    private long _nextSequence;
    private bool _disposed;

    internal UnixSharedMemoryTinyMessageBus(ChannelInfo channelInfo)
        : this(channelInfo.Name, checked(channelInfo.Size)) {
    }

    public UnixSharedMemoryTinyMessageBus(string channelName, int maxPayloadBytes) {
        if (string.IsNullOrWhiteSpace(channelName))
            throw new ArgumentException("Channel name must be provided.", nameof(channelName));

        if (maxPayloadBytes <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxPayloadBytes));

        _channelName = channelName;
        _requestedBufferBytes = maxPayloadBytes;

        int requestedSlotCount = ResolveSlotCount();
        _sizing = RingSizingPolicy.Compute(maxPayloadBytes, requestedSlotCount, HeaderSize, SlotHeaderSize);
        int requestedImageSize = _sizing.ImageSize;

        UnixSharedStorageHelpers.EnsureSharedDirectoryExists();
        _metadataPath = UnixSharedStorageHelpers.BuildSharedFilePath(channelName, "busmeta");

        BusMetadata desiredMetadata = BuildDesiredMetadata();
        BusMetadata? existingMetadata = TryReadMetadata();

        if (existingMetadata is not null && !IsCompatible(existingMetadata, desiredMetadata)) {
            throw new InvalidOperationException(
                $"Existing shared bus metadata for channel '{_channelName}' is incompatible. " +
                $"Existing slotCount={existingMetadata.SlotCount}, slotPayloadBytes={existingMetadata.SlotPayloadBytes}, imageSize={existingMetadata.ImageSize}, generationId={existingMetadata.GenerationId}; " +
                $"requested slotCount={desiredMetadata.SlotCount}, slotPayloadBytes={desiredMetadata.SlotPayloadBytes}, imageSize={desiredMetadata.ImageSize}.");
        }

        int mappedImageSize = existingMetadata is null
            ? requestedImageSize
            : Math.Max(requestedImageSize, existingMetadata.ImageSize);

        _writerLock = new UnixSharedFileLock(channelName, "buslock");
        _region = new UnixSharedMemoryRegion(channelName, "bus_shm", mappedImageSize, _writerLock, recreateIfSizeMismatch: true);

        TinyIpcLogger.Info(
            nameof(UnixSharedMemoryTinyMessageBus),
            "Initialized",
            "Initialized shared-memory TinyMessageBus.",
            ("channel", _channelName),
            ("instanceId", _instanceId),
            ("requestedBufferBytes", _requestedBufferBytes),
            ("effectiveBudgetBytes", _sizing.BudgetBytes),
            ("effectiveSlotPayloadBytes", _sizing.SlotPayloadBytes),
            ("requestedSlotCount", _sizing.SlotCount),
            ("requestedImageSize", requestedImageSize),
            ("mappedImageSize", mappedImageSize),
            ("budgetWasCapped", _sizing.WasCapped),
            ("metadataPath", _metadataPath));

        EnsureInitialized(existingMetadata, desiredMetadata);
        _nextSequence = CaptureCurrentHeadSequence();

        _subscriberLoopTask = Task.Run(() => SubscriberLoopAsync(_cts.Token));
        _dispatchLoopTask = Task.Run(() => DispatchLoopAsync(_cts.Token));
    }

    public event EventHandler<XivMessageReceivedEventArgs>? MessageReceived;

    public unsafe Task PublishAsync(byte[] message) {
        ArgumentNullException.ThrowIfNull(message);
        ThrowIfDisposed();

        if (message.Length > _sizing.SlotPayloadBytes) {
            throw new InvalidOperationException(
                $"Message length {message.Length} exceeds the configured per-message capacity of {_sizing.SlotPayloadBytes} bytes. " +
                $"requestedBufferBytes={_requestedBufferBytes}, effectiveBudgetBytes={_sizing.BudgetBytes}.");
        }

        if (TinyIpcLogger.IsEnabled(TinyIpcLogLevel.Debug)) {
            TinyIpcLogger.Debug(
                nameof(UnixSharedMemoryTinyMessageBus),
                "Publish",
                "Publishing message to shared-memory bus.",
                ("channel", _channelName),
                ("bytes", message.Length),
                ("payloadPreview", TinyIpcLogger.CreatePayloadPreview(message)));
        }

        _writerLock.Execute(() => {
            byte* image = (byte*)_region.Pointer;
            EnsureValidatedImage(image, _region.Length);

            int slotCount = ReadInt32(image, HeaderSlotCountOffset);
            int slotPayloadBytes = ReadInt32(image, HeaderSlotPayloadOffset);
            long head = ReadInt64(image, HeaderHeadSeqOffset);
            long tail = ReadInt64(image, HeaderTailSeqOffset);
            long ttlMs = ReadInt64(image, HeaderTtlMsOffset);

            if (message.Length > slotPayloadBytes)
                throw new InvalidOperationException($"Message length {message.Length} exceeds the configured per-message capacity of {slotPayloadBytes} bytes.");

            long nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            tail = PruneExpired(image, slotCount, slotPayloadBytes, head, tail, nowMs, ttlMs);

            if (head - tail >= slotCount)
                tail = head - slotCount + 1;

            int slotIndex = checked((int)(head % slotCount));
            int slotOffset = GetSlotOffset(slotIndex, slotPayloadBytes);
            ClearBytes(image + slotOffset, SlotHeaderSize + slotPayloadBytes);

            if (message.Length > 0) {
                fixed (byte* messagePtr = message)
                    Buffer.MemoryCopy(messagePtr, image + slotOffset + SlotHeaderSize, slotPayloadBytes, message.Length);
            }

            WriteInt32(image, slotOffset + SlotPayloadLenOffset, message.Length);
            WriteInt64(image, slotOffset + SlotSequenceOffset, head);
            WriteInt64(image, slotOffset + SlotTimestampMsOffset, nowMs);
            WriteGuid(image, slotOffset + SlotSenderGuidOffset, _instanceId);
            WriteInt32(image, slotOffset + SlotReservedOffset, 0);
            Volatile.Write(ref *(uint*)(image + slotOffset + SlotMagicOffset), SlotMagic);

            head++;
            Volatile.Write(ref *(long*)(image + HeaderHeadSeqOffset), head);
            Volatile.Write(ref *(long*)(image + HeaderTailSeqOffset), tail);
            Interlocked.Increment(ref *(int*)(image + HeaderWakeSeqOffset));
        });

        WakeSubscribers();
        return Task.CompletedTask;
    }

    public void Dispose() {
        if (_disposed)
            return;

        _disposed = true;

        TinyIpcLogger.Info(
            nameof(UnixSharedMemoryTinyMessageBus),
            "Dispose",
            "Disposing shared-memory TinyMessageBus.",
            ("channel", _channelName),
            ("instanceId", _instanceId),
            ("generationId", _generationId));

        try { _cts.Cancel(); } catch (Exception ex) {
            TinyIpcLogger.Warning(nameof(UnixSharedMemoryTinyMessageBus), "CancelFailed", "Failed to cancel shared-memory bus tasks.", ex, ("channel", _channelName));
        }

        try { WakeSubscribers(); } catch (Exception ex) {
            TinyIpcLogger.Warning(nameof(UnixSharedMemoryTinyMessageBus), "WakeFailed", "Failed to wake subscribers during dispose.", ex, ("channel", _channelName));
        }

        try { _pendingSignal.Release(); } catch {
        }

        try { Task.WaitAll(new[] { _subscriberLoopTask, _dispatchLoopTask }, TimeSpan.FromSeconds(2)); } catch (Exception ex) {
            TinyIpcLogger.Warning(nameof(UnixSharedMemoryTinyMessageBus), "WaitFailed", "Timed out or failed waiting for bus tasks.", ex, ("channel", _channelName));
        }

        try { _pendingSignal.Dispose(); } catch (Exception ex) {
            TinyIpcLogger.Warning(nameof(UnixSharedMemoryTinyMessageBus), "SignalDisposeFailed", "Failed to dispose pending signal.", ex, ("channel", _channelName));
        }

        try { _cts.Dispose(); } catch (Exception ex) {
            TinyIpcLogger.Warning(nameof(UnixSharedMemoryTinyMessageBus), "CtsDisposeFailed", "Failed to dispose cancellation source.", ex, ("channel", _channelName));
        }

        try { _region.Dispose(); } catch (Exception ex) {
            TinyIpcLogger.Warning(nameof(UnixSharedMemoryTinyMessageBus), "RegionDisposeFailed", "Failed to dispose shared-memory region.", ex, ("channel", _channelName));
        }
    }

    public ValueTask DisposeAsync() {
        Dispose();
        return ValueTask.CompletedTask;
    }

    private unsafe void EnsureInitialized(BusMetadata? existingMetadata, BusMetadata desiredMetadata) {
        _writerLock.Execute(() => {
            byte* image = (byte*)_region.Pointer;
            ImageValidationResult imageValidation = ValidateImage(image, _region.Length);

            if (existingMetadata is not null && !IsCompatible(existingMetadata, desiredMetadata)) {
                throw new InvalidOperationException(
                    $"Existing shared bus metadata for channel '{_channelName}' is incompatible. " +
                    $"Existing slotCount={existingMetadata.SlotCount}, slotPayloadBytes={existingMetadata.SlotPayloadBytes}, imageSize={existingMetadata.ImageSize}, generationId={existingMetadata.GenerationId}; " +
                    $"requested slotCount={desiredMetadata.SlotCount}, slotPayloadBytes={desiredMetadata.SlotPayloadBytes}, imageSize={desiredMetadata.ImageSize}.");
            }

            if (imageValidation.IsValid) {
                EnsureCompatibleWithLocalInstance(image);

                Guid imageGeneration = ReadGuid(image, HeaderGenerationOffset);

                if (existingMetadata is null) {
                    BusMetadata rebuilt = BuildMetadataFromImage(image, imageGeneration);
                    WriteMetadata(rebuilt);
                    _generationId = rebuilt.GenerationId;
                } else {
                    _generationId = existingMetadata.GenerationId;

                    if (imageGeneration == Guid.Empty || imageGeneration != existingMetadata.GenerationId) {
                        WriteGuid(image, HeaderGenerationOffset, existingMetadata.GenerationId);
                        _generationId = existingMetadata.GenerationId;
                    }
                }

                return;
            }

            if (existingMetadata is not null &&
                imageValidation.FailureReason == ImageValidationFailureReason.MappedLengthTooSmall) {
                throw new InvalidOperationException(
                    $"Existing shared-memory bus image for channel '{_channelName}' requires a larger mapping. " +
                    $"MappedLength={_region.Length}, RequiredLength={imageValidation.RequiredLength}, " +
                    $"HeaderSlotCount={imageValidation.SlotCount}, HeaderSlotPayloadBytes={imageValidation.SlotPayloadBytes}, " +
                    $"MetadataImageSize={existingMetadata.ImageSize}. " +
                    $"This usually indicates the region was opened with a smaller local size than the existing bus.");
            }

            CreateEmptyImage(image, desiredMetadata);
            WriteMetadata(desiredMetadata);
            _generationId = desiredMetadata.GenerationId;

            TinyIpcLogger.Info(
                nameof(UnixSharedMemoryTinyMessageBus),
                "RegionInitialized",
                "Initialized shared-memory region and metadata.",
                ("channel", _channelName),
                ("slotCount", desiredMetadata.SlotCount),
                ("slotPayloadBytes", desiredMetadata.SlotPayloadBytes),
                ("imageSize", desiredMetadata.ImageSize),
                ("generationId", desiredMetadata.GenerationId));
        });
    }

    private async Task SubscriberLoopAsync(CancellationToken cancellationToken) {
        while (!cancellationToken.IsCancellationRequested && !_disposed) {
            try {
                DrainAvailableMessages();
            } catch (OperationCanceledException) {
                break;
            } catch (Exception ex) {
                TinyIpcLogger.Error(
                    nameof(UnixSharedMemoryTinyMessageBus),
                    "SubscriberLoopFailed",
                    "Shared-memory subscriber loop encountered an unexpected exception.",
                    ex,
                    ("channel", _channelName));
            }

            if (_disposed || cancellationToken.IsCancellationRequested)
                break;

            int expectedWake;
            unsafe {
                byte* image = (byte*)_region.Pointer;
                expectedWake = Volatile.Read(ref *(int*)(image + HeaderWakeSeqOffset));
            }

            DrainAvailableMessages();

            if (_disposed || cancellationToken.IsCancellationRequested)
                break;

            bool shouldWait;
            unsafe {
                byte* image = (byte*)_region.Pointer;
                shouldWait = expectedWake == Volatile.Read(ref *(int*)(image + HeaderWakeSeqOffset));
            }

            if (shouldWait) {
                try {
                    unsafe {
                        byte* image = (byte*)_region.Pointer;
                        LinuxFutex.Wait((int*)(image + HeaderWakeSeqOffset), expectedWake, FutexWaitTimeoutMs);
                    }
                } catch (Exception ex) {
                    TinyIpcLogger.Warning(
                        nameof(UnixSharedMemoryTinyMessageBus),
                        "FutexWaitFailed",
                        "Futex wait failed; falling back to delay.",
                        ex,
                        ("channel", _channelName));

                    await Task.Delay(5, cancellationToken).ConfigureAwait(false);
                }
            }
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
                        nameof(UnixSharedMemoryTinyMessageBus),
                        "MessageHandlerFailed",
                        "A MessageReceived handler threw an exception.",
                        ex,
                        ("channel", _channelName),
                        ("bytes", payload.Length));
                }
            }
        }
    }

    private unsafe void DrainAvailableMessages() {
        byte* image = (byte*)_region.Pointer;
        if (!TryValidateImage(image, _region.Length))
            return;

        int slotCount = ReadInt32(image, HeaderSlotCountOffset);
        int slotPayloadBytes = ReadInt32(image, HeaderSlotPayloadOffset);
        long head = Volatile.Read(ref *(long*)(image + HeaderHeadSeqOffset));
        long tail = Volatile.Read(ref *(long*)(image + HeaderTailSeqOffset));
        long ttlMs = ReadInt64(image, HeaderTtlMsOffset);
        long nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        if (_nextSequence < tail) {
            TinyIpcLogger.Warning(
                nameof(UnixSharedMemoryTinyMessageBus),
                "SequenceAdvanced",
                "Subscriber sequence fell behind retained tail and was advanced.",
                null,
                ("channel", _channelName),
                ("previousSequence", _nextSequence),
                ("tail", tail));

            _nextSequence = tail;
        }

        while (_nextSequence < head) {
            int slotIndex = checked((int)(_nextSequence % slotCount));
            int slotOffset = GetSlotOffset(slotIndex, slotPayloadBytes);

            uint slotMagic = Volatile.Read(ref *(uint*)(image + slotOffset + SlotMagicOffset));
            int payloadLen = ReadInt32(image, slotOffset + SlotPayloadLenOffset);
            long sequence = ReadInt64(image, slotOffset + SlotSequenceOffset);
            long timestampMs = ReadInt64(image, slotOffset + SlotTimestampMsOffset);

            if (slotMagic != SlotMagic ||
                sequence != _nextSequence ||
                payloadLen < 0 ||
                payloadLen > slotPayloadBytes) {
                _nextSequence++;
                continue;
            }

            if (ttlMs > 0 && nowMs - timestampMs > ttlMs) {
                _nextSequence++;
                continue;
            }

            Guid senderId = ReadGuid(image, slotOffset + SlotSenderGuidOffset);
            if (senderId != _instanceId) {
                byte[] payload = new byte[payloadLen];
                if (payloadLen > 0)
                    MarshalCopy(image + slotOffset + SlotHeaderSize, payload);

                _pendingMessages.Enqueue(payload);
                _pendingSignal.Release();
            }

            _nextSequence++;
        }
    }

    private unsafe long CaptureCurrentHeadSequence() {
        byte* image = (byte*)_region.Pointer;
        if (!TryValidateImage(image, _region.Length))
            return 0;

        return Volatile.Read(ref *(long*)(image + HeaderHeadSeqOffset));
    }

    private unsafe void WakeSubscribers() {
        byte* image = (byte*)_region.Pointer;
        if (image == null)
            return;

        LinuxFutex.WakeAll((int*)(image + HeaderWakeSeqOffset));
    }

    private unsafe void EnsureValidatedImage(byte* image, int imageLength) {
        ImageValidationResult result = ValidateImage(image, imageLength);
        if (!result.IsValid) {
            throw new InvalidOperationException(
                $"Existing shared-memory bus image could not be validated for channel '{_channelName}'. " +
                $"Reason={result.FailureReason}, MappedLength={imageLength}, RequiredLength={result.RequiredLength}, " +
                $"HeaderSlotCount={result.SlotCount}, HeaderSlotPayloadBytes={result.SlotPayloadBytes}, " +
                $"HeaderVersion={result.Version}, Head={result.Head}, Tail={result.Tail}, GenerationId={result.GenerationId}.");
        }

        EnsureCompatibleWithLocalInstance(image);
    }

    private unsafe void EnsureCompatibleWithLocalInstance(byte* image) {
        int slotCount = ReadInt32(image, HeaderSlotCountOffset);
        int slotPayloadBytes = ReadInt32(image, HeaderSlotPayloadOffset);
        Guid generation = ReadGuid(image, HeaderGenerationOffset);

        if (slotPayloadBytes < _sizing.SlotPayloadBytes) {
            throw new InvalidOperationException(
                $"Existing shared bus payload capacity is {slotPayloadBytes} bytes, which is smaller than requested {_sizing.SlotPayloadBytes} bytes. " +
                $"requestedBufferBytes={_requestedBufferBytes}, effectiveBudgetBytes={_sizing.BudgetBytes}.");
        }

        if (slotCount < _sizing.SlotCount) {
            throw new InvalidOperationException(
                $"Existing shared bus slot count is {slotCount}, which is smaller than requested {_sizing.SlotCount}.");
        }

        if (generation == Guid.Empty)
            throw new InvalidOperationException("Existing shared bus generation id is missing.");
    }

    private BusMetadata BuildDesiredMetadata() {
        long ttlMs = ResolveMessageTtlMs();

        return new BusMetadata(
            MetadataMagic,
            MetadataVersion,
            _channelName,
            _sizing.SlotCount,
            _sizing.SlotPayloadBytes,
            _sizing.ImageSize,
            ttlMs,
            Guid.NewGuid(),
            DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            RuntimeEnvironmentDetector.GetCurrentProcessId());
    }

    private unsafe BusMetadata BuildMetadataFromImage(byte* image, Guid generationId) {
        int slotCount = ReadInt32(image, HeaderSlotCountOffset);
        int slotPayloadBytes = ReadInt32(image, HeaderSlotPayloadOffset);
        long ttlMs = ReadInt64(image, HeaderTtlMsOffset);
        int imageSize = ComputeRequiredImageSize(slotCount, slotPayloadBytes);

        return new BusMetadata(
            MetadataMagic,
            MetadataVersion,
            _channelName,
            slotCount,
            slotPayloadBytes,
            imageSize,
            ttlMs,
            generationId == Guid.Empty ? Guid.NewGuid() : generationId,
            DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            RuntimeEnvironmentDetector.GetCurrentProcessId());
    }

    private BusMetadata? TryReadMetadata() {
        try {
            if (!File.Exists(_metadataPath))
                return null;

            Dictionary<string, string> values = new(StringComparer.OrdinalIgnoreCase);
            foreach (string rawLine in File.ReadAllLines(_metadataPath)) {
                if (string.IsNullOrWhiteSpace(rawLine))
                    continue;

                int separator = rawLine.IndexOf('=');
                if (separator <= 0)
                    continue;

                string key = rawLine[..separator].Trim();
                string value = rawLine[(separator + 1)..].Trim();
                values[key] = value;
            }

            if (!values.TryGetValue("magic", out string? magic) || !string.Equals(magic, MetadataMagic, StringComparison.Ordinal))
                return null;

            if (!TryReadInt32(values, "version", out int version) || version != MetadataVersion)
                return null;

            if (!values.TryGetValue("channel", out string? channel) || !string.Equals(channel, _channelName, StringComparison.Ordinal))
                return null;

            if (!TryReadInt32(values, "slotCount", out int slotCount) || slotCount < MinimumSlotCount)
                return null;

            if (!TryReadInt32(values, "slotPayloadBytes", out int slotPayloadBytes) || slotPayloadBytes <= 0)
                return null;

            if (!TryReadInt32(values, "imageSize", out int imageSize) || imageSize < HeaderSize)
                return null;

            if (!TryReadInt64(values, "ttlMs", out long ttlMs) || ttlMs < 0)
                return null;

            if (!values.TryGetValue("generationId", out string? generationRaw) ||
                !Guid.TryParse(generationRaw, out Guid generationId) ||
                generationId == Guid.Empty) {
                return null;
            }

            long createdUnixMs = 0;
            if (values.TryGetValue("createdUnixMs", out string? createdRaw))
                _ = long.TryParse(createdRaw, NumberStyles.Integer, CultureInfo.InvariantCulture, out createdUnixMs);

            int creatorPid = 0;
            if (values.TryGetValue("creatorPid", out string? pidRaw))
                _ = int.TryParse(pidRaw, NumberStyles.Integer, CultureInfo.InvariantCulture, out creatorPid);

            return new BusMetadata(
                magic,
                version,
                channel,
                slotCount,
                slotPayloadBytes,
                imageSize,
                ttlMs,
                generationId,
                createdUnixMs,
                creatorPid);
        } catch (Exception ex) {
            TinyIpcLogger.Warning(
                nameof(UnixSharedMemoryTinyMessageBus),
                "MetadataReadFailed",
                "Failed to read shared-memory bus metadata.",
                ex,
                ("channel", _channelName),
                ("metadataPath", _metadataPath));

            return null;
        }
    }

    private void WriteMetadata(BusMetadata metadata) {
        try {
            string directory = Path.GetDirectoryName(_metadataPath) ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(directory))
                Directory.CreateDirectory(directory);

            string tempPath = _metadataPath + ".tmp";

            string[] lines =
            {
                    $"magic={metadata.Magic}",
                    $"version={metadata.Version.ToString(CultureInfo.InvariantCulture)}",
                    $"channel={metadata.Channel}",
                    $"slotCount={metadata.SlotCount.ToString(CultureInfo.InvariantCulture)}",
                    $"slotPayloadBytes={metadata.SlotPayloadBytes.ToString(CultureInfo.InvariantCulture)}",
                    $"imageSize={metadata.ImageSize.ToString(CultureInfo.InvariantCulture)}",
                    $"ttlMs={metadata.TtlMs.ToString(CultureInfo.InvariantCulture)}",
                    $"generationId={metadata.GenerationId:D}",
                    $"createdUnixMs={metadata.CreatedUnixMs.ToString(CultureInfo.InvariantCulture)}",
                    $"creatorPid={metadata.CreatorPid.ToString(CultureInfo.InvariantCulture)}"
                };

            File.WriteAllLines(tempPath, lines);
            File.Move(tempPath, _metadataPath, overwrite: true);

            try {
                UnixSharedStorageHelpers.ApplyPermissions(_metadataPath, isDirectory: false);
            } catch {
            }
        } catch (Exception ex) {
            throw new InvalidOperationException(
                $"Failed to write shared-memory metadata file '{_metadataPath}' for channel '{_channelName}'.",
                ex);
        }
    }

    private static bool IsCompatible(BusMetadata existing, BusMetadata desired) {
        return string.Equals(existing.Magic, MetadataMagic, StringComparison.Ordinal)
            && existing.Version == MetadataVersion
            && string.Equals(existing.Channel, desired.Channel, StringComparison.Ordinal)
            && existing.SlotCount >= desired.SlotCount
            && existing.SlotPayloadBytes >= desired.SlotPayloadBytes
            && existing.ImageSize >= desired.ImageSize
            && existing.TtlMs >= 0
            && existing.GenerationId != Guid.Empty;
    }

    private static bool TryReadInt32(Dictionary<string, string> values, string key, out int value) {
        if (values.TryGetValue(key, out string? raw) &&
            int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out value)) {
            return true;
        }

        value = 0;
        return false;
    }

    private static bool TryReadInt64(Dictionary<string, string> values, string key, out long value) {
        if (values.TryGetValue(key, out string? raw) &&
            long.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out value)) {
            return true;
        }

        value = 0;
        return false;
    }

    private static unsafe void CreateEmptyImage(byte* image, BusMetadata metadata) {
        int totalBytes = ComputeRequiredImageSize(metadata.SlotCount, metadata.SlotPayloadBytes);
        ClearBytes(image, totalBytes);

        WriteUInt32(image, HeaderMagicOffset, BusMagic);
        WriteInt32(image, HeaderVersionOffset, BusVersion);
        WriteInt32(image, HeaderSlotCountOffset, metadata.SlotCount);
        WriteInt32(image, HeaderSlotPayloadOffset, metadata.SlotPayloadBytes);
        WriteInt64(image, HeaderHeadSeqOffset, 0);
        WriteInt64(image, HeaderTailSeqOffset, 0);
        WriteInt64(image, HeaderTtlMsOffset, metadata.TtlMs);
        WriteInt32(image, HeaderWakeSeqOffset, 0);
        WriteInt32(image, HeaderReservedOffset, 0);
        WriteGuid(image, HeaderGenerationOffset, metadata.GenerationId);
    }

    private static unsafe bool TryValidateImage(byte* image, int imageLength)
        => ValidateImage(image, imageLength).IsValid;

    private static unsafe ImageValidationResult ValidateImage(byte* image, int imageLength) {
        if (image == null)
            return new ImageValidationResult(false, ImageValidationFailureReason.NullImage);

        if (imageLength < HeaderSize) {
            return new ImageValidationResult(
                false,
                ImageValidationFailureReason.ImageTooSmall,
                RequiredLength: HeaderSize);
        }

        uint magic = ReadUInt32(image, HeaderMagicOffset);
        if (magic != BusMagic) {
            return new ImageValidationResult(
                false,
                ImageValidationFailureReason.InvalidMagic,
                Magic: magic);
        }

        int version = ReadInt32(image, HeaderVersionOffset);
        int slotCount = ReadInt32(image, HeaderSlotCountOffset);
        int slotPayloadBytes = ReadInt32(image, HeaderSlotPayloadOffset);
        long head = ReadInt64(image, HeaderHeadSeqOffset);
        long tail = ReadInt64(image, HeaderTailSeqOffset);
        long ttlMs = ReadInt64(image, HeaderTtlMsOffset);
        Guid generation = ReadGuid(image, HeaderGenerationOffset);

        if (version != BusVersion) {
            return new ImageValidationResult(
                false,
                ImageValidationFailureReason.InvalidVersion,
                Version: version,
                SlotCount: slotCount,
                SlotPayloadBytes: slotPayloadBytes,
                Head: head,
                Tail: tail,
                TtlMs: ttlMs,
                GenerationId: generation);
        }

        if (slotCount < MinimumSlotCount) {
            return new ImageValidationResult(
                false,
                ImageValidationFailureReason.InvalidSlotCount,
                Version: version,
                SlotCount: slotCount,
                SlotPayloadBytes: slotPayloadBytes,
                Head: head,
                Tail: tail,
                TtlMs: ttlMs,
                GenerationId: generation);
        }

        if (slotPayloadBytes <= 0) {
            return new ImageValidationResult(
                false,
                ImageValidationFailureReason.InvalidSlotPayloadBytes,
                Version: version,
                SlotCount: slotCount,
                SlotPayloadBytes: slotPayloadBytes,
                Head: head,
                Tail: tail,
                TtlMs: ttlMs,
                GenerationId: generation);
        }

        int requiredLength;
        try {
            requiredLength = ComputeRequiredImageSize(slotCount, slotPayloadBytes);
        } catch (OverflowException) {
            return new ImageValidationResult(
                false,
                ImageValidationFailureReason.InvalidLayoutOverflow,
                Version: version,
                SlotCount: slotCount,
                SlotPayloadBytes: slotPayloadBytes,
                Head: head,
                Tail: tail,
                TtlMs: ttlMs,
                GenerationId: generation);
        }

        if (head < 0 || tail < 0 || tail > head) {
            return new ImageValidationResult(
                false,
                ImageValidationFailureReason.InvalidHeadTail,
                RequiredLength: requiredLength,
                Version: version,
                SlotCount: slotCount,
                SlotPayloadBytes: slotPayloadBytes,
                Head: head,
                Tail: tail,
                TtlMs: ttlMs,
                GenerationId: generation);
        }

        if (ttlMs < 0) {
            return new ImageValidationResult(
                false,
                ImageValidationFailureReason.InvalidTtl,
                RequiredLength: requiredLength,
                Version: version,
                SlotCount: slotCount,
                SlotPayloadBytes: slotPayloadBytes,
                Head: head,
                Tail: tail,
                TtlMs: ttlMs,
                GenerationId: generation);
        }

        if (generation == Guid.Empty) {
            return new ImageValidationResult(
                false,
                ImageValidationFailureReason.MissingGeneration,
                RequiredLength: requiredLength,
                Version: version,
                SlotCount: slotCount,
                SlotPayloadBytes: slotPayloadBytes,
                Head: head,
                Tail: tail,
                TtlMs: ttlMs,
                GenerationId: generation);
        }

        if (imageLength < requiredLength) {
            return new ImageValidationResult(
                false,
                ImageValidationFailureReason.MappedLengthTooSmall,
                RequiredLength: requiredLength,
                Version: version,
                SlotCount: slotCount,
                SlotPayloadBytes: slotPayloadBytes,
                Head: head,
                Tail: tail,
                TtlMs: ttlMs,
                GenerationId: generation);
        }

        return new ImageValidationResult(
            true,
            ImageValidationFailureReason.None,
            RequiredLength: requiredLength,
            Version: version,
            SlotCount: slotCount,
            SlotPayloadBytes: slotPayloadBytes,
            Head: head,
            Tail: tail,
            TtlMs: ttlMs,
            GenerationId: generation,
            Magic: magic);
    }

    private static unsafe long PruneExpired(byte* image, int slotCount, int slotPayloadBytes, long head, long tail, long nowMs, long ttlMs) {
        if (ttlMs <= 0)
            return tail;

        while (tail < head) {
            int slotIndex = checked((int)(tail % slotCount));
            int slotOffset = GetSlotOffset(slotIndex, slotPayloadBytes);
            long timestampMs = ReadInt64(image, slotOffset + SlotTimestampMsOffset);
            long sequence = ReadInt64(image, slotOffset + SlotSequenceOffset);
            uint slotMagic = ReadUInt32(image, slotOffset + SlotMagicOffset);

            if (slotMagic != SlotMagic || sequence != tail) {
                tail++;
                continue;
            }

            if (nowMs - timestampMs <= ttlMs)
                break;

            tail++;
        }

        return tail;
    }

    private static int ResolveSlotCount() {
        string? configured = TinyIpcEnvironment.GetEnvironmentVariable(TinyIpcEnvironment.SlotCount);
        if (int.TryParse(configured, out int value) && value >= MinimumSlotCount)
            return value;

        return DefaultSlotCount;
    }

    private static long ResolveMessageTtlMs() {
        string? configured = TinyIpcEnvironment.GetEnvironmentVariable(TinyIpcEnvironment.MessageTtlMs);
        if (long.TryParse(configured, out long value) && value >= 0)
            return value;

        return DefaultMessageTtlMs;
    }

    private static int ComputeRequiredImageSize(int slotCount, int slotPayloadBytes)
        => checked(HeaderSize + (slotCount * (SlotHeaderSize + slotPayloadBytes)));

    private static int GetSlotOffset(int slotIndex, int slotPayloadBytes)
        => checked(HeaderSize + (slotIndex * (SlotHeaderSize + slotPayloadBytes)));

    private static unsafe void MarshalCopy(byte* source, byte[] destination) {
        if (destination.Length == 0)
            return;

        fixed (byte* destinationPtr = destination) {
            Buffer.MemoryCopy(source, destinationPtr, destination.Length, destination.Length);
        }
    }

    private static unsafe void ClearBytes(byte* destination, int length)
        => new Span<byte>(destination, length).Clear();

    private static unsafe int ReadInt32(byte* image, int offset) => Unsafe.ReadUnaligned<int>(image + offset);
    private static unsafe long ReadInt64(byte* image, int offset) => Unsafe.ReadUnaligned<long>(image + offset);
    private static unsafe uint ReadUInt32(byte* image, int offset) => Unsafe.ReadUnaligned<uint>(image + offset);
    private static unsafe void WriteInt32(byte* image, int offset, int value) => Unsafe.WriteUnaligned(image + offset, value);
    private static unsafe void WriteInt64(byte* image, int offset, long value) => Unsafe.WriteUnaligned(image + offset, value);
    private static unsafe void WriteUInt32(byte* image, int offset, uint value) => Unsafe.WriteUnaligned(image + offset, value);

    private static unsafe Guid ReadGuid(byte* image, int offset)
        => new Guid(new ReadOnlySpan<byte>(image + offset, 16));

    private static unsafe void WriteGuid(byte* image, int offset, Guid value)
        => value.TryWriteBytes(new Span<byte>(image + offset, 16));

    private void ThrowIfDisposed() {
        if (_disposed)
            throw new ObjectDisposedException(nameof(UnixSharedMemoryTinyMessageBus));
    }

    private sealed record BusMetadata(
        string Magic,
        int Version,
        string Channel,
        int SlotCount,
        int SlotPayloadBytes,
        int ImageSize,
        long TtlMs,
        Guid GenerationId,
        long CreatedUnixMs,
        int CreatorPid);

    private enum ImageValidationFailureReason {
        None,
        NullImage,
        ImageTooSmall,
        InvalidMagic,
        InvalidVersion,
        InvalidSlotCount,
        InvalidSlotPayloadBytes,
        InvalidLayoutOverflow,
        InvalidHeadTail,
        InvalidTtl,
        MissingGeneration,
        MappedLengthTooSmall
    }

    private sealed record ImageValidationResult(
        bool IsValid,
        ImageValidationFailureReason FailureReason,
        int RequiredLength = 0,
        int Version = 0,
        int SlotCount = 0,
        int SlotPayloadBytes = 0,
        long Head = 0,
        long Tail = 0,
        long TtlMs = 0,
        Guid GenerationId = default,
        uint Magic = 0);
}
