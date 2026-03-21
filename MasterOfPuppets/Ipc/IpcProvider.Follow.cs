using MasterOfPuppets.Movement;

namespace MasterOfPuppets.Ipc;

internal partial class IpcProvider {

    public void Follow(uint entityId) {
        BroadCast(IpcMessage.Create(IpcMessageType.StartFollow, entityId).Serialize(), includeSelf: false);
    }

    [IpcHandle(IpcMessageType.StartFollow)]
    private void HandleFollow(IpcMessage message) {
        uint entityId = message.DataStruct<uint>();
        MovementManager.Follow(entityId);
    }

    public void StopFollow() {
        BroadCast(IpcMessage.Create(IpcMessageType.StopFollow).Serialize(), includeSelf: false);
    }

    [IpcHandle(IpcMessageType.StopFollow)]
    private void HandleStopFollow(IpcMessage message) {
        MovementManager.StopFollow();
    }
}





