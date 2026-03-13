using System;
using System.Collections.Generic;
using System.Linq;

using MasterOfPuppets.Util;

namespace MasterOfPuppets.Ipc;

internal partial class IpcProvider {

    public void RunMacro(int macroIndex, Dictionary<string, string>? inlineVars = null, bool includeSelf = true) {
        DalamudApi.PluginLog.Debug($"[Run Macro] {macroIndex + 1}");
        if (inlineVars is { Count: > 0 }) {
            var varsToken = "-var=" + string.Join(";", inlineVars.Select(kv => $"${kv.Key}={kv.Value}"));
            BroadCast(IpcMessage.Create(IpcMessageType.RunMacro, macroIndex.ToString(), varsToken).Serialize(), includeSelf);
        } else {
            BroadCast(IpcMessage.Create(IpcMessageType.RunMacro, macroIndex.ToString()).Serialize(), includeSelf);
        }
    }

    [IpcHandle(IpcMessageType.RunMacro)]
    private void HandleRunMacro(IpcMessage message) {
        if (message.StringData is not { Length: > 0 } || !int.TryParse(message.StringData[0], out int macroIndex))
            return;
        var inlineVars = message.StringData.Length > 1
            ? ArgumentParser.ParseInlineVars(message.StringData[1])
            : null;
        Plugin.MacroHandler.ExecuteMacro(macroIndex, inlineVars);
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

    public void PauseMacroExecution() {
        BroadCast(IpcMessage.Create(IpcMessageType.PauseMacroExecution).Serialize(), includeSelf: true);
    }

    [IpcHandle(IpcMessageType.PauseMacroExecution)]
    private void HandlePauseMacroExecution(IpcMessage message) {
        Plugin.MacroHandler.Pause();
    }

    public void ResumeMacroExecution() {
        BroadCast(IpcMessage.Create(IpcMessageType.ResumeMacroExecution).Serialize(), includeSelf: true);
    }

    [IpcHandle(IpcMessageType.ResumeMacroExecution)]
    private void HandleResumeMacroExecution(IpcMessage message) {
        Plugin.MacroHandler.Resume();
    }
}
