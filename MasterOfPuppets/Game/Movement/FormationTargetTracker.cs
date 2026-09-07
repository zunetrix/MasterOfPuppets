using System;
using System.Numerics;

using MasterOfPuppets.Extensions;

namespace MasterOfPuppets.Movement;

/// <summary>
/// Tracks successive world-space formation-slot samples so Natural can
/// distinguish a moving slot from a stationary one without predicting ahead.
/// </summary>
public sealed class FormationTargetTracker {
    public const float MaxSampleIntervalSeconds = 1f;
    public const float TeleportResetDistance = 8f;
    public const float VelocityBlend = 0.45f;
    public const float MaxTrackedSpeed = 15f;
    public const float SampleNoiseDistance = 0.015f;
    public const float MovingSlotSpeed = 0.2f;
    public const float HoldResumeBuffer = 0.08f;
    public const float FacingUpdateThresholdRadians = 0.0175f;
    public const float RelativeDirectionSwitchRatio = 1.25f;
    public const float PursuitPredictionSeconds = 0.35f;
    public const float MaximumPursuitLeadDistance = 1.5f;
    public const long MotionHoldMs = 250;

    private bool _hasTarget;
    private long _lastUpdateMs;
    private long _lastMovingMs;

    public Vector3 Target { get; private set; }
    public Vector3 Velocity { get; private set; }

    public void Reset(Vector3 target, long nowMs) {
        _hasTarget = true;
        _lastUpdateMs = nowMs;
        _lastMovingMs = 0;
        Target = target;
        Velocity = Vector3.Zero;
    }

    public void UpdateTarget(Vector3 target, long nowMs) {
        if (!_hasTarget) {
            Reset(target, nowMs);
            return;
        }

        var elapsedSeconds = (nowMs - _lastUpdateMs) / 1000f;
        var sampleDistance = Target.Distance2D(target);
        if (elapsedSeconds <= 0f
            || elapsedSeconds > MaxSampleIntervalSeconds
            || sampleDistance >= TeleportResetDistance) {
            Velocity = Vector3.Zero;
        } else if (sampleDistance < SampleNoiseDistance) {
            // Network transform noise at a stationary anchor must not classify
            // the exact Natural target as moving. Decay remaining motion promptly.
            Velocity *= 1f - VelocityBlend;
        } else {
            var sampledVelocity = (target - Target) / elapsedSeconds;
            sampledVelocity.Y = 0f;
            sampledVelocity = ClampLength2D(sampledVelocity, MaxTrackedSpeed);
            Velocity = Vector3.Lerp(Velocity, sampledVelocity, VelocityBlend);
        }

        if (Length2D(Velocity) >= MovingSlotSpeed) {
            _lastMovingMs = nowMs;
        }

        Target = target;
        _lastUpdateMs = nowMs;
    }

    public bool IsSlotMoving => Length2D(Velocity) >= MovingSlotSpeed || (Environment.TickCount64 - _lastMovingMs <= MotionHoldMs);

    public static bool ShouldHold(float distance, float precision, bool wasHolding, bool slotMoving = false) {
        // A live trajectory point is not an arrival destination. Releasing input
        // whenever the actor catches it produces a visible stop/start cadence.
        if (slotMoving)
            return false;

        var holdRadius = Math.Max(0f, precision) + (wasHolding ? HoldResumeBuffer : 0f);
        return distance <= holdRadius;
    }

    public Vector3 GetPursuitTarget() {
        var lead = Velocity * PursuitPredictionSeconds;
        lead.Y = 0f;
        lead = ClampLength2D(lead, MaximumPursuitLeadDistance);
        return Target + lead;
    }

    public static bool ShouldUpdateFacing(float? previousRotation, float nextRotation) {
        if (!previousRotation.HasValue)
            return true;

        var delta = MathF.Atan2(
            MathF.Sin(nextRotation - previousRotation.Value),
            MathF.Cos(nextRotation - previousRotation.Value));
        return MathF.Abs(delta) >= FacingUpdateThresholdRadians;
    }

    public static float StepRotationToward(float currentRotation, float desiredRotation, float maximumStep) {
        var delta = MathF.Atan2(
            MathF.Sin(desiredRotation - currentRotation),
            MathF.Cos(desiredRotation - currentRotation));
        return currentRotation + Math.Clamp(delta, -MathF.Abs(maximumStep), MathF.Abs(maximumStep));
    }

