using System.IO.MemoryMappedFiles;
using System.Runtime.InteropServices;

using Microsoft.Win32.SafeHandles;

using XivIpc.Internal;

namespace XivIpc.Messaging;

internal readonly record struct BrokeredJournalPublishResult(
    long HeadSequenceBefore,
    long TailSequenceBefore,
    long HeadSequenceAfter,
    long TailSequenceAfter,
    int RetainedBytesBefore,
    int RetainedBytesAfter,
    bool PrunedExpired,
    bool Compacted);

internal readonly record struct BrokeredJournalDrainResult(
    List<byte[]> Messages,
    bool CaughtUpToTail,
    long TailSequenceObserved,
    long HeadSequenceObserved,
    long NextSequenceBefore,
    long NextSequenceAfter,
    long LagBefore,
    int RetainedBytes);

internal sealed unsafe class BrokeredChannelJournal : IDisposable {
    private const uint JournalMagic = 0x5449424a; // "TIBJ"
    private const uint RecordMagic = 0x54494252;  // "TIBR"
    private const int JournalVersion = 1;

    private const int HeaderMagicOffset = 0;
    private const int HeaderVersionOffset = 4;
    private const int HeaderCapacityBytesOffset = 8;
    private const int HeaderMaxPayloadBytesOffset = 12;
    private const int HeaderTailOffsetOffset = 16;
    private const int HeaderHeadOffsetOffset = 24;
    private const int HeaderTailSequenceOffset = 32;
    private const int HeaderHeadSequenceOffset = 40;
    private const int HeaderMinAgeMsOffset = 48;
    internal const int HeaderBytes = 64;

    private const int RecordMagicOffset = 0;
    private const int RecordTotalLengthOffset = 4;
    private const int RecordSequenceOffset = 8;
    private const int RecordTimestampMsOffset = 16;
    private const int RecordSenderGuidOffset = 24;
    private const int RecordPayloadLengthOffset = 40;
    private const int RecordFlagsOffset = 44;
    internal const int RecordHeaderBytes = 48;

    private readonly object _gate = new();
    private readonly FileStream _stream;
    private readonly MemoryMappedFile _mappedFile;
    private readonly MemoryMappedViewAccessor _view;
    private readonly SafeMemoryMappedViewHandle _viewHandle;
    private bool _disposed;

    private BrokeredChannelJournal(
        string filePath,
        FileStream stream,
        MemoryMappedFile mappedFile,
        MemoryMappedViewAccessor view,
        SafeMemoryMappedViewHandle viewHandle,
        byte* pointer,
        int capacityBytes,
        int maxPayloadBytes,
        long minAgeMs) {
        FilePath = filePath;
        _stream = stream;
        _mappedFile = mappedFile;
        _view = view;
        _viewHandle = viewHandle;
        Pointer = (IntPtr)pointer;
        CapacityBytes = capacityBytes;
        MaxPayloadBytes = maxPayloadBytes;
        MinAgeMs = minAgeMs;
        Length = checked(HeaderBytes + capacityBytes);
    }

    internal string FilePath { get; }
    internal IntPtr Pointer { get; }
    internal int CapacityBytes { get; }
    internal int MaxPayloadBytes { get; }
    internal long MinAgeMs { get; }
    internal int Length { get; }

    internal static BrokeredChannelJournal Create(string filePath, int capacityBytes, int maxPayloadBytes, long minAgeMs) {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        int length = checked(HeaderBytes + capacityBytes);
        string runtimePath = UnixSharedStorageHelpers.ConvertPathForCurrentRuntime(filePath);
        string? directory = Path.GetDirectoryName(runtimePath);
        if (!string.IsNullOrWhiteSpace(directory)) {
            Directory.CreateDirectory(directory);
            UnixSharedStorageHelpers.ApplyBrokerPermissions(directory, isDirectory: true);
        }

        FileStream stream = OpenFileStream(runtimePath, length, create: true);
        try {
            UnixSharedStorageHelpers.ApplyBrokerPermissions(runtimePath, isDirectory: false);
            MemoryMappedFile mappedFile = MemoryMappedFile.CreateFromFile(
                stream,
                null,
                length,
                MemoryMappedFileAccess.ReadWrite,
                HandleInheritability.None,
                leaveOpen: true);
            MemoryMappedViewAccessor view = mappedFile.CreateViewAccessor(0, length, MemoryMappedFileAccess.ReadWrite);
            byte* pointer = AcquirePointer(view.SafeMemoryMappedViewHandle);
            var journal = new BrokeredChannelJournal(filePath, stream, mappedFile, view, view.SafeMemoryMappedViewHandle, pointer, capacityBytes, maxPayloadBytes, minAgeMs);
            journal.Initialize();
            return journal;
        } catch {
            stream.Dispose();
            throw;
        }
    }

