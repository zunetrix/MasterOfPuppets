using System;
using System.Collections.Generic;
using System.Linq;

namespace MasterOfPuppets.LuaScripting.Synchronization;

public enum LuaDistributedRunPhase {
    Prepared,
    Staging,
    Ready,
    GoScheduled,
    Running,
    Completed,
    Stopped,
    Failed,
}

public enum LuaParticipantProtocolState {
    Missing,
    Staging,
    Ready,
    Running,
    Completed,
    Failed,
}

public enum LuaReadinessTimeoutPolicy {
    Abort,
    ContinueWithReadyParticipants,
    ContinueAll,
}

public sealed record LuaDistributedRunDescriptor(
    string RunId,
    string BundleHash,
    string Conductor,
    IReadOnlyList<ulong> ParticipantCids,
    DateTimeOffset PreparedAt,
    TimeSpan ReadyTimeout) {

    public LuaDistributedRunDescriptor Validate() {
        if (string.IsNullOrWhiteSpace(RunId))
            throw new ArgumentException("Distributed Lua run ID is required.");
        if (BundleHash?.Length != 64 || BundleHash.Any(character => !Uri.IsHexDigit(character)))
            throw new ArgumentException("Distributed Lua bundle hash must be SHA-256 hex.");
        if (string.IsNullOrWhiteSpace(Conductor))
            throw new ArgumentException("Distributed Lua conductor identity is required.");
        if (ParticipantCids == null || ParticipantCids.Count is 0 or > 256
            || ParticipantCids.Any(cid => cid == 0)
            || ParticipantCids.Distinct().Count() != ParticipantCids.Count)
            throw new ArgumentException("Distributed Lua roster must contain 1 to 256 unique non-zero content IDs.");
        if (ReadyTimeout <= TimeSpan.Zero || ReadyTimeout > TimeSpan.FromMinutes(2))
            throw new ArgumentOutOfRangeException(nameof(ReadyTimeout));
        return this with {
            RunId = RunId.Trim(),
            BundleHash = BundleHash.ToLowerInvariant(),
            Conductor = Conductor.Trim(),
            ParticipantCids = ParticipantCids.ToArray(),
        };
    }
}

public readonly record struct LuaStageObservation(
    ulong ContentId,
    float PositionError,
    float FacingErrorRadians,
    float Speed,
    DateTimeOffset ObservedAt) {

    public bool IsFinite => float.IsFinite(PositionError)
        && float.IsFinite(FacingErrorRadians)
        && float.IsFinite(Speed);
}

public sealed record LuaParticipantProtocolSnapshot(
    ulong ContentId,
    LuaParticipantProtocolState State,
    float PositionError,
    float FacingErrorRadians,
    float Speed,
    DateTimeOffset? WithinToleranceSince,
    DateTimeOffset? LastObservedAt,
    DateTimeOffset? LastHeartbeatAt,
    string Error);

public sealed record LuaDistributedRunProtocolSnapshot(
    string RunId,
    LuaDistributedRunPhase Phase,
    DateTimeOffset? GoEpoch,
    IReadOnlyList<LuaParticipantProtocolSnapshot> Participants,
    string Detail,
    long Revision) {

    public int ReadyCount => Participants.Count(participant => participant.State is
        LuaParticipantProtocolState.Ready or LuaParticipantProtocolState.Running or LuaParticipantProtocolState.Completed);
}

/// <summary>
/// Pure authoritative PREPARE/STAGE/READY/GO state machine. Transport adapters
/// deliver idempotent observations; no Dalamud or wall-clock access occurs here.
/// </summary>
public sealed class LuaDistributedRunCoordinator {
    public static readonly TimeSpan MaximumLateStartRecovery = TimeSpan.FromSeconds(5);
    private readonly object _sync = new();
    private readonly LuaDistributedRunDescriptor _descriptor;
    private readonly Dictionary<ulong, ParticipantState> _participants;
    private readonly float _positionTolerance;
    private readonly float _facingTolerance;
    private readonly float _settledSpeed;
    private readonly TimeSpan _settleDuration;
    private LuaDistributedRunPhase _phase = LuaDistributedRunPhase.Prepared;
    private DateTimeOffset? _goEpoch;
    private LuaReadinessTimeoutPolicy _goTimeoutPolicy = LuaReadinessTimeoutPolicy.Abort;
    private TimeSpan _lateStartDelay;
    private string _detail = "prepared";
    private long _revision;

