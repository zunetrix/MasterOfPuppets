using System;
using System.Numerics;
using System.Threading;

using Dalamud.Game.ClientState.Conditions;

using MasterOfPuppets.Util;

namespace MasterOfPuppets.Movement;

public sealed class SimpleInputMovement : IDisposable {
    public const float ArrivalWalkBuffer = 0.5f;
    public const float SettleHysteresis = 0.05f;
    public const int SettleFrameCount = 3;
    public const float ContinuousPassEpsilon = 0.01f;

    private readonly NativeStop _nativeStop = new();
    private readonly ForwardInputMovementController _forwardInput = new();
    private readonly ContinuousForwardMovementStrategy _continuousForward;
    private readonly FormationNaturalMovementStrategy _formationNatural;
    private readonly ForwardPreciseMovementStrategy _forwardPrecise;
    private readonly ArrivePreciseMovementStrategy _arrivePrecise;
    private CancellationTokenSource? _cts;
    private ISimpleMovementStrategy? _activeStrategy;
    private SimpleMovementMode? _activeMovementMode;
    private string? _activeTrackingKey;
    private MovementControlState? _controlBaseline;
    private bool? _walkBaseline;

    public SimpleInputMovement() {
        _continuousForward = new ContinuousForwardMovementStrategy(_forwardInput);
        _formationNatural = new FormationNaturalMovementStrategy(_forwardInput);
        _forwardPrecise = new ForwardPreciseMovementStrategy(_forwardInput);
        _arrivePrecise = new ArrivePreciseMovementStrategy(_forwardInput, _nativeStop);
    }

    public void Dispose() {
        var cts = _cts;
        _cts = null;
        cts?.Cancel();
        cts?.Dispose();
        StopStrategies();
        RestoreControlBaseline();
        RestoreWalkBaseline();
        _forwardInput.Dispose();
    }

    public void StopMove() {
        if (DalamudApi.Framework.IsInFrameworkUpdateThread) {
            CancelActiveMove(callNativeStop: true);
            return;
        }

        _ = DalamudApi.Framework.RunOnFrameworkThread(() => CancelActiveMove(callNativeStop: true));
    }

    public bool IsMoving => _activeStrategy != null || _cts != null;

    public bool IsActiveLiveFormationMove(string trackingKey) =>
        _activeStrategy == _formationNatural
        && _activeMovementMode == SimpleMovementMode.Natural
        && _cts is { IsCancellationRequested: false }
        && string.Equals(_activeTrackingKey, trackingKey, StringComparison.Ordinal);