    internal static BrokeredChannelJournal Attach(string filePath, int capacityBytes, int maxPayloadBytes, int length, long minAgeMs) {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        string runtimePath = UnixSharedStorageHelpers.ConvertPathForCurrentRuntime(filePath);
        FileStream stream = OpenFileStream(runtimePath, length, create: false);
        try {
            MemoryMappedFile mappedFile = MemoryMappedFile.CreateFromFile(
                stream,
                null,
                length,
                MemoryMappedFileAccess.ReadWrite,
                HandleInheritability.None,
                leaveOpen: true);
            MemoryMappedViewAccessor view = mappedFile.CreateViewAccessor(0, length, MemoryMappedFileAccess.ReadWrite);
            byte* pointer = AcquirePointer(view.SafeMemoryMappedViewHandle);
            var journal = new BrokeredChannelJournal(filePath, stream, mappedFile, view, view.SafeMemoryMappedViewHandle, pointer, capacityBytes, maxPayloadBytes, minAgeMs);
            journal.Validate();
            return journal;
        } catch {
            stream.Dispose();
            throw;
        }
    }

    internal long CaptureHeadSequence() {
        lock (_gate) {
            ThrowIfDisposed();
            return ReadInt64((byte*)Pointer, HeaderHeadSequenceOffset);
        }
    }

    internal long CaptureTailSequence() {
        lock (_gate) {
            ThrowIfDisposed();
            return ReadInt64((byte*)Pointer, HeaderTailSequenceOffset);
        }
    }

    internal int CaptureHeadOffset() {
        lock (_gate) {
            ThrowIfDisposed();
            return ReadInt32((byte*)Pointer, HeaderHeadOffsetOffset);
        }
    }

    internal int CaptureTailOffset() {
        lock (_gate) {
            ThrowIfDisposed();
            return ReadInt32((byte*)Pointer, HeaderTailOffsetOffset);
        }
    }

    internal BrokeredJournalPublishResult Publish(Guid senderInstanceId, byte[] message) {
        ArgumentNullException.ThrowIfNull(message);

        lock (_gate) {
            ThrowIfDisposed();

            if (message.Length > MaxPayloadBytes)
                throw new InvalidOperationException($"Message length {message.Length} exceeds configured max {MaxPayloadBytes}.");

            int requiredBytes = checked(RecordHeaderBytes + message.Length);
            if (requiredBytes > CapacityBytes)
                throw new InvalidOperationException($"Message length {message.Length} exceeds journal capacity {CapacityBytes}.");

            Validate();

            byte* image = (byte*)Pointer;
            int tailOffset = ReadInt32(image, HeaderTailOffsetOffset);
            int headOffset = ReadInt32(image, HeaderHeadOffsetOffset);
            long tailSequence = ReadInt64(image, HeaderTailSequenceOffset);
            long headSequence = ReadInt64(image, HeaderHeadSequenceOffset);
            int retainedBytesBefore = headOffset - tailOffset;
            long tailSequenceBefore = tailSequence;
            bool prunedExpired = false;
            bool compacted = false;
            long nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            while (tailOffset < headOffset) {
                int recordOffset = HeaderBytes + tailOffset;
                long timestampMs = ReadInt64(image, recordOffset + RecordTimestampMsOffset);
                if (MinAgeMs > 0 && nowMs - timestampMs <= MinAgeMs)
                    break;

                int recordLength = ReadInt32(image, recordOffset + RecordTotalLengthOffset);
                if (recordLength < RecordHeaderBytes || tailOffset + recordLength > headOffset)
                    break;

                tailOffset += recordLength;
                tailSequence++;
                prunedExpired = true;
            }

            if (tailOffset > 0) {
                int retainedBytes = headOffset - tailOffset;
                if (retainedBytes > 0) {
                    Buffer.MemoryCopy(
                        image + HeaderBytes + tailOffset,
                        image + HeaderBytes,
                        CapacityBytes,
                        retainedBytes);
                }

                ClearBytes(image + HeaderBytes + retainedBytes, CapacityBytes - retainedBytes);
                headOffset = retainedBytes;
                tailOffset = 0;
                compacted = true;
            }

            int retainedBytesAfterPrune = headOffset - tailOffset;
            if (retainedBytesAfterPrune + requiredBytes > CapacityBytes) {
                throw new InvalidOperationException(
                    $"Broker journal is full and no records older than the minimum age window can be trimmed yet. " +
                    $"messageLength={message.Length}, requiredBytes={requiredBytes}, retainedBytes={retainedBytesAfterPrune}, " +
                    $"capacityBytes={CapacityBytes}, minAgeMs={MinAgeMs}.");
            }

            int writeOffset = HeaderBytes + headOffset;
            ClearBytes(image + writeOffset, requiredBytes);

            if (message.Length > 0) {
                fixed (byte* source = message) {
                    Buffer.MemoryCopy(
                        source,
                        image + writeOffset + RecordHeaderBytes,
                        message.Length,
                        message.Length);
                }
            }

            WriteInt32(image, writeOffset + RecordTotalLengthOffset, requiredBytes);
            WriteInt64(image, writeOffset + RecordSequenceOffset, headSequence);
            WriteInt64(image, writeOffset + RecordTimestampMsOffset, nowMs);
            WriteGuid(image + writeOffset + RecordSenderGuidOffset, senderInstanceId);
            WriteInt32(image, writeOffset + RecordPayloadLengthOffset, message.Length);
            WriteInt32(image, writeOffset + RecordFlagsOffset, 0);
            WriteUInt32(image, writeOffset + RecordMagicOffset, RecordMagic);

            WriteInt32(image, HeaderTailOffsetOffset, tailOffset);
            WriteInt32(image, HeaderHeadOffsetOffset, headOffset + requiredBytes);
            WriteInt64(image, HeaderTailSequenceOffset, tailSequence);
            WriteInt64(image, HeaderHeadSequenceOffset, headSequence + 1);
            _view.Flush();

            return new BrokeredJournalPublishResult(
                HeadSequenceBefore: headSequence,
                TailSequenceBefore: tailSequenceBefore,
                HeadSequenceAfter: headSequence + 1,
                TailSequenceAfter: tailSequence,
                RetainedBytesBefore: retainedBytesBefore,
                RetainedBytesAfter: headOffset + requiredBytes - tailOffset,
                PrunedExpired: prunedExpired,
                Compacted: compacted);
        }
    }

