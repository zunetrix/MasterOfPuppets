using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;

using Dalamud.Game.Command;
using Dalamud.Interface.ImGuiNotification;

using MasterOfPuppets.Camera;
using MasterOfPuppets.Formations;
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
    private readonly Dictionary<string, SeasonalEventRunner> _events = new(StringComparer.OrdinalIgnoreCase) {
        ["easter"] = new EasterHatchingTide(),
        ["afkguys"] = new FallGuys(),
    };

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
        foreach (var runner in _events.Values) runner.Dispose();
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
                case "tmt":
                    Plugin.IpcProvider.ExecuteTargetMyTarget();
                    break;
                case "interactwithmytarget":
                case "iwmt":
                    Plugin.IpcProvider.ExecuteInteractWithMyTarget();
                    break;
                case "interactwithtarget":
                case "iwt":
                    Plugin.IpcProvider.ExecuteInteractWithTarget();
                    break;
                case "targetclear":
                case "tc":
                    Plugin.IpcProvider.ExecuteTargetClear();
                    break;
                case "queue":
                    Plugin.Ui.MacroQueueWindow.Toggle();
                    break;
                case "actions":
                    Plugin.Ui.ActionsBroadcastWindow.Toggle();
                    break;
                case "moveinput": {
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
                            DalamudApi.PluginLog.Warning($"[mop move] invalid argument float parse: \"{parsedArgs[1]}\"");
                            return;
                        }

                        var destination = DalamudApi.ObjectTable.LocalPlayer.Position + new Vector3(x, y, z);
                        Plugin.SimpleInputMovement.MoveTo(destination);
                    }
                    break;
                case "stopmoveinput":
                    Plugin.SimpleInputMovement.StopMove();
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
                            DalamudApi.PluginLog.Warning($"[mop move] invalid argument float parse: \"{parsedArgs[1]}\"");
                            return;
                        }

                        Angle? facing = null;
                        if (parsedArgs.Count >= 3 &&
                            float.TryParse(parsedArgs[2], NumberStyles.Float, CultureInfo.InvariantCulture, out float angleDeg))
                            facing = angleDeg.Degrees();

                        Plugin.MovementManager.MoveTo(new Vector3(x, y, z), DalamudApi.ObjectTable.LocalPlayer.Position, facing);
                    }
                    break;
                case "face": {
                        // relative sum to current rotation
                        if (parsedArgs.Count < 2 ||
                            !float.TryParse(parsedArgs[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float faceAngle)) {
                            DalamudApi.ShowNotification("Invalid arguments. Expected angle in degrees", NotificationType.Error, 5000);
                            return;
                        }
                        var player = DalamudApi.ObjectTable.LocalPlayer;
                        if (player == null) return;
                        // Subtract because the Angle struct increases CCW while user-facing degrees are CW
                        var target = (player.Rotation.Radians() - faceAngle.Degrees()).Normalized();
                        // Plugin.MovementManager.FaceDirection(target);
                        GameFunctions.FaceDirectionDeferred(target);
                    }
                    break;

                case "faceabs": {
                        // absolute
                        if (parsedArgs.Count < 2 ||
                            !float.TryParse(parsedArgs[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float faceAngle)) {
                            DalamudApi.ShowNotification("Invalid arguments. Expected angle in degrees", NotificationType.Error, 5000);
                            return;
                        }
                        // (180f - angle)  (north=0, cw)
                        // game convention (north=0, ccw)
                        var target = (180f - faceAngle).Degrees().Normalized();
                        // Plugin.MovementManager.FaceDirection(target);
                        GameFunctions.FaceDirectionDeferred(target);
                    }
                    break;
                case "stopmove":
                    Plugin.IpcProvider.StopMovement();
                    break;
                case "follow":
                    Plugin.IpcProvider.Follow(DalamudApi.PlayerState.EntityId);
                    break;
                case "stopfollow":
                    Plugin.IpcProvider.StopFollow();
                    break;
                case "movetotarget":
                case "mtt":
                    Plugin.MovementManager.MoveToTarget();
                    break;
                case "stackonme":
                case "som":
                    Plugin.IpcProvider.ExecuteStackOnMe();
                    break;
                case "movetocharacter":
                case "mtc": {
                        if (parsedArgs.Count <= 1) {
                            DalamudApi.ShowNotification($"Invalid arguments expected character name", NotificationType.Error, 5000);
                            return;
                        }
                        Plugin.MovementManager.MoveTo(parsedArgs[1]);
                    }
                    break;
                case "movetomytarget":
                case "mtmt":
                    Plugin.IpcProvider.ExecuteMoveToMyTarget();
                    break;
                case "enablewalk":
                    MovementManager.SetWalking(true);
                    break;
                case "disablewalk":
                    MovementManager.SetWalking(false);
                    break;
                case "togglewalk":
                    MovementManager.ToggleWalking();
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
                case "swapgearsets": {
                        if (parsedArgs.Count < 2) {
                            DalamudApi.ShowNotification(
                                "Invalid arguments. Expected gearset numbers (e.g. 1,2)",
                                NotificationType.Error, 5000);
                            return;
                        }

                        var parts = parsedArgs[1].Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

                        if (parts.Length != 2) {
                            DalamudApi.ShowNotification(
                                "You must provide exactly 2 gearset numbers (e.g. 1,2)",
                                NotificationType.Error, 5000);
                            return;
                        }

                        if (!int.TryParse(parts[0], out var gearset1) ||
                            !int.TryParse(parts[1], out var gearset2) ||
                            gearset1 is < 1 or > 100 ||
                            gearset2 is < 1 or > 100) {
                            DalamudApi.ShowNotification(
                                "Invalid gearset numbers. Use values between 1 and 100",
                                NotificationType.Error, 5000);
                            return;
                        }

                        // zero-based
                        GearsetManager.SwapGearsets(Plugin, gearset1 - 1, gearset2 - 1);
                    }
                    break;
                case "invite": {
                        if (parsedArgs.Count == 2) {
                            // with characterFullname arg name@world
                            PartyManager.Invite(parsedArgs[1]);
                            return;
                        }
                        Plugin.IpcProvider.RequestInviteAllToParty();
                    }
                    break;
                case "getleader":
                    Plugin.IpcProvider.RequestPartyLeader();
                    break;
                case "disband":
                    Plugin.IpcProvider.RequestDisbandParty();
                    break;
                case "enterhouse":
                    Plugin.IpcProvider.ExecuteEnterHouse();
                    break;
                case "exithouse":
                    Plugin.IpcProvider.ExecuteExitHouse();
                    break;
                case "estate":
                    if (parsedArgs.Count < 3) {
                        DalamudApi.ShowNotification("Usage: estate \"Friend Name\" <fc|pe|ap>", NotificationType.Error, 5000);
                        return;
                    }
                    EstateTeleportManager.TeleportToEstate(parsedArgs[1], parsedArgs[2]);
                    break;
                case "movefrontdoor":
                    Plugin.IpcProvider.ExecuteMoveToFrontDoor();
                    break;
                case "ward": {
                        if (parsedArgs.Count < 2 ||
                            !int.TryParse(parsedArgs[1], out var wardNumber) ||
                            wardNumber is < 1 or > 30) {
                            DalamudApi.ShowNotification("Invalid arguments. Expected ward number (1-30)", NotificationType.Error, 5000);
                            return;
                        }
                        Plugin.IpcProvider.ExecuteTeleportToWard(wardNumber);
                    }
                    break;
                case "world": {
                        if (parsedArgs.Count < 2 || string.IsNullOrWhiteSpace(parsedArgs[1])) {
                            DalamudApi.ShowNotification("Invalid arguments. Expected world name", NotificationType.Error, 5000);
                            return;
                        }
                        Plugin.IpcProvider.ExecuteTravelToWorld(parsedArgs[1]);
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
                        GameSettingsManager.SetDisplayObjectLimit(displayObjectLimitType);
                    }
                    break;
                case "sound": {
                        if (parsedArgs.Count < 2) {
                            DalamudApi.ShowNotification("Invalid arguments. Expected \"on|off\"", NotificationType.Error, 5000);
                            return;
                        }

                        if (parsedArgs[1].Equals("on", StringComparison.OrdinalIgnoreCase))
                            GameSettingsManager.SetSoundMaster(0);
                        else if (parsedArgs[1].Equals("off", StringComparison.OrdinalIgnoreCase))
                            GameSettingsManager.SetSoundMaster(1);
                    }
                    break;
                case "formation": {
                        if (parsedArgs.Count < 2) {
                            Plugin.Ui.FormationWindow.Toggle();
                            return;
                        }
                        FormationExecutionMode? modeOverride = null;
                        if (parsedArgs.Count >= 3 && TryParseFormationExecutionMode(parsedArgs[2], out var mode))
                            modeOverride = mode;
                        Plugin.IpcProvider.ExecuteFormation(parsedArgs[1], modeOverride);
                    }
                    break;
                case "layout": {
                        if (parsedArgs.Count < 2) {
                            Plugin.Ui.WindowLayoutWindow.Toggle();
                            return;
                        }
                        Plugin.GameWindowManager.ApplyWindowLayout(parsedArgs[1]);
                    }
                    break;
                case "camhack":
                case "ch":
                    if (parsedArgs.Count < 2) {
                        DalamudApi.ShowNotification("Invalid arguments. Expected \"on|off\"", NotificationType.Error, 5000);
                        return;
                    }
                    if (parsedArgs[1].Equals("on", StringComparison.OrdinalIgnoreCase))
                        GameCameraManager.EnableCamHighHeight();
                    else if (parsedArgs[1].Equals("off", StringComparison.OrdinalIgnoreCase))
                        GameCameraManager.Disable();
                    break;
                case "renderhack":
                case "rh":
                    if (parsedArgs.Count < 2) {
                        DalamudApi.ShowNotification("Invalid arguments. Expected \"on|off|toggle\"", NotificationType.Error, 5000);
                        return;
                    }
                    if (parsedArgs[1].Equals("on", StringComparison.OrdinalIgnoreCase))
                        Plugin.GameRenderManager.DisableRendering(true);
                    else if (parsedArgs[1].Equals("off", StringComparison.OrdinalIgnoreCase))
                        Plugin.GameRenderManager.DisableRendering(false);
                    else if (parsedArgs[1].Equals("toggle", StringComparison.OrdinalIgnoreCase))
                        Plugin.GameRenderManager.DisableRendering(!Plugin.GameRenderManager.Enabled);
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
                case "logout":
                    GameExitManager.Logout();
                    break;
                case "shutdown":
                    GameExitManager.Shutdown();
                    break;
                case "addon":
                    GameDialogManager.ShowMoogleCollection();
                    break;
                case "settingsprofile": {
                        if (parsedArgs.Count < 2) {
                            DalamudApi.ChatGui.PrintError("Usage: /mop settingsprofile \"profile name\"");
                            return;
                        }
                        Plugin.IpcProvider.BroadcastApplyGameSettingsProfile(parsedArgs[1]);
                    }
                    break;
                case "settings":
                    Plugin.Ui.SettingsWindow.Toggle();
                    break;
                case "ad":
                    GameFunctions.AbandonDuty();
                    break;
                case "event":
                    if (parsedArgs.Count < 2) {
                        DalamudApi.ChatGui.PrintError("Usage: event <name> | event stop");
                        return;
                    }
                    if (parsedArgs[1].Equals("stop", StringComparison.OrdinalIgnoreCase)) {
                        foreach (var r in _events.Values) r.Stop();
                    } else {
                        if (!_events.TryGetValue(parsedArgs[1], out var runner)) {
                            DalamudApi.ChatGui.PrintError($"Unknown event: '{parsedArgs[1]}'");
                            return;
                        }
                        var active = _events.FirstOrDefault(e => e.Value.IsRunning);
                        if (active.Value != null) {
                            DalamudApi.ChatGui.PrintError(
                                active.Key.Equals(parsedArgs[1], StringComparison.OrdinalIgnoreCase)
                                    ? $"Event '{active.Key}' is already running."
                                    : $"Cannot start '{parsedArgs[1]}': stop '{active.Key}' first.");
                            return;
                        }
                        runner.Start(Plugin);
                    }
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

    private static bool TryParseFormationExecutionMode(string input, out FormationExecutionMode mode) {
        mode = FormationExecutionMode.LeaderOrigin;
        input = input.Trim().TrimStart('-');

        if (input.Equals("origin", StringComparison.OrdinalIgnoreCase) ||
            input.Equals("leader", StringComparison.OrdinalIgnoreCase) ||
            input.Equals("leaderorigin", StringComparison.OrdinalIgnoreCase)) {
            mode = FormationExecutionMode.LeaderOrigin;
            return true;
        }

        if (input.Equals("relative", StringComparison.OrdinalIgnoreCase) ||
            input.Equals("btb", StringComparison.OrdinalIgnoreCase)) {
            mode = FormationExecutionMode.RelativeToLocalAssignedPoint;
            return true;
        }

        if (input.Equals("sequence", StringComparison.OrdinalIgnoreCase) ||
            input.Equals("clientorder", StringComparison.OrdinalIgnoreCase)) {
            mode = FormationExecutionMode.ClientOrder;
            return true;
        }

        return Enum.TryParse(input, ignoreCase: true, out mode);
    }
}
