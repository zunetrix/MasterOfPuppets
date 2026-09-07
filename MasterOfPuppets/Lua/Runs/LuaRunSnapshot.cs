using System;

namespace MasterOfPuppets.LuaScripting.Runs;

public sealed record LuaRunSnapshot(
    string RunId,
    string ScriptName,
    string ScriptHash,
    LuaRunState State,
    int Slot,
    int ParticipantCount,
    int Seed,
    DateTimeOffset CreatedAt,
    DateTimeOffset? StartedAt,
    DateTimeOffset? EndedAt,
    string Detail,
    string StopReason,
    LuaRunError? Error,
    long Revision) {

    public TimeSpan Elapsed(DateTimeOffset now) {
        var start = StartedAt ?? CreatedAt;
        return (EndedAt ?? now) - start;
    }

    public string ToStatusText() {
        var slot = ParticipantCount > 0 ? $" slot {Slot + 1}/{ParticipantCount}" : string.Empty;
        var detail = string.IsNullOrWhiteSpace(Detail) ? string.Empty : $" — {Detail}";
        var reason = State.IsTerminal() && !string.IsNullOrWhiteSpace(StopReason) ? $" ({StopReason})" : string.Empty;
        return $"{State.ToString().ToLowerInvariant()}: {ScriptName}{slot} [{RunId}]{reason}{detail}";
    }
}
