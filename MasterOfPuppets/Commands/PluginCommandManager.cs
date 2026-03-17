using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;

using Dalamud.Game.Command;
using Dalamud.Interface.ImGuiNotification;

using MasterOfPuppets.Movement;
using MasterOfPuppets.Util;

namespace MasterOfPuppets;

public class PluginCommandManager : IDisposable {
    private Plugin Plugin { get; }

    public record MopCommandDef(
        string Key,
        string DefaultCommand,
        string[] DefaultAliases,
        string HelpMessage
    );

    private static readonly MopCommandDef[] CommandDefs = [
        new("mop",    "/mop",    ["/mop"], "Show/hide UI. Subcommands: run, stop, queue, move, ..."),
        new("mopbr",  "/mopbr",  ["/br"],  "Broadcast a command to all local clients"),
        new("mopbrn", "/mopbrn", ["/brn"], "Broadcast a command to all local clients except yourself"),
        new("mopbrc", "/mopbrc", ["/brc"], "Broadcast a command to a specific character"),
    ];

    public static IReadOnlyList<MopCommandDef> Definitions => CommandDefs;

    // key → all currently registered command names (main + aliases)
    private readonly Dictionary<string, List<string>> _registered = new();

    public PluginCommandManager(Plugin plugin) {
        Plugin = plugin;
        RegisterAll();
    }

    public void RefreshCustomCommands() {
        UnregisterAll();
        RegisterAll();
    }

    private void RegisterAll() {
        foreach (var def in CommandDefs) {
            var registered = new List<string>();
            TryRegister(def.DefaultCommand, GetHandlerForKey(def.Key), def.HelpMessage, showInHelp: true, registered);

            Plugin.Config.EnabledCommandAliases.TryGetValue(def.Key, out var enabled);

            foreach (var alias in def.DefaultAliases) {
                if (enabled == null || !enabled.Contains(alias, StringComparer.OrdinalIgnoreCase)) continue;
                var effectiveAlias = alias;
                if (Plugin.Config.CustomAliasNames.TryGetValue(def.Key, out var aliasNames) &&
                    aliasNames.TryGetValue(alias, out var custom) &&
                    !string.IsNullOrWhiteSpace(custom))
                    effectiveAlias = custom;
                if (effectiveAlias.Equals(def.DefaultCommand, StringComparison.OrdinalIgnoreCase)) continue;
                TryRegister(effectiveAlias, GetHandlerForKey(def.Key), $"Alias for {def.DefaultCommand}", showInHelp: false, registered);
            }

            _registered[def.Key] = registered;
        }
    }

    private void TryRegister(string cmd, IReadOnlyCommandInfo.HandlerDelegate handler, string helpMessage, bool showInHelp, List<string> registered) {
        if (DalamudApi.CommandManager.Commands.ContainsKey(cmd)) {
            DalamudApi.PluginLog.Warning($"[PluginCommandManager] '{cmd}' is already registered.");
            return;
        }
        try {
            DalamudApi.CommandManager.AddHandler(cmd, new CommandInfo(handler) {
                HelpMessage = helpMessage,
                ShowInHelp = showInHelp,
            });
            registered.Add(cmd);
        } catch (Exception ex) {
            DalamudApi.PluginLog.Warning(ex, $"[PluginCommandManager] Failed to register '{cmd}'.");
        }
    }

    private void UnregisterAll() {
        foreach (var (_, cmds) in _registered)
            foreach (var cmd in cmds)
                DalamudApi.CommandManager.RemoveHandler(cmd);
        _registered.Clear();
    }

    private IReadOnlyCommandInfo.HandlerDelegate GetHandlerForKey(string key) => key switch {
        "mopbr" => OnBroadcastCommand,
        "mopbrn" => OnBroadcastNotMeCommand,
        "mopbrc" => OnBroadcastCharacterCommand,
        _ => OnMainCommand,
    };

