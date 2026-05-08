using System;
using System.Numerics;
using System.Threading;

using Dalamud.Game.ClientState.Conditions;

using MasterOfPuppets.Extensions;
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
    private readonly ForwardPreciseMovementStrategy _forwardPrecise;
    private readonly ArrivePreciseMovementStrategy _arrivePrecise;
    private CancellationTokenSource? _cts;
    private ISimpleMovementStrategy? _activeStrategy;

    public SimpleInputMovement() {
        _continuousForward = new ContinuousForwardMovementStrategy(_forwardInput);
        _forwardPrecise = new ForwardPreciseMovementStrategy(_forwardInput);
        _arrivePrecise = new ArrivePreciseMovementStrategy(_forwardInput, _nativeStop);
    }

    public void Dispose() {
        var cts = _cts;
        _cts = null;
        cts?.Cancel();
        cts?.Dispose();
        StopStrategies();
        _forwardInput.Dispose();
    }

    public void StopMove() {
        if (DalamudApi.Framework.IsInFrameworkUpdateThread) {
            CancelActiveMove(callNativeStop: true);
            return;
        }

        _ = DalamudApi.Framework.RunOnFrameworkThread(() => CancelActiveMove(callNativeStop: true));
    }

    public CancellationTokenSource? MoveTo(
        Vector3 destination,
        float precision = 0.3f,
        float? faceDirection = null,
        SimpleMovementMode movementMode = SimpleMovementMode.Precise,
        bool stopOnStuck = false,
        float stuckTolerance = 0.05f,
        int stuckTimeoutMs = 500) {
        if (!DalamudApi.Framework.IsInFrameworkUpdateThread) {
            _ = DalamudApi.Framework.RunOnFrameworkThread(() => MoveTo(
                destination,
                precision,
                faceDirection,
                movementMode,
                stopOnStuck,
                stuckTolerance,
                stuckTimeoutMs));
            return null;
        }

        if (DalamudApi.Condition[ConditionFlag.Performing])
            return null;

        CancelActiveMove(callNativeStop: true);

        var context = new SimpleMovementContext(destination, precision, faceDirection);
        var strategy = SelectStrategy(movementMode);
        strategy.Start(context);
        _activeStrategy = strategy;

        var cts = new CancellationTokenSource();
        _cts = cts;
        var stuckTracker = stopOnStuck ? new SimpleMovementProgressTracker() : null;

        uint savedMoveMode = DalamudApi.GameConfig.UiControl.GetUInt("MoveMode");
        uint savedPadMode = DalamudApi.GameConfig.UiConfig.GetUInt("PadMode");
        var savedIsWalking = SimpleMovementWalkState.IsWalking;
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
                    DalamudApi.PluginLog.Warning(
                        $"[SimpleInputMovement] Stuck for {stuckTimeoutMs}ms near {player.Position}; destination={destination}; mode={movementMode}; stopping.");
                    CancelActiveMove(callNativeStop: true);
                    return;
                }

                movementComplete = strategy.Update(context, player.Position) == SimpleMovementUpdateResult.Complete;
            },
            callback: () => {
                var newerMoveStarted = _cts != null && !ReferenceEquals(_cts, cts);
                if (!newerMoveStarted) {
                    DalamudApi.GameConfig.UiControl.Set("MoveMode", savedMoveMode);
                    DalamudApi.GameConfig.UiConfig.Set("PadMode", savedPadMode);

                    StopStrategies();
                    if (strategy.UsesNativeStopOnCompletion)
                        _nativeStop.Stop();

                    SimpleMovementWalkState.IsWalking = savedIsWalking;

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

        return false;
    }

    private ISimpleMovementStrategy SelectStrategy(SimpleMovementMode mode) =>
        mode switch {
            SimpleMovementMode.Continuous => _continuousForward,
            SimpleMovementMode.Forward => _forwardPrecise,
            _ => _arrivePrecise,
        };

    private void CancelActiveMove(bool callNativeStop) {
        var cts = _cts;
        if (cts != null) {
            cts.Cancel();
            if (ReferenceEquals(_cts, cts))
                _cts = null;
        }

        StopStrategies();
        SimpleMovementWalkState.IsWalking = false;

        if (!callNativeStop)
            return;

        if (DalamudApi.ObjectTable.LocalPlayer == null || !DalamudApi.ClientState.IsLoggedIn)
            return;

        _nativeStop.Stop();
    }

    private void StopStrategies() {
        _activeStrategy?.Stop();
        _activeStrategy = null;
        _continuousForward.Stop();
        _forwardPrecise.Stop();
        _arrivePrecise.Stop();
    }
}

public enum SimpleMovementMode {
    Continuous,
    Precise,
    Forward,
}

public enum ArrivalMovementState {
    Run,
    Walk,
    Stop,
}

public readonly record struct ContinuousMovementProgress(bool Complete, bool HasApproached);

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
