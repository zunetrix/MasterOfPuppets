using MasterOfPuppets.Extensions;

namespace MasterOfPuppets.Ipc;

internal partial class IpcProvider {

    public void SyncConfiguration() {
        Plugin.Config.Save();
        var message = IpcMessage.Create(IpcMessageType.SyncConfiguration, Plugin.Config.JsonSerialize(), Plugin.Config.SaveConfigAfterSync.ToString()).Serialize();
        BroadCast(message, includeSelf: false);
    }

    [IpcHandle(IpcMessageType.SyncConfiguration)]
    private void HandleSyncConfiguration(IpcMessage message) {
        var configString = message.StringData[0];
        bool saveConfigAfterSync = bool.TryParse(message.StringData[1], out var parsed) && parsed;
        Plugin.Config.UpdateFromJson(configString);
        if (!AutoLoginPlanner.HasEnabledCandidates(Plugin.Config.Characters))
            Plugin.AutoLoginManager.Stop();

        if (saveConfigAfterSync)
            Plugin.Config.Save();
    }

    public void RefreshCommands() {
        BroadCast(IpcMessage.Create(IpcMessageType.RefreshCommands).Serialize(), includeSelf: true);
    }

    [IpcHandle(IpcMessageType.RefreshCommands)]
    private void HandleRefreshCommands(IpcMessage message) {
        Plugin.PluginCommandManager.RefreshCustomCommands();
    }
}
