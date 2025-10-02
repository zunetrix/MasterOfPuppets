using System;
using System.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;

using Dalamud.Game.Command;
using Dalamud.Interface.ImGuiNotification;

namespace MasterOfPuppets;

public class PluginCommandManager : IDisposable {
    private Plugin Plugin { get; }

    public PluginCommandManager(Plugin plugin) {
        Plugin = plugin;

        DalamudApi.CommandManager.AddHandler("/mop", new CommandInfo(OnMainCommand) {
            HelpMessage = """
            Subcommands:
                /mop -> show / hide UI
                /mop run "Macro name" -> execute macro
                /mop stop -> stop macro execution
                /mop queue -> show queue window
                /mop targetmytarget
                /mop targetclear
            """,
        });

        DalamudApi.CommandManager.AddHandler("/mopbr", new CommandInfo(OnBroadcastCommand) {
            HelpMessage = """
            Broadcast a command to all local clients
            Example:
                /mopbr /clap
            """,
        });

        DalamudApi.CommandManager.AddHandler("/mopbrn", new CommandInfo(OnBroadcastNotMeCommand) {
            HelpMessage = """
            Broadcast a command to local clients except yourself
            Example:
                /mopbrn /clap
            """,
        });
    }

    public void Dispose() {
        DalamudApi.CommandManager.RemoveHandler("/mop");
        DalamudApi.CommandManager.RemoveHandler("/mopbr");
        DalamudApi.CommandManager.RemoveHandler("/mopbrn");
    }

    private static List<string> ParseArgs(string args) {
        var matches = Regex.Matches(args.ToLowerInvariant(), @"[\""].+?[\""]|[^ ]+");
        var list = new List<string>();

        foreach (Match match in matches) {
            var value = match.Value;

            if (value.StartsWith("\"") && value.EndsWith("\"")) {
                value = value.Substring(1, value.Length - 2);
            }

            list.Add(value);
        }

        return list;
    }

    private void OnMainCommand(string command, string arguments) {
        var args = ParseArgs(arguments);
        // DalamudApi.PluginLog.Debug($"command: {command}: {string.Join('|', args)}");

        if (args.Any()) {
            var subcommand = args[0];
            switch (subcommand) {
                case "run": {
                        if (args.Count <= 1) {
                            DalamudApi.ShowNotification($"Invalid arguments to run macro", NotificationType.Error, 5000);
                            return;
                        }

                        var macroNameOrNumber = args[1];
                        int macroIndex = Plugin.MacroManager.FindMacroIndex(macroNameOrNumber);
                        Plugin.IpcProvider.RunMacro(macroIndex);
                    }
                    break;

                case "stop":
                    Plugin.IpcProvider.StopMacroExecution();
                    break;

                case "queue":
                    Plugin.Ui.MacroQueueWindow.Toggle();
                    break;

                case "targetmytarget":
                    Plugin.IpcProvider.ExecuteTargetMyTarget();
                    break;

                case "targetclear":
                    Plugin.IpcProvider.ExecuteTargetClear();
                    break;
                default:
                    DalamudApi.ChatGui.PrintError($"Unrecognized subcommand: '{subcommand}'");
                    return;
                    // case "objectquantity":
                    //     {
                    //         if (args.Count <= 1)
                    //         {
                    //             DalamudApi.ShowNotification($"Invalid arguments to setobjectquantity", NotificationType.Error, 5000);
                    //             return;
                    //         }

                    //         if (!Enum.TryParse<SettingsDisplayObjectLimitType>(args[1], ignoreCase: true, out var displayObjectLimitType)
                    //             || !Enum.IsDefined(typeof(SettingsDisplayObjectLimitType), displayObjectLimitType))
                    //         {
                    //             DalamudApi.PluginLog.Warning($"Invalid object quantity value (0-5): {displayObjectLimitType}");
                    //             return;
                    //         }

                    //         IpcProvider.SetGameSettingsObjectQuantity(displayObjectLimitType);
                    //     }
                    //     break;
                    // try {

                    // } catch (ArgumentException ex) {
                    //     DalamudApi.ChatGui.PrintError(ex.Message);
                    // }
            }
        } else {
            // no args toggle plugin window
            Plugin.Ui.MainWindow.Toggle();
        }
    }

    private void OnBroadcastCommand(string command, string arguments) {
        if (arguments.Any()) {
            Plugin.IpcProvider.EnqueueMacroActions(arguments, includeSelf: true);
        }
    }
    private void OnBroadcastNotMeCommand(string command, string arguments) {
        if (arguments.Any()) {
            Plugin.IpcProvider.EnqueueMacroActions(arguments, includeSelf: false);
        }
    }
}