    public LuaDistributedRunCoordinator(
        LuaDistributedRunDescriptor descriptor,
        float positionTolerance = 0.12f,
        float facingToleranceRadians = 0.0872665f,
        float settledSpeed = 0.05f,
        TimeSpan? settleDuration = null) {
        _descriptor = descriptor.Validate();
        if (!float.IsFinite(positionTolerance) || positionTolerance <= 0f
            || !float.IsFinite(facingToleranceRadians) || facingToleranceRadians <= 0f
            || !float.IsFinite(settledSpeed) || settledSpeed < 0f)
            throw new ArgumentOutOfRangeException(nameof(positionTolerance));
        _positionTolerance = positionTolerance;
        _facingTolerance = facingToleranceRadians;
        _settledSpeed = settledSpeed;
        _settleDuration = settleDuration ?? TimeSpan.FromMilliseconds(500);
        if (_settleDuration <= TimeSpan.Zero || _settleDuration > TimeSpan.FromSeconds(10))
            throw new ArgumentOutOfRangeException(nameof(settleDuration));
        _participants = _descriptor.ParticipantCids.ToDictionary(cid => cid, cid => new ParticipantState(cid));
    }

    public LuaDistributedRunProtocolSnapshot Snapshot {
        get { lock (_sync) return SnapshotUnsafe(); }
    }

    public void BeginStaging(DateTimeOffset now) {
        lock (_sync) {
            RequirePhase(LuaDistributedRunPhase.Prepared);
            if (now < _descriptor.PreparedAt)
                throw new ArgumentOutOfRangeException(nameof(now), "Staging cannot begin before PREPARE time.");
            _phase = LuaDistributedRunPhase.Staging;
            _detail = "participants are moving to initial slots";
            _revision++;
        }
    }

    public bool ObserveStage(LuaStageObservation observation, out string reason) {
        lock (_sync) {
            if (_phase is not (LuaDistributedRunPhase.Staging or LuaDistributedRunPhase.Ready)) {
                reason = $"stage observation is invalid while run is {_phase}";
                return false;
            }
            if (!_participants.TryGetValue(observation.ContentId, out var participant)) {
                reason = $"content ID {observation.ContentId} is not in the authoritative roster";
                return false;
            }
            if (!observation.IsFinite || observation.PositionError < 0f || observation.Speed < 0f) {
                reason = "stage observation contains invalid measurements";
                return false;
            }
            if (participant.LastObservedAt is { } previous && observation.ObservedAt < previous) {
                reason = "out-of-order stage observation ignored";
                return false;
            }

            participant.PositionError = observation.PositionError;
            participant.FacingError = MathF.Abs(Wrap(observation.FacingErrorRadians));
            participant.Speed = observation.Speed;
            participant.LastObservedAt = observation.ObservedAt;
            participant.LastHeartbeatAt = observation.ObservedAt;
            var withinTolerance = participant.PositionError <= _positionTolerance
                && participant.FacingError <= _facingTolerance
                && participant.Speed <= _settledSpeed;
            if (!withinTolerance) {
                participant.WithinToleranceSince = null;
                participant.State = LuaParticipantProtocolState.Staging;
                if (_phase == LuaDistributedRunPhase.Ready)
                    _phase = LuaDistributedRunPhase.Staging;
            } else {
                participant.WithinToleranceSince ??= observation.ObservedAt;
                participant.State = observation.ObservedAt - participant.WithinToleranceSince >= _settleDuration
                    ? LuaParticipantProtocolState.Ready
                    : LuaParticipantProtocolState.Staging;
            }

            if (_participants.Values.All(value => value.State == LuaParticipantProtocolState.Ready)) {
                _phase = LuaDistributedRunPhase.Ready;
                _detail = "authoritative roster ready";
            } else {
                _detail = $"{_participants.Values.Count(value => value.State == LuaParticipantProtocolState.Ready)}/{_participants.Count} ready";
            }
            _revision++;
            reason = string.Empty;
            return true;
        }
    }

