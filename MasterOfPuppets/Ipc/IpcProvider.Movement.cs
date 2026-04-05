using MasterOfPuppets.Movement;

namespace MasterOfPuppets.Ipc;

internal partial class IpcProvider {

    public void ExecuteMoveToMyTarget() {
        if (DalamudApi.ObjectTable.LocalPlayer == null) return;
        var targetObjectId = DalamudApi.ObjectTable.LocalPlayer.TargetObjectId;
        if (targetObjectId == 0) return;
        BroadCast(IpcMessage.Create(IpcMessageType.ExecuteMoveToMyTarget, targetObjectId).Serialize(), includeSelf: false);
    }

    [IpcHandle(IpcMessageType.ExecuteMoveToMyTarget)]
    private void HandleExecuteMoveToMyTarget(IpcMessage message) {
        Plugin.MovementManager.MoveTo(message.DataStruct<ulong>());
    }

    public void ExecuteStackOnMe() {
        if (DalamudApi.ObjectTable.LocalPlayer == null) return;
        var targetObjectId = DalamudApi.ObjectTable.LocalPlayer.GameObjectId;
        BroadCast(IpcMessage.Create(IpcMessageType.ExecuteStackOnMe, targetObjectId).Serialize(), includeSelf: false);
    }

    [IpcHandle(IpcMessageType.ExecuteStackOnMe)]
    private void HandleExecuteStackOnMe(IpcMessage message) {
        Plugin.MovementManager.MoveTo(message.DataStruct<ulong>());
    }

    public void ExecuteToggleWalking() {
        BroadCast(IpcMessage.Create(IpcMessageType.ExecuteToggleWalking).Serialize(), includeSelf: true);
    }

    [IpcHandle(IpcMessageType.ExecuteToggleWalking)]
    private void HandleExecuteToggleWalking(IpcMessage message) {
        MovementManager.ToggleWalking();
    }

    public void StopMovement() {
        BroadCast(IpcMessage.Create(IpcMessageType.StopMovement).Serialize(), includeSelf: true);
    }

    [IpcHandle(IpcMessageType.StopMovement)]
    private void HandleStopMovement(IpcMessage message) {
        Plugin.MovementManager.StopMove();
    }
}
