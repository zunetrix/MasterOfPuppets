using System;
using System.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;

using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Interface.ImGuiNotification;

using MasterOfPuppets.Ipc;

namespace MasterOfPuppets;

internal class ChatWatcher : IDisposable
{
    private Plugin Plugin { get; }

    public readonly HashSet<XivChatType> AllowedChatTypes = new()
    {
        // XivChatType.Say,
        XivChatType.Party,
        XivChatType.CrossParty,
        XivChatType.FreeCompany,
        XivChatType.Alliance,
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

    public ChatWatcher(Plugin plugin)
    {
        Plugin = plugin;
        CommandHandlers = new(StringComparer.OrdinalIgnoreCase)
        {
            ["moprun"] = HandleRunMacro,
            ["mopstop"] = HandleStopMacroExecution
        };

        DalamudApi.ChatGui.ChatMessage += OnChatMessage;
    }

    public void Dispose()
    {
        DalamudApi.ChatGui.ChatMessage -= OnChatMessage;
    }

    private List<string> ParseArgs(string args)
    {
        var matches = Regex.Matches(args.ToLowerInvariant(), @"[\""].+?[\""]|[^ ]+");
        var list = new List<string>();

        foreach (Match match in matches)
        {
            var value = match.Value;

            if (value.StartsWith("\"") && value.EndsWith("\""))
            {
                value = value.Substring(1, value.Length - 2);
            }

            list.Add(value.ToLower());
        }

        return list;
    }

    private void OnChatMessage(XivChatType type, int timestamp, ref SeString sender, ref SeString message, ref bool isHandled)
    {
        if (!Plugin.Config.UseChatSync) return;
        if (!AllowedChatTypes.Contains(type)
            || !Plugin.Config.ListenedChatTypes.Contains(type)
            || !Plugin.Config.ChatCommandSenderWhitelist.Contains(sender.ToString()))
        {
            return;
        }

        if (isHandled) return;

        var parsedArgs = ParseArgs(message.ToString());
        if (!parsedArgs.Any()) return;

        string command = parsedArgs[0].ToLower();
        string[] args = parsedArgs.Skip(1).ToArray();

        DalamudApi.PluginLog.Debug($"OnChatMessage: [{command}]: {string.Join(" | ", args)}");

        if (CommandHandlers.TryGetValue(command, out var action))
        {
            action.Invoke(args);
            // prevent show chat text
            // isHandled = true;
        }
    }

    public void SendChatRunMacro(string macroName)
    {
        var message = $"/p moprun {macroName}";
        Chat.SendMessage(message);
    }

    private void HandleRunMacro(string[] args)
    {
        if (args.Length < 1) return;

        int macroIndexByName = Plugin.Config.Macros.FindIndex(m => string.Equals(m.Name, args[0], StringComparison.OrdinalIgnoreCase));

        if (!int.TryParse(args[0], out var macroIndexArg) && macroIndexByName == -1)
        {
            DalamudApi.ShowNotification($"Invalid arguments to run macro", NotificationType.Error, 5000);
            return;
        }

        // user input 1 index based
        int macroIndex = macroIndexByName != -1 ? macroIndexByName : macroIndexArg - 1;
        var isValidMacroIndex = Plugin.Config.Macros.IndexExists(macroIndex);
        if (!isValidMacroIndex) return;

        var macro = Plugin.Config.Macros[macroIndex];
        var playerCid = DalamudApi.ClientState.LocalContentId;

        var playerActions = macro.Commands?
            .FirstOrDefault(c => c.Cids.Contains(playerCid))?.Actions;

        if (string.IsNullOrWhiteSpace(playerActions)) return;

        string[] actions = playerActions.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries);
        MacroQueueExecutor.EnqueueMacroActions(actions, Plugin.Config.DelayBetweenActions);
    }

    public void SendChatStopMacroExecution()
    {
        var message = $"/p mopstop";
        Chat.SendMessage(message);
    }

    private void HandleStopMacroExecution(string[] args)
    {
        Plugin.IpcProvider.StopMacroExecution();
    }
}

