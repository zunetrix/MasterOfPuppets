using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace MasterOfPuppets.LuaScripting.Synchronization;

public enum LuaDistributedMessageKind : byte {
    Prepare = 1,
    Stage = 2,
    Ready = 3,
    Go = 4,
    Heartbeat = 5,
    Ack = 6,
    Nack = 7,
    Stop = 8,
    Complete = 9,
    Error = 10,
    ClockProbe = 11,
    ClockReply = 12,
    SharedVariable = 13,
    ParticipantMessage = 14,
}

public sealed record LuaDistributedWireEnvelope {
    public LuaDistributedMessageKind Kind { get; init; }
    public Guid MessageId { get; init; }
    public long CreatedUnixMilliseconds { get; init; }
    public string RunToken { get; init; } = string.Empty;
    public ulong SenderContentId { get; init; }
    public string BundleHash { get; init; } = string.Empty;
    public IReadOnlyList<ulong> ParticipantCids { get; init; } = Array.Empty<ulong>();
    public int ReadyTimeoutMilliseconds { get; init; }
    public LuaReadinessTimeoutPolicy TimeoutPolicy { get; init; }
    public float PositionError { get; init; }
    public float FacingErrorRadians { get; init; }
    public float Speed { get; init; }
    public long GoUnixMilliseconds { get; init; }
    public double GoSharedClockSeconds { get; init; }
    public LuaParticipantProtocolState ParticipantState { get; init; }
    public double SharedClockSeconds { get; init; }
    public Guid CorrelationMessageId { get; init; }
    public double ProbeLocalSendSeconds { get; init; }
    public double RemoteReceiveSeconds { get; init; }
    public double RemoteSendSeconds { get; init; }
    public long CoordinationSequence { get; init; }
    public ulong TargetContentId { get; init; }
    public int SchemaVersion { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Detail { get; init; } = string.Empty;
}

/// <summary>
/// Compact authenticated-by-context transport framing for chat delivery. The
/// four-byte digest detects corruption; sender trust and replay policy remain
/// separate and must use the actual chat sender.
/// </summary>
public static class LuaDistributedWireCodec {
    public const byte ProtocolVersion = 3;
    public const int MaximumRosterCount = 32;
    public const int MaximumNameBytes = 32;
    public const int MaximumDetailBytes = 96;
    private static readonly byte[] Magic = "MLD"u8.ToArray();