    public CancellationTokenSource? MoveTo(
        Vector3 destination,
        float precision = 0.3f,
        float? faceDirection = null,
        SimpleMovementMode movementMode = SimpleMovementMode.Precise,
        bool stopOnStuck = false,
        float stuckTolerance = 0.05f,
        int stuckTimeoutMs = 500,
        string? trackingKey = null,
        bool useFormationRelativeMovement = false) {
        if (!DalamudApi.Framework.IsInFrameworkUpdateThread) {
            _ = DalamudApi.Framework.RunOnFrameworkThread(() => MoveTo(
                destination,
                precision,
                faceDirection,
                movementMode,
                stopOnStuck,
                stuckTolerance,
                stuckTimeoutMs,
                trackingKey,
                useFormationRelativeMovement));
            return null;
        }

        if (DalamudApi.Condition[ConditionFlag.Performing])
            return null;

        if (movementMode == SimpleMovementMode.Natural
            && _activeStrategy == _formationNatural
            && _activeMovementMode == SimpleMovementMode.Natural
            && _cts is { IsCancellationRequested: false } activeCts
            && string.Equals(_activeTrackingKey, trackingKey, StringComparison.Ordinal)) {
            _formationNatural.UpdateTarget(destination, faceDirection, useFormationRelativeMovement);
            return activeCts;
        }

        // MoveMode/PadMode use one baseline for the whole replacement chain. Walk/run state is
        // different: non-preserving modes keep their own baseline, while Natural deliberately
        // releases it so manual walk/run changes remain live.
        CaptureControlBaselineIfNeeded();
        var preserveWalkState = PreservesWalkState(movementMode);
        if (!preserveWalkState) {
            _walkBaseline = CaptureWalkBaseline(
                _walkBaseline,
                SimpleMovementWalkState.IsWalking);
        }

        CancelActiveMove(
            callNativeStop: true,
            restoreControlBaseline: false,
            restoreWalkBaseline: false);

        if (preserveWalkState)
            RestoreWalkBaseline();

        var context = new SimpleMovementContext(
            destination,
            precision,
            faceDirection,
            useFormationRelativeMovement);
        var strategy = SelectStrategy(movementMode);
        strategy.Start(context);
        _activeStrategy = strategy;
        _activeMovementMode = movementMode;
        _activeTrackingKey = trackingKey;

        var cts = new CancellationTokenSource();
        _cts = cts;
        // A persistent formation slot is expected to be stationary whenever its
        // anchor is stationary, so ordinary stuck detection must not end it.
        var stuckTracker = stopOnStuck && !UsesLiveFormationTracking(movementMode)
            ? new SimpleMovementProgressTracker()
            : null;

        var movementComplete = false;

        DalamudApi.GameConfig.UiControl.Set("MoveMode", 0u);
        DalamudApi.GameConfig.UiConfig.Set("PadMode", 0u);

        Coroutine.StartRunOnFramework(
            runFunction: () => {
                var player = DalamudApi.ObjectTable.LocalPlayer;
                if (player == null) return;

                if (DalamudApi.Condition[ConditionFlag.Performing]) {
                    StopMove();
                    return;
                }

                if (stuckTracker != null && stuckTracker.Update(player.Position, Environment.TickCount64, stuckTolerance, stuckTimeoutMs)) {
                    DalamudApi.PluginLog.Warning($"[SimpleInputMovement] Stuck for {stuckTimeoutMs}ms near {player.Position}; destination={destination}; mode={movementMode}; stopping.");
                    CancelActiveMove(callNativeStop: true);
                    return;
                }

                movementComplete = strategy.Update(context, player.Position) == SimpleMovementUpdateResult.Complete;
            },
            callback: () => {
                var newerMoveStarted = _cts != null && !ReferenceEquals(_cts, cts);
                if (!newerMoveStarted) {
                    StopStrategies();
                    if (strategy.UsesNativeStopOnCompletion)
                        _nativeStop.Stop();

                    RestoreControlBaseline();
                    RestoreWalkBaseline();

                    if (faceDirection is float rot
                        && !cts.IsCancellationRequested
                        && !DalamudApi.Condition[ConditionFlag.Performing]) {
                        GameFunctions.FaceDirectionDeferred(rot.Radians());
                    }
                }

                if (ReferenceEquals(_cts, cts))
                    _cts = null;
                cts.Dispose();
            },
            stopWhen: () => {
                var player = DalamudApi.ObjectTable.LocalPlayer;
                return movementComplete
                    || player == null
                    || !DalamudApi.ClientState.IsLoggedIn
                    || cts.IsCancellationRequested
                    || DalamudApi.Condition[ConditionFlag.Performing];
            },
            cancellationToken: cts.Token);

        return cts;
    }

    public static ArrivalMovementState GetArrivalState(
        float distance,
        float precision,
        SimpleMovementMode movementMode = SimpleMovementMode.Precise) {
        if (distance <= precision)
            return ArrivalMovementState.Stop;

        return movementMode switch {
            SimpleMovementMode.Continuous => ArrivalMovementState.Run,
            SimpleMovementMode.Forward => distance <= precision + ArrivalWalkBuffer ? ArrivalMovementState.Walk : ArrivalMovementState.Run,
            SimpleMovementMode.Precise => distance <= precision + ArrivalWalkBuffer ? ArrivalMovementState.Walk : ArrivalMovementState.Run,
            _ => ArrivalMovementState.Run,
        };
    }

    public static ContinuousMovementProgress UpdateContinuousProgress(
        float distance,
        float precision,
        float? previousDistance,
        bool hasApproached) {
        if (distance <= precision)
            return new ContinuousMovementProgress(true, hasApproached);

        if (previousDistance is not { } previous)
            return new ContinuousMovementProgress(false, hasApproached);

        var approached = hasApproached || distance < previous - ContinuousPassEpsilon;
        var passedClosestApproach = approached && distance > previous + ContinuousPassEpsilon;
        return new ContinuousMovementProgress(passedClosestApproach, approached);
    }

    public static string FormatMode(SimpleMovementMode mode) =>
        mode.ToString().ToLowerInvariant();

    public static SimpleMovementMode ParseModeOrDefault(string value, SimpleMovementMode fallback = SimpleMovementMode.Continuous) {
        return TryParseMode(value, out var mode) ? mode : fallback;
    }

    public static bool TryParseMode(string value, out SimpleMovementMode mode) {
        mode = SimpleMovementMode.Continuous;
        if (value.Equals("continuous", StringComparison.OrdinalIgnoreCase)) {
            mode = SimpleMovementMode.Continuous;
            return true;
        }

        if (value.Equals("precise", StringComparison.OrdinalIgnoreCase)) {
            mode = SimpleMovementMode.Precise;
            return true;
        }

        if (value.Equals("forward", StringComparison.OrdinalIgnoreCase)) {
            mode = SimpleMovementMode.Forward;
            return true;
        }

        if (value.Equals("natural", StringComparison.OrdinalIgnoreCase)) {
            mode = SimpleMovementMode.Natural;
            return true;
        }

        return false;
    }