    public bool TryScheduleGo(
        DateTimeOffset now,
        TimeSpan leadTime,
        LuaReadinessTimeoutPolicy timeoutPolicy,
        out DateTimeOffset goEpoch,
        out string reason) {
        lock (_sync) {
            goEpoch = default;
            if (_phase == LuaDistributedRunPhase.GoScheduled && _goEpoch.HasValue) {
                goEpoch = _goEpoch.Value;
                reason = string.Empty;
                return true;
            }
            if (_phase is not (LuaDistributedRunPhase.Staging or LuaDistributedRunPhase.Ready)) {
                reason = $"GO cannot be scheduled while run is {_phase}";
                return false;
            }
            if (leadTime < TimeSpan.FromMilliseconds(500) || leadTime > TimeSpan.FromSeconds(30)) {
                reason = "GO lead time must be between 500 ms and 30 seconds";
                return false;
            }

            var allReady = _participants.Values.All(value => value.State == LuaParticipantProtocolState.Ready);
            var timedOut = now - _descriptor.PreparedAt >= _descriptor.ReadyTimeout;
            if (!allReady && !timedOut) {
                reason = $"waiting for readiness: {_participants.Values.Count(value => value.State == LuaParticipantProtocolState.Ready)}/{_participants.Count}";
                return false;
            }
            if (!allReady && timeoutPolicy == LuaReadinessTimeoutPolicy.Abort) {
                _phase = LuaDistributedRunPhase.Failed;
                _detail = "readiness timeout";
                _revision++;
                reason = "readiness timeout policy aborted the run";
                return false;
            }
            if (!allReady && timeoutPolicy == LuaReadinessTimeoutPolicy.ContinueWithReadyParticipants) {
                foreach (var participant in _participants.Values.Where(value => value.State != LuaParticipantProtocolState.Ready))
                    participant.State = LuaParticipantProtocolState.Missing;
            }

            _goEpoch = now + leadTime;
            _lateStartDelay = TimeSpan.Zero;
            _goTimeoutPolicy = timeoutPolicy;
            _phase = LuaDistributedRunPhase.GoScheduled;
            _detail = allReady ? "GO scheduled; roster ready" : $"GO scheduled after timeout using {timeoutPolicy}";
            _revision++;
            goEpoch = _goEpoch.Value;
            reason = string.Empty;
            return true;
        }
    }

    public void Tick(DateTimeOffset now) {
        lock (_sync) {
            if (_phase != LuaDistributedRunPhase.GoScheduled || !_goEpoch.HasValue || now < _goEpoch.Value)
                return;
            _phase = LuaDistributedRunPhase.Running;
            foreach (var participant in _participants.Values) {
                if (participant.State == LuaParticipantProtocolState.Ready
                    || (_goTimeoutPolicy == LuaReadinessTimeoutPolicy.ContinueAll
                        && participant.State is LuaParticipantProtocolState.Missing or LuaParticipantProtocolState.Staging))
                    participant.State = LuaParticipantProtocolState.Running;
            }
            _detail = _lateStartDelay > TimeSpan.Zero
                ? $"running after late-start recovery ({_lateStartDelay.TotalMilliseconds:0} ms)"
                : "running";
            _revision++;
        }
    }

    public bool AcceptScheduledGo(
        DateTimeOffset receivedAt,
        DateTimeOffset goEpoch,
        LuaReadinessTimeoutPolicy timeoutPolicy,
        out string reason) {
        lock (_sync) {
            if (_phase == LuaDistributedRunPhase.GoScheduled && _goEpoch == goEpoch) {
                reason = string.Empty;
                return true;
            }
            if (_phase is not (LuaDistributedRunPhase.Staging or LuaDistributedRunPhase.Ready)) {
                reason = $"GO cannot be accepted while run is {_phase}";
                return false;
            }
            var untilGo = goEpoch - receivedAt;
            if (untilGo < -MaximumLateStartRecovery || untilGo > TimeSpan.FromSeconds(30)) {
                reason = $"received GO epoch must be no more than {MaximumLateStartRecovery.TotalSeconds:0} seconds late or 30 seconds in the future";
                return false;
            }
            if (!Enum.IsDefined(timeoutPolicy)) {
                reason = "received GO timeout policy is invalid";
                return false;
            }
            var allReady = _participants.Values.All(value => value.State == LuaParticipantProtocolState.Ready);
            if (!allReady && timeoutPolicy == LuaReadinessTimeoutPolicy.Abort) {
                reason = "received GO requires the authoritative roster to be ready";
                return false;
            }
            if (!allReady && timeoutPolicy == LuaReadinessTimeoutPolicy.ContinueWithReadyParticipants) {
                foreach (var participant in _participants.Values.Where(value => value.State != LuaParticipantProtocolState.Ready))
                    participant.State = LuaParticipantProtocolState.Missing;
            }
            _goEpoch = goEpoch;
            _lateStartDelay = untilGo < TimeSpan.Zero ? -untilGo : TimeSpan.Zero;
            _goTimeoutPolicy = timeoutPolicy;
            _phase = LuaDistributedRunPhase.GoScheduled;
            var timing = untilGo < TimeSpan.Zero
                ? $"late-start recovery ({-untilGo.TotalMilliseconds:0} ms)"
                : "received GO";
            _detail = allReady ? $"{timing}; roster ready" : $"{timing} using {timeoutPolicy}";
            _revision++;
            reason = string.Empty;
            return true;
        }
    }