    /// <summary>
    /// Chooses a movement input in the character's desired-facing coordinate frame.
    /// This lets formation followers backpedal or strafe toward their slot without
    /// turning away from the formation's facing.
    /// </summary>
    public static MovementDirection SelectRelativeMovementDirection(
        Vector3 playerPosition,
        Vector3 targetPosition,
        float facingRadians,
        MovementDirection previousDirection = MovementDirection.None) {
        var error = targetPosition - playerPosition;
        error.Y = 0f;
        if (error.LengthSquared() <= float.Epsilon)
            return MovementDirection.None;

        var forward = facingRadians.Radians().ToDirectionXZ();
        // With the game's rotation convention, this is the character's local right.
        var right = new Vector3(-forward.Z, 0f, forward.X);
        var forwardAmount = Vector3.Dot(error, forward);
        var rightAmount = Vector3.Dot(error, right);

        var candidate = MathF.Abs(forwardAmount) >= MathF.Abs(rightAmount)
            ? forwardAmount >= 0f
                ? MovementDirection.Forward
                : MovementDirection.Backward
            : rightAmount >= 0f
                ? MovementDirection.StrafeRight
                : MovementDirection.StrafeLeft;

        if (previousDirection == MovementDirection.None || previousDirection == candidate)
            return candidate;

        var candidateAmount = DirectionAmount(candidate, forwardAmount, rightAmount);
        var previousAmount = DirectionAmount(previousDirection, forwardAmount, rightAmount);
        return previousAmount > 0f && candidateAmount < previousAmount * RelativeDirectionSwitchRatio
            ? previousDirection
            : candidate;
    }

    private static float DirectionAmount(
        MovementDirection direction,
        float forwardAmount,
        float rightAmount) => direction switch {
        MovementDirection.Forward => forwardAmount,
        MovementDirection.Backward => -forwardAmount,
        MovementDirection.StrafeRight => rightAmount,
        MovementDirection.StrafeLeft => -rightAmount,
        _ => 0f,
    };

    private static Vector3 ClampLength2D(Vector3 value, float maximumLength) {
        var length = Length2D(value);
        if (length <= maximumLength || length <= float.Epsilon)
            return value;

        var scale = maximumLength / length;
        return new Vector3(value.X * scale, value.Y, value.Z * scale);
    }

    private static float Length2D(Vector3 value) =>
        MathF.Sqrt(value.X * value.X + value.Z * value.Z);
}

/// <summary>
/// Classifies the anchor's own locomotion. This deliberately ignores the
/// follower slot's orbital motion so rotating formations retain the original
/// face-target-and-run behavior.
/// </summary>
public sealed class FormationAnchorLocomotionTracker {
    public const float TranslationSampleDistance = 0.01f;
    public const float RotationSampleRadians = 0.0175f;
    public const long MotionHoldMs = 150;

    private Vector3 _lastPosition;
    private float _rotationReference;
    private long _lastTranslationMs;
    private long _lastRotationMs;
    private bool _hasTranslation;
    private bool _hasRotation;
    private bool _lastTranslationWasBackwardOrStrafe;

    public FormationAnchorLocomotionTracker(Vector3 position, float rotation, long nowMs) {
        Reset(position, rotation, nowMs);
    }

    public void Reset(Vector3 position, float rotation, long nowMs) {
        _lastPosition = position;
        _rotationReference = rotation;
        _lastTranslationMs = nowMs;
        _lastRotationMs = nowMs;
        _hasTranslation = false;
        _hasRotation = false;
        _lastTranslationWasBackwardOrStrafe = false;
    }

    public bool Update(Vector3 position, float rotation, long nowMs) {
        var rotationDelta = MathF.Atan2(
            MathF.Sin(rotation - _rotationReference),
            MathF.Cos(rotation - _rotationReference));
        if (MathF.Abs(rotationDelta) >= RotationSampleRadians) {
            _rotationReference = rotation;
            _lastRotationMs = nowMs;
            _hasRotation = true;
        }

        var translation = position - _lastPosition;
        translation.Y = 0f;
        _lastPosition = position;
        if (translation.LengthSquared() >= TranslationSampleDistance * TranslationSampleDistance) {
            _lastTranslationWasBackwardOrStrafe = IsBackwardOrStrafe(translation, rotation);
            _lastTranslationMs = nowMs;
            _hasTranslation = true;
        }

        var rotationIsActive = _hasRotation && nowMs - _lastRotationMs <= MotionHoldMs;
        var translationIsActive = _hasTranslation && nowMs - _lastTranslationMs <= MotionHoldMs;
        return translationIsActive
            && !rotationIsActive
            && _lastTranslationWasBackwardOrStrafe;
    }

    public static bool IsBackwardOrStrafe(Vector3 translation, float facingRadians) {
        translation.Y = 0f;
        if (translation.LengthSquared() <= float.Epsilon)
            return false;

        var forward = facingRadians.Radians().ToDirectionXZ();
        var right = new Vector3(-forward.Z, 0f, forward.X);
        var forwardAmount = Vector3.Dot(translation, forward);
        var rightAmount = Vector3.Dot(translation, right);

        return forwardAmount < 0f
            || MathF.Abs(rightAmount) > MathF.Abs(forwardAmount);
    }
}
