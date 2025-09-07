using System;
using System.Linq;

namespace MasterOfPuppets.Ipc;

internal class IpcHandlers
{
    private readonly Plugin Plugin;

    public IpcHandlers(Plugin plugin) => Plugin = plugin;

    [IpcHandle(IpcMessageType.SyncConfiguration)]
    private void HandleSyncConfiguration(IpcMessage message)
    {
        var configString = message.StringData[0];
        bool saveConfigAfterSync = bool.TryParse(message.StringData[1], out var tmp) && tmp;

        Plugin.Config.UpdateFromJson(configString);

        if (saveConfigAfterSync)
            Plugin.Config.Save();
    }

    [IpcHandle(IpcMessageType.ExecuteTextCommand)]
    private void HandleExecuteTextCommand(IpcMessage message)
    {
        var textCommand = message.StringData[0];
        DalamudApi.Framework.RunOnTick(() => Chat.SendMessage(textCommand));
    }

    [IpcHandle(IpcMessageType.ExecuteActionCommand)]
    private void HandleExecuteActionCommand(IpcMessage message)
    {
        GameActionManager.UseActionById(message.DataStruct<uint>());
    }

    [IpcHandle(IpcMessageType.ExecuteItemCommand)]
    private void HandleExecuteItemCommand(IpcMessage message)
    {
        GameActionManager.UseItemById(message.DataStruct<uint>());
    }

    [IpcHandle(IpcMessageType.StopMacroExecution)]
    private void HandleStopMacroExecution(IpcMessage message)
    {
        MacroQueueExecutor.StopMacroQueueExecution();
    }

    [IpcHandle(IpcMessageType.ExecuteTargetMyTarget)]
    private void HandleExecuteTargetMyTarget(IpcMessage message)
    {
        ulong targetObjectId = message.DataStruct<ulong>();
        TargetManager.TargetByObjectId(targetObjectId);
    }

    [IpcHandle(IpcMessageType.ExecuteTargetClear)]
    private void HandleExecuteTargetClear(IpcMessage message)
    {
        TargetManager.TargetClear();
    }

    [IpcHandle(IpcMessageType.RunMacro)]
    private void HandleRunMacro(IpcMessage message)
    {
        var macroIndex = message.DataStruct<int>();
        if (macroIndex < 0 || macroIndex >= Plugin.Config.Macros.Count) return;

        var macro = Plugin.Config.Macros[macroIndex];
        var playerCid = DalamudApi.ClientState.LocalContentId;

        var playerActions = macro.Commands?
            .FirstOrDefault(c => c.Cids.Contains(playerCid))?.Actions;

        if (string.IsNullOrWhiteSpace(playerActions)) return;

        string[] actions = playerActions.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries);
        MacroQueueExecutor.EnqueueMacroActions(actions, Plugin.Config.DelayBetweenActions);
    }
}
