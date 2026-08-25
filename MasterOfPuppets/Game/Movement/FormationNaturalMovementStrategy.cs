using System;
using System.Numerics;

using MasterOfPuppets.Extensions;

namespace MasterOfPuppets.Movement;

/// <summary>
/// Follows the exact live formation slot without projecting it forward.
/// </summary>
internal sealed class FormationNaturalMovementStrategy : ISimpleMovementStrategy {
    private readonly ForwardInputMovementController _forwardInput;
    private readonly FormationTargetTracker _tracker = new();
    private float? _faceDirection;
    private float? _lastIssuedFormationFacing;
    private bool _useFormationRelativeMovement;
    private bool _holding;

    public FormationNaturalMovementStrategy(ForwardInputMovementController forwardInput) {
        _forwardInput = forwardInput;
    }

    public string Name => "Natural";
    public bool UsesNativeStopOnCompletion => false;

    public void Start(SimpleMovementContext context) {
        _tracker.Reset(context.Destination, Environment.TickCount64);
        _faceDirection = context.FaceDirection;
        _lastIssuedFormationFacing = null;
        _useFormationRelativeMovement = context.UseFormationRelativeMovement;
        _holding = false;
    }

    public void UpdateTarget(
        Vector3 destination,
        float? faceDirection,
        bool useFormationRelativeMovement) {
        _tracker.UpdateTarget(destination, Environment.TickCount64);
        _faceDirection = faceDirection;
        _useFormationRelativeMovement = useFormationRelativeMovement;
    }

    public SimpleMovementUpdateResult Update(SimpleMovementContext context, Vector3 playerPosition) {
        var target = _tracker.Target;
        var distance = playerPosition.Distance2D(target);
        _holding = FormationTargetTracker.ShouldHold(
            distance,
            context.Precision,
            _holding,
            _tracker.IsSlotMoving);
        if (_holding) {
            _forwardInput.Stop();
            ApplyFormationFacing();
            return SimpleMovementUpdateResult.Running;
        }

        if (_useFormationRelativeMovement && _faceDirection is { } relativeFacing) {
            ApplyFormationFacing();
            _forwardInput.Move(FormationTargetTracker.SelectRelativeMovementDirection(
                playerPosition,
                target,
                relativeFacing));
            return SimpleMovementUpdateResult.Running;
        }

        var desiredAngle = MathF.Atan2(target.X - playerPosition.X, target.Z - playerPosition.Z);
        if (FormationTargetTracker.ShouldUpdateFacing(_lastIssuedFormationFacing, desiredAngle)) {
            GameFunctions.FaceDirection(desiredAngle.Radians());
            _lastIssuedFormationFacing = desiredAngle;
        }

        _forwardInput.MoveForward();
        return SimpleMovementUpdateResult.Running;
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
    }
}
