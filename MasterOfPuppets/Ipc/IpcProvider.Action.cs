namespace MasterOfPuppets.Ipc;

internal partial class IpcProvider {

    public void ExecuteActionCommand(uint actionId) {
        BroadCast(IpcMessage.Create(IpcMessageType.ExecuteActionCommand, actionId).Serialize(), includeSelf: true);
    }

    [IpcHandle(IpcMessageType.ExecuteActionCommand)]
    private void HandleExecuteActionCommand(IpcMessage message) {
        GameActionManager.UseAction(message.DataStruct<uint>());
    }

    public void ExecuteGeneralActionCommand(uint actionId) {
        BroadCast(IpcMessage.Create(IpcMessageType.ExecuteGeneralActionCommand, actionId).Serialize(), includeSelf: true);
    }

    [IpcHandle(IpcMessageType.ExecuteGeneralActionCommand)]
    private void HandleExecuteGeneralActionCommand(IpcMessage message) {
        GameActionManager.UseGeneralAction(message.DataStruct<uint>());
    }

    public void ExecuteItemCommand(uint itemId) {
        BroadCast(IpcMessage.Create(IpcMessageType.ExecuteItemCommand, itemId).Serialize(), includeSelf: true);
    }

    [IpcHandle(IpcMessageType.ExecuteItemCommand)]
    private void HandleExecuteItemCommand(IpcMessage message) {
        GameActionManager.UseItem(message.DataStruct<uint>());
    }
}
