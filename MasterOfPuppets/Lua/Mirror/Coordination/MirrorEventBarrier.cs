using System;

namespace MasterOfPuppets.LuaScripting.Mirror.Coordination;

public enum MirrorBarrierPhase {
    CollectingReady,
    Released,
    AwaitingRetryDecision,
    Converged,
    Superseded,
}

public enum MirrorRetryScope {
    FailedParticipantsOnly,
    WholeParticipatingRoster,
}

public sealed record MirrorBarrierSnapshot(
    MirrorEventStamp Stamp,
    MirrorBarrierPhase Phase,
    uint Attempt,
    MirrorParticipantMask EventParticipants,
    MirrorParticipantMask AttemptParticipants,
    MirrorParticipantMask ReadyParticipants,
    MirrorParticipantMask SucceededParticipants,
    MirrorParticipantMask FailedParticipants,
    MirrorParticipantMask ConvergedParticipants,
    MirrorParticipantMask PendingOutcomeParticipants,
    MirrorParticipantMask RecommendedRetryParticipants,
    ulong Revision);

/// <summary>
/// Pure event barrier and result reducer. It never advances on a timer and never
/// removes participants. A caller must explicitly decide whether to retry or
/// supersede when readiness/outcomes cannot complete (OD-A/OD-C).
/// </summary>
public sealed class MirrorEventBarrier {
    private readonly MirrorRetryScope _retryScope;
    private readonly MirrorParticipantMask _eventParticipants;
    private MirrorParticipantMask _attemptParticipants;
    private MirrorParticipantMask _ready;
    private MirrorParticipantMask _succeeded;
    private MirrorParticipantMask _failed;
    private MirrorParticipantMask _converged;
    private MirrorBarrierPhase _phase = MirrorBarrierPhase.CollectingReady;
    private uint _attempt = 1;
    private ulong _revision;

    public MirrorEventBarrier(
        MirrorEventStamp stamp,
        MirrorParticipantMask participants,
        MirrorRetryScope retryScope) {
        Stamp = stamp.Validate();
        if (participants.IsEmpty)
            throw new ArgumentException("Mirror barrier requires at least one participant.", nameof(participants));
        if (!Enum.IsDefined(retryScope))
            throw new ArgumentOutOfRangeException(nameof(retryScope));
        _eventParticipants = participants;
        _attemptParticipants = participants;
        _retryScope = retryScope;
    }

    public MirrorEventStamp Stamp { get; }
    public MirrorBarrierSnapshot Snapshot => CreateSnapshot();

    public bool MarkReady(MirrorParticipantMask participants, out string reason) {
        if (_phase != MirrorBarrierPhase.CollectingReady) {
            reason = $"readiness is not accepted while barrier is {_phase}";
            return false;
        }
        if (!participants.IsSubsetOf(_attemptParticipants)) {
            reason = "readiness mask contains a participant outside the active attempt";
            return false;
        }
        _ready = _ready.Union(participants);
        _revision++;
        reason = string.Empty;
        return true;
    }

    public bool TryRelease(out string reason) {
        if (_phase == MirrorBarrierPhase.Released) {
            reason = string.Empty;
            return true;
        }
        if (_phase != MirrorBarrierPhase.CollectingReady) {
            reason = $"barrier cannot release while {_phase}";
            return false;
        }
        var missing = _attemptParticipants.Except(_ready);
        if (!missing.IsEmpty) {
            reason = $"waiting for participant mask 0x{missing.Bits:X8}";
            return false;
        }
        _phase = MirrorBarrierPhase.Released;
        _revision++;
        reason = string.Empty;
        return true;
    }

    public bool RecordOutcome(int participantIndex, uint attempt, bool succeeded, out string reason) {
        if (_phase != MirrorBarrierPhase.Released) {
            reason = $"outcome is not accepted while barrier is {_phase}";
            return false;
        }
        if (attempt != _attempt) {
            reason = attempt < _attempt ? "stale attempt outcome" : "future attempt outcome";
            return false;
        }
        var participant = MirrorParticipantMask.One(participantIndex);
        if (!participant.IsSubsetOf(_attemptParticipants)) {
            reason = "outcome participant is outside the active attempt";
            return false;
        }
        if (!participant.Intersect(_succeeded.Union(_failed)).IsEmpty) {
            var same = succeeded
                ? !participant.Intersect(_succeeded).IsEmpty
                : !participant.Intersect(_failed).IsEmpty;
            reason = same ? string.Empty : "conflicting outcome for participant and attempt";
            return same;
        }

        if (succeeded) {
            _succeeded = _succeeded.Union(participant);
            _converged = _converged.Union(participant);
        } else {
            _failed = _failed.Union(participant);
        }
        _revision++;

        if (_attemptParticipants.Except(_succeeded.Union(_failed)).IsEmpty)
            _phase = _failed.IsEmpty
                ? MirrorBarrierPhase.Converged
                : MirrorBarrierPhase.AwaitingRetryDecision;

        reason = string.Empty;
        return true;
    }

    public bool BeginRetry(out string reason) {
        if (_phase != MirrorBarrierPhase.AwaitingRetryDecision) {
            reason = $"retry cannot begin while barrier is {_phase}";
            return false;
        }
        _attemptParticipants = _retryScope == MirrorRetryScope.WholeParticipatingRoster
            ? _eventParticipants
            : _failed;
        if (_retryScope == MirrorRetryScope.WholeParticipatingRoster)
            _converged = MirrorParticipantMask.Empty;
        if (_attempt == uint.MaxValue)
            throw new InvalidOperationException("Mirror barrier attempt counter is exhausted.");
        _attempt++;
        _ready = MirrorParticipantMask.Empty;
        _succeeded = MirrorParticipantMask.Empty;
        _failed = MirrorParticipantMask.Empty;
        _phase = MirrorBarrierPhase.CollectingReady;
        _revision++;
        reason = string.Empty;
        return true;
    }

    public void Supersede() {
        if (_phase == MirrorBarrierPhase.Superseded)
            return;
        _phase = MirrorBarrierPhase.Superseded;
        _revision++;
    }

    private MirrorBarrierSnapshot CreateSnapshot() {
        var completed = _succeeded.Union(_failed);
        var retry = _phase == MirrorBarrierPhase.AwaitingRetryDecision
            ? (_retryScope == MirrorRetryScope.WholeParticipatingRoster ? _eventParticipants : _failed)
            : MirrorParticipantMask.Empty;
        return new MirrorBarrierSnapshot(
            Stamp,
            _phase,
            _attempt,
            _eventParticipants,
            _attemptParticipants,
            _ready,
            _succeeded,
            _failed,
            _converged,
            _attemptParticipants.Except(completed),
            retry,
            _revision);
    }
}
