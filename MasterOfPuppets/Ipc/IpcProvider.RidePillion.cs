namespace MasterOfPuppets.Ipc;

internal partial class IpcProvider {

    public void ExecuteRidePillion() {
        if (Plugin.Config.PreferredMultiRiderMountId == 0) return;
        var action = MountHelper.GetExecutableAction(Plugin.Config.PreferredMultiRiderMountId);
        Chat.SendMessage(action.TextCommand);

        BroadCast(IpcMessage.Create(IpcMessageType.RidePillion).Serialize(), includeSelf: false);
    }

    [IpcHandle(IpcMessageType.RidePillion)]
    private void HandleExecuteRidePillion(IpcMessage message) {
        DalamudApi.Framework.RunOnTick(() => {
            Plugin.MacroHandler.EnqueueMacroActions("#mop-inline-macro", actions: ["/mopwait 3", "/ridepillion <2>"], delayBetweenActions: 0);
        });
    }
}





