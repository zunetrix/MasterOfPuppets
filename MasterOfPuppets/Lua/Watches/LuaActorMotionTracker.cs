using System;

namespace MasterOfPuppets.LuaScripting.Watches;

/// <summary>
/// Derives a stable horizontal-movement edge from the cached actor sample.
/// A short hold bridges network interpolation gaps while still cancelling a
/// local persistent emote within a few frames of movement beginning.
/// </summary>
internal sealed class LuaActorMotionTracker {
    private const float MinimumDistance = 0.008f;
    private const float MinimumSampleDisplacement = 0.0005f;
    private const float MinimumSpeed = 0.08f;
    private const long MovingHoldMs = 160;

    private bool _initialized;
    private float _previousX;
    private float _previousZ;
    private float _stationaryX;
    private float _stationaryZ;
    private long _previousAtMs;
    private long _movingUntilMs;
    private bool _lastWalking;

    public LuaActorWatchState Observe(in LuaActorWatchState raw, long nowMs) {
        if (!_initialized) {
            _initialized = true;
            _previousX = raw.PositionX;
            _previousZ = raw.PositionZ;
            _stationaryX = raw.PositionX;
            _stationaryZ = raw.PositionZ;
            _previousAtMs = nowMs;
            _lastWalking = raw.IsWalking;
            return raw with { IsMoving = false, IsWalking = raw.IsWalking };
        }

        var elapsedMs = Math.Max(1, nowMs - _previousAtMs);
        var dx = raw.PositionX - _previousX;
        var dz = raw.PositionZ - _previousZ;
        var distance = MathF.Sqrt((dx * dx) + (dz * dz));
        var baselineDx = raw.PositionX - _stationaryX;
        var baselineDz = raw.PositionZ - _stationaryZ;
        var displacement = MathF.Sqrt((baselineDx * baselineDx) + (baselineDz * baselineDz));
        var speed = distance / (elapsedMs / 1000f);
        if ((distance >= MinimumDistance && speed >= MinimumSpeed)
            || (distance >= MinimumSampleDisplacement && displacement >= MinimumDistance)) {
            _movingUntilMs = nowMs + MovingHoldMs;
            if (speed >= MinimumSpeed)
                _lastWalking = speed <= 3.2f;
        }

        _previousX = raw.PositionX;
        _previousZ = raw.PositionZ;
        _previousAtMs = nowMs;
        var isMoving = nowMs < _movingUntilMs;
        if (!isMoving && distance < MinimumSampleDisplacement) {
            _stationaryX = raw.PositionX;
            _stationaryZ = raw.PositionZ;
        }
        var isWalking = raw.IsWalking || (isMoving ? _lastWalking : _lastWalking);
        return raw with { IsMoving = isMoving, IsWalking = isWalking };
    }

    public void Reset() {
        _initialized = false;
        _previousX = 0;
        _previousZ = 0;
        _stationaryX = 0;
        _stationaryZ = 0;
        _previousAtMs = 0;
        _movingUntilMs = 0;
        _lastWalking = false;
    }
}