    public static bool PreservesWalkState(SimpleMovementMode mode) =>
        mode == SimpleMovementMode.Natural;

    public static bool UsesLiveFormationTracking(SimpleMovementMode mode) =>
        mode == SimpleMovementMode.Natural;

    public static MovementControlState CaptureControlBaseline(
        MovementControlState? existingBaseline,
        MovementControlState currentState) =>
        existingBaseline ?? currentState;

    public static bool CaptureWalkBaseline(bool? existingBaseline, bool currentState) =>
        existingBaseline ?? currentState;

    private ISimpleMovementStrategy SelectStrategy(SimpleMovementMode mode) =>
        mode switch {
            SimpleMovementMode.Continuous => _continuousForward,
            SimpleMovementMode.Natural => _formationNatural,
            SimpleMovementMode.Forward => _forwardPrecise,
            _ => _arrivePrecise,
        };

    private void CancelActiveMove(
        bool callNativeStop,
        bool restoreControlBaseline = true,
        bool restoreWalkBaseline = true) {
        var wasMovingWithNonPreservedWalk = _activeMovementMode is { } activeMode
            && !PreservesWalkState(activeMode);
        var cts = _cts;
        if (cts != null) {
            cts.Cancel();
            if (ReferenceEquals(_cts, cts))
                _cts = null;
        }

        StopStrategies();
        if (wasMovingWithNonPreservedWalk)
            SimpleMovementWalkState.IsWalking = false;

        if (callNativeStop
            && DalamudApi.ObjectTable.LocalPlayer != null
            && DalamudApi.ClientState.IsLoggedIn) {
            _nativeStop.Stop();
        }

        if (restoreControlBaseline)
            RestoreControlBaseline();
        if (restoreWalkBaseline)
            RestoreWalkBaseline();
    }

    private void CaptureControlBaselineIfNeeded() {
        var currentState = new MovementControlState(
            DalamudApi.GameConfig.UiControl.GetUInt("MoveMode"),
            DalamudApi.GameConfig.UiConfig.GetUInt("PadMode"));
        _controlBaseline = CaptureControlBaseline(_controlBaseline, currentState);
    }

    private void RestoreControlBaseline() {
        if (_controlBaseline is not { } baseline)
            return;

        DalamudApi.GameConfig.UiControl.Set("MoveMode", baseline.MoveMode);
        DalamudApi.GameConfig.UiConfig.Set("PadMode", baseline.PadMode);
        _controlBaseline = null;
    }

    private void RestoreWalkBaseline() {
        if (_walkBaseline is not { } baseline)
            return;

        SimpleMovementWalkState.IsWalking = baseline;
        _walkBaseline = null;
    }

    private void StopStrategies() {
        _activeStrategy?.Stop();
        _activeStrategy = null;
        _activeMovementMode = null;
        _activeTrackingKey = null;
        _continuousForward.Stop();
        _formationNatural.Stop();
        _forwardPrecise.Stop();
        _arrivePrecise.Stop();
    }
}

public enum SimpleMovementMode {
    Continuous,
    Precise,
    Forward,
    Natural,
}

public enum ArrivalMovementState {
    Run,
    Walk,
    Stop,
}

public readonly record struct ContinuousMovementProgress(bool Complete, bool HasApproached);

public readonly record struct MovementControlState(uint MoveMode, uint PadMode);

public sealed class SimpleMovementProgressTracker {
    private Vector3 _lastSignificantPosition;
    private long _lastProgressMs;
    private bool _hasPosition;

    public void Reset() {
        _lastSignificantPosition = default;
        _lastProgressMs = 0;
        _hasPosition = false;
    }

    public bool Update(Vector3 position, long nowMs, float movementTolerance, int timeoutMs) {
        if (!_hasPosition) {
            _lastSignificantPosition = position;
            _lastProgressMs = nowMs;
            _hasPosition = true;
            return false;
        }

        if (Vector3.Distance(position, _lastSignificantPosition) > Math.Max(0, movementTolerance)) {
            _lastSignificantPosition = position;
            _lastProgressMs = nowMs;
            return false;
        }

        return nowMs - _lastProgressMs >= Math.Max(1, timeoutMs);
    }
}

public enum MovementDirection : int {
    None = 0,
    Forward = 0x141,
    Backward = 0x142,
    RotateLeft = 0x143,
    RotateRight = 0x144,
    StrafeLeft = 0x145,
    StrafeRight = 0x146,
}
