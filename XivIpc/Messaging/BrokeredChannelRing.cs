using System.IO.MemoryMappedFiles;

using Microsoft.Win32.SafeHandles;

using XivIpc.Internal;

namespace XivIpc.Messaging;

internal readonly record struct BrokeredRingPublishResult(
    long HeadBefore,
    long TailBefore,
    long HeadAfter,
    long TailAfter,
    bool OverwroteOldest,
    int RetainedDepth);

internal readonly record struct BrokeredRingDrainResult(
    List<byte[]> Messages,
    bool CaughtUpToTail,
    long TailObserved,
    long HeadObserved,
    long NextSequenceBefore,
    long NextSequenceAfter,
    long LagBefore,
    int RetainedDepth);

internal sealed unsafe class BrokeredChannelRing : IDisposable {
    private const uint RingMagic = 0x54494242;   // "TIBB"
    private const uint SlotMagic = 0x54494253;   // "TIBS"
    private const int RingVersion = 1;

    private const int HeaderMagicOffset = 0;
    private const int HeaderVersionOffset = 4;
    private const int HeaderSlotCountOffset = 8;
    private const int HeaderSlotPayloadOffset = 12;
    private const int HeaderHeadSeqOffset = 16;
    private const int HeaderTailSeqOffset = 24;
    private const int HeaderReservedOffset = 32;
    internal const int HeaderBytes = 64;

    private const int SlotMagicOffset = 0;
    private const int SlotPayloadLenOffset = 4;
    private const int SlotSequenceOffset = 8;
    private const int SlotTimestampMsOffset = 16;
    private const int SlotSenderSessionOffset = 24;
    private const int SlotFlagsOffset = 32;
    private const int SlotReservedOffset = 36;
    internal const int SlotHeaderBytes = 40;

    private readonly object _gate = new();
    private readonly FileStream _stream;
    private readonly MemoryMappedFile _mappedFile;
    private readonly MemoryMappedViewAccessor _view;
    private readonly SafeMemoryMappedViewHandle _viewHandle;
    private bool _disposed;

    private BrokeredChannelRing(
        string filePath,
        FileStream stream,
        MemoryMappedFile mappedFile,
        MemoryMappedViewAccessor view,
        SafeMemoryMappedViewHandle viewHandle,
        byte* pointer,
        int slotCount,
        int slotPayloadBytes) {
        FilePath = filePath;
        _stream = stream;
        _mappedFile = mappedFile;
        _view = view;
        _viewHandle = viewHandle;
        Pointer = (IntPtr)pointer;
        SlotCount = slotCount;
        SlotPayloadBytes = slotPayloadBytes;
        Length = ComputeRequiredImageSize(slotCount, slotPayloadBytes);
    }

    internal string FilePath { get; }

    internal IntPtr Pointer { get; }

    internal int Length { get; }

    internal int SlotCount { get; }

    internal int SlotPayloadBytes { get; }

    internal static BrokeredChannelRing Create(string filePath, int slotCount, int slotPayloadBytes) {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        int length = ComputeRequiredImageSize(slotCount, slotPayloadBytes);
        string runtimePath = UnixSharedStorageHelpers.ConvertPathForCurrentRuntime(filePath);
        string? directory = Path.GetDirectoryName(runtimePath);
        if (!string.IsNullOrWhiteSpace(directory)) {
            Directory.CreateDirectory(directory);
            UnixSharedStorageHelpers.ApplyBrokerPermissions(directory, isDirectory: true);
        }

        FileStream stream = OpenFileStream(runtimePath, length, create: true);
        try {
            UnixSharedStorageHelpers.ApplyBrokerPermissions(runtimePath, isDirectory: false);
            MemoryMappedFile mappedFile = MemoryMappedFile.CreateFromFile(stream, null, length, MemoryMappedFileAccess.ReadWrite, HandleInheritability.None, leaveOpen: true);
            MemoryMappedViewAccessor view = mappedFile.CreateViewAccessor(0, length, MemoryMappedFileAccess.ReadWrite);
            byte* pointer = AcquirePointer(view.SafeMemoryMappedViewHandle);

            var ring = new BrokeredChannelRing(filePath, stream, mappedFile, view, view.SafeMemoryMappedViewHandle, pointer, slotCount, slotPayloadBytes);
            ring.Initialize();
            return ring;
        } catch {
            stream.Dispose();
            throw;
        }
    }

