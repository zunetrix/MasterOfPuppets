using System;

using MasterOfPuppets.Movement;

namespace MasterOfPuppets.Ipc;

[AttributeUsage(AttributeTargets.Method)]
internal class IpcHandleAttribute : Attribute {
    public IpcMessageType MessageType { get; }

    public IpcHandleAttribute(IpcMessageType messageType) {
        MessageType = messageType;
    }
}

internal class IpcHandlers {
    private readonly Plugin Plugin;

    public IpcHandlers(Plugin plugin) {
        Plugin = plugin;
    }

    [IpcHandle(IpcMessageType.SyncConfiguration)]
    private void HandleSyncConfiguration(IpcMessage message) {
        var configString = message.StringData[0];
        bool saveConfigAfterSync = bool.TryParse(message.StringData[1], out var tmp) && tmp;

        Plugin.Config.UpdateFromJson(configString);

        if (saveConfigAfterSync)
            Plugin.Config.Save();
    }

    [IpcHandle(IpcMessageType.ExecuteTextCommand)]
    private void HandleExecuteTextCommand(IpcMessage message) {
        var textCommand = message.StringData[0];
        Chat.SendMessage(textCommand);
        // DalamudApi.Framework.RunOnTick(() => Chat.SendMessage(textCommand));
    }

    [IpcHandle(IpcMessageType.ExecuteActionCommand)]
    private void HandleExecuteActionCommand(IpcMessage message) {
        GameActionManager.UseAction(message.DataStruct<uint>());
    }

    [IpcHandle(IpcMessageType.ExecuteGeneralActionCommand)]
    private void HandleExecuteGeneralActionCommand(IpcMessage message) {
        GameActionManager.UseGeneralAction(message.DataStruct<uint>());
    }

    [IpcHandle(IpcMessageType.ExecuteChangeGearset)]
    private void HandleExecuteCHangeGearset(IpcMessage message) {
        int gearsetIndex = message.DataStruct<int>();
        GearSetHelper.ChangeGearset(Plugin, gearsetIndex);
    }

    [IpcHandle(IpcMessageType.ExecuteItemCommand)]
    private unsafe void HandleExecuteItemCommand(IpcMessage message) {
        uint itemId = message.DataStruct<uint>();
        GameActionManager.UseItem(itemId);
    }

    [IpcHandle(IpcMessageType.ExecuteTargetMyTarget)]
    private void HandleExecuteTargetMyTarget(IpcMessage message) {
        ulong targetObjectId = message.DataStruct<ulong>();
        GameTargetManager.TargetObject(targetObjectId);
    }

    [IpcHandle(IpcMessageType.ExecuteMoveToMyTarget)]
    private void ExecuteMoveToMyTarget(IpcMessage message) {
        ulong targetObjectId = message.DataStruct<ulong>();
        Plugin.MovementManager.MoveToObject(targetObjectId);
    }

    [IpcHandle(IpcMessageType.ExecuteToggleWalking)]
    private void ExecuteToggleWalking(IpcMessage message) {
        Plugin.MovementManager.ToggleWalking();
    }

    [IpcHandle(IpcMessageType.ExecuteTargetClear)]
    private void HandleExecuteTargetClear(IpcMessage message) {
        GameTargetManager.TargetClear();
    }

    [IpcHandle(IpcMessageType.ExecuteAbandonDuty)]
    private void HandleExecuteAbandonDuty(IpcMessage message) {
        GameFunctions.AbandonDuty();
    }

    [IpcHandle(IpcMessageType.SetGameSettingsObjectQuantity)]
    private void HandleSetGameSettingsObjectQuantity(IpcMessage message) {
        SettingsDisplayObjectLimitType displayObjectLimitType = message.DataStruct<SettingsDisplayObjectLimitType>();

        if (!Enum.IsDefined(typeof(SettingsDisplayObjectLimitType), displayObjectLimitType)) {
            DalamudApi.PluginLog.Warning($"Invalid object quantity value: {displayObjectLimitType}");
            return;
        }

        GameSettingsManager.SetDisplayObjectLimit(displayObjectLimitType);
    }

    [IpcHandle(IpcMessageType.StopMacroExecution)]
    private void HandleStopMacroExecution(IpcMessage message) {
        Plugin.MacroHandler.StopMacroQueueExecution();
    }

    [IpcHandle(IpcMessageType.StopMovement)]
    private void HandleStopMovement(IpcMessage message) {
        Plugin.MovementManager.StopMove();
    }

    [IpcHandle(IpcMessageType.RunMacro)]
    private void HandleRunMacro(IpcMessage message) {
        int macroIndex = message.DataStruct<int>();
        Plugin.MacroHandler.ExecuteMacro(macroIndex);
    }

    [IpcHandle(IpcMessageType.EnqueueMacroActions)]
    private void HandleEnqueueMacroActions(IpcMessage message) {
        var textCommand = message.StringData[0];
        Plugin.MacroHandler.EnqueueMacroActions("#mop-inline-macro", actions: [textCommand], delayBetweenActions: 0);
    }

    [IpcHandle(IpcMessageType.EnqueueCharacterMacroActions)]
    private void HandleEnqueueCharacterMacroActions(IpcMessage message) {
        if (message.StringData.Length < 2) return;

        var textCommand = message.StringData[0];
        var characterName = message.StringData[1];

        DalamudApi.Framework.RunOnTick(() => {
            var localPlayerName = $"{DalamudApi.PlayerState.CharacterName}@{DalamudApi.PlayerState.HomeWorld.Value.Name}";
            if (!localPlayerName.Contains(characterName, StringComparison.InvariantCultureIgnoreCase)) return;

            Plugin.MacroHandler.EnqueueMacroActions("#mop-inline-macro-char", actions: [textCommand], delayBetweenActions: 0);
        });
    }
}
