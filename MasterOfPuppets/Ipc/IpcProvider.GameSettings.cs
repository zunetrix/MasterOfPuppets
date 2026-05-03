using System;
using System.Linq;

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

    public void BroadcastApplyGameSettingsProfile(string profileName) {
        BroadCast(IpcMessage.Create(IpcMessageType.ApplyGameSettingsProfile, profileName).Serialize(), includeSelf: true);
    }

    [IpcHandle(IpcMessageType.ApplyGameSettingsProfile)]
    private void HandleApplyGameSettingsProfile(IpcMessage message) {
        var profileName = message.StringData?.FirstOrDefault() ?? string.Empty;
        var profile = Plugin.Config.GameSettingsProfiles.FirstOrDefault(p => string.Equals(p.Name, profileName, StringComparison.OrdinalIgnoreCase));
        if (profile != null) {
            GameSettingsManager.ApplyProfile(profile, Plugin.Config.GameSettingsProfileKeys);
        } else {
            DalamudApi.PluginLog.Warning($"Could not find Game Settings Profile to apply: {profileName}");
        }
    }
}
