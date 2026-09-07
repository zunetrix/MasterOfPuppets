using System;
using System.Collections.Generic;

namespace MasterOfPuppets.LuaScripting.Coordination;

public sealed record LuaSharedVariableSnapshot(
    string Key,
    string Value,
    long Sequence,
    ulong SenderContentId,
    DateTimeOffset UpdatedAt);

public sealed record LuaParticipantMessageSnapshot(
    Guid MessageId,
    string Topic,
    string Payload,
    int SchemaVersion,
    long Sequence,
    ulong SenderContentId,
    ulong TargetContentId,
    DateTimeOffset ReceivedAt);

public sealed record LuaCoordinationResult(bool Ok, string Status, string Message) {
    public static LuaCoordinationResult Success(string status) => new(true, status, string.Empty);
    public static LuaCoordinationResult Failure(string message) => new(false, "error", message);
}

public interface ILuaCoordinationFacade {
    bool IsDistributed { get; }
    bool IsConductor { get; }
    ulong LocalContentId { get; }
    IReadOnlyList<ulong> ParticipantContentIds { get; }
    IReadOnlyList<LuaSharedVariableSnapshot> SharedVariables { get; }
    LuaCoordinationResult SetShared(string key, string value);
    bool TryGetShared(string key, out LuaSharedVariableSnapshot? value);
    LuaCoordinationResult SendMessage(string topic, string payload, int schemaVersion, ulong targetContentId);
    bool TryReadMessage(string? topic, out LuaParticipantMessageSnapshot? message);
}
