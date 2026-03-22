using System.Buffers.Binary;
using System.Net.Sockets;

namespace XivIpc.Internal;

internal enum SidecarFrameType : byte {
    Hello = 1,
    Publish = 2,
    Dispose = 3,
    Heartbeat = 4,
    Notify = 5,
    AttachRing = 10,
    Ready = 11,
    Error = 13
}

[Flags]
internal enum SidecarClientCapabilities : int {
    None = 0,
    BrokeredRing = 1
}

internal readonly record struct SidecarHello(
    string Channel,
    int MaxBytes,
    int OwnerPid,
    int HeartbeatIntervalMs,
    int HeartbeatTimeoutMs,
    Guid ClientInstanceId = default,
    SidecarClientCapabilities Capabilities = SidecarClientCapabilities.BrokeredRing,
    int ProtocolVersion = 3);

internal readonly record struct SidecarAttachRing(
    string RingPath,
    int SlotCount,
    int SlotPayloadBytes,
    long StartSequence,
    long SessionId,
    int RingLength,
    int ProtocolVersion = 3);

internal readonly record struct SidecarFrame(SidecarFrameType Type, ReadOnlyMemory<byte> Payload);

internal static class SidecarProtocol {
    private const int LengthPrefixBytes = 4;
    private const int AttachRingFixedPayloadBytes = 4 + 4 + 4 + 4 + 8 + 8 + 4 + 8;

    public static void WriteHello(Socket socket, SidecarHello hello) {
        byte[] channelBytes = System.Text.Encoding.UTF8.GetBytes(hello.Channel ?? string.Empty);
        int payloadLength = 4 + 4 + 4 + 4 + 4 + 16 + 4 + 4 + channelBytes.Length;
        byte[] buffer = new byte[LengthPrefixBytes + 1 + payloadLength];

        BinaryPrimitives.WriteInt32LittleEndian(buffer.AsSpan(0, 4), 1 + payloadLength);
        buffer[4] = (byte)SidecarFrameType.Hello;

        int offset = 5;
        WriteInt32(buffer, ref offset, hello.ProtocolVersion);
        WriteInt32(buffer, ref offset, hello.OwnerPid);
        WriteInt32(buffer, ref offset, hello.MaxBytes);
        WriteInt32(buffer, ref offset, hello.HeartbeatIntervalMs);
        WriteInt32(buffer, ref offset, hello.HeartbeatTimeoutMs);
        WriteGuid(buffer, ref offset, hello.ClientInstanceId);
        WriteInt32(buffer, ref offset, (int)hello.Capabilities);
        WriteInt32(buffer, ref offset, channelBytes.Length);
        channelBytes.CopyTo(buffer, offset);

        SendAll(socket, buffer);
    }

    public static SidecarHello ReadHello(Socket socket) {
        SidecarFrame frame = ReadFrame(socket);
        if (frame.Type != SidecarFrameType.Hello)
            throw new InvalidDataException($"Expected sidecar frame '{SidecarFrameType.Hello}' but received '{frame.Type}'.");

        ReadOnlySpan<byte> span = frame.Payload.Span;
        if (span.Length < 28)
            throw new InvalidDataException("HELLO payload is truncated.");

        int offset = 0;
        int version = ReadInt32(span, ref offset);
        int ownerPid = ReadInt32(span, ref offset);
        int maxBytes = ReadInt32(span, ref offset);
        int heartbeatIntervalMs = ReadInt32(span, ref offset);
        int heartbeatTimeoutMs = ReadInt32(span, ref offset);
        Guid clientInstanceId = version >= 3
            ? ReadGuid(span, ref offset)
            : Guid.Empty;
        SidecarClientCapabilities capabilities = (SidecarClientCapabilities)ReadInt32(span, ref offset);
        int channelLength = ReadInt32(span, ref offset);

        if (channelLength < 0 || offset + channelLength > span.Length)
            throw new InvalidDataException("HELLO channel payload is invalid.");

        string channel = System.Text.Encoding.UTF8.GetString(span.Slice(offset, channelLength));
        return new SidecarHello(channel, maxBytes, ownerPid, heartbeatIntervalMs, heartbeatTimeoutMs, clientInstanceId, capabilities, version);
    }

