using System.Numerics;

using MasterOfPuppets.Extensions;

namespace MasterOfPuppets.Movement;

internal sealed class ContinuousForwardMovementStrategy : ISimpleMovementStrategy {
    private readonly ForwardInputMovementController _forwardInput;

    public ContinuousForwardMovementStrategy(ForwardInputMovementController forwardInput) {
        _forwardInput = forwardInput;
    }

    public string Name => "Continuous";
    public bool UsesNativeStopOnCompletion => false;

    public void Start(SimpleMovementContext context) {
    }

    public SimpleMovementUpdateResult Update(SimpleMovementContext context, Vector3 playerPosition) {
        if (playerPosition.Distance2D(context.Destination) <= context.Precision)
            return SimpleMovementUpdateResult.Complete;

        GameFunctions.FaceDirection(context.Destination);
        _forwardInput.MoveForward();
        SimpleMovementWalkState.IsWalking = false;
        return SimpleMovementUpdateResult.Running;
    }

    public void Stop() {
        _forwardInput.Stop();
    }
}
