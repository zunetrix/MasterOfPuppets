using System.Numerics;

namespace MasterOfPuppets.Movement;

public readonly record struct SimpleMovementContext(
    Vector3 Destination,
    float Precision,
    float? FaceDirection,
    bool UseFormationRelativeMovement = false,
    bool UsePursuitTarget = false,
    bool AllowHoldWhileTargetMoving = true,
    bool RateLimitTravelFacing = false);

internal enum SimpleMovementUpdateResult {
    Running,
    Complete,
}

internal interface ISimpleMovementStrategy {
    string Name { get; }
    void Start(SimpleMovementContext context);
    SimpleMovementUpdateResult Update(SimpleMovementContext context, Vector3 playerPosition);
    void Stop();
}