    public static void WriteAttachRing(Socket socket, SidecarAttachRing attach) {
        byte[] ringPathBytes = System.Text.Encoding.UTF8.GetBytes(attach.RingPath ?? string.Empty);
        byte[] payload = new byte[AttachRingFixedPayloadBytes + ringPathBytes.Length];
        int offset = 0;
        WriteInt32(payload, ref offset, attach.ProtocolVersion);
        WriteInt32(payload, ref offset, ringPathBytes.Length);
        WriteInt32(payload, ref offset, attach.SlotCount);
        WriteInt32(payload, ref offset, attach.SlotPayloadBytes);
        WriteInt64(payload, ref offset, attach.StartSequence);
        WriteInt64(payload, ref offset, attach.SessionId);
        WriteInt32(payload, ref offset, attach.RingLength);
        WriteInt64(payload, ref offset, attach.ProtocolVersion >= 4 ? attach.StartSequence : 0);
        ringPathBytes.CopyTo(payload, offset);
        SendAll(socket, BuildFrame(SidecarFrameType.AttachRing, payload));
    }

    public static SidecarAttachRing ReadAttachRing(Socket socket) {
        SidecarFrame frame = ReadFrame(socket);
        return DecodeAttachRing(frame);
    }

    public static SidecarAttachRing DecodeAttachRing(SidecarFrame frame) {
        SidecarFrameType frameType = frame.Type;
        if (frameType == SidecarFrameType.Error) {
            string message = frame.Payload.Length > 0
                ? System.Text.Encoding.UTF8.GetString(frame.Payload.Span)
                : "Broker attach failed.";
            throw new InvalidOperationException(message);
        }

        if (frameType != SidecarFrameType.AttachRing)
            throw new InvalidDataException("Expected ATTACH_RING from the broker.");

        ReadOnlySpan<byte> span = frame.Payload.Span;
        if (span.Length < AttachRingFixedPayloadBytes)
            throw new InvalidDataException($"ATTACH_RING payload is truncated. length={span.Length} minimum={AttachRingFixedPayloadBytes}.");

        int offset = 0;
        int version = ReadInt32(span, ref offset);
        int ringPathLength = ReadInt32(span, ref offset);
        int slotCount = ReadInt32(span, ref offset);
        int slotPayloadBytes = ReadInt32(span, ref offset);
        long startSequence = ReadInt64(span, ref offset);
        long sessionId = ReadInt64(span, ref offset);
        int ringLength = ReadInt32(span, ref offset);
        if (version >= 4)
            _ = ReadInt64(span, ref offset);

        if (version is not (3 or 4))
            throw new InvalidDataException($"ATTACH_RING protocol version '{version}' is unsupported.");

        if (ringPathLength < 0)
            throw new InvalidDataException($"ATTACH_RING ring path length is invalid. ringPathLength={ringPathLength}.");

        int remainingBytes = span.Length - offset;
        if (ringPathLength > remainingBytes) {
            throw new InvalidDataException(
                $"ATTACH_RING ring path payload is invalid. ringPathLength={ringPathLength} remainingBytes={remainingBytes} payloadLength={span.Length}.");
        }

        if (slotCount <= 0)
            throw new InvalidDataException($"ATTACH_RING slot count is invalid. slotCount={slotCount}.");

        if (slotPayloadBytes <= 0)
            throw new InvalidDataException($"ATTACH_RING slot payload size is invalid. slotPayloadBytes={slotPayloadBytes}.");

        if (ringLength <= 0)
            throw new InvalidDataException($"ATTACH_RING ring length is invalid. ringLength={ringLength}.");

        if (startSequence < 0)
            throw new InvalidDataException($"ATTACH_RING start sequence is invalid. startSequence={startSequence}.");

        if (sessionId <= 0)
            throw new InvalidDataException($"ATTACH_RING session id is invalid. sessionId={sessionId}.");

        string ringPath = System.Text.Encoding.UTF8.GetString(span.Slice(offset, ringPathLength));
        return new SidecarAttachRing(ringPath, slotCount, slotPayloadBytes, startSequence, sessionId, ringLength, version);
    }

