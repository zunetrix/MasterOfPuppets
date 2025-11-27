using System;
using System.Collections.Generic;
using System.Linq;

using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;

using MasterOfPuppets.Util;

namespace MasterOfPuppets;

internal class ChatWatcher : IDisposable {
    private Plugin Plugin { get; }

    public readonly HashSet<XivChatType> AllowedChatTypes = new()
    {
        // XivChatType.Say,
        XivChatType.Party,
        // XivChatType.CrossParty,
        XivChatType.FreeCompany,
        // XivChatType.Alliance,
        XivChatType.Ls1,
        XivChatType.Ls2,
        XivChatType.Ls3,
        XivChatType.Ls4,
        XivChatType.Ls5,
        XivChatType.Ls6,
        XivChatType.Ls7,
        XivChatType.Ls8,
        XivChatType.CrossLinkShell1,
        XivChatType.CrossLinkShell2,
        XivChatType.CrossLinkShell3,
        XivChatType.CrossLinkShell4,
        XivChatType.CrossLinkShell5,
        XivChatType.CrossLinkShell6,
        XivChatType.CrossLinkShell7,
        XivChatType.CrossLinkShell8,
    };

    private readonly Dictionary<string, Action<string[], string>> CommandHandlers;

    public ChatWatcher(Plugin plugin) {
        Plugin = plugin;
        CommandHandlers = new(StringComparer.OrdinalIgnoreCase) {
            ["moprun"] = HandleRunMacro,
            ["mopstop"] = HandleStopMacroExecution,
            ["mopbr"] = HandleBroadcastCommandExecution,
            ["mopbrn"] = HandleBroadcastNotMeCommandExecution,
            ["mopbrc"] = HandleBroadcastCharacterCommandExecution,
        };

        DalamudApi.ChatGui.ChatMessage += OnChatMessage;
    }

    public void Dispose() {
        DalamudApi.ChatGui.ChatMessage -= OnChatMessage;
    }

    private void OnChatMessage(XivChatType type, int timestamp, ref SeString sender, ref SeString message, ref bool isHandled) {
        if (!Plugin.Config.UseChatSync) return;
        if (isHandled) return;

        if (!AllowedChatTypes.Contains(type)
            || !Plugin.Config.ListenedChatTypes.Contains(type)
            || (Plugin.Config.UseChatCommandSenderWhitelist && !Plugin.Config.ChatCommandSenderWhitelist.Contains(sender.ToString()))
        ) {
            return;
        }

        // listen only to known commands
        var messageString = message.ToString();
        if (!CommandHandlers.Keys.Any(cmd => messageString.StartsWith(cmd, StringComparison.OrdinalIgnoreCase))) {
            return;
        }

        var parsedArgs = ArgumentParser.ParseChatArgs(messageString);
        if (!parsedArgs.Any()) return;

        string command = parsedArgs[0].ToLower();
        string[] args = parsedArgs.Skip(1).ToArray();

        // DalamudApi.PluginLog.Debug($"OnChatMessage: [{command}]: {string.Join("|", args)}");

        if (CommandHandlers.TryGetValue(command, out var action)) {
            action.Invoke(args, sender.ToString());
            // prevent show chat text
            // isHandled = true;
        }
    }

    public void SendChatRunMacro(string macroName) {
        var message = $"/p moprun {macroName}";
        Chat.SendMessage(message);
    }

    private void HandleRunMacro(string[] args, string senderName) {
        if (args.Length < 1) {
            DalamudApi.ChatGui.PrintError($"Invalid command arguments expected 1 <macro name>");
            return;
        }

        // macro execution
        string macroNameOrIndex = args[0];
        int macroIndex = Plugin.MacroManager.FindMacroIndex(macroNameOrIndex);
        var macro = Plugin.Config.Macros[macroIndex];
        var playerCid = DalamudApi.ClientState.LocalContentId;
        var playerActions = macro.GetCidActions(playerCid);

        Plugin.MacroHandler.EnqueueMacroActions(macro.Name, playerActions, Plugin.Config.DelayBetweenActions);
    }

    public void SendChatStopMacroExecution() {
        var message = $"/p mopstop";
        Chat.SendMessage(message);
    }

    private void HandleStopMacroExecution(string[] args, string senderName) {
        Plugin.IpcProvider.StopMacroExecution();
    }

    private void HandleBroadcastCommandExecution(string[] args, string senderName) {
        if (args.Length < 1) {
            DalamudApi.ChatGui.PrintError($"Invalid command arguments expected 1 <command>");
            return;
        }

        var textCommand = args[0];
        Plugin.MacroHandler.EnqueueMacroActions("#mopbr-inline-macro", actions: [textCommand], Plugin.Config.DelayBetweenActions);
    }

    private void HandleBroadcastNotMeCommandExecution(string[] args, string senderName) {
        if (args.Length < 1) {
            DalamudApi.ChatGui.PrintError($"Invalid command arguments expected 1 <command>");
            return;
        }

        var localPlayerName = DalamudApi.ClientState.LocalPlayer?.Name.ToString();
        if (string.Equals(localPlayerName, senderName, StringComparison.OrdinalIgnoreCase)) return;

        var textCommand = args[0];
        Plugin.MacroHandler.EnqueueMacroActions("#mopbrn-inline-macro", actions: [textCommand], Plugin.Config.DelayBetweenActions);
    }

    private void HandleBroadcastCharacterCommandExecution(string[] args, string senderName) {
        if (args.Length < 2) {
            DalamudApi.ChatGui.PrintError($"Invalid command arguments expected 2 \"Character Name\" <command>");
            return;
        }

        var characterName = args[0];
        var textCommand = args[1];
        var localPlayerName = DalamudApi.ClientState.LocalPlayer?.Name.ToString();
        if (!string.Equals(localPlayerName, characterName, StringComparison.OrdinalIgnoreCase)) return;

        Plugin.MacroHandler.EnqueueMacroActions("#mopbrc-inline-macro", actions: [textCommand], Plugin.Config.DelayBetweenActions);
    }
}

