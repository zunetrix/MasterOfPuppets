using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace MasterOfPuppets.LuaScripting.Mirror.Coordination;

public enum MirrorCoordinationMessageKind {
    Ready,
    Release,
    Outcome,
    FallbackSelection,
    Superseded,
}

public sealed record MirrorCoordinationEnvelope(
    Guid MessageId,
    MirrorEventStamp Stamp,
    int SenderIndex,
    ulong SenderSequence,
    MirrorCoordinationMessageKind Kind,
    uint Attempt,
    MirrorParticipantMask Participants,
    string Payload);

/// <summary>Transport boundary for IPC/chat/local adapters.</summary>
public interface IMirrorCoordinationTransport {
    ValueTask PublishAsync(MirrorCoordinationEnvelope envelope, CancellationToken cancellationToken);
}

/// <summary>
/// Admission gate for one authoritative event. It rejects old/future event data,
/// duplicate message IDs, and non-increasing per-sender sequences before state is
/// reduced. Moving the gate requires an explicitly newer authoritative stamp.
/// </summary>
public sealed class MirrorMessageAdmissionGate {
    public const int DefaultReplayCapacity = 2048;

    private readonly MirrorRoster _roster;
    private readonly int _replayCapacity;
    private readonly HashSet<Guid> _seenMessageIds = new();
    private readonly Queue<Guid> _seenOrder = new();
    private readonly ulong[] _lastSenderSequences;
    private MirrorEventStamp _authoritativeStamp;

    public MirrorMessageAdmissionGate(
        MirrorRoster roster,
        MirrorEventStamp authoritativeStamp,
        int replayCapacity = DefaultReplayCapacity) {
        _roster = roster ?? throw new ArgumentNullException(nameof(roster));
        _authoritativeStamp = authoritativeStamp.Validate();
        if (replayCapacity < MirrorRoster.MaximumParticipants)
            throw new ArgumentOutOfRangeException(nameof(replayCapacity));
        _replayCapacity = replayCapacity;
        _lastSenderSequences = new ulong[roster.Count];
    }

    public MirrorEventStamp AuthoritativeStamp => _authoritativeStamp;

    public void MoveTo(MirrorEventStamp authoritativeStamp) {
        var next = authoritativeStamp.Validate();
        if (next.RunId != _authoritativeStamp.RunId)
            throw new InvalidOperationException("Cannot move a Mirror admission gate to another run.");
        if (next.Epoch <= _authoritativeStamp.Epoch)
            throw new InvalidOperationException("Authoritative Mirror event epoch must increase.");
        _authoritativeStamp = next;
        Array.Clear(_lastSenderSequences);
    }

    public bool TryAccept(MirrorCoordinationEnvelope envelope, out string reason) {
        ArgumentNullException.ThrowIfNull(envelope);
        if (envelope.MessageId == Guid.Empty) {
            reason = "coordination message ID is required";
            return false;
        }
        MirrorEventStamp stamp;
        try {
            stamp = envelope.Stamp.Validate();
        } catch (ArgumentException exception) {
            reason = exception.Message;
            return false;
        }
        if (stamp.RunId != _authoritativeStamp.RunId) {
            reason = "coordination message belongs to another Mirror run";
            return false;
        }
        if (stamp != _authoritativeStamp) {
            reason = stamp.Epoch < _authoritativeStamp.Epoch
                ? "stale Mirror event message"
                : "message does not match the current authoritative Mirror event";
            return false;
        }
        if (envelope.SenderIndex < 0 || envelope.SenderIndex >= _roster.Count) {
            reason = "coordination sender is outside the authoritative Mirror roster";
            return false;
        }
        if (envelope.SenderSequence == 0) {
            reason = "coordination sender sequence must be positive";
            return false;
        }
        if (!envelope.Participants.IsSubsetOf(_roster.AllParticipants)) {
            reason = "coordination participant mask is outside the authoritative Mirror roster";
            return false;
        }
        if (_seenMessageIds.Contains(envelope.MessageId)) {
            reason = "coordination message is a replay";
            return false;
        }
        if (envelope.SenderSequence <= _lastSenderSequences[envelope.SenderIndex]) {
            reason = "coordination sender sequence is stale or replayed";
            return false;
        }

        _lastSenderSequences[envelope.SenderIndex] = envelope.SenderSequence;
        _seenMessageIds.Add(envelope.MessageId);
        _seenOrder.Enqueue(envelope.MessageId);
        while (_seenOrder.Count > _replayCapacity)
            _seenMessageIds.Remove(_seenOrder.Dequeue());
        reason = string.Empty;
        return true;
    }
}

/// <summary>
/// Authoritative per-attempt result broadcast. OutcomeRevision permits every
/// receiver to converge on the same latest coordinator snapshot.
/// </summary>
public sealed record MirrorAuthoritativeOutcome(
    MirrorEventStamp Stamp,
    uint Attempt,
    MirrorParticipantMask AttemptParticipants,
    MirrorParticipantMask SucceededParticipants,
    MirrorParticipantMask FailedParticipants,
    ulong OutcomeRevision) {

    public MirrorAuthoritativeOutcome Validate() {
        var stamp = Stamp.Validate();
        if (Attempt == 0)
            throw new ArgumentOutOfRangeException(nameof(Attempt));
        if (AttemptParticipants.IsEmpty)
            throw new ArgumentException("Authoritative outcome requires attempt participants.", nameof(AttemptParticipants));
        if (!SucceededParticipants.IsSubsetOf(AttemptParticipants)
            || !FailedParticipants.IsSubsetOf(AttemptParticipants))
            throw new ArgumentException("Authoritative outcome contains a participant outside the attempt.");
        if (!SucceededParticipants.Intersect(FailedParticipants).IsEmpty)
            throw new ArgumentException("A participant cannot be both successful and failed in one attempt.");
        if (OutcomeRevision == 0)
            throw new ArgumentOutOfRangeException(nameof(OutcomeRevision));
        return this with { Stamp = stamp };
    }
}

public sealed class MirrorOutcomeConvergenceState {
    private readonly MirrorEventStamp _stamp;
    private MirrorAuthoritativeOutcome _latest;

    public MirrorOutcomeConvergenceState(MirrorEventStamp stamp) {
        _stamp = stamp.Validate();
    }

    public MirrorAuthoritativeOutcome Latest => _latest;

    public bool TryApply(MirrorAuthoritativeOutcome outcome, out string reason) {
        ArgumentNullException.ThrowIfNull(outcome);
        MirrorAuthoritativeOutcome validated;
        try {
            validated = outcome.Validate();
        } catch (ArgumentException exception) {
            reason = exception.Message;
            return false;
        }
        if (validated.Stamp != _stamp) {
            reason = validated.Stamp.RunId == _stamp.RunId && validated.Stamp.Epoch < _stamp.Epoch
                ? "stale authoritative outcome"
                : "outcome does not belong to this authoritative Mirror event";
            return false;
        }
        if (_latest != null && validated.OutcomeRevision <= _latest.OutcomeRevision) {
            reason = "stale or replayed authoritative outcome revision";
            return false;
        }
        if (_latest != null && validated.Attempt < _latest.Attempt) {
            reason = "authoritative outcome attempt moved backward";
            return false;
        }
        if (_latest != null
            && validated.Attempt == _latest.Attempt
            && validated.AttemptParticipants != _latest.AttemptParticipants) {
            reason = "authoritative outcome changed participants within an attempt";
            return false;
        }
        _latest = validated;
        reason = string.Empty;
        return true;
    }
}