    public static void WritePublish(Socket socket, ReadOnlyMemory<byte> payload)
        => SendAll(socket, BuildFrame(SidecarFrameType.Publish, payload.Span));

    public static void WriteDispose(Socket socket)
        => SendAll(socket, BuildFrame(SidecarFrameType.Dispose, ReadOnlySpan<byte>.Empty));

    public static void WriteHeartbeat(Socket socket)
        => SendAll(socket, BuildFrame(SidecarFrameType.Heartbeat, ReadOnlySpan<byte>.Empty));

    public static void WriteNotify(Socket socket)
        => SendAll(socket, BuildFrame(SidecarFrameType.Notify, ReadOnlySpan<byte>.Empty));

    public static void WriteReady(Socket socket)
        => SendAll(socket, BuildFrame(SidecarFrameType.Ready, ReadOnlySpan<byte>.Empty));

    public static void WriteError(Socket socket, string message)
        => SendAll(socket, BuildFrame(SidecarFrameType.Error, System.Text.Encoding.UTF8.GetBytes(message ?? string.Empty)));

    public static SidecarFrame ReadFrame(Socket socket) {
        byte[] header = ReceiveExact(socket, LengthPrefixBytes);
        int frameLength = BinaryPrimitives.ReadInt32LittleEndian(header);
        if (frameLength < 1)
            throw new InvalidDataException("Frame length is invalid.");

        byte[] content = ReceiveExact(socket, frameLength);
        SidecarFrameType type = (SidecarFrameType)content[0];
        byte[] payload = content.Length > 1 ? content[1..] : Array.Empty<byte>();
        return new SidecarFrame(type, payload);
    }

    private static byte[] BuildFrame(SidecarFrameType type, ReadOnlySpan<byte> payload) {
        byte[] buffer = new byte[LengthPrefixBytes + 1 + payload.Length];
        BinaryPrimitives.WriteInt32LittleEndian(buffer.AsSpan(0, 4), 1 + payload.Length);
        buffer[4] = (byte)type;
        payload.CopyTo(buffer.AsSpan(5));
        return buffer;
    }

    private static byte[] ReceiveExact(Socket socket, int length) {
        byte[] buffer = new byte[length];
        int offset = 0;

        while (offset < length) {
            int read = socket.Receive(buffer, offset, length - offset, SocketFlags.None);
            if (read == 0)
                throw new EndOfStreamException();

            offset += read;
        }

        return buffer;
    }

    private static void SendAll(Socket socket, byte[] buffer) {
        int offset = 0;
        while (offset < buffer.Length) {
            int written = socket.Send(buffer, offset, buffer.Length - offset, SocketFlags.None);
            if (written <= 0)
                throw new IOException("Socket send returned no bytes.");

            offset += written;
        }
    }

    private static void WriteInt32(byte[] buffer, ref int offset, int value) {
        BinaryPrimitives.WriteInt32LittleEndian(buffer.AsSpan(offset, 4), value);
        offset += 4;
    }

    private static void WriteGuid(byte[] buffer, ref int offset, Guid value) {
        value.TryWriteBytes(buffer.AsSpan(offset, 16));
        offset += 16;
    }

    private static void WriteInt64(byte[] buffer, ref int offset, long value) {
        BinaryPrimitives.WriteInt64LittleEndian(buffer.AsSpan(offset, 8), value);
        offset += 8;
    }

    private static int ReadInt32(ReadOnlySpan<byte> span, ref int offset) {
        int value = BinaryPrimitives.ReadInt32LittleEndian(span.Slice(offset, 4));
        offset += 4;
        return value;
    }

    private static long ReadInt64(ReadOnlySpan<byte> span, ref int offset) {
        long value = BinaryPrimitives.ReadInt64LittleEndian(span.Slice(offset, 8));
        offset += 8;
        return value;
    }

    private static Guid ReadGuid(ReadOnlySpan<byte> span, ref int offset) {
        Guid value = new(span.Slice(offset, 16));
        offset += 16;
        return value;
    }
}