    public bool Heartbeat(ulong contentId, DateTimeOffset observedAt) {
        lock (_sync) {
            if (!_participants.TryGetValue(contentId, out var participant))
                return false;
            if (participant.LastHeartbeatAt is { } previous && observedAt < previous)
                return false;
            participant.LastHeartbeatAt = observedAt;
            _revision++;
            return true;
        }
    }

    public IReadOnlyList<ulong> MarkHeartbeatTimeouts(DateTimeOffset now, TimeSpan timeout) {
        if (timeout <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(timeout));
        lock (_sync) {
            var timedOut = _participants.Values
                .Where(participant => participant.State is LuaParticipantProtocolState.Ready or LuaParticipantProtocolState.Running)
                .Where(participant => participant.LastHeartbeatAt == null || now - participant.LastHeartbeatAt > timeout)
                .ToArray();
            foreach (var participant in timedOut)
                participant.State = LuaParticipantProtocolState.Missing;
            if (timedOut.Length > 0) {
                _detail = $"heartbeat timeout: {string.Join(",", timedOut.Select(value => value.ContentId))}";
                _revision++;
            }
            return timedOut.Select(value => value.ContentId).ToArray();
        }
    }

    public bool CompleteParticipant(ulong contentId, string? error = null) {
        lock (_sync) {
            if (!_participants.TryGetValue(contentId, out var participant))
                return false;
            participant.Error = error?.Trim() ?? string.Empty;
            participant.State = participant.Error.Length == 0
                ? LuaParticipantProtocolState.Completed
                : LuaParticipantProtocolState.Failed;
            if (_participants.Values.All(value => value.State == LuaParticipantProtocolState.Completed)) {
                _phase = LuaDistributedRunPhase.Completed;
                _detail = "all participants completed";
            } else if (participant.State == LuaParticipantProtocolState.Failed) {
                _detail = $"participant {contentId} failed: {participant.Error}";
            }
            _revision++;
            return true;
        }
    }

    public void Stop(string reason) {
        lock (_sync) {
            if (_phase is LuaDistributedRunPhase.Completed or LuaDistributedRunPhase.Stopped or LuaDistributedRunPhase.Failed)
                return;
            _phase = LuaDistributedRunPhase.Stopped;
            _detail = string.IsNullOrWhiteSpace(reason) ? "stopped" : reason.Trim();
            _revision++;
        }
    }

    private LuaDistributedRunProtocolSnapshot SnapshotUnsafe() => new(
        _descriptor.RunId,
        _phase,
        _goEpoch,
        _participants.Values.Select(value => new LuaParticipantProtocolSnapshot(
            value.ContentId,
            value.State,
            value.PositionError,
            value.FacingError,
            value.Speed,
            value.WithinToleranceSince,
            value.LastObservedAt,
            value.LastHeartbeatAt,
            value.Error)).ToArray(),
        _detail,
        _revision);

    private void RequirePhase(LuaDistributedRunPhase expected) {
        if (_phase != expected)
            throw new InvalidOperationException($"Expected distributed run phase {expected}, actual {_phase}.");
    }

    private static float Wrap(float value) => MathF.Atan2(MathF.Sin(value), MathF.Cos(value));

    private sealed class ParticipantState(ulong contentId) {
        public ulong ContentId { get; } = contentId;
        public LuaParticipantProtocolState State { get; set; } = LuaParticipantProtocolState.Missing;
        public float PositionError { get; set; } = float.PositiveInfinity;
        public float FacingError { get; set; } = float.PositiveInfinity;
        public float Speed { get; set; } = float.PositiveInfinity;
        public DateTimeOffset? WithinToleranceSince { get; set; }
        public DateTimeOffset? LastObservedAt { get; set; }
        public DateTimeOffset? LastHeartbeatAt { get; set; }
        public string Error { get; set; } = string.Empty;
    }
}
