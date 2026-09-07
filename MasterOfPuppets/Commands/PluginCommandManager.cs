using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;

using Dalamud.Game.Command;
using Dalamud.Interface.ImGuiNotification;

using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.Control;

using MasterOfPuppets.Camera;
using MasterOfPuppets.Extensions;
using MasterOfPuppets.Formations;
using MasterOfPuppets.LuaScripting;
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
        new("mopbrg", "/mopbrg", ["/brg"], "Broadcast a command to a specific group"),
        new("moptargetclear", "/moptargetclear", [], "Clear target on all local clients"),
        new("moppetplace", "/moppetplace", [], "Place summoned pet at an offset relative to an anchor"),
        new("moppetformationplace", "/moppetformationplace", [], "Place summoned pet at a formation point"),
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
        "mopbrg" => OnBroadcastGroupCommand,
        "moptargetclear" => (_, _) => Plugin.IpcProvider.ExecuteTargetClear(),
        "moppetplace" => OnPetPlaceCommand,
        "moppetformationplace" => OnPetFormationPlaceCommand,
        _ => OnMainCommand,
    };

    public void Dispose() {
        UnregisterAll();
    }

    private void OnMainCommand(string command, string arguments) {
        var parsedArgs = ArgumentParser.ParseCommandArgs(arguments);
        if (parsedArgs.Count == 1 && parsedArgs[0].StartsWith("gamemacro ", StringComparison.OrdinalIgnoreCase)) {
            parsedArgs = ArgumentParser.ParseMacroArgs(parsedArgs[0]);
        }
        if (parsedArgs.Count == 1 && parsedArgs[0].StartsWith("lua ", StringComparison.OrdinalIgnoreCase)) {
            parsedArgs = ArgumentParser.ParseMacroArgs(parsedArgs[0]);
        }

        if (parsedArgs.Any()) {
            var subcommand = parsedArgs[0];
            switch (subcommand) {
                case "run": {
                        if (parsedArgs.Count <= 1) {
                            DalamudApi.ChatGui.PrintError("Invalid arguments. Usage: /mop run <index|name>");
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
                case "gamemacro": {
                        if (parsedArgs.Count <= 1) {
                            DalamudApi.ChatGui.PrintError("Invalid arguments. Usage: /mop gamemacro <index|name> [individual|i|shared|share|s]");
                            return;
                        }

                        var scopeArg = parsedArgs.Count > 2 ? parsedArgs[2] : null;
                        if (!GameMacroManager.TryExecute(parsedArgs[1], scopeArg, out var error)) {
                            DalamudApi.ShowNotification(error, NotificationType.Error, 5000);
                        }
                    }
                    break;
                case "stop":
                    Plugin.StopAllMovementLocal();
                    Plugin.MacroHandler.StopMacroQueueExecution();
                    break;
                case "stopmacro":
                    Plugin.MacroHandler.StopMacroQueueExecution();
                    break;
                case "stopmove":
                    Plugin.StopAllMovementLocal();
                    break;
                case "resetemote":
                    ForceResetLocalEmote();
                    break;
                case "lua": {
                        var luaAction = parsedArgs.Count >= 2 ? parsedArgs[1] : "status";
                        switch (luaAction.ToLowerInvariant()) {
                            case "run": {
                                    if (parsedArgs.Count < 3) {
                                        DalamudApi.ChatGui.PrintError(
                                            "Invalid arguments. Usage: /mop lua run \"script name\" [-var=$name=value;...]");
                                        break;
                                    }

                                    var inlineVars = parsedArgs.Count > 3
                                        ? ArgumentParser.ParseInlineVars(parsedArgs[3])
                                        : null;
                                    Plugin.IpcProvider.StartLuaScript(parsedArgs[2], inlineVars);
                                }
                                break;
                            case "sync": {
                                    if (parsedArgs.Count < 3) {
                                        DalamudApi.ChatGui.PrintError(
                                            "Invalid arguments. Usage: /mop lua sync \"script name\" [-var=$name=value;...]");
                                        break;
                                    }

                                    var chatPrefix = Plugin.Config.DefaultChatSyncPrefix?.Trim();
                                    if (string.IsNullOrWhiteSpace(chatPrefix)) {
                                        DalamudApi.ChatGui.PrintError("[MoP] Configure a default Chat Sync prefix before starting synchronized Lua.");
                                        break;
                                    }

                                    var varArg = parsedArgs.Count > 3 ? $" {parsedArgs[3]}" : string.Empty;
                                    Chat.SendMessage($"{chatPrefix} mopluarun \"{parsedArgs[2]}\"{varArg}");
                                }
                                break;
                            case "stop":
                                Plugin.IpcProvider.StopLuaScript(parsedArgs.Count > 2 ? parsedArgs[2] : null);
                                break;
                            case "pause":
                                Plugin.IpcProvider.PauseLuaScript(parsedArgs.Count > 2 ? parsedArgs[2] : null);
                                break;
                            case "resume":
                                Plugin.IpcProvider.ResumeLuaScript(parsedArgs.Count > 2 ? parsedArgs[2] : null);
                                break;
                            case "restart": {
                                    var selector = parsedArgs.Count > 2 ? parsedArgs[2] : null;
                                    if (!Plugin.LuaScriptManager.TryResolveScriptName(selector, out var scriptName)
                                        && (string.IsNullOrWhiteSpace(selector)
                                            || LuaScriptCatalog.Find(Plugin.Config, selector) == null)) {
                                        DalamudApi.ChatGui.PrintError(
                                            string.IsNullOrWhiteSpace(selector)
                                                ? "No Lua run is available to restart."
                                                : $"No Lua run or configured script matches '{selector}'.");
                                        break;
                                    }

                                    Plugin.IpcProvider.StopLuaScript(selector);
                                    Plugin.IpcProvider.StartLuaScript(scriptName);
                                }
                                break;
                            case "status":
                                DalamudApi.ChatGui.Print(
                                    $"Lua: {Plugin.LuaScriptManager.GetStatusText(parsedArgs.Count > 2 ? parsedArgs[2] : null)}");
                                break;
                            default:
                                DalamudApi.ChatGui.PrintError(
                                    "Invalid arguments. Usage: /mop lua <sync \"script\"|run \"script\"|pause [run|script]|resume [run|script]|stop [run|script]|restart [run|script]|status [run|script]>");
                                break;
                        }
                    }
                    break;
                case "reload":
                case "loadconfig":
                case "reloadconfig":
                    Plugin.ReloadConfigFromDisk();
                    break;
                case "setvar":
                case "setvars": {
                        if (parsedArgs.Count < 2) {
                            DalamudApi.ChatGui.PrintError("Invalid arguments. Usage: /mop setvar -var=$name=value[;$other=value]");
                            return;
                        }

                        var variables = ArgumentParser.ParseInlineVars(parsedArgs[1]);
                        if (variables.Count == 0) {
                            DalamudApi.ChatGui.PrintError("No valid variables found. Usage: /mop setvar -var=$name=value[;$other=value]");
                            return;
                        }

                        Plugin.IpcProvider.UpdateMacroVariables(variables);
                    }
                    break;
                case "target":
                    if (parsedArgs.Count <= 1) {
                        DalamudApi.ChatGui.PrintError("Invalid arguments. Usage: /mop target <name>");
                        return;
                    }
                    GameTargetManager.TargetObject(parsedArgs[1]);
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
                case "checkvisible": {
                        var group = Plugin.Config.CidsGroups.FirstOrDefault(g => g.Name.Equals("32 Ordered", StringComparison.OrdinalIgnoreCase));
                        if (group == null) {
                            DalamudApi.ChatGui.PrintError("Group '32 Ordered' not found.");
                            return;
                        }
                        var chars = Plugin.Config.Characters.ToDictionary(c => c.Cid, c => c.Name);
                        var freshPeers = Plugin.IpcProvider.GetFreshPeerCharacterData().Select(p => p.ContentId).ToHashSet();
                        var visible = new List<string>();
                        var missing = new List<string>();
                        foreach (var cid in group.Cids) {
                            chars.TryGetValue(cid, out var name);
                            var shortName = (name ?? cid.ToString()).Split('@')[0];
                            var isVis = LuaParticipantResolver.IsVisible(cid, name);
                            var isPeer = freshPeers.Contains(cid);
                            if (isVis || isPeer)
                                visible.Add($"{shortName}{(isPeer ? "[IPC]" : "[3D]")}");
                            else
                                missing.Add(shortName);
                        }
                        DalamudApi.ChatGui.Print($"[MoP] Visible/Peer ({visible.Count}): {string.Join(", ", visible)}");
                        DalamudApi.ChatGui.Print($"[MoP] Missing ({missing.Count}): {string.Join(", ", missing)}");
                    }
                    break;
                case "macro":
                case "macros":
                    Plugin.Ui.MacroWindow.Toggle();
                    break;
                case "script":
                case "scripts":
                    if (parsedArgs.Count < 2) {
                        Plugin.Ui.LuaScriptsWindow.Toggle();
                    } else {
                        goto case "lua";
                    }
                    break;
                case "queue":
                    Plugin.Ui.MacroQueueWindow.Toggle();
                    break;
                case "actions":
                    Plugin.Ui.ActionsBroadcastWindow.Toggle();
                    break;
                case "item": {
                        if (parsedArgs.Count < 2) {
                            DalamudApi.ChatGui.PrintError("Invalid arguments. Usage: /mop item <id|name>");
                            return;
                        }
                        var itemIdOrName = parsedArgs[1];
                        if (uint.TryParse(itemIdOrName, out uint itemId)) {
                            GameActionManager.UseItem(itemId);
                        } else {
                            GameActionManager.UseItem(itemIdOrName);
                        }
                    }
                    break;
                case "moveinput": {
                        if (parsedArgs.Count < 2) {
                            DalamudApi.ChatGui.PrintError("Invalid arguments. Usage: /mop moveinput x y z");
                            return;
                        }

                        var argParts = parsedArgs[1].Split(" ");
                        if (argParts.Length != 3) {
                            DalamudApi.ChatGui.PrintError($"Invalid arguments. Usage: /mop moveinput x y z | {parsedArgs[1]}");
                            return;
                        }

                        if (!float.TryParse(argParts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out float x)
                        || !float.TryParse(argParts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float y)
                        || !float.TryParse(argParts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out float z)) {
                            DalamudApi.PluginLog.Warning($"[mop moveinput] invalid argument float parse: \"{parsedArgs[1]}\"");
                            return;
                        }

                        var destination = DalamudApi.ObjectTable.LocalPlayer.Position + new Vector3(x, y, z);
                        Plugin.SimpleInputMovement.MoveTo(destination);
                    }
                    break;
                case "move": {
                        if (parsedArgs.Count < 2) {
                            DalamudApi.ChatGui.PrintError("Invalid arguments. Usage: /mop move x y z");
                            return;
                        }

                        var argParts = parsedArgs[1].Split(" ");
                        if (argParts.Length != 3) {
                            DalamudApi.ChatGui.PrintError($"Invalid arguments. Usage: /mop move x y z | {parsedArgs[1]}");
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
                case "petplace":
                    if (parsedArgs.Count < 2) {
                        DalamudApi.ChatGui.PrintError("Invalid arguments. Usage: /mop petplace x y z [anchor]");
                        return;
                    }
                    OnPetPlaceCommand(subcommand, parsedArgs[1]);
                    break;
                case "petformationplace":
                    if (parsedArgs.Count < 2) {
                        DalamudApi.ChatGui.PrintError("Invalid arguments. Usage: /mop petformationplace \"Formation Name\" <point> [anchor]");
                        return;
                    }
                    OnPetFormationPlaceCommand(subcommand, parsedArgs[1]);
                    break;
                case "face": {
                        // relative sum to current rotation
                        if (parsedArgs.Count < 2 ||
                            !float.TryParse(parsedArgs[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float faceAngle)) {
                            DalamudApi.ChatGui.PrintError("Invalid arguments. Usage: /mop face <degrees>");
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
                            DalamudApi.ChatGui.PrintError("Invalid arguments. Usage: /mop faceabs <degrees>");
                            return;
                        }
                        // (180f - angle)  (north=0, cw)
                        // game convention (north=0, ccw)
                        var target = (180f - faceAngle).Degrees().Normalized();
                        // Plugin.MovementManager.FaceDirection(target);
                        GameFunctions.FaceDirectionDeferred(target);
                    }
                    break;
                case "follow":
                    Plugin.IpcProvider.Follow(DalamudApi.PlayerState.EntityId);
                    break;
                case "stopfollow":
                case "unfollow":
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
                            DalamudApi.ChatGui.PrintError("Invalid arguments. Usage: /mop movetocharacter <name>");
                            return;
                        }
                        Plugin.MovementManager.MoveTo(parsedArgs[1]);
                    }
                    break;
                case "movetomytarget":
                case "mtmt":
                    Plugin.IpcProvider.ExecuteMoveToMyTarget();
                    break;
                case "walk":
                    if (parsedArgs.Count < 2) {
                        DalamudApi.ChatGui.PrintError("Invalid arguments. Usage: /mop walk <on|off|toggle>");
                        return;
                    }
                    if (parsedArgs[1].Equals("on", StringComparison.OrdinalIgnoreCase))
                        MovementManager.SetWalking(true);
                    else if (parsedArgs[1].Equals("off", StringComparison.OrdinalIgnoreCase))
                        MovementManager.SetWalking(false);
                    else if (parsedArgs[1].Equals("toggle", StringComparison.OrdinalIgnoreCase))
                        MovementManager.ToggleWalking();
                    break;
                case "gs": {
                        if (parsedArgs.Count < 2 ||
                            !int.TryParse(parsedArgs[1], out var gearsetIndex) ||
                            gearsetIndex is <= 0 or > 100) {
                            DalamudApi.ChatGui.PrintError("Invalid arguments. Usage: /mop gs <1-100>");
                            return;
                        }
                        GearsetManager.ChangeGearset(Plugin, gearsetIndex - 1);
                    }
                    break;
                case "movegearsets": {
                        if (parsedArgs.Count < 2) {
                            DalamudApi.ChatGui.PrintError("Invalid arguments. Usage: /mop movegearsets 1,2,3");
                            return;
                        }
                        var indices = new List<int>();
                        foreach (var part in parsedArgs[1].Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)) {
                            if (int.TryParse(part, out int n) && n is >= 1 and <= 100)
                                indices.Add(n - 1);
                        }
                        if (indices.Count == 0) {
                            DalamudApi.ChatGui.PrintError("Invalid gearset numbers (1-100)");
                            return;
                        }
                        GearsetManager.MoveGearsetsToArmoury(Plugin, indices);
                    }
                    break;
                case "swapgearsets": {
                        if (parsedArgs.Count < 2) {
                            DalamudApi.ChatGui.PrintError("Invalid arguments. Usage: /mop swapgearsets 1,2");
                            return;
                        }

                        var parts = parsedArgs[1].Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

                        if (parts.Length != 2) {
                            DalamudApi.ChatGui.PrintError("Invalid arguments. Usage: /mop swapgearsets 1,2 expected 2 gearset numbers");
                            return;
                        }

                        if (!int.TryParse(parts[0], out var gearset1) || !int.TryParse(parts[1], out var gearset2) ||
                            gearset1 is < 1 or > 100 || gearset2 is < 1 or > 100) {
                            DalamudApi.ChatGui.PrintError("Invalid gearset numbers. Expected values between 1 and 100");
                            return;
                        }

                        // zero-based
                        GearsetManager.SwapGearsets(Plugin, gearset1 - 1, gearset2 - 1);
                    }
                    break;
                case "facewearoff": {
                        GameActionManager.UnequipGlasses();
                    }
                    break;
                case "invite": {
                        if (parsedArgs.Count == 2) {
                            // character full name name@world
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
                        DalamudApi.ChatGui.PrintError("Invalid arguments. Usage: /mop estate \"Friend Name\" <fc|pe|ap>");
                        return;
                    }
                    EstateTeleportManager.TeleportToEstate(parsedArgs[1], parsedArgs[2]);
                    break;
                case "movefrontdoor":
                    Plugin.IpcProvider.ExecuteMoveToFrontDoor();
                    break;
                case "ward":
                case "residentialtravel":
                case "rt": {
                        if (parsedArgs.Count < 2 ||
                            !int.TryParse(parsedArgs[1], out var wardNumber) ||
                            wardNumber is < 1 or > 30) {
                            DalamudApi.ChatGui.PrintError("Invalid arguments. Usage: /mop ward <1-30>");
                            return;
                        }
                        ResidentialTeleportManager.TeleportToWard(wardNumber, Plugin);
                    }
                    break;
                case "world":
                case "worldtravel":
                case "wt": {
                        if (parsedArgs.Count < 2 || string.IsNullOrWhiteSpace(parsedArgs[1])) {
                            DalamudApi.ChatGui.PrintError("Invalid arguments. Usage: /mop world <name>");
                            return;
                        }
                        WorldTravelManager.TravelToWorld(parsedArgs[1], Plugin);
                    }
                    break;
                case "objectquantity": {
                        if (parsedArgs.Count <= 1) {
                            DalamudApi.ChatGui.PrintError("Invalid arguments. Usage: /mop objectquantity <0-5>");
                            return;
                        }
                        if (!Enum.TryParse<SettingsDisplayObjectLimitType>(parsedArgs[1], ignoreCase: true, out var displayObjectLimitType)
                            || !Enum.IsDefined(typeof(SettingsDisplayObjectLimitType), displayObjectLimitType)) {
                            DalamudApi.ChatGui.PrintError($"Invalid arguments. Usage: /mop objectquantity <0-5> {displayObjectLimitType}");
                            return;
                        }
                        GameSettingsManager.SetDisplayObjectLimit(displayObjectLimitType);
                    }
                    break;
                case "sound": {
                        if (parsedArgs.Count < 2) {
                            DalamudApi.ChatGui.PrintError("Invalid arguments. Usage: /mop sound <on|off|toggle>");
                            return;
                        }

                        if (parsedArgs[1].Equals("on", StringComparison.OrdinalIgnoreCase))
                            GameSettingsManager.SetSoundMaster(0);
                        else if (parsedArgs[1].Equals("off", StringComparison.OrdinalIgnoreCase))
                            GameSettingsManager.SetSoundMaster(1);
                        else if (parsedArgs[1].Equals("toggle", StringComparison.OrdinalIgnoreCase))
                            GameSettingsManager.SetSoundMaster(DalamudApi.GameConfig.System.GetUInt("IsSndMaster") == 1u ? 0u : 1u);
                    }
                    break;
                case "formation": {
                        if (!DalamudApi.ClientState.IsLoggedIn) return;

                        if (parsedArgs.Count < 2) {
                            Plugin.Ui.FormationWindow.Toggle();
                            return;
                        }

                        var anchor = FormationAnchorArgumentParser.ParseAnchorAndArrival(
                            parsedArgs.Skip(2),
                            FormationAnchorReference.Self);
                        if (anchor.InvalidArgument != null) {
                            DalamudApi.ChatGui.PrintError($"Invalid formation argument: {anchor.InvalidArgument}");
                            return;
                        }

                        Plugin.IpcProvider.ExecuteFormation(parsedArgs[1], anchor.Anchor, anchor.MovementMode);
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
                        DalamudApi.ChatGui.PrintError("Invalid arguments. Usage: /mop camhack <on|off|toggle>");
                        return;
                    }
                    if (parsedArgs[1].Equals("on", StringComparison.OrdinalIgnoreCase))
                        GameCameraManager.EnableCamHighHeight();
                    else if (parsedArgs[1].Equals("off", StringComparison.OrdinalIgnoreCase))
                        GameCameraManager.Disable();
                    else if (parsedArgs[1].Equals("toggle", StringComparison.OrdinalIgnoreCase))
                        GameCameraManager.Toggle();
                    break;
                case "renderhack":
                case "rh":
                    if (parsedArgs.Count < 2) {
                        DalamudApi.ChatGui.PrintError("Invalid arguments. Usage: /mop renderhack <on|off|toggle>");
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
                case "kb":
                    if (parsedArgs.Count < 2) {
                        DalamudApi.ChatGui.PrintError("Invalid arguments. Usage: /mop keybroadcast <on|off|toggle>");
                        return;
                    }

                    if (parsedArgs[1].Equals("on", StringComparison.OrdinalIgnoreCase))
                        Plugin.IpcProvider.EnableKeyboardBroadcast();
                    else if (parsedArgs[1].Equals("off", StringComparison.OrdinalIgnoreCase))
                        Plugin.IpcProvider.DisableKeyboardBroadcast();
                    else if (parsedArgs[1].Equals("toggle", StringComparison.OrdinalIgnoreCase))
                        Plugin.IpcProvider.ToggleKeyboardBroadcast();
                    break;
                case "logout":
                    GameExitManager.Logout();
                    break;
                case "shutdown":
                    GameExitManager.Shutdown();
                    break;
                case "settingsprofile": {
                        if (parsedArgs.Count < 2) {
                            DalamudApi.ChatGui.PrintError("Invalid arguments. Usage: /mop settingsprofile \"profile name\"");
                            return;
                        }

                        var profile = Plugin.Config.GameSettingsProfiles.FirstOrDefault(p => string.Equals(p.Name, parsedArgs[1], StringComparison.OrdinalIgnoreCase));
                        if (profile != null) {
                            GameSettingsManager.ApplyProfile(profile, Plugin.Config.GameSettingsProfileKeys);
                        }
                    }
                    break;
                case "settings":
                    Plugin.Ui.SettingsWindow.Toggle();
                    break;
                case "abandonduty":
                case "ad":
                    GameFunctions.AbandonDuty();
                    break;
                case "monitor":
                case "peer":
                    Plugin.Ui.PeerMonitorWindow.Toggle();
                    break;
                case "launcher":
                    Plugin.Ui.XivLauncherWindow.Toggle();
                    break;
                case "gamesettings":
                    Plugin.Ui.GameSettingsWindow.Toggle();
                    break;
                case "killxivlauncher":
                    XivLauncherManager.KillXivLauncher();
                    break;
                case "globaldelay":
                    if (parsedArgs.Count < 2) {
                        DalamudApi.ChatGui.PrintError("Invalid arguments. Usage: /mop globaldelay <0.00 - 60.00>");
                        return;
                    }

                    if (!double.TryParse(
                            parsedArgs[1],
                            NumberStyles.Float,
                            CultureInfo.InvariantCulture,
                            out var globalDelay)) {
                        DalamudApi.ChatGui.PrintError("Invalid delay value.");
                        return;
                    }

                    globalDelay = Math.Round(globalDelay, 2, MidpointRounding.AwayFromZero);
                    globalDelay = Math.Clamp(globalDelay, 0.0, 60.0);

                    Plugin.Config.DelayBetweenActions = globalDelay;
                    Plugin.Config.Save();
                    break;
                case "mapflag":
                    Plugin.IpcProvider.BroadcastMyFlagMapMarker();
                    break;
                case "resetwindow":
                    Plugin.Ui.MainWindow.Position = new Vector2(10, 10);
                    DalamudApi.Framework.RunOnTick(() => {
                        Plugin.Ui.MainWindow.Position = null;
                    }, delayTicks: 3);
                    break;
                default:
                    DalamudApi.ChatGui.PrintError($"Unrecognized subcommand: '{subcommand}'");
                    break;
            }
        } else {
            Plugin.Ui.MainWindow.Toggle();
        }
    }

    private static unsafe void ForceResetLocalEmote() {
        var player = Control.GetLocalPlayer();
        if (player == null) {
            DalamudApi.ChatGui.PrintError("Unable to reset emote: local player is unavailable.");
            return;
        }

        player->Character.Timeline.BaseOverride = 0;
        player->Character.Timeline.LipsOverride = 0;
        player->Character.EmoteController.EmoteId = 0;
        player->Character.EmoteController.Target = 0;
        player->Character.EmoteController.Stance = 0;
        player->Character.EmoteController.CPoseState = 0;
        player->Character.SetMode(CharacterModes.Normal, 0);
        player->Character.Timeline.PlayActionTimeline(1);
        DalamudApi.ChatGui.Print("Emote/pose animation reset requested.");
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

    private void OnBroadcastGroupCommand(string command, string arguments) {
        var parsedArgs = ArgumentParser.ParseCommandArgs(arguments);
        if (parsedArgs.Count >= 2)
            Plugin.IpcProvider.EnqueueGroupMacroActions(parsedArgs[1], parsedArgs[0]);
    }

    private void OnPetPlaceCommand(string command, string arguments) {
        var parts = ArgumentParser.ParseMacroArgs(arguments);
        if (parts.Count < 3) {
            DalamudApi.ChatGui.PrintError("Invalid arguments. Usage: /moppetplace x y z [anchor]");
            return;
        }

        if (!float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out float x)
            || !float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float y)
            || !float.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out float z)) {
            DalamudApi.ChatGui.PrintError($"[moppetplace] invalid coordinate offsets: \"{arguments}\"");
            return;
        }

        DalamudApi.Framework.RunOnFrameworkThread(() => {
            var defaultAnchor = DalamudApi.TargetManager.Target != null
                ? FormationAnchorReference.Target
                : FormationAnchorReference.Self;

            var anchorParse = FormationAnchorArgumentParser.ParseAnchorAndArrival(
                parts.Skip(3),
                defaultAnchor);

            if (!FormationAnchorResolver.TryResolve(Plugin, new Formation(), anchorParse.Anchor, out var resolved, out var failureReason, out _)) {
                if (anchorParse.Fallback != null && FormationAnchorResolver.TryResolve(Plugin, new Formation(), anchorParse.Fallback, out var fallbackResolved, out _, out _)) {
                    resolved = fallbackResolved;
                } else {
                    DalamudApi.ChatGui.PrintError($"[moppetplace] failed to resolve anchor: {failureReason}");
                    return;
                }
            }

            var offset = new Vector3(x, y, z);
            var worldPos = offset.ApplyLeaderRotation(resolved.Rotation, resolved.Position);
            worldPos.Y = resolved.Position.Y + y;

            bool isSelf = anchorParse.Anchor.Kind == FormationAnchorKind.Self;
            var targetActor = resolved.Actor ?? (resolved.GameObjectId.HasValue ? DalamudApi.ObjectTable.FirstOrDefault(a => a != null && a.GameObjectId == resolved.GameObjectId) : null);

            GameActionManager.PlacePet(worldPos, targetActor, isSelf);
        });
    }

    private void OnPetFormationPlaceCommand(string command, string arguments) {
        var options = MacroHandler.ParseFormationGotoCommandArgs(arguments);
        if (options == null || options.PointIndex < 0) {
            DalamudApi.ChatGui.PrintError("Invalid arguments. Usage: /moppetformationplace \"Formation Name\" <point> [anchor]");
            return;
        }

        DalamudApi.Framework.RunOnFrameworkThread(() =>
            FormationLocalMovementExecutor.ExecuteFormationPetPlace(
                Plugin,
                options.FormationName,
                options.PointIndex,
                options.Anchor,
                logPrefix: "moppetformationplace",
                fallbackAnchor: options.Fallback));
    }
}