    internal static BrokeredChannelRing Attach(string filePath, int slotCount, int slotPayloadBytes) {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        int length = ComputeRequiredImageSize(slotCount, slotPayloadBytes);
        string runtimePath = UnixSharedStorageHelpers.ConvertPathForCurrentRuntime(filePath);
        FileStream stream = OpenFileStream(runtimePath, length, create: false);

        try {
            MemoryMappedFile mappedFile = MemoryMappedFile.CreateFromFile(stream, null, length, MemoryMappedFileAccess.ReadWrite, HandleInheritability.None, leaveOpen: true);
            MemoryMappedViewAccessor view = mappedFile.CreateViewAccessor(0, length, MemoryMappedFileAccess.ReadWrite);
            byte* pointer = AcquirePointer(view.SafeMemoryMappedViewHandle);

            var ring = new BrokeredChannelRing(filePath, stream, mappedFile, view, view.SafeMemoryMappedViewHandle, pointer, slotCount, slotPayloadBytes);
            ring.Validate();
            return ring;
        } catch {
            stream.Dispose();
            throw;
        }
    }

    internal long CaptureHead()
        => Volatile.Read(ref *(long*)((byte*)Pointer + HeaderHeadSeqOffset));

    internal long CaptureTail()
        => Volatile.Read(ref *(long*)((byte*)Pointer + HeaderTailSeqOffset));

    internal static int ComputeRequiredImageSize(int slotCount, int slotPayloadBytes)
        => checked(HeaderBytes + (slotCount * (SlotHeaderBytes + slotPayloadBytes)));