    public void Dispose() {
        UnregisterAll();
    }

    private void OnMainCommand(string command, string arguments) {
        var parsedArgs = ArgumentParser.ParseCommandArgs(arguments);

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

                        var inlineVars = parsedArgs.Count > 2
                            ? ArgumentParser.ParseInlineVars(parsedArgs[2])
                            : null;

                        Plugin.IpcProvider.RunMacro(macroIndex, inlineVars);
                    }
                    break;

                case "stop":
                    Plugin.MacroHandler.StopMacroQueueExecution();
                    break;
                case "targetmytarget":
                    Plugin.IpcProvider.ExecuteTargetMyTarget();
                    break;
                case "interactwithmytarget":
                    Plugin.IpcProvider.ExecuteInteractWithMyTarget();
                    break;
                case "interactwithtarget":
                    Plugin.IpcProvider.ExecuteInteractWithTarget();
                    break;
                case "targetclear":
                    Plugin.IpcProvider.ExecuteTargetClear();
                    break;
                case "queue":
                    Plugin.Ui.MacroQueueWindow.Toggle();
                    break;
                case "actions":
                    Plugin.Ui.ActionsBroadcastWindow.Toggle();
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

                        Angle? facing = null;
                        if (parsedArgs.Count >= 3 &&
                            float.TryParse(parsedArgs[2], NumberStyles.Float, CultureInfo.InvariantCulture, out float angleDeg))
                            facing = angleDeg.Degrees();

