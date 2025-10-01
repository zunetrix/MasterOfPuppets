using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;

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

    private readonly Dictionary<string, Action<string[]>> CommandHandlers;

    public ChatWatcher(Plugin plugin) {
        Plugin = plugin;
        CommandHandlers = new(StringComparer.OrdinalIgnoreCase) {
            ["moprun"] = HandleRunMacro,
            ["mopstop"] = HandleStopMacroExecution
        };

        DalamudApi.ChatGui.ChatMessage += OnChatMessage;
    }

    public void Dispose() {
        DalamudApi.ChatGui.ChatMessage -= OnChatMessage;
    }

    private List<string> ParseChatArgs(string args) {
        var list = new List<string>();

        if (string.IsNullOrWhiteSpace(args))
            return list;

        // preserve args with double quotes as one argument
        var matches = Regex.Matches(args, @"[\""].+?[\""]|[^ ]+");
        foreach (Match match in matches) {
            list.Add(match.Value);
        }

        // inline execution
        if (list.Count > 1 && list[1].StartsWith("/")) {
            string combined = string.Join(" ", list.Skip(1));
            return new List<string> { list[0], combined };
        }

        // normal macro name
        for (int i = 1; i < list.Count; i++) {
            if (list[i].StartsWith("\"") && list[i].EndsWith("\"")) {
                list[i] = list[i].Substring(1, list[i].Length - 2);
            }
        }

        return list;
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

        var parsedArgs = ParseChatArgs(messageString);
        if (!parsedArgs.Any()) return;

        string command = parsedArgs[0].ToLower();
        string[] args = parsedArgs.Skip(1).ToArray();

        DalamudApi.PluginLog.Debug($"OnChatMessage: [{command}]: {string.Join("|", args)}");

        if (CommandHandlers.TryGetValue(command, out var action)) {
            action.Invoke(args);
            // prevent show chat text
            // isHandled = true;
        }
    }

    public void SendChatRunMacro(string macroName) {
        var message = $"/p moprun {macroName}";
        Chat.SendMessage(message);
    }

    private void HandleRunMacro(string[] args) {
        if (args.Length < 1) return;

        // inline execution direct command
        if (args[0].StartsWith("/", StringComparison.OrdinalIgnoreCase)) {
            // string[] tokens = { "[1]", "[2]", "[3]", "[4]", "[5]", "[6]", "[7]", "[8]", "[t]", "[me]", "[tt]" };
            // replace to the original game tokens that canot be sent via chat <me> will be translated to the char name
            args[0] = Regex.Replace(
                args[0],
                @"\[(1|2|3|4|5|6|7|8|t|me|tt)\]",
                m => $"<{m.Groups[1].Value}>",
                RegexOptions.IgnoreCase
            );

            Plugin.MacroHandler.EnqueueMacroActions("mop-inline-macro", actions: args, Plugin.Config.DelayBetweenActions);
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

    private void HandleStopMacroExecution(string[] args) {
        Plugin.IpcProvider.StopMacroExecution();
    }
}

