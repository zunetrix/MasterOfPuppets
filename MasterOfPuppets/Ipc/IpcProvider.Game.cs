using System;

using MasterOfPuppets.Camera;

namespace MasterOfPuppets.Ipc;

internal partial class IpcProvider {
    public void ExecuteAbandonDuty() {
        BroadCast(IpcMessage.Create(IpcMessageType.ExecuteAbandonDuty).Serialize(), includeSelf: true);
    }

    [IpcHandle(IpcMessageType.ExecuteAbandonDuty)]
    private void HandleExecuteAbandonDuty(IpcMessage message) {
        GameFunctions.AbandonDuty();
    }

    public void EnableCamHack() {
        BroadCast(IpcMessage.Create(IpcMessageType.EnableCamHack).Serialize(), includeSelf: false);
    }

    [IpcHandle(IpcMessageType.EnableCamHack)]
    private void HandleEnableCamHack(IpcMessage message) {
        GameCameraManager.EnableCamHighHeight();
    }

    public void DisableCamHack() {
        BroadCast(IpcMessage.Create(IpcMessageType.DisableCamHack).Serialize(), includeSelf: false);
    }

    [IpcHandle(IpcMessageType.DisableCamHack)]
    private void HandleDisableCamHack(IpcMessage message) {
        GameCameraManager.Disable();
    }
}
