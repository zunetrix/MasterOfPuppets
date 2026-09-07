using System;
using System.Numerics;

using MasterOfPuppets.Extensions;

namespace MasterOfPuppets.Movement;

/// <summary>
/// Follows the exact live formation slot without projecting it forward.
/// </summary>
internal sealed class FormationNaturalMovementStrategy : ISimpleMovementStrategy {
    private const float MaximumTurnRateRadiansPerSecond = 2.5f;

    private readonly ForwardInputMovementController _forwardInput;
    private readonly FormationTargetTracker _tracker = new();
    private float? _faceDirection;
    private float _precision;
    private float? _lastIssuedFormationFacing;
    private bool _useFormationRelativeMovement;
    private bool _usePursuitTarget;
    private bool _allowHoldWhileTargetMoving;
    private bool _rateLimitTravelFacing;
    private MovementDirection _relativeMovementDirection;
    private bool _holding;
    private long _lastSteeringUpdateMs;

    public FormationNaturalMovementStrategy(ForwardInputMovementController forwardInput) {
        _forwardInput = forwardInput;
    }

    public string Name => "Natural";
    public void Start(SimpleMovementContext context) {
        _tracker.Reset(context.Destination, Environment.TickCount64);
        _faceDirection = context.FaceDirection;
        _precision = context.Precision;
        _lastIssuedFormationFacing = null;
        _useFormationRelativeMovement = context.UseFormationRelativeMovement;
        _usePursuitTarget = context.UsePursuitTarget;
        _allowHoldWhileTargetMoving = context.AllowHoldWhileTargetMoving;
        _rateLimitTravelFacing = context.RateLimitTravelFacing;
        _relativeMovementDirection = MovementDirection.None;
        _holding = false;
        _lastSteeringUpdateMs = Environment.TickCount64;
    }

    public void UpdateTarget(
        Vector3 destination,
        float precision,
        float? faceDirection,
        bool useFormationRelativeMovement,
        bool usePursuitTarget,
        bool allowHoldWhileTargetMoving,
        bool rateLimitTravelFacing) {
        _tracker.UpdateTarget(destination, Environment.TickCount64);
        _precision = precision;
        _faceDirection = faceDirection;
        _useFormationRelativeMovement = useFormationRelativeMovement;
        _usePursuitTarget = usePursuitTarget;
        _allowHoldWhileTargetMoving = allowHoldWhileTargetMoving;
        _rateLimitTravelFacing = rateLimitTravelFacing;
    }

    public bool IsSlotMoving => _tracker.IsSlotMoving;
    public bool IsIssuingMovement => _forwardInput.Direction != MovementDirection.None;

    public SimpleMovementUpdateResult Update(SimpleMovementContext context, Vector3 playerPosition) {
        var slotMoving = _tracker.IsSlotMoving;
        var distance = playerPosition.Distance2D(_tracker.Target);
        _holding = FormationTargetTracker.ShouldHold(
            distance,
            _precision,
            _holding,
            slotMoving && !_allowHoldWhileTargetMoving);
        if (_holding) {
            _forwardInput.Stop();
            ApplyFormationFacing();
            return SimpleMovementUpdateResult.Running;
        }

        // Lead moving slots slightly so continuous trajectories behave like a
        // path to run along, rather than a series of tiny arrival destinations.
        var target = slotMoving && _usePursuitTarget
            ? _tracker.GetPursuitTarget()
            : _tracker.Target;

        if (_useFormationRelativeMovement && _faceDirection is { } relativeFacing) {
            if (_rateLimitTravelFacing)
                ApplyRateLimitedTravelFacing(relativeFacing);
            else
                ApplyFormationFacing();
            _relativeMovementDirection = FormationTargetTracker.SelectRelativeMovementDirection(
                playerPosition,
                target,
                relativeFacing,
                _relativeMovementDirection);
            _forwardInput.Move(_relativeMovementDirection);
            return SimpleMovementUpdateResult.Running;
        }

        var desiredAngle = distance > 0.15f || !_lastIssuedFormationFacing.HasValue
            ? MathF.Atan2(target.X - playerPosition.X, target.Z - playerPosition.Z)
            : _lastIssuedFormationFacing.Value;
        if (_rateLimitTravelFacing)
            ApplyRateLimitedTravelFacing(desiredAngle);
        else
            ApplyImmediateTravelFacing(desiredAngle);
        _forwardInput.MoveForward();
        return SimpleMovementUpdateResult.Running;
    }

    private void ApplyImmediateTravelFacing(float desiredAngle) {
        if (!FormationTargetTracker.ShouldUpdateFacing(_lastIssuedFormationFacing, desiredAngle))
            return;

        GameFunctions.FaceDirection(desiredAngle.Radians());
        _lastIssuedFormationFacing = desiredAngle;
    }

    private void ApplyRateLimitedTravelFacing(float desiredAngle) {
        var player = DalamudApi.ObjectTable.LocalPlayer;
        if (player == null)
            return;

        var nowMs = Environment.TickCount64;
        var elapsedSeconds = Math.Clamp((nowMs - _lastSteeringUpdateMs) / 1000f, 0f, 0.05f);
        _lastSteeringUpdateMs = nowMs;
        if (elapsedSeconds <= 0f)
            return;

        var delta = MathF.Atan2(
            MathF.Sin(desiredAngle - player.Rotation),
            MathF.Cos(desiredAngle - player.Rotation));
        var maximumStep = MaximumTurnRateRadiansPerSecond * elapsedSeconds;
        var nextRotation = FormationTargetTracker.StepRotationToward(
            player.Rotation,
            desiredAngle,
            maximumStep);
        if (MathF.Abs(delta) >= FormationTargetTracker.FacingUpdateThresholdRadians)
            GameFunctions.FaceDirection(nextRotation.Radians());
    }

    private void ApplyFormationFacing() {
        if (_faceDirection is not { } rotation
            || !FormationTargetTracker.ShouldUpdateFacing(_lastIssuedFormationFacing, rotation))
            return;

        GameFunctions.FaceDirection(rotation.Radians());
        _lastIssuedFormationFacing = rotation;
    }

    public void Stop() {
        _forwardInput.Stop();
        _holding = false;
        _lastIssuedFormationFacing = null;
        _useFormationRelativeMovement = false;
        _usePursuitTarget = false;
        _allowHoldWhileTargetMoving = true;
        _rateLimitTravelFacing = false;
        _relativeMovementDirection = MovementDirection.None;
        _lastSteeringUpdateMs = 0;
        _precision = 0f;
    }
}
