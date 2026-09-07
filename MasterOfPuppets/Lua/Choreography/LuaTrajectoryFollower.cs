using System;
using System.Numerics;

namespace MasterOfPuppets.LuaScripting.Choreography;

public enum LuaTrajectoryLocomotion {
    Hold,
    Walk,
    Run,
}

public sealed record LuaTrajectoryFollowerOptions {
    public float PhaseGain { get; init; } = 0.9f;
    public float RadialGain { get; init; } = 1.4f;
    public float MaximumRadialSpeed { get; init; } = 0.8f;
    public float MaximumSpeed { get; init; } = 6.4f;
    public float MaximumAcceleration { get; init; } = 2.5f;
    public float MaximumTurnRate { get; init; } = 2.5f;
    public float WalkSpeedThreshold { get; init; } = 1.25f;
    public float HoldSpeedThreshold { get; init; } = 0.08f;
    public float LookAheadSeconds { get; init; } = 0.32f;
    public float MinimumRadiusFraction { get; init; } = 0.65f;

    public LuaTrajectoryFollowerOptions Validate() {
        if (!float.IsFinite(PhaseGain) || PhaseGain < 0f
            || !float.IsFinite(RadialGain) || RadialGain < 0f
            || !float.IsFinite(MaximumRadialSpeed) || MaximumRadialSpeed <= 0f
            || !float.IsFinite(MaximumSpeed) || MaximumSpeed <= 0f
            || !float.IsFinite(MaximumAcceleration) || MaximumAcceleration <= 0f
            || !float.IsFinite(MaximumTurnRate) || MaximumTurnRate <= 0f
            || !float.IsFinite(WalkSpeedThreshold) || WalkSpeedThreshold <= 0f
            || !float.IsFinite(HoldSpeedThreshold) || HoldSpeedThreshold < 0f
            || !float.IsFinite(LookAheadSeconds) || LookAheadSeconds <= 0f
            || !float.IsFinite(MinimumRadiusFraction) || MinimumRadiusFraction is <= 0f or > 1f)
            throw new ArgumentOutOfRangeException(nameof(LuaTrajectoryFollowerOptions), "Trajectory follower limits must be finite and positive.");
        return this;
    }
}

public readonly record struct LuaTrajectoryFollowerInput(
    Vector3 AnchorPosition,
    Vector3 AnchorVelocity,
    Vector3 ActorPosition,
    float ActorFacing,
    Vector3 DesiredPosition,
    float DesiredPhase,
    float DesiredRadius,
    float DesiredTangentialSpeed,
    int Direction,
    float DeltaSeconds);

public readonly record struct LuaTrajectoryFollowerOutput(
    Vector3 LookAheadTarget,
    Vector3 Velocity,
    float RequestedSpeed,
    float RequestedFacing,
    float RadialError,
    float PhaseError,
    LuaTrajectoryLocomotion Locomotion,
    bool Recovering) {

    public bool IsFinite =>
        IsFiniteVector(LookAheadTarget)
        && IsFiniteVector(Velocity)
        && float.IsFinite(RequestedSpeed)
        && float.IsFinite(RequestedFacing)
        && float.IsFinite(RadialError)
        && float.IsFinite(PhaseError);

    private static bool IsFiniteVector(Vector3 value) =>
        float.IsFinite(value.X) && float.IsFinite(value.Y) && float.IsFinite(value.Z);
}

/// <summary>Pure, stateful steering law for an actor following a circular choreography slot.</summary>
public sealed class LuaTrajectoryFollower {
    private readonly LuaTrajectoryFollowerOptions _options;
    private float _requestedSpeed;

    public LuaTrajectoryFollower(LuaTrajectoryFollowerOptions? options = null) {
        _options = (options ?? new LuaTrajectoryFollowerOptions()).Validate();
    }