    internal BrokeredRingPublishResult Publish(long senderSessionId, byte[] message) {
        if (message.Length > SlotPayloadBytes)
            throw new InvalidOperationException($"Message length {message.Length} exceeds configured max {SlotPayloadBytes}.");

        lock (_gate) {
            Validate();

            byte* image = (byte*)Pointer;
            long head = ReadInt64(image, HeaderHeadSeqOffset);
            long tail = ReadInt64(image, HeaderTailSeqOffset);
            long originalTail = tail;
            bool overwroteOldest = false;

            if (head - tail >= SlotCount) {
                tail = head - SlotCount + 1;
                overwroteOldest = true;
            }

            int slotIndex = checked((int)(head % SlotCount));
            int slotOffset = GetSlotOffset(slotIndex);
            ClearBytes(image + slotOffset, SlotHeaderBytes + SlotPayloadBytes);

            if (message.Length > 0) {
                fixed (byte* source = message)
                    Buffer.MemoryCopy(source, image + slotOffset + SlotHeaderBytes, SlotPayloadBytes, message.Length);
            }

            WriteInt32(image, slotOffset + SlotPayloadLenOffset, message.Length);
            WriteInt64(image, slotOffset + SlotSequenceOffset, head);
            WriteInt64(image, slotOffset + SlotTimestampMsOffset, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
            WriteInt64(image, slotOffset + SlotSenderSessionOffset, senderSessionId);
            WriteInt32(image, slotOffset + SlotFlagsOffset, 0);
            WriteUInt32(image, slotOffset + SlotMagicOffset, SlotMagic);

            WriteInt64(image, HeaderHeadSeqOffset, head + 1);
            WriteInt64(image, HeaderTailSeqOffset, tail);
            _view.Flush();

            return new BrokeredRingPublishResult(
                HeadBefore: head,
                TailBefore: originalTail,
                HeadAfter: head + 1,
                TailAfter: tail,
                OverwroteOldest: overwroteOldest,
                RetainedDepth: checked((int)((head + 1) - tail)));
        }
    }

    internal BrokeredRingDrainResult Drain(long selfSessionId, ref long nextSequence) {
        Validate();

        byte* image = (byte*)Pointer;
        long head = Volatile.Read(ref *(long*)(image + HeaderHeadSeqOffset));
        long tail = Volatile.Read(ref *(long*)(image + HeaderTailSeqOffset));
        long nextSequenceBefore = nextSequence;
        long lagBefore = head - nextSequenceBefore;
        bool caughtUpToTail = false;

        if (nextSequence < tail) {
            nextSequence = tail;
            caughtUpToTail = true;
        }

        var messages = new List<byte[]>();
        while (nextSequence < head) {
            int slotIndex = checked((int)(nextSequence % SlotCount));
            int slotOffset = GetSlotOffset(slotIndex);

            uint slotMagic = Volatile.Read(ref *(uint*)(image + slotOffset + SlotMagicOffset));
            int payloadLen = ReadInt32(image, slotOffset + SlotPayloadLenOffset);
            long sequence = ReadInt64(image, slotOffset + SlotSequenceOffset);
            long senderSessionId = ReadInt64(image, slotOffset + SlotSenderSessionOffset);

            if (slotMagic == SlotMagic &&
                sequence == nextSequence &&
                payloadLen >= 0 &&
                payloadLen <= SlotPayloadBytes &&
                senderSessionId != selfSessionId) {
                byte[] payload = new byte[payloadLen];
                if (payloadLen > 0)
                    MarshalCopy(image + slotOffset + SlotHeaderBytes, payload);

                messages.Add(payload);
            }

            nextSequence++;
        }

        return new BrokeredRingDrainResult(
            Messages: messages,
            CaughtUpToTail: caughtUpToTail,
            TailObserved: tail,
            HeadObserved: head,
            NextSequenceBefore: nextSequenceBefore,
            NextSequenceAfter: nextSequence,
            LagBefore: lagBefore,
            RetainedDepth: checked((int)(head - tail)));
    }

    public void Dispose() {
        if (_disposed)
            return;

        _disposed = true;

        try { _viewHandle.ReleasePointer(); } catch { }
        try { _view.Dispose(); } catch { }
        try { _mappedFile.Dispose(); } catch { }
        try { _stream.Dispose(); } catch { }
    }

    private void Initialize() {
        byte* image = (byte*)Pointer;
        ClearBytes(image, Length);
        WriteUInt32(image, HeaderMagicOffset, RingMagic);
        WriteInt32(image, HeaderVersionOffset, RingVersion);
        WriteInt32(image, HeaderSlotCountOffset, SlotCount);
        WriteInt32(image, HeaderSlotPayloadOffset, SlotPayloadBytes);
        WriteInt64(image, HeaderHeadSeqOffset, 0);
        WriteInt64(image, HeaderTailSeqOffset, 0);
        WriteInt64(image, HeaderReservedOffset, 0);
        _view.Flush();
    }

    private void Validate() {
        byte* image = (byte*)Pointer;
        uint magic = Volatile.Read(ref *(uint*)(image + HeaderMagicOffset));
        int version = ReadInt32(image, HeaderVersionOffset);
        int slotCount = ReadInt32(image, HeaderSlotCountOffset);
        int slotPayloadBytes = ReadInt32(image, HeaderSlotPayloadOffset);

        if (magic != RingMagic || version != RingVersion || slotCount != SlotCount || slotPayloadBytes != SlotPayloadBytes) {
            throw new InvalidOperationException(
                $"Brokered channel ring validation failed. magic={magic}, version={version}, slotCount={slotCount}, slotPayloadBytes={slotPayloadBytes}.");
        }
    }

    private int GetSlotOffset(int slotIndex)
        => checked(HeaderBytes + (slotIndex * (SlotHeaderBytes + SlotPayloadBytes)));

    private static FileStream OpenFileStream(string path, int length, bool create) {
        FileMode mode = create ? FileMode.OpenOrCreate : FileMode.Open;
        var stream = new FileStream(path, mode, FileAccess.ReadWrite, FileShare.ReadWrite | FileShare.Delete);
        if (stream.Length != length)
            stream.SetLength(length);

        return stream;
    }

    private static byte* AcquirePointer(SafeMemoryMappedViewHandle handle) {
        byte* pointer = null;
        handle.AcquirePointer(ref pointer);
        return pointer;
    }

    private static void MarshalCopy(byte* source, byte[] destination) {
        fixed (byte* destinationPtr = destination)
            Buffer.MemoryCopy(source, destinationPtr, destination.Length, destination.Length);
    }

    private static void ClearBytes(byte* start, int length) {
        new Span<byte>(start, length).Clear();
    }

    private static int ReadInt32(byte* image, int offset)
        => Volatile.Read(ref *(int*)(image + offset));

    private static long ReadInt64(byte* image, int offset)
        => Volatile.Read(ref *(long*)(image + offset));

    private static void WriteInt32(byte* image, int offset, int value)
        => Volatile.Write(ref *(int*)(image + offset), value);

    private static void WriteInt64(byte* image, int offset, long value)
        => Volatile.Write(ref *(long*)(image + offset), value);

    private static void WriteUInt32(byte* image, int offset, uint value)
        => Volatile.Write(ref *(uint*)(image + offset), value);
}

