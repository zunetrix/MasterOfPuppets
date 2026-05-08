using System.Numerics;

using MasterOfPuppets.Extensions;

namespace MasterOfPuppets.Movement;

internal sealed class ContinuousForwardMovementStrategy : ISimpleMovementStrategy {
    private readonly ForwardInputMovementController _forwardInput;
    private float? _previousDistance;
    private bool _hasApproachedDestination;

    public ContinuousForwardMovementStrategy(ForwardInputMovementController forwardInput) {
        _forwardInput = forwardInput;
    }

    public string Name => "Continuous";
    public bool UsesNativeStopOnCompletion => false;

    public void Start(SimpleMovementContext context) {
        _previousDistance = null;
        _hasApproachedDestination = false;
    }

    public SimpleMovementUpdateResult Update(SimpleMovementContext context, Vector3 playerPosition) {
        var distance = playerPosition.Distance2D(context.Destination);
        var progress = SimpleInputMovement.UpdateContinuousProgress(
            distance,
            context.Precision,
            _previousDistance,
            _hasApproachedDestination);

        _previousDistance = distance;
        _hasApproachedDestination = progress.HasApproached;

        if (progress.Complete)
            return SimpleMovementUpdateResult.Complete;

        GameFunctions.FaceDirection(context.Destination);
        _forwardInput.MoveForward();
        SimpleMovementWalkState.IsWalking = false;
        return SimpleMovementUpdateResult.Running;
    }

    public void Stop() {
        _previousDistance = null;
        _hasApproachedDestination = false;
        _forwardInput.Stop();
    }
}
