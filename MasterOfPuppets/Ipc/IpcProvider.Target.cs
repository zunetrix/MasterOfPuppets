namespace MasterOfPuppets.Ipc;

internal partial class IpcProvider {

    public void ExecuteTargetMyTarget() {
        if (DalamudApi.ObjectTable.LocalPlayer == null) return;
        var targetObjectId = DalamudApi.ObjectTable.LocalPlayer.TargetObjectId;
        if (targetObjectId == 0) return;
        BroadCast(IpcMessage.Create(IpcMessageType.ExecuteTargetMyTarget, targetObjectId).Serialize(), includeSelf: false);
    }

    [IpcHandle(IpcMessageType.ExecuteTargetMyTarget)]
    private void HandleExecuteTargetMyTarget(IpcMessage message) {
        GameTargetManager.TargetObject(message.DataStruct<ulong>());
    }

    public void ExecuteInteractWithMyTarget() {
        if (DalamudApi.ObjectTable.LocalPlayer == null) return;
        var targetObjectId = DalamudApi.ObjectTable.LocalPlayer.TargetObjectId;
        if (targetObjectId == 0) return;
        BroadCast(IpcMessage.Create(IpcMessageType.ExecuteInteractWithMyTarget, targetObjectId).Serialize(), includeSelf: true);
    }

    [IpcHandle(IpcMessageType.ExecuteInteractWithMyTarget)]
    private void HandleExecuteInteractWithMyTarget(IpcMessage message) {
        GameTargetManager.InteractWithMyTarget(message.DataStruct<ulong>());
    }

    public void ExecuteInteractWithTarget() {
        BroadCast(IpcMessage.Create(IpcMessageType.ExecuteInteractWithTarget).Serialize(), includeSelf: true);
    }

    [IpcHandle(IpcMessageType.ExecuteInteractWithTarget)]
    private void HandleExecuteInteractWithTarget(IpcMessage message) {
        GameTargetManager.InteractWithTarget();
    }

    public void ExecuteTargetClear() {
        BroadCast(IpcMessage.Create(IpcMessageType.ExecuteTargetClear).Serialize(), includeSelf: true);
    }

    [IpcHandle(IpcMessageType.ExecuteTargetClear)]
    private void HandleExecuteTargetClear(IpcMessage message) {
        GameTargetManager.TargetClear();
    }
}