                        Plugin.MovementManager.MoveToPosition(new Vector3(x, y, z), facing);
                    }
                    break;
                case "face": {
                        if (parsedArgs.Count < 2 ||
                            !float.TryParse(parsedArgs[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float faceAngle)) {
                            DalamudApi.ShowNotification("Invalid arguments. Expected angle in degrees", NotificationType.Error, 5000);
                            return;
                        }
                        var player = DalamudApi.ObjectTable.LocalPlayer;
                        if (player == null) return;
                        var target = (player.Rotation.Radians() - faceAngle.Degrees()).Normalized();
                        Plugin.MovementManager.FaceDirection(target);
                    }
                    break;
                case "faceabs": {
                        if (parsedArgs.Count < 2 ||
                            !float.TryParse(parsedArgs[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float faceAngle)) {
                            DalamudApi.ShowNotification("Invalid arguments. Expected angle in degrees", NotificationType.Error, 5000);
                            return;
                        }
                        var target = (180f - faceAngle).Degrees().Normalized();
                        Plugin.MovementManager.FaceDirection(target);
                    }
                    break;
                case "stopmove":
                    Plugin.IpcProvider.StopMovement();
                    break;
                case "movetotarget":
                    Plugin.MovementManager.MoveToTargetPosition();
                    break;
                case "stackonme":
                    Plugin.IpcProvider.ExecuteStackOnMe();
                    break;
                case "movetocharacter": {
                        if (parsedArgs.Count <= 1) {
                            DalamudApi.ShowNotification($"Invalid arguments expected character name", NotificationType.Error, 5000);
                            return;
                        }
                        Plugin.MovementManager.MoveToObject(parsedArgs[1]);
                    }
                    break;
                case "movetomytarget":
                    Plugin.IpcProvider.ExecuteMoveToMyTarget();
                    break;
                case "enablewalk":
                    Plugin.MovementManager.SetWalking(true);
                    break;
                case "disablewalk":
                    Plugin.MovementManager.SetWalking(false);
                    break;
                case "togglewalk":
                    Plugin.MovementManager.ToggleWalking();
                    break;
                case "gs": {
                        if (parsedArgs.Count < 2 ||
                            !int.TryParse(parsedArgs[1], out var gearsetIndex) ||
                            gearsetIndex is <= 0 or > 100) {
                            DalamudApi.ShowNotification("Invalid arguments. Expected gearset number (1-100)", NotificationType.Error, 5000);
                            return;
                        }
                        GearsetManager.ChangeGearset(Plugin, gearsetIndex - 1);
                    }
                    break;
                case "movegearsets": {
                        if (parsedArgs.Count < 2) {
                            DalamudApi.ShowNotification("Invalid arguments. Expected gearset numbers (e.g. 1,2,3)", NotificationType.Error, 5000);
                            return;
                        }
                        var indices = new List<int>();
                        foreach (var part in parsedArgs[1].Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)) {
                            if (int.TryParse(part, out int n) && n is >= 1 and <= 100)
                                indices.Add(n - 1);
                        }
                        if (indices.Count == 0) {
                            DalamudApi.ShowNotification("No valid gearset numbers provided (1-100)", NotificationType.Error, 5000);
                            return;
                        }
                        GearsetManager.MoveGearsetsToArmoury(Plugin, indices);
                    }
                    break;
                case "invite": {
                        if (parsedArgs.Count < 2) {
                            DalamudApi.ShowNotification("Invalid arguments. Expected \"Character Name@World\"", NotificationType.Error, 5000);
                            return;
                        }
                        GameFunctions.InviteToParty(parsedArgs[1]);
                    }
                    break;
                case "objectquantity": {
                        if (parsedArgs.Count <= 1) {
                            DalamudApi.ShowNotification($"Invalid arguments to setobjectquantity", NotificationType.Error, 5000);
                            return;
                        }
                        if (!Enum.TryParse<SettingsDisplayObjectLimitType>(parsedArgs[1], ignoreCase: true, out var displayObjectLimitType)
                            || !Enum.IsDefined(typeof(SettingsDisplayObjectLimitType), displayObjectLimitType)) {
                            DalamudApi.PluginLog.Warning($"Invalid object quantity value (0-5): {displayObjectLimitType}");
                            return;
                        }
                        Plugin.IpcProvider.SetGameSettingsObjectQuantity(displayObjectLimitType);
                    }
                    break;
                case "formations":
                    Plugin.Ui.FormationWindow.Toggle();
                    break;
                case "formation": {
                        if (parsedArgs.Count < 2) {
                            DalamudApi.ShowNotification("Invalid arguments. Expected formation name", NotificationType.Error, 5000);
                            return;
                        }
                        Plugin.IpcProvider.ExecuteFormation(parsedArgs[1]);
                    }
                    break;
                case "keybroadcast":
                    if (parsedArgs.Count < 2) {
                        DalamudApi.ShowNotification("Invalid arguments. Expected \"on|off\"", NotificationType.Error, 5000);
                        return;
                    }
                    if (parsedArgs[1].Equals("on", StringComparison.OrdinalIgnoreCase))
                        Plugin.IpcProvider.EnableKeyboardBroadcast();
                    else if (parsedArgs[1].Equals("off", StringComparison.OrdinalIgnoreCase))
                        Plugin.IpcProvider.DisableKeyboardBroadcast();
                    break;
                default:
                    DalamudApi.ChatGui.PrintError($"Unrecognized subcommand: '{subcommand}'");
                    break;
            }
        } else {
            Plugin.Ui.MainWindow.Toggle();
        }
    }

    private void OnBroadcastCommand(string command, string arguments) {
        if (arguments.Any())
            Plugin.IpcProvider.EnqueueMacroActions(arguments, includeSelf: true);
    }

    private void OnBroadcastNotMeCommand(string command, string arguments) {
        if (arguments.Any())
            Plugin.IpcProvider.EnqueueMacroActions(arguments, includeSelf: false);
    }

    private void OnBroadcastCharacterCommand(string command, string arguments) {
        var parsedArgs = ArgumentParser.ParseCommandArgs(arguments);
        if (parsedArgs.Count >= 2)
            Plugin.IpcProvider.EnqueueCharacterMacroActions(parsedArgs[1], parsedArgs[0]);
    }
}
