namespace MasterOfPuppets.Ipc;

internal partial class IpcProvider {

    public void SetWindowTitle(bool enabled) {
        BroadCast(IpcMessage.Create(IpcMessageType.SetWindowTitle, enabled).Serialize(), includeSelf: true);
    }

    [IpcHandle(IpcMessageType.SetWindowTitle)]
    private void HandleSetWindowTitle(IpcMessage message) {
        var enabled = message.DataStruct<bool>();
        Plugin.GameWindowManager.SetCharacterNameWindowsTitle(enabled);
    }

    public void SetWindowResize(bool enabled) {
        BroadCast(IpcMessage.Create(IpcMessageType.SetWindowResize, enabled).Serialize(), includeSelf: true);
    }

    [IpcHandle(IpcMessageType.SetWindowResize)]
    private void HandleSetWindowResize(IpcMessage message) {
        var enabled = message.DataStruct<bool>();
        if (enabled) {
            Plugin.GameWindowManager.RemoveSizeRestrictions();
        } else {
            Plugin.GameWindowManager.RestoreSizeRestrictions();
        }
    }
}
