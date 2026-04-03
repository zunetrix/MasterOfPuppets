using System;

namespace MasterOfPuppets.Ipc;

internal partial class IpcProvider {
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

    public void SetGameSettingsAlwaysInput(uint enabled) {
        BroadCast(IpcMessage.Create(IpcMessageType.SetGameSettingsAlwaysInput, enabled).Serialize(), includeSelf: true);
    }

    [IpcHandle(IpcMessageType.SetGameSettingsAlwaysInput)]
    private void HandleSetGameSettingsAlwaysInput(IpcMessage message) {
        var enabled = message.DataStruct<uint>();
        GameSettingsManager.SetAlwaysInput(enabled);
    }
}
