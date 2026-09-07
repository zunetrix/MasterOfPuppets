using System;
using System.Numerics;

namespace MasterOfPuppets.LuaScripting;

/// <summary>
/// Maintains a shared formation frame behind a freely controlled leader. The
/// frame center remains anchored to the observed leader so clients that attach
/// at different times converge immediately; only rotation is rate-limited so
/// wide outside slots are not asked to pivot at an impossible angular speed.
/// </summary>
internal sealed class RigidFormationPoseTracker {
    public const float MaximumAngularRateRadiansPerSecond = 2.5f;
    public const float TurningTranslationFraction = 0.78f;
    public const float AboutFaceThresholdRadians = MathF.PI * 5f / 6f;
    public const float RotationErrorThresholdRadians = 0.01f;
    public const float StationaryLeaderDistanceThreshold = 0.15f;
    public const float StationaryRotationNoiseThresholdRadians = 0.25f;
    public const float LeaderTurnThresholdRadiansPerSecond = 0.08f;
    public const float TeleportDistance = 25f;

    private bool _initialized;
    private Vector3 _position;
    private float _rotation;
    private Vector3 _lastLeaderPosition;
    private float _lastLeaderRotation;
    private long _lastUpdateMs;
    private float _smoothedLeaderSpeed;

    public void Reset() {
        _initialized = false;
        _position = default;
        _rotation = 0f;
        _lastLeaderPosition = default;
        _lastLeaderRotation = 0f;
        _lastUpdateMs = 0;
        _smoothedLeaderSpeed = 0f;
    }

    public RigidFormationPose Step(
        Vector3 leaderPosition,
        float leaderRotation,
        float formationRadius,
        float maximumTravelSpeed,
        long nowMs) {
        if (!_initialized) {
            _initialized = true;
            _position = leaderPosition;
            _rotation = NormalizeAngle(leaderRotation);
            _lastLeaderPosition = leaderPosition;
            _lastLeaderRotation = _rotation;
            _lastUpdateMs = nowMs;
            return new RigidFormationPose(_position, _rotation, 0f, 0f, false);
        }

        var elapsed = Math.Clamp((nowMs - _lastUpdateMs) / 1000f, 0.001f, 0.1f);
        _lastUpdateMs = nowMs;

        var leaderDelta = leaderPosition - _lastLeaderPosition;
        leaderDelta.Y = 0f;
        var leaderDistance = leaderDelta.Length();
        var rawLeaderSpeed = leaderDistance >= TeleportDistance ? 0f : leaderDistance / elapsed;
        _smoothedLeaderSpeed = _smoothedLeaderSpeed <= 0f
            ? rawLeaderSpeed
            : _smoothedLeaderSpeed * 0.75f + rawLeaderSpeed * 0.25f;

        var leaderRotationDelta = ShortestAngle(_lastLeaderRotation, leaderRotation);
        var leaderAngularSpeed = leaderRotationDelta / elapsed;
        var isAboutFace = MathF.Abs(leaderRotationDelta) >= AboutFaceThresholdRadians;
        var rotationError = ShortestAngle(_rotation, leaderRotation);
        // A stationary actor's rotation samples can jitter by a few degrees.
        // At the outside of a formation that noise becomes a large slot
        // displacement, so do not turn the shared frame until it accumulates
        // beyond a small stationary deadband.
        var leaderIsStationary = leaderDistance < StationaryLeaderDistanceThreshold;
        var isTurning = MathF.Abs(rotationError) >= (leaderIsStationary
                ? StationaryRotationNoiseThresholdRadians
                : RotationErrorThresholdRadians)
            || (!leaderIsStationary
                && MathF.Abs(leaderAngularSpeed) >= LeaderTurnThresholdRadiansPerSecond);

        _lastLeaderPosition = leaderPosition;
        _lastLeaderRotation = NormalizeAngle(leaderRotation);

        if (leaderDistance >= TeleportDistance) {
            _position = leaderPosition;
            _rotation = NormalizeAngle(leaderRotation);
            return new RigidFormationPose(_position, _rotation, 0f, 0f, false);
        }

        // A near-instant reversal is an about-face, not a corner. Following a
        // 180-degree angular path makes every occupied slot perform a needless
        // semicircle. Flip the shared frame immediately instead; each follower
        // then takes the shorter chord through the leader to its reversed slot.
        if (isAboutFace) {
            _position = leaderPosition;
            _rotation = NormalizeAngle(leaderRotation);
            return new RigidFormationPose(
                _position,
                _rotation,
                _smoothedLeaderSpeed,
                leaderAngularSpeed,
                true);
        }

        maximumTravelSpeed = Math.Clamp(maximumTravelSpeed, 0.1f, 20f);
        formationRadius = Math.Clamp(formationRadius, 0f, 100f);
        // Never integrate the frame center independently on each client. When
        // its maximum speed matched the leader's, launch-time and observation
        // differences became permanent longitudinal gaps during straight-line
        // travel. All clients instead derive the center directly from the same
        // observed leader pose. Rotation remains rate-limited below.
        _position = leaderPosition;

        var translationSpeedForBudget = MathF.Min(
            _smoothedLeaderSpeed,
            maximumTravelSpeed * (isTurning ? TurningTranslationFraction : 1f));
        var tangentialBudgetSquared = maximumTravelSpeed * maximumTravelSpeed
            - translationSpeedForBudget * translationSpeedForBudget;
        var tangentialBudget = MathF.Sqrt(MathF.Max(0f, tangentialBudgetSquared));
        var angularLimit = formationRadius <= 0.05f
            ? MaximumAngularRateRadiansPerSecond
            : MathF.Min(MaximumAngularRateRadiansPerSecond, tangentialBudget / formationRadius);
        if (!isTurning)
            angularLimit = MaximumAngularRateRadiansPerSecond;
        if (isTurning)
            _rotation = StepAngle(_rotation, leaderRotation, angularLimit * elapsed);

        return new RigidFormationPose(
            _position,
            _rotation,
            _smoothedLeaderSpeed,
            leaderAngularSpeed,
            isTurning);
    }

    internal static float StepAngle(float current, float target, float maximumStep) {
        var delta = ShortestAngle(current, target);
        if (MathF.Abs(delta) <= maximumStep)
            return NormalizeAngle(target);
        return NormalizeAngle(current + MathF.CopySign(maximumStep, delta));
    }

    internal static float ShortestAngle(float current, float target) =>
        MathF.Atan2(MathF.Sin(target - current), MathF.Cos(target - current));

    internal static float NormalizeAngle(float value) =>
        MathF.Atan2(MathF.Sin(value), MathF.Cos(value));

}

internal readonly record struct RigidFormationPose(
    Vector3 Position,
    float Rotation,
    float LeaderSpeed,
    float LeaderAngularSpeed,
    bool IsTurning);
