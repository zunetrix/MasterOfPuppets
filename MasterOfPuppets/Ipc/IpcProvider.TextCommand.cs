namespace MasterOfPuppets.Ipc;

internal partial class IpcProvider {

    public void ExecuteTextCommand(string text, bool includeSelf = true) {
        BroadCast(IpcMessage.Create(IpcMessageType.ExecuteTextCommand, text).Serialize(), includeSelf);
    }

    [IpcHandle(IpcMessageType.ExecuteTextCommand)]
    private void HandleExecuteTextCommand(IpcMessage message) {
        Chat.SendMessage(message.StringData[0]);
    }
}