    public static string CreateRunToken(string runId) {
        if (string.IsNullOrWhiteSpace(runId))
            throw new ArgumentException("run ID is required", nameof(runId));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(runId.Trim()))[..16]).ToLowerInvariant();
    }

    public static string Encode(LuaDistributedWireEnvelope envelope) {
        Validate(envelope);
        using var stream = new MemoryStream(384);
        using (var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true)) {
            writer.Write(Magic);
            writer.Write(ProtocolVersion);
            writer.Write((byte)envelope.Kind);
            writer.Write(envelope.MessageId.ToByteArray());
            writer.Write(envelope.CreatedUnixMilliseconds);
            writer.Write(envelope.SenderContentId);
            writer.Write(Convert.FromHexString(envelope.RunToken));
            switch (envelope.Kind) {
                case LuaDistributedMessageKind.Prepare:
                    writer.Write(Convert.FromHexString(envelope.BundleHash));
                    writer.Write((byte)envelope.ParticipantCids.Count);
                    foreach (var cid in envelope.ParticipantCids)
                        writer.Write(cid);
                    writer.Write(envelope.ReadyTimeoutMilliseconds);
                    writer.Write((byte)envelope.TimeoutPolicy);
                    break;
                case LuaDistributedMessageKind.Stage:
                case LuaDistributedMessageKind.Ready:
                    writer.Write(envelope.PositionError);
                    writer.Write(envelope.FacingErrorRadians);
                    writer.Write(envelope.Speed);
                    break;
                case LuaDistributedMessageKind.Go:
                    writer.Write(envelope.GoUnixMilliseconds);
                    writer.Write((byte)envelope.TimeoutPolicy);
                    writer.Write(envelope.GoSharedClockSeconds);
                    break;
                case LuaDistributedMessageKind.Heartbeat:
                    writer.Write((byte)envelope.ParticipantState);
                    writer.Write(envelope.SharedClockSeconds);
                    break;
                case LuaDistributedMessageKind.Nack:
                case LuaDistributedMessageKind.Stop:
                case LuaDistributedMessageKind.Complete:
                case LuaDistributedMessageKind.Error:
                    WriteShortString(writer, envelope.Detail);
                    break;
                case LuaDistributedMessageKind.Ack:
                    break;
                case LuaDistributedMessageKind.ClockProbe:
                    writer.Write(envelope.ProbeLocalSendSeconds);
                    break;
                case LuaDistributedMessageKind.ClockReply:
                    writer.Write(envelope.CorrelationMessageId.ToByteArray());
                    writer.Write(envelope.ProbeLocalSendSeconds);
                    writer.Write(envelope.RemoteReceiveSeconds);
                    writer.Write(envelope.RemoteSendSeconds);
                    break;
                case LuaDistributedMessageKind.SharedVariable:
                    writer.Write(envelope.CoordinationSequence);
                    WriteShortString(writer, envelope.Name, MaximumNameBytes);
                    WriteShortString(writer, envelope.Detail);
                    break;
                case LuaDistributedMessageKind.ParticipantMessage:
                    writer.Write(envelope.CoordinationSequence);
                    writer.Write(envelope.TargetContentId);
                    writer.Write((byte)envelope.SchemaVersion);
                    WriteShortString(writer, envelope.Name, MaximumNameBytes);
                    WriteShortString(writer, envelope.Detail);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(envelope.Kind));
            }
        }

        var body = stream.ToArray();
        var digest = SHA256.HashData(body);
        var framed = new byte[body.Length + 4];
        body.CopyTo(framed, 0);
        digest.AsSpan(0, 4).CopyTo(framed.AsSpan(body.Length));
        return Convert.ToBase64String(framed).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    public static bool TryDecode(string token, out LuaDistributedWireEnvelope envelope, out string error) {
        envelope = new LuaDistributedWireEnvelope();
        error = string.Empty;
        try {
            if (string.IsNullOrWhiteSpace(token) || token.Length > 700)
                throw new InvalidDataException("distributed Lua token length is invalid");
            var normalized = token.Trim().Replace('-', '+').Replace('_', '/');
            normalized = normalized.PadRight((normalized.Length + 3) / 4 * 4, '=');
            var framed = Convert.FromBase64String(normalized);
            if (framed.Length < 3 + 1 + 1 + 16 + 8 + 8 + 16 + 4)
                throw new InvalidDataException("distributed Lua token is truncated");
            var body = framed.AsSpan(0, framed.Length - 4);
            var expectedDigest = SHA256.HashData(body);
            if (!CryptographicOperations.FixedTimeEquals(expectedDigest.AsSpan(0, 4), framed.AsSpan(framed.Length - 4)))
                throw new InvalidDataException("distributed Lua token checksum failed");

            using var stream = new MemoryStream(body.ToArray(), writable: false);
            using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: false);
            if (!reader.ReadBytes(3).SequenceEqual(Magic))
                throw new InvalidDataException("distributed Lua token magic is invalid");
            var version = reader.ReadByte();
            if (version != ProtocolVersion)
                throw new InvalidDataException($"unsupported distributed Lua protocol version {version}");
            var kind = (LuaDistributedMessageKind)reader.ReadByte();
            var value = new LuaDistributedWireEnvelope {
                Kind = kind,
                MessageId = new Guid(ReadExact(reader, 16)),
                CreatedUnixMilliseconds = reader.ReadInt64(),
                SenderContentId = reader.ReadUInt64(),
                RunToken = Convert.ToHexString(ReadExact(reader, 16)).ToLowerInvariant(),
            };
            value = kind switch {
                LuaDistributedMessageKind.Prepare => value with {
                    BundleHash = Convert.ToHexString(ReadExact(reader, 32)).ToLowerInvariant(),
                    ParticipantCids = ReadRoster(reader),
                    ReadyTimeoutMilliseconds = reader.ReadInt32(),
                    TimeoutPolicy = (LuaReadinessTimeoutPolicy)reader.ReadByte(),
                },
                LuaDistributedMessageKind.Stage or LuaDistributedMessageKind.Ready => value with {
                    PositionError = reader.ReadSingle(),
                    FacingErrorRadians = reader.ReadSingle(),
                    Speed = reader.ReadSingle(),
                },
                LuaDistributedMessageKind.Go => value with {
                    GoUnixMilliseconds = reader.ReadInt64(),
                    TimeoutPolicy = (LuaReadinessTimeoutPolicy)reader.ReadByte(),
                    GoSharedClockSeconds = reader.ReadDouble(),
                },
                LuaDistributedMessageKind.Heartbeat => value with {
                    ParticipantState = (LuaParticipantProtocolState)reader.ReadByte(),
                    SharedClockSeconds = reader.ReadDouble(),
                },
                LuaDistributedMessageKind.Nack or LuaDistributedMessageKind.Stop
                    or LuaDistributedMessageKind.Complete or LuaDistributedMessageKind.Error =>
                    value with { Detail = ReadShortString(reader) },
                LuaDistributedMessageKind.Ack => value,
                LuaDistributedMessageKind.ClockProbe => value with {
                    ProbeLocalSendSeconds = reader.ReadDouble(),
                },
                LuaDistributedMessageKind.ClockReply => value with {
                    CorrelationMessageId = new Guid(ReadExact(reader, 16)),
                    ProbeLocalSendSeconds = reader.ReadDouble(),
                    RemoteReceiveSeconds = reader.ReadDouble(),
                    RemoteSendSeconds = reader.ReadDouble(),
                },
                LuaDistributedMessageKind.SharedVariable => value with {
                    CoordinationSequence = reader.ReadInt64(),
                    Name = ReadShortString(reader, MaximumNameBytes),
                    Detail = ReadShortString(reader),
                },
                LuaDistributedMessageKind.ParticipantMessage => value with {
                    CoordinationSequence = reader.ReadInt64(),
                    TargetContentId = reader.ReadUInt64(),
                    SchemaVersion = reader.ReadByte(),
                    Name = ReadShortString(reader, MaximumNameBytes),
                    Detail = ReadShortString(reader),
                },
                _ => throw new InvalidDataException($"unknown distributed Lua message kind {(byte)kind}"),
            };
            if (stream.Position != stream.Length)
                throw new InvalidDataException("distributed Lua token has trailing data");
            Validate(value);
            envelope = value;
            return true;
        } catch (Exception ex) when (ex is ArgumentException or FormatException or EndOfStreamException or InvalidDataException or IOException) {
            error = ex.Message;
            return false;
        }
    }

    private static void Validate(LuaDistributedWireEnvelope envelope) {
        if (!Enum.IsDefined(envelope.Kind))
            throw new ArgumentOutOfRangeException(nameof(envelope.Kind));
        if (envelope.MessageId == Guid.Empty)
            throw new ArgumentException("distributed Lua message ID is required");
        if (envelope.CreatedUnixMilliseconds <= 0)
            throw new ArgumentException("distributed Lua creation time is required");
        if (!IsHex(envelope.RunToken, 32))
            throw new ArgumentException("distributed Lua run token must be 128-bit hex");
        if (envelope.SenderContentId == 0)
            throw new ArgumentException("distributed Lua sender content ID is required");

        switch (envelope.Kind) {
            case LuaDistributedMessageKind.Prepare:
                if (!IsHex(envelope.BundleHash, 64))
                    throw new ArgumentException("distributed Lua bundle hash must be SHA-256 hex");
                if (envelope.ParticipantCids.Count is 0 or > MaximumRosterCount
                    || envelope.ParticipantCids.Any(cid => cid == 0)
                    || envelope.ParticipantCids.Distinct().Count() != envelope.ParticipantCids.Count)
                    throw new ArgumentException($"distributed Lua chat roster must contain 1 to {MaximumRosterCount} unique content IDs");
                if (envelope.ReadyTimeoutMilliseconds is < 1000 or > 120_000)
                    throw new ArgumentOutOfRangeException(nameof(envelope.ReadyTimeoutMilliseconds));
                if (!Enum.IsDefined(envelope.TimeoutPolicy))
                    throw new ArgumentOutOfRangeException(nameof(envelope.TimeoutPolicy));
                break;
            case LuaDistributedMessageKind.Stage:
            case LuaDistributedMessageKind.Ready:
                if (!float.IsFinite(envelope.PositionError) || envelope.PositionError < 0
                    || !float.IsFinite(envelope.FacingErrorRadians)
                    || !float.IsFinite(envelope.Speed) || envelope.Speed < 0)
                    throw new ArgumentException("distributed Lua stage observation is invalid");
                break;
            case LuaDistributedMessageKind.ClockProbe:
                if (!double.IsFinite(envelope.ProbeLocalSendSeconds) || envelope.ProbeLocalSendSeconds < 0)
                    throw new ArgumentException("distributed Lua clock probe is invalid");
                break;
            case LuaDistributedMessageKind.ClockReply:
                if (envelope.CorrelationMessageId == Guid.Empty
                    || !double.IsFinite(envelope.ProbeLocalSendSeconds)
                    || !double.IsFinite(envelope.RemoteReceiveSeconds)
                    || !double.IsFinite(envelope.RemoteSendSeconds)
                    || envelope.ProbeLocalSendSeconds < 0
                    || envelope.RemoteReceiveSeconds < 0
                    || envelope.RemoteSendSeconds < envelope.RemoteReceiveSeconds)
                    throw new ArgumentException("distributed Lua clock reply is invalid");
                break;
            case LuaDistributedMessageKind.Go:
                if (envelope.GoUnixMilliseconds <= envelope.CreatedUnixMilliseconds)
                    throw new ArgumentException("distributed Lua GO epoch must be in the future");
                if (!Enum.IsDefined(envelope.TimeoutPolicy))
                    throw new ArgumentOutOfRangeException(nameof(envelope.TimeoutPolicy));
                if (!double.IsFinite(envelope.GoSharedClockSeconds) || envelope.GoSharedClockSeconds <= 0)
                    throw new ArgumentException("distributed Lua GO shared clock is invalid");
                break;
            case LuaDistributedMessageKind.SharedVariable:
                ValidateCoordinationText(envelope, requireSchema: false);
                break;
            case LuaDistributedMessageKind.ParticipantMessage:
                ValidateCoordinationText(envelope, requireSchema: true);
                break;
            case LuaDistributedMessageKind.Heartbeat:
                if (!Enum.IsDefined(envelope.ParticipantState) || !double.IsFinite(envelope.SharedClockSeconds))
                    throw new ArgumentException("distributed Lua heartbeat state is invalid");
                break;
            case LuaDistributedMessageKind.Nack:
            case LuaDistributedMessageKind.Stop:
            case LuaDistributedMessageKind.Complete:
            case LuaDistributedMessageKind.Error:
                if (Encoding.UTF8.GetByteCount(envelope.Detail ?? string.Empty) > MaximumDetailBytes)
                    throw new ArgumentException("distributed Lua detail is too long");
                break;
        }
    }

    private static IReadOnlyList<ulong> ReadRoster(BinaryReader reader) {
        var count = reader.ReadByte();
        if (count is 0 or > MaximumRosterCount)
            throw new InvalidDataException("distributed Lua roster count is invalid");
        var roster = new ulong[count];
        for (var index = 0; index < count; index++)
            roster[index] = reader.ReadUInt64();
        return roster;
    }

    private static byte[] ReadExact(BinaryReader reader, int count) {
        var bytes = reader.ReadBytes(count);
        if (bytes.Length != count)
            throw new EndOfStreamException();
        return bytes;
    }

    private static void WriteShortString(BinaryWriter writer, string value, int maximumBytes = MaximumDetailBytes) {
        var bytes = Encoding.UTF8.GetBytes(value ?? string.Empty);
        if (bytes.Length > maximumBytes)
            throw new ArgumentException("distributed Lua detail is too long");
        writer.Write((byte)bytes.Length);
        writer.Write(bytes);
    }

    private static string ReadShortString(BinaryReader reader, int maximumBytes = MaximumDetailBytes) {
        var count = reader.ReadByte();
        if (count > maximumBytes)
            throw new InvalidDataException("distributed Lua detail is too long");
        return Encoding.UTF8.GetString(ReadExact(reader, count));
    }

    private static void ValidateCoordinationText(LuaDistributedWireEnvelope envelope, bool requireSchema) {
        if (envelope.CoordinationSequence <= 0)
            throw new ArgumentException("distributed Lua coordination sequence is required");
        if (string.IsNullOrWhiteSpace(envelope.Name)
            || Encoding.UTF8.GetByteCount(envelope.Name) > MaximumNameBytes
            || envelope.Name.IndexOfAny(['\0', '\r', '\n']) >= 0)
            throw new ArgumentException("distributed Lua coordination name is invalid");
        if (Encoding.UTF8.GetByteCount(envelope.Detail ?? string.Empty) > MaximumDetailBytes
            || (envelope.Detail?.IndexOfAny(['\0', '\r', '\n']) ?? -1) >= 0)
            throw new ArgumentException("distributed Lua coordination payload is invalid");
        if (requireSchema && envelope.SchemaVersion is < 1 or > byte.MaxValue)
            throw new ArgumentException("distributed Lua message schema version is invalid");
    }

    private static bool IsHex(string? value, int length) =>
        value?.Length == length && value.All(Uri.IsHexDigit);
}
