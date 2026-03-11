using System;

namespace MasterOfPuppets.Ipc;

internal partial class IpcProvider {

    public void ExecuteChangeGearset(int gearsetIndex) {
        BroadCast(IpcMessage.Create(IpcMessageType.ExecuteChangeGearset, gearsetIndex).Serialize(), includeSelf: true);
    }

    [IpcHandle(IpcMessageType.ExecuteChangeGearset)]
    private void HandleExecuteChangeGearset(IpcMessage message) {
        GearsetManager.ChangeGearset(Plugin, message.DataStruct<int>());
    }

    public void ExecuteAbandonDuty() {
        BroadCast(IpcMessage.Create(IpcMessageType.ExecuteAbandonDuty).Serialize(), includeSelf: true);
    }

    [IpcHandle(IpcMessageType.ExecuteAbandonDuty)]
    private void HandleExecuteAbandonDuty(IpcMessage message) {
        GameFunctions.AbandonDuty();
    }

    public void SetGameSettingsObjectQuantity(SettingsDisplayObjectLimitType displayObjectLimitType) {
        BroadCast(IpcMessage.Create(IpcMessageType.SetGameSettingsObjectQuantity, displayObjectLimitType).Serialize(), includeSelf: true);
    }

    [IpcHandle(IpcMessageType.SetGameSettingsObjectQuantity)]
    private void HandleSetGameSettingsObjectQuantity(IpcMessage message) {
        var displayObjectLimitType = message.DataStruct<SettingsDisplayObjectLimitType>();
        if (!Enum.IsDefined(typeof(SettingsDisplayObjectLimitType), displayObjectLimitType)) {
            DalamudApi.PluginLog.Warning($"Invalid object quantity value: {displayObjectLimitType}");
            return;
        }
        GameSettingsManager.SetDisplayObjectLimit(displayObjectLimitType);
    }
}
