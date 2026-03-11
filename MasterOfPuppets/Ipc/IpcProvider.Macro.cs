using System;

namespace MasterOfPuppets.Ipc;

internal partial class IpcProvider {

    public void RunMacro(int macroIndex, bool includeSelf = true) {
        DalamudApi.PluginLog.Debug($"[Run Macro] {macroIndex + 1}");
        BroadCast(IpcMessage.Create(IpcMessageType.RunMacro, macroIndex).Serialize(), includeSelf);
    }

    [IpcHandle(IpcMessageType.RunMacro)]
    private void HandleRunMacro(IpcMessage message) {
        Plugin.MacroHandler.ExecuteMacro(message.DataStruct<int>());
    }

    public void EnqueueMacroActions(string textCommand, bool includeSelf) {
        BroadCast(IpcMessage.Create(IpcMessageType.EnqueueMacroActions, textCommand).Serialize(), includeSelf);
    }

    [IpcHandle(IpcMessageType.EnqueueMacroActions)]
    private void HandleEnqueueMacroActions(IpcMessage message) {
        Plugin.MacroHandler.EnqueueMacroActions("#mop-inline-macro", actions: [message.StringData[0]], delayBetweenActions: 0);
    }

    public void EnqueueCharacterMacroActions(string textCommand, string characterName) {
        BroadCast(IpcMessage.Create(IpcMessageType.EnqueueCharacterMacroActions, textCommand, characterName).Serialize(), includeSelf: true);
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

    public void StopMacroExecution() {
        BroadCast(IpcMessage.Create(IpcMessageType.StopMacroExecution).Serialize(), includeSelf: true);
    }

    [IpcHandle(IpcMessageType.StopMacroExecution)]
    private void HandleStopMacroExecution(IpcMessage message) {
        Plugin.MacroHandler.StopMacroQueueExecution();
    }
}