    public LuaTrajectoryFollowerOutput Step(LuaTrajectoryFollowerInput input) {
        Validate(input);
        var direction = input.Direction >= 0 ? 1 : -1;
        var relative = Flatten(input.ActorPosition - input.AnchorPosition);
        var actualRadius = relative.Length();
        var safeRadius = MathF.Max(input.DesiredRadius * _options.MinimumRadiusFraction, 0.1f);
        var radialOut = actualRadius > 0.001f
            ? relative / actualRadius
            : UnitFromPhase(input.DesiredPhase);
        var actualPhase = MathF.Atan2(relative.X, relative.Z);
        var phaseError = Wrap(input.DesiredPhase - actualPhase) * direction;
        var phaseDistance = phaseError * MathF.Max(input.DesiredRadius, safeRadius);
        var radialError = input.DesiredRadius - actualRadius;

        // Never request reverse motion. An actor ahead of its slot slows or
        // holds while the slot catches it; a late actor receives bounded catch-up.
        var forwardSpeed = Math.Clamp(
            input.DesiredTangentialSpeed + phaseDistance * _options.PhaseGain,
            0f,
            _options.MaximumSpeed);
        var radialSpeed = Math.Clamp(
            radialError * _options.RadialGain,
            -_options.MaximumRadialSpeed,
            _options.MaximumRadialSpeed);
        var tangent = direction * new Vector3(radialOut.Z, 0f, -radialOut.X);
        var relativeVelocity = tangent * forwardSpeed + radialOut * radialSpeed;
        var desiredRelativeSpeed = Math.Clamp(relativeVelocity.Length(), 0f, _options.MaximumSpeed);
        var maximumSpeedChange = _options.MaximumAcceleration * input.DeltaSeconds;
        _requestedSpeed = MoveToward(_requestedSpeed, desiredRelativeSpeed, maximumSpeedChange);
        var limitedRelativeVelocity = desiredRelativeSpeed > 0.0001f
            ? relativeVelocity * (_requestedSpeed / desiredRelativeSpeed)
            : Vector3.Zero;
        var velocity = Flatten(input.AnchorVelocity) + limitedRelativeVelocity;

        var desiredFacing = velocity.LengthSquared() > 0.000001f
            ? MathF.Atan2(velocity.X, velocity.Z)
            : input.ActorFacing;
        var requestedFacing = StepAngle(
            input.ActorFacing,
            desiredFacing,
            _options.MaximumTurnRate * input.DeltaSeconds);
        var lookAhead = input.ActorPosition + velocity * _options.LookAheadSeconds;
        var projected = Flatten(lookAhead - input.AnchorPosition);
        var isAnchorMoving = input.AnchorVelocity.Length() > 0.25f;
        if (!isAnchorMoving && projected.Length() < safeRadius) {
            var protectedDirection = projected.LengthSquared() > 0.000001f
                ? Vector3.Normalize(projected)
                : radialOut;
            lookAhead = input.AnchorPosition + protectedDirection * safeRadius;
            lookAhead.Y = input.DesiredPosition.Y;
        }

        var locomotion = isAnchorMoving
            ? LuaTrajectoryLocomotion.Run
            : (_requestedSpeed <= _options.HoldSpeedThreshold
                ? LuaTrajectoryLocomotion.Hold
                : _requestedSpeed <= _options.WalkSpeedThreshold
                    ? LuaTrajectoryLocomotion.Walk
                    : LuaTrajectoryLocomotion.Run);
        return new LuaTrajectoryFollowerOutput(
            lookAhead,
            velocity,
            _requestedSpeed,
            requestedFacing,
            radialError,
            phaseError,
            locomotion,
            MathF.Abs(radialError) > 0.35f || MathF.Abs(phaseDistance) > 0.5f);
    }

    public void Reset() => _requestedSpeed = 0f;

    private static void Validate(LuaTrajectoryFollowerInput input) {
        if (!IsFinite(input.AnchorPosition)
            || !IsFinite(input.AnchorVelocity)
            || !IsFinite(input.ActorPosition)
            || !IsFinite(input.DesiredPosition)
            || !float.IsFinite(input.ActorFacing)
            || !float.IsFinite(input.DesiredPhase)
            || !float.IsFinite(input.DesiredRadius) || input.DesiredRadius <= 0f
            || !float.IsFinite(input.DesiredTangentialSpeed) || input.DesiredTangentialSpeed < 0f
            || !float.IsFinite(input.DeltaSeconds) || input.DeltaSeconds is <= 0f or > 0.25f)
            throw new ArgumentOutOfRangeException(nameof(input), "Trajectory follower input must be finite and within its supported time/radius range.");
    }

    private static Vector3 Flatten(Vector3 value) => new(value.X, 0f, value.Z);
    private static bool IsFinite(Vector3 value) =>
        float.IsFinite(value.X) && float.IsFinite(value.Y) && float.IsFinite(value.Z);
    private static Vector3 UnitFromPhase(float phase) => new(MathF.Sin(phase), 0f, MathF.Cos(phase));
    private static float Wrap(float radians) => MathF.Atan2(MathF.Sin(radians), MathF.Cos(radians));
    private static float MoveToward(float current, float target, float maximumDelta) =>
        current < target ? MathF.Min(current + maximumDelta, target) : MathF.Max(current - maximumDelta, target);
    private static float StepAngle(float current, float target, float maximumDelta) =>
        Wrap(current + Math.Clamp(Wrap(target - current), -maximumDelta, maximumDelta));
}