    internal BrokeredJournalDrainResult Drain(Guid selfInstanceId, ref long nextSequence) {
        lock (_gate) {
            ThrowIfDisposed();
            Validate();

            byte* image = (byte*)Pointer;
            int tailOffset = ReadInt32(image, HeaderTailOffsetOffset);
            int headOffset = ReadInt32(image, HeaderHeadOffsetOffset);
            long tailSequence = ReadInt64(image, HeaderTailSequenceOffset);
            long headSequence = ReadInt64(image, HeaderHeadSequenceOffset);
            long nextSequenceBefore = nextSequence;
            long lagBefore = headSequence - nextSequenceBefore;
            bool caughtUpToTail = false;

            if (nextSequence < tailSequence) {
                nextSequence = tailSequence;
                caughtUpToTail = true;
            }

            int offset = tailOffset;
            long sequence = tailSequence;
            while (sequence < nextSequence && offset < headOffset) {
                int recordOffset = HeaderBytes + offset;
                int recordLength = ReadInt32(image, recordOffset + RecordTotalLengthOffset);
                if (recordLength < RecordHeaderBytes || offset + recordLength > headOffset)
                    break;

                offset += recordLength;
                sequence++;
            }

            var messages = new List<byte[]>();
            while (sequence < headSequence && offset < headOffset) {
                int recordOffset = HeaderBytes + offset;
                uint magic = ReadUInt32(image, recordOffset + RecordMagicOffset);
                int recordLength = ReadInt32(image, recordOffset + RecordTotalLengthOffset);
                long recordSequence = ReadInt64(image, recordOffset + RecordSequenceOffset);
                int payloadLength = ReadInt32(image, recordOffset + RecordPayloadLengthOffset);
                Guid senderInstanceId = ReadGuid(image + recordOffset + RecordSenderGuidOffset);

                if (magic != RecordMagic ||
                    recordLength < RecordHeaderBytes ||
                    offset + recordLength > headOffset ||
                    recordSequence != sequence ||
                    payloadLength < 0 ||
                    payloadLength > MaxPayloadBytes ||
                    RecordHeaderBytes + payloadLength > recordLength) {
                    break;
                }

                if (senderInstanceId != selfInstanceId) {
                    byte[] payload = new byte[payloadLength];
                    if (payloadLength > 0)
                        MarshalCopy(image + recordOffset + RecordHeaderBytes, payload);
                    messages.Add(payload);
                }

                offset += recordLength;
                sequence++;
                nextSequence = sequence;
            }

            return new BrokeredJournalDrainResult(
                Messages: messages,
                CaughtUpToTail: caughtUpToTail,
                TailSequenceObserved: tailSequence,
                HeadSequenceObserved: headSequence,
                NextSequenceBefore: nextSequenceBefore,
                NextSequenceAfter: nextSequence,
                LagBefore: lagBefore,
                RetainedBytes: headOffset - tailOffset);
        }
    }

