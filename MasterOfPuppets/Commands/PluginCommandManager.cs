using System;
using System.Globalization;
using System.Linq;
using System.Numerics;

using Dalamud.Game.Command;
using Dalamud.Interface.ImGuiNotification;

using MasterOfPuppets.Util;

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
                /mop queue -> toggle queue window
                /mop emote -> toggle emote window
                /mop fashion -> toggle fashion window
                /mop facewear -> toggle facewear window
                /mop mount -> toggle mount window
                /mop minion -> shtoggleow minion window
                /mop item -> toggle item window
                /mop targetmytarget -> make all clients target your current target
                /mop targetclear - > clear all targets
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

        DalamudApi.CommandManager.AddHandler("/mopbrc", new CommandInfo(OnBroadcastCharacterCommand) {
            HelpMessage = """
            Broadcast a command to a specific character
            Example:
                /mopbrc "Character Name" /clap
                /mopbrc "Character Name" /s hello
                /mopbrc "Character Name" /moptarget "Character Name2"
            """,
        });
    }

    public void Dispose() {
        DalamudApi.CommandManager.RemoveHandler("/mop");
        DalamudApi.CommandManager.RemoveHandler("/mopbr");
        DalamudApi.CommandManager.RemoveHandler("/mopbrn");
        DalamudApi.CommandManager.RemoveHandler("/mopbrc");
    }

    private void OnMainCommand(string command, string arguments) {
        var parsedArgs = ArgumentParser.ParseCommandArgs(arguments);
        // DalamudApi.PluginLog.Warning($"command: [{command}] {string.Join('|', parsedArgs)}");

        if (parsedArgs.Any()) {
            var subcommand = parsedArgs[0];
            switch (subcommand) {
                case "run": {
                        if (parsedArgs.Count <= 1) {
                            DalamudApi.ShowNotification($"Invalid arguments to run macro", NotificationType.Error, 5000);
                            return;
                        }

                        var macroNameOrNumber = parsedArgs[1];
                        int macroIndex = Plugin.MacroManager.FindMacroIndex(macroNameOrNumber);
                        Plugin.IpcProvider.RunMacro(macroIndex);
                    }
                    break;

                case "stop":
                    Plugin.IpcProvider.StopMacroExecution();
                    break;

                case "targetmytarget":
                    Plugin.IpcProvider.ExecuteTargetMyTarget();
                    break;

                case "targetclear":
                    Plugin.IpcProvider.ExecuteTargetClear();
                    break;
                case "queue":
                    Plugin.Ui.MacroQueueWindow.Toggle();
                    break;
                case "emote":
                    Plugin.Ui.EmotesWindow.Toggle();
                    break;
                case "fashion":
                    Plugin.Ui.FashionAccessoriesWindow.Toggle();
                    break;
                case "facewear":
                    Plugin.Ui.FacewearWindow.Toggle();
                    break;
                case "mount":
                    Plugin.Ui.MountWindow.Toggle();
                    break;
                case "minion":
                    Plugin.Ui.MinionWindow.Toggle();
                    break;
                case "item":
                    Plugin.Ui.ItemWindow.Toggle();
                    break;
                case "gearset":
                    Plugin.Ui.GearSetWindow.Toggle();
                    break;
                case "move": {
                        if (parsedArgs.Count < 2) {
                            DalamudApi.ShowNotification($"Invalid arguments to move", NotificationType.Error, 5000);
                            return;
                        }

                        var argParts = parsedArgs[1].Split(" ");
                        if (argParts.Length != 3) {
                            DalamudApi.PluginLog.Debug($"Invalid coord amount expected x y z {parsedArgs[1]}");
                            return;
                        }

                        if (!float.TryParse(argParts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out float x)
                        || !float.TryParse(argParts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float y)
                        || !float.TryParse(argParts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out float z)) {
                            DalamudApi.PluginLog.Warning($"[mopmove] invalid argument float parse: \"{parsedArgs[1]}\"");
                            return;
                        }

                        var offsetXYZ = new Vector3(x, y, z);
                        Plugin.MovementManager.MoveToPosition(offsetXYZ);
                    }
                    break;
                case "stopmove": {
                        Plugin.IpcProvider.StopMovement();
                    }
                    break;
                case "movetotarget": {
                        Plugin.MovementManager.MoveToTargetPosition();
                    }
                    break;
                case "movetocharacter": {
                        if (parsedArgs.Count <= 1) {
                            DalamudApi.ShowNotification($"Invalid arguments expected character name", NotificationType.Error, 5000);
                            return;
                        }

                        var characterName = parsedArgs[1];
                        Plugin.MovementManager.MoveToObject(characterName);
                    }
                    break;
                case "movetomytarget":
                    Plugin.IpcProvider.ExecuteMoveToMyTarget();
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

    private void OnBroadcastCharacterCommand(string command, string arguments) {
        var parsedArgs = ArgumentParser.ParseCommandArgs(arguments);
        if (parsedArgs.Count >= 2) {
            string characterName = parsedArgs[0];
            string textCommand = parsedArgs[1];
            Plugin.IpcProvider.EnqueueCharacterMacroActions(textCommand, characterName);
        }
    }
}
