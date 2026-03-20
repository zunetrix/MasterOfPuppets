namespace MasterOfPuppets.Ipc;

internal partial class IpcProvider {

    public void ExecuteLogout(bool includeSelf = false) {
        BroadCast(IpcMessage.Create(IpcMessageType.ExecuteLogout).Serialize(), includeSelf);
    }

    [IpcHandle(IpcMessageType.ExecuteLogout)]
    private void HandleExecuteLogout(IpcMessage message) {
        GameExitManager.Logout();
    }

    public void ExecuteShutdown(bool includeSelf = false) {
        BroadCast(IpcMessage.Create(IpcMessageType.ExecuteShutdown).Serialize(), includeSelf);
    }

    [IpcHandle(IpcMessageType.ExecuteShutdown)]
    private void HandleExecuteShutdown(IpcMessage message) {
        GameExitManager.Shutdown();
    }
}
