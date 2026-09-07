using System;
using System.Collections.Generic;
using System.Linq;

using MasterOfPuppets.Formations;

namespace MasterOfPuppets.LuaScripting.Synchronization;

public sealed record LuaDistributedSenderIdentity(ulong ContentId, string Name);

public static class LuaDistributedSenderPolicy {
    public static bool MatchesClaimedContentId(
        string actualSender,
        ulong claimedContentId,
        LuaDistributedSenderIdentity localPlayer,
        IReadOnlyCollection<LuaDistributedSenderIdentity> configuredParticipants,
        out string reason) {
        var sender = FormationCharacterName.NormalizeWorldSeparator(actualSender);
        if (claimedContentId == 0 || string.IsNullOrWhiteSpace(sender)) {
            reason = "phase sender identity is incomplete";
            return false;
        }
        var candidates = configuredParticipants
            .Append(localPlayer)
            .Where(candidate => candidate.ContentId == claimedContentId)
            .Select(candidate => FormationCharacterName.NormalizeWorldSeparator(candidate.Name))
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (candidates.Any(name => FormationCharacterName.Matches(name, sender))) {
            reason = string.Empty;
            return true;
        }
        reason = $"chat sender '{sender}' does not exactly match claimed content ID {claimedContentId}";
        return false;
    }
}

public sealed record LuaDistributedSessionSnapshot(
    string RunToken,
    string Conductor,
    ulong ConductorContentId,
    LuaDistributedRunProtocolSnapshot Protocol,
    LuaDistributedMessageKind LastMessageKind,
    ulong LastSenderContentId,
    DateTimeOffset LastMessageAt);

/// <summary>
/// Bounded transport-facing registry. It validates phase ordering through the
/// pure coordinator but does not start scripts or perform chat IO.
/// </summary>
public sealed class LuaDistributedSessionRegistry {
    public const int MaximumSessions = 64;
    private readonly object _sync = new();
    private readonly Dictionary<string, Session> _sessions = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<LuaDistributedSessionSnapshot> Snapshot() {
        lock (_sync)
            return _sessions.Values
                .OrderByDescending(session => session.LastMessageAt)
                .Select(ToSnapshot)
                .ToArray();
    }

    public void Tick(DateTimeOffset now, TimeSpan? heartbeatTimeout = null) {
        var timeout = heartbeatTimeout ?? TimeSpan.FromSeconds(30);
        if (timeout <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(heartbeatTimeout));
        lock (_sync) {
            foreach (var session in _sessions.Values) {
                session.Protocol.Tick(now);
                if (session.Protocol.Snapshot.Phase == LuaDistributedRunPhase.Running) {
                    var timedOut = session.Protocol.MarkHeartbeatTimeouts(now, timeout);
                    if (timedOut.Contains(session.ConductorContentId))
                        session.Protocol.Stop($"conductor heartbeat timeout ({session.ConductorContentId})");
                }
            }
            Prune(now);
        }
    }

    public void StopAll(string reason) {
        lock (_sync)
            foreach (var session in _sessions.Values)
                session.Protocol.Stop(reason);
    }

    public void Stop(string runToken, string reason) {
        lock (_sync) {
            if (_sessions.TryGetValue(runToken, out var session))
                session.Protocol.Stop(reason);
        }
    }

    public bool TryScheduleGo(
        string runToken,
        DateTimeOffset now,
        TimeSpan leadTime,
        LuaReadinessTimeoutPolicy timeoutPolicy,
        out DateTimeOffset goEpoch,
        out string reason) {
        lock (_sync) {
            if (!_sessions.TryGetValue(runToken, out var session)) {
                goEpoch = default;
                reason = $"distributed Lua session '{runToken}' was not prepared";
                return false;
            }
            return session.Protocol.TryScheduleGo(now, leadTime, timeoutPolicy, out goEpoch, out reason);
        }
    }

