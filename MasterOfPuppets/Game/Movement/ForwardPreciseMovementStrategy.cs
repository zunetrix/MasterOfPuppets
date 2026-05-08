using System.Numerics;

using MasterOfPuppets.Extensions;

namespace MasterOfPuppets.Movement;

internal sealed class ForwardPreciseMovementStrategy : ISimpleMovementStrategy {
    private readonly ForwardInputMovementController _forwardInput;

    public ForwardPreciseMovementStrategy(ForwardInputMovementController forwardInput) {
        _forwardInput = forwardInput;
    }

    public string Name => "Forward";
    public bool UsesNativeStopOnCompletion => true;

    public void Start(SimpleMovementContext context) {
    }

    public SimpleMovementUpdateResult Update(SimpleMovementContext context, Vector3 playerPosition) {
        var distance = playerPosition.Distance2D(context.Destination);
        if (distance <= context.Precision)
            return SimpleMovementUpdateResult.Complete;

        GameFunctions.FaceDirection(context.Destination);
        _forwardInput.MoveForward();
        SimpleMovementWalkState.IsWalking = distance <= context.Precision + SimpleInputMovement.ArrivalWalkBuffer;
        return SimpleMovementUpdateResult.Running;
    }

    public void Stop() {
        _forwardInput.Stop();
    }
}
