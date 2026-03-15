using System;
using System.Collections.Generic;
using System.Linq;

using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;

using MasterOfPuppets.Util;

namespace MasterOfPuppets;

internal class ChatWatcher : IDisposable {
    private Plugin Plugin { get; }
    // private bool _isRegistered;

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
        // UpdateRegistration();
    }

    public void Dispose() {
        DalamudApi.ChatGui.ChatMessage -= OnChatMessage;
    }

    // public void UpdateRegistration() {
    //     if (Plugin.Config.UseChatSync && !_isRegistered) {
    //         DalamudApi.ChatGui.ChatMessage += OnChatMessage;
    //         _isRegistered = true;
    //     } else if (!Plugin.Config.UseChatSync && _isRegistered) {
    //         DalamudApi.ChatGui.ChatMessage -= OnChatMessage;
    //         _isRegistered = false;
    //     }
    // }

    // public void Dispose() {
    //     if (_isRegistered) {
    //         DalamudApi.ChatGui.ChatMessage -= OnChatMessage;
    //         _isRegistered = false;
    //     }
    // }

    private void OnChatMessage(XivChatType type, int timestamp, ref SeString sender, ref SeString message, ref bool isHandled) {
        if (!Plugin.Config.UseChatSync) return;
        if (isHandled) return;

        var senderName = SanitizeSenderName(sender.ToString());

        if (!AllowedChatTypes.Contains(type)
            || !Plugin.Config.ListenedChatTypes.Contains(type)
            || (Plugin.Config.UseChatCommandSenderWhitelist && !Plugin.Config.ChatCommandSenderWhitelist.Contains(senderName))
        ) {
            return;
        }

        var parsedArgs = ArgumentParser.ParseChatArgs(message.ToString());
        if (!parsedArgs.Any()) return;

#if DEBUG
        DalamudApi.PluginLog.Debug($"OnChatMessage: [{parsedArgs[0]}]: {string.Join("|", parsedArgs.Skip(1))}");
#endif

        if (CommandHandlers.TryGetValue(parsedArgs[0], out var action)) {
            action.Invoke(parsedArgs.Skip(1).ToArray(), senderName);
            // prevent show chat text
            // isHandled = true;
        }
    }

    private void HandleRunMacro(string[] args, string senderName) {
        if (args.Length < 1) {
            DalamudApi.ChatGui.PrintError($"Invalid command arguments expected 1 <macro name>");
            return;
        }

        var inlineVars = args.Length > 1
            ? ArgumentParser.ParseInlineVars(args[1])
            : null;

        string macroNameOrIndex = args[0];
        int macroIndex = Plugin.MacroManager.FindMacroIndex(macroNameOrIndex);
        var macro = Plugin.MacroManager.GetMacroByIndex(macroIndex);
        var playerActions = macro.GetCidActions(DalamudApi.PlayerState.ContentId, Plugin.Config.CidsGroups, inlineVars);

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
        Plugin.MacroHandler.EnqueueMacroActions("#mopbr-inline-macro", actions: [textCommand], delayBetweenActions: 0);
    }

    private void HandleBroadcastNotMeCommandExecution(string[] args, string senderName) {
        if (args.Length < 1) {
            DalamudApi.ChatGui.PrintError($"Invalid command arguments expected 1 <command>");
            return;
        }

        var localPlayerName = DalamudApi.PlayerState.CharacterName;
        if (string.Equals(localPlayerName, senderName, StringComparison.OrdinalIgnoreCase)) return;

        var textCommand = args[0];
        Plugin.MacroHandler.EnqueueMacroActions("#mopbrn-inline-macro", actions: [textCommand], delayBetweenActions: 0);
    }

    private void HandleBroadcastCharacterCommandExecution(string[] args, string senderName) {
        if (args.Length < 2) {
            DalamudApi.ChatGui.PrintError($"Invalid command arguments expected 2 \"Character Name\" <command>");
            return;
        }

        var characterName = args[0];
        var textCommand = args[1];
        var localPlayerName = $"{DalamudApi.PlayerState.CharacterName}@{DalamudApi.PlayerState.HomeWorld.Value.Name}";
        if (!localPlayerName.Contains(characterName, StringComparison.InvariantCultureIgnoreCase)) return;

        Plugin.MacroHandler.EnqueueMacroActions("#mopbrc-inline-macro", actions: [textCommand], delayBetweenActions: 0);
    }

    private static string SanitizeSenderName(string raw) {
        var i = 0;
        while (i < raw.Length && !char.IsLetter(raw[i])) i++;
        return i > 0 ? raw[i..] : raw;
    }
}