    private void Initialize() {
        byte* image = (byte*)Pointer;
        ClearBytes(image, Length);
        WriteUInt32(image, HeaderMagicOffset, JournalMagic);
        WriteInt32(image, HeaderVersionOffset, JournalVersion);
        WriteInt32(image, HeaderCapacityBytesOffset, CapacityBytes);
        WriteInt32(image, HeaderMaxPayloadBytesOffset, MaxPayloadBytes);
        WriteInt32(image, HeaderTailOffsetOffset, 0);
        WriteInt32(image, HeaderHeadOffsetOffset, 0);
        WriteInt64(image, HeaderTailSequenceOffset, 0);
        WriteInt64(image, HeaderHeadSequenceOffset, 0);
        WriteInt64(image, HeaderMinAgeMsOffset, MinAgeMs);
        _view.Flush();
    }

    private void Validate() {
        byte* image = (byte*)Pointer;
        uint magic = ReadUInt32(image, HeaderMagicOffset);
        int version = ReadInt32(image, HeaderVersionOffset);
        int capacityBytes = ReadInt32(image, HeaderCapacityBytesOffset);
        int maxPayloadBytes = ReadInt32(image, HeaderMaxPayloadBytesOffset);
        int tailOffset = ReadInt32(image, HeaderTailOffsetOffset);
        int headOffset = ReadInt32(image, HeaderHeadOffsetOffset);
        long minAgeMs = ReadInt64(image, HeaderMinAgeMsOffset);

        if (magic != JournalMagic ||
            version != JournalVersion ||
            capacityBytes != CapacityBytes ||
            maxPayloadBytes != MaxPayloadBytes ||
            minAgeMs != MinAgeMs) {
            throw new InvalidDataException("Broker journal header is invalid.");
        }

        if (tailOffset < 0 || headOffset < tailOffset || headOffset > CapacityBytes)
            throw new InvalidDataException("Broker journal offsets are invalid.");
    }

    public void Dispose() {
        lock (_gate) {
            if (_disposed)
                return;

            _disposed = true;
            _viewHandle.ReleasePointer();
            _view.Dispose();
            _mappedFile.Dispose();
            _stream.Dispose();
        }
    }

    private void ThrowIfDisposed() {
        if (_disposed)
            throw new ObjectDisposedException(nameof(BrokeredChannelJournal));
    }

    private static FileStream OpenFileStream(string path, int length, bool create) {
        FileMode mode = create ? FileMode.OpenOrCreate : FileMode.Open;
        var stream = new FileStream(path, mode, FileAccess.ReadWrite, FileShare.ReadWrite | FileShare.Delete);
        stream.SetLength(length);
        return stream;
    }

    private static byte* AcquirePointer(SafeMemoryMappedViewHandle handle) {
        byte* pointer = null;
        handle.AcquirePointer(ref pointer);
        return pointer;
    }

    private static void MarshalCopy(byte* source, byte[] destination)
        => Marshal.Copy((IntPtr)source, destination, 0, destination.Length);

    private static void ClearBytes(byte* pointer, int length) {
        new Span<byte>(pointer, length).Clear();
    }

    private static int ReadInt32(byte* image, int offset)
        => *(int*)(image + offset);

    private static long ReadInt64(byte* image, int offset)
        => *(long*)(image + offset);

    private static uint ReadUInt32(byte* image, int offset)
        => *(uint*)(image + offset);

    private static void WriteInt32(byte* image, int offset, int value)
        => *(int*)(image + offset) = value;

    private static void WriteInt64(byte* image, int offset, long value)
        => *(long*)(image + offset) = value;

    private static void WriteUInt32(byte* image, int offset, uint value)
        => *(uint*)(image + offset) = value;

    private static Guid ReadGuid(byte* image) {
        Span<byte> buffer = stackalloc byte[16];
        new Span<byte>(image, 16).CopyTo(buffer);
        return new Guid(buffer);
    }

    private static void WriteGuid(byte* image, Guid value) {
        Span<byte> buffer = stackalloc byte[16];
        value.TryWriteBytes(buffer);
        buffer.CopyTo(new Span<byte>(image, 16));
    }
}
