using System;
using System.Threading;

namespace MasterOfPuppets.LuaScripting.Runs;

public sealed class LuaRunInstance : IDisposable {
    private readonly object _lock = new();
    private readonly TimeProvider _timeProvider;
    private readonly CancellationTokenSource _timeoutCts;
    private readonly CancellationTokenSource _stopCts = new();
    private readonly CancellationTokenSource _linkedCts;
    private LuaRunState _state = LuaRunState.Created;
    private LuaRunState _resumeState = LuaRunState.Running;
    private DateTimeOffset? _startedAt;
    private DateTimeOffset? _endedAt;
    private string _detail = string.Empty;
    private string _stopReason = string.Empty;
    private LuaRunError? _error;
    private LuaRunState? _requestedTerminalState;
    private long _revision;
    private bool _disposed;

    public LuaRunInstance(
        string runId,
        string scriptName,
        string scriptHash,
        int slot,
        int participantCount,
        int seed,
        TimeSpan maximumDuration,
        TimeProvider? timeProvider = null) {
        if (string.IsNullOrWhiteSpace(runId)) throw new ArgumentException("Run ID is required.", nameof(runId));
        if (string.IsNullOrWhiteSpace(scriptName)) throw new ArgumentException("Script name is required.", nameof(scriptName));
        if (slot < 0 || slot >= participantCount) throw new ArgumentOutOfRangeException(nameof(slot));
        if (maximumDuration <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(maximumDuration));

        RunId = runId.Trim();
        ScriptName = scriptName.Trim();
        ScriptHash = scriptHash?.Trim() ?? string.Empty;
        Slot = slot;
        ParticipantCount = participantCount;
        Seed = seed;
        _timeProvider = timeProvider ?? TimeProvider.System;
        CreatedAt = _timeProvider.GetUtcNow();
        _timeoutCts = new CancellationTokenSource(maximumDuration, _timeProvider);
        _timeoutCts.Token.Register(() => RequestCancellation(LuaRunState.TimedOut, "maximum run time exceeded", cancelStopSource: false));
        _linkedCts = CancellationTokenSource.CreateLinkedTokenSource(_timeoutCts.Token, _stopCts.Token);
    }

    public string RunId { get; }
    public string ScriptName { get; }
    public string ScriptHash { get; }
    public int Slot { get; }
    public int ParticipantCount { get; }
    public int Seed { get; }
    public DateTimeOffset CreatedAt { get; }
    public LuaExecutionControl Control { get; } = new();
    public CancellationToken CancellationToken => _linkedCts.Token;

    public LuaRunSnapshot Snapshot {
        get {
            lock (_lock)
                return SnapshotUnsafe();
        }
    }

    public void MarkWaiting(string detail = "") => Transition(LuaRunState.Waiting, detail);

    public void MarkRunning(string detail = "") {
        lock (_lock) {
            EnsureTransition(_state, LuaRunState.Running);
            _state = LuaRunState.Running;
            _startedAt ??= _timeProvider.GetUtcNow();
            _detail = detail?.Trim() ?? string.Empty;
            _revision++;
        }
    }

    public bool Pause(string detail = "paused by user") {
        lock (_lock) {
            if (_state is not (LuaRunState.Waiting or LuaRunState.Running))
                return false;
            if (!Control.Pause())
                return false;
            _resumeState = _state;
            _state = LuaRunState.Paused;
            _detail = detail?.Trim() ?? string.Empty;
            _revision++;
            return true;
        }
    }

    public bool Resume(string detail = "") {
        lock (_lock) {
            if (_state != LuaRunState.Paused)
                return false;
            Control.Resume();
            _state = _resumeState;
            _detail = detail?.Trim() ?? string.Empty;
            _revision++;
            return true;
        }
    }

    public void SetDetail(string detail) {
        lock (_lock) {
            if (_state.IsTerminal())
                return;
            _detail = detail?.Trim() ?? string.Empty;
            _revision++;
        }
    }

    public bool RequestStop(string reason = "stopped") =>
        RequestCancellation(LuaRunState.Stopped, reason, cancelStopSource: true);

    public bool RequestCancel(string reason = "cancelled") =>
        RequestCancellation(LuaRunState.Cancelled, reason, cancelStopSource: true);

    public void Complete() => TransitionTerminal(LuaRunState.Completed, string.Empty, null);

    public void CompleteCancellation() {
        LuaRunState state;
        string reason;
        lock (_lock) {
            state = _requestedTerminalState ?? (_timeoutCts.IsCancellationRequested ? LuaRunState.TimedOut : LuaRunState.Cancelled);
            reason = _stopReason;
        }
        TransitionTerminal(state, reason, null);
    }

    public void Fail(Exception exception) =>
        TransitionTerminal(LuaRunState.Failed, exception.Message, LuaRunError.FromException(exception));

    private bool RequestCancellation(LuaRunState terminalState, string reason, bool cancelStopSource) {
        lock (_lock) {
            if (_state.IsTerminal() || _requestedTerminalState.HasValue)
                return false;
            _requestedTerminalState = terminalState;
            _stopReason = reason?.Trim() ?? string.Empty;
            _revision++;
        }
        Control.Resume();
        if (cancelStopSource)
            _stopCts.Cancel();
        return true;
    }

    private void Transition(LuaRunState state, string detail) {
        lock (_lock) {
            EnsureTransition(_state, state);
            _state = state;
            _detail = detail?.Trim() ?? string.Empty;
            _revision++;
        }
    }

    private void TransitionTerminal(LuaRunState state, string reason, LuaRunError? error) {
        lock (_lock) {
            if (_state.IsTerminal())
                return;
            EnsureTransition(_state, state);
            _state = state;
            _endedAt = _timeProvider.GetUtcNow();
            _stopReason = reason?.Trim() ?? string.Empty;
            _error = error;
            _revision++;
        }
        Control.Resume();
    }

    private static void EnsureTransition(LuaRunState from, LuaRunState to) {
        var allowed = from switch {
            LuaRunState.Created => to is LuaRunState.Waiting or LuaRunState.Running or LuaRunState.Stopped or LuaRunState.Cancelled or LuaRunState.Failed,
            LuaRunState.Waiting => to is LuaRunState.Running or LuaRunState.Paused or LuaRunState.Stopped or LuaRunState.Cancelled or LuaRunState.TimedOut or LuaRunState.Failed,
            LuaRunState.Running => to is LuaRunState.Paused or LuaRunState.Completed or LuaRunState.Stopped or LuaRunState.Cancelled or LuaRunState.TimedOut or LuaRunState.Failed,
            LuaRunState.Paused => to is LuaRunState.Waiting or LuaRunState.Running or LuaRunState.Stopped or LuaRunState.Cancelled or LuaRunState.TimedOut or LuaRunState.Failed,
            _ => false,
        };
        if (!allowed)
            throw new InvalidOperationException($"Invalid Lua run transition: {from} -> {to}");
    }

    private LuaRunSnapshot SnapshotUnsafe() => new(
        RunId,
        ScriptName,
        ScriptHash,
        _state,
        Slot,
        ParticipantCount,
        Seed,
        CreatedAt,
        _startedAt,
        _endedAt,
        _detail,
        _stopReason,
        _error,
        _revision);

    public void Dispose() {
        lock (_lock) {
            if (_disposed)
                return;
            _disposed = true;
        }
        _timeoutCts.Dispose();
        _stopCts.Dispose();
        _linkedCts.Dispose();
    }
}
