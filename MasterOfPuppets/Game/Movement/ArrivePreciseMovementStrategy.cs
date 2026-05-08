using System.Numerics;

using MasterOfPuppets.Extensions;

namespace MasterOfPuppets.Movement;

internal sealed class ArrivePreciseMovementStrategy : ISimpleMovementStrategy {
    private readonly ForwardInputMovementController _forwardInput;
    private readonly NativeStop _nativeStop;
    private bool _settling;
    private int _settleFrames;

    public ArrivePreciseMovementStrategy(ForwardInputMovementController forwardInput, NativeStop nativeStop) {
        _forwardInput = forwardInput;
        _nativeStop = nativeStop;
    }

    public string Name => "Precise";
    public bool UsesNativeStopOnCompletion => true;

    public void Start(SimpleMovementContext context) {
        _settling = false;
        _settleFrames = 0;
    }

    public SimpleMovementUpdateResult Update(SimpleMovementContext context, Vector3 playerPosition) {
        var distance = playerPosition.Distance2D(context.Destination);
        if (_settling) {
            if (distance > context.Precision + SimpleInputMovement.SettleHysteresis) {
                _settling = false;
                _settleFrames = 0;
            } else {
                _forwardInput.Stop();
                SimpleMovementWalkState.IsWalking = false;
                _settleFrames++;
                return _settleFrames >= SimpleInputMovement.SettleFrameCount
                    ? SimpleMovementUpdateResult.Complete
                    : SimpleMovementUpdateResult.Running;
            }
        }

        if (distance <= context.Precision) {
            _forwardInput.Stop();
            SimpleMovementWalkState.IsWalking = false;
            _nativeStop.Stop();
            _settling = true;
            _settleFrames = 0;
            return SimpleMovementUpdateResult.Running;
        }

        GameFunctions.FaceDirection(context.Destination);
        _forwardInput.MoveForward();
        SimpleMovementWalkState.IsWalking = distance <= context.Precision + SimpleInputMovement.ArrivalWalkBuffer;
        return SimpleMovementUpdateResult.Running;
    }

    public void Stop() {
        _settling = false;
        _settleFrames = 0;
        _forwardInput.Stop();
    }
}
