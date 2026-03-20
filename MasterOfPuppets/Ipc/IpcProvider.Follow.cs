namespace MasterOfPuppets.Ipc;

internal partial class IpcProvider {

    public void StartFollow(uint entityId) {
        BroadCast(IpcMessage.Create(IpcMessageType.StartFollow, entityId).Serialize(), includeSelf: false);
    }

    [IpcHandle(IpcMessageType.StartFollow)]
    private void HandleStartFollow(IpcMessage message) {
        uint entityId = message.DataStruct<uint>();
        GameFunctions.FollowStart(entityId);
    }

    public void StopFollow() {
        BroadCast(IpcMessage.Create(IpcMessageType.StopFollow).Serialize(), includeSelf: false);
    }

    [IpcHandle(IpcMessageType.StopFollow)]
    private void HandleStopFollow(IpcMessage message) {
        GameFunctions.FollowStop();
    }
}





