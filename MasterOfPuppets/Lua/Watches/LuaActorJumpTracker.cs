using System;

namespace MasterOfPuppets.LuaScripting.Watches;

/// <summary>
/// Adds reliable jump edges to target-scoped samples. The native flag is used
/// whenever available. Remote actors whose native flag remains false fall back
/// to a short-window vertical-rise detector without publishing ordinary
/// position changes.
/// </summary>
internal sealed class LuaActorJumpTracker {
    private const float MinimumRise = 0.12f;
    private const float MinimumRiseSpeed = 1.00f;
    private const long MaximumRiseWindowMs = 450;
    private const long InferredAirborneMs = 700;
    private const long MinimumJumpSeparationMs = 250;

    private bool _initialized;
    private bool _previousNativeJump;
    private bool _effectiveJump;
    private float _riseBaselineY;
    private long _riseBaselineAtMs;
    private long _inferredUntilMs;
    private long _lastJumpAtMs = long.MinValue / 2;
    private long _jumpSequence;

    public LuaActorWatchState Observe(in LuaActorWatchState raw, long nowMs) {
        if (!_initialized) {
            _initialized = true;
            _previousNativeJump = raw.IsJumping;
            _effectiveJump = raw.IsJumping;
            _riseBaselineY = raw.PositionY;
            _riseBaselineAtMs = nowMs;
            if (raw.IsJumping) {
                _lastJumpAtMs = nowMs;
                _jumpSequence = 1;
            }
            return raw with { IsJumping = _effectiveJump, JumpSequence = _jumpSequence };
        }

        var nativeStarted = raw.IsJumping && !_previousNativeJump;
        var elapsed = Math.Max(1, nowMs - _riseBaselineAtMs);
        var rise = raw.PositionY - _riseBaselineY;
        var inferredStarted = !raw.IsJumping
            && !_effectiveJump
            && nowMs - _lastJumpAtMs >= MinimumJumpSeparationMs
            && elapsed <= MaximumRiseWindowMs
            && rise >= MinimumRise
            && rise / (elapsed / 1000f) >= MinimumRiseSpeed;

        if (nativeStarted || inferredStarted) {
            _jumpSequence++;
            _lastJumpAtMs = nowMs;
            if (inferredStarted)
                _inferredUntilMs = nowMs + InferredAirborneMs;
        }

        _effectiveJump = raw.IsJumping || nowMs < _inferredUntilMs;
        _previousNativeJump = raw.IsJumping;

        if (!_effectiveJump) {
            if (raw.PositionY <= _riseBaselineY + 0.02f || elapsed > MaximumRiseWindowMs) {
                _riseBaselineY = raw.PositionY;
                _riseBaselineAtMs = nowMs;
            }
        } else if (raw.PositionY < _riseBaselineY) {
            _riseBaselineY = raw.PositionY;
            _riseBaselineAtMs = nowMs;
        }

        return raw with { IsJumping = _effectiveJump, JumpSequence = _jumpSequence };
    }

    public void Reset() {
        _initialized = false;
        _previousNativeJump = false;
        _effectiveJump = false;
        _riseBaselineY = 0;
        _riseBaselineAtMs = 0;
        _inferredUntilMs = 0;
        _lastJumpAtMs = long.MinValue / 2;
        _jumpSequence = 0;
    }
}