    public bool TryAccept(
        LuaDistributedWireEnvelope envelope,
        string actualSender,
        DateTimeOffset receivedAt,
        out LuaDistributedSessionSnapshot? snapshot,
        out string reason) {
        snapshot = null;
        reason = string.Empty;
        lock (_sync) {
            Prune(receivedAt);
            if (envelope.Kind == LuaDistributedMessageKind.Prepare)
                return AcceptPrepare(envelope, actualSender, receivedAt, out snapshot, out reason);
            if (!_sessions.TryGetValue(envelope.RunToken, out var session)) {
                reason = $"distributed Lua session '{envelope.RunToken}' was not prepared";
                return false;
            }
            if ((envelope.Kind is LuaDistributedMessageKind.Go or LuaDistributedMessageKind.Stop
                    or LuaDistributedMessageKind.ClockReply or LuaDistributedMessageKind.SharedVariable)
                && !FormationCharacterName.Matches(session.Conductor, actualSender)) {
                reason = $"distributed Lua {envelope.Kind} sender is not the prepared conductor";
                return false;
            }
            if (!session.Protocol.Snapshot.Participants.Any(item => item.ContentId == envelope.SenderContentId)) {
                reason = $"sender content ID {envelope.SenderContentId} is not in the authoritative roster";
                return false;
            }
            if (envelope.Kind == LuaDistributedMessageKind.ParticipantMessage
                && envelope.TargetContentId != 0
                && !session.Protocol.Snapshot.Participants.Any(item => item.ContentId == envelope.TargetContentId)) {
                reason = $"participant message target content ID {envelope.TargetContentId} is not in the authoritative roster";
                return false;
            }

            var observedAt = DateTimeOffset.FromUnixTimeMilliseconds(envelope.CreatedUnixMilliseconds);
            var accepted = envelope.Kind switch {
                LuaDistributedMessageKind.Stage or LuaDistributedMessageKind.Ready =>
                    session.Protocol.ObserveStage(new LuaStageObservation(
                        envelope.SenderContentId,
                        envelope.PositionError,
                        envelope.FacingErrorRadians,
                        envelope.Speed,
                        observedAt), out reason),
                LuaDistributedMessageKind.Go => session.Protocol.AcceptScheduledGo(
                    receivedAt,
                    DateTimeOffset.FromUnixTimeMilliseconds(envelope.GoUnixMilliseconds),
                    envelope.TimeoutPolicy,
                    out reason),
                LuaDistributedMessageKind.Heartbeat => session.Protocol.Heartbeat(envelope.SenderContentId, observedAt),
                LuaDistributedMessageKind.Complete => session.Protocol.CompleteParticipant(envelope.SenderContentId),
                LuaDistributedMessageKind.Error => session.Protocol.CompleteParticipant(envelope.SenderContentId, envelope.Detail),
                LuaDistributedMessageKind.Stop => AcceptStop(session, envelope.Detail),
                LuaDistributedMessageKind.Ack or LuaDistributedMessageKind.Nack
                    or LuaDistributedMessageKind.ClockProbe or LuaDistributedMessageKind.ClockReply
                    or LuaDistributedMessageKind.SharedVariable or LuaDistributedMessageKind.ParticipantMessage => true,
                _ => false,
            };
            if (!accepted) {
                if (string.IsNullOrWhiteSpace(reason))
                    reason = $"distributed Lua {envelope.Kind} message was rejected by the session state";
                return false;
            }
            session.LastMessageKind = envelope.Kind;
            session.LastSenderContentId = envelope.SenderContentId;
            session.LastMessageAt = receivedAt;
            session.Protocol.Tick(receivedAt);
            snapshot = ToSnapshot(session);
            return true;
        }
    }

    private bool AcceptPrepare(
        LuaDistributedWireEnvelope envelope,
        string actualSender,
        DateTimeOffset receivedAt,
        out LuaDistributedSessionSnapshot? snapshot,
        out string reason) {
        if (_sessions.TryGetValue(envelope.RunToken, out var existing)) {
            var state = existing.Protocol.Snapshot;
            if (!FormationCharacterName.Matches(existing.Conductor, actualSender)
                || !existing.BundleHash.Equals(envelope.BundleHash, StringComparison.OrdinalIgnoreCase)
                || !state.Participants.Select(item => item.ContentId).SequenceEqual(envelope.ParticipantCids)) {
                snapshot = null;
                reason = "distributed Lua PREPARE conflicts with the existing session";
                return false;
            }
            existing.LastMessageAt = receivedAt;
            snapshot = ToSnapshot(existing);
            reason = string.Empty;
            return true;
        }
        if (_sessions.Count >= MaximumSessions) {
            snapshot = null;
            reason = "distributed Lua session registry is full";
            return false;
        }
        var preparedAt = DateTimeOffset.FromUnixTimeMilliseconds(envelope.CreatedUnixMilliseconds);
        var coordinator = new LuaDistributedRunCoordinator(new LuaDistributedRunDescriptor(
            envelope.RunToken,
            envelope.BundleHash,
            actualSender,
            envelope.ParticipantCids,
            preparedAt,
            TimeSpan.FromMilliseconds(envelope.ReadyTimeoutMilliseconds)));
        coordinator.BeginStaging(receivedAt);
        var session = new Session(
            envelope.RunToken,
            actualSender,
            envelope.SenderContentId,
            envelope.BundleHash,
            coordinator,
            envelope.Kind,
            envelope.SenderContentId,
            receivedAt);
        _sessions.Add(envelope.RunToken, session);
        snapshot = ToSnapshot(session);
        reason = string.Empty;
        return true;
    }

    private static bool AcceptStop(Session session, string reason) {
        session.Protocol.Stop(reason);
        return true;
    }

    private void Prune(DateTimeOffset now) {
        foreach (var token in _sessions
                     .Where(pair => now - pair.Value.LastMessageAt > TimeSpan.FromMinutes(10))
                     .Select(pair => pair.Key)
                     .ToArray())
            _sessions.Remove(token);
    }

    private static LuaDistributedSessionSnapshot ToSnapshot(Session session) => new(
        session.RunToken,
        session.Conductor,
        session.ConductorContentId,
        session.Protocol.Snapshot,
        session.LastMessageKind,
        session.LastSenderContentId,
        session.LastMessageAt);

    private sealed class Session(
        string runToken,
        string conductor,
        ulong conductorContentId,
        string bundleHash,
        LuaDistributedRunCoordinator protocol,
        LuaDistributedMessageKind lastMessageKind,
        ulong lastSenderContentId,
        DateTimeOffset lastMessageAt) {
        public string RunToken { get; } = runToken;
        public string Conductor { get; } = conductor;
        public ulong ConductorContentId { get; } = conductorContentId;
        public string BundleHash { get; } = bundleHash;
        public LuaDistributedRunCoordinator Protocol { get; } = protocol;
        public LuaDistributedMessageKind LastMessageKind { get; set; } = lastMessageKind;
        public ulong LastSenderContentId { get; set; } = lastSenderContentId;
        public DateTimeOffset LastMessageAt { get; set; } = lastMessageAt;
    }
}
