using System;
using System.Collections.Generic;

using Dalamud.Game.ClientState.Conditions;
using Dalamud.Interface;

using MasterOfPuppets.Util.ImGuiExt;

namespace MasterOfPuppets;

/// <summary>
/// Action bar docked to the screen side while standing near a house entrance (residential districts)
/// near the exit inside a house, or near the inn room exit. Every button broadcasts, so all clients enter / leave through their nearest door.
/// </summary>
public class HousingOverlayWindow : ThemedWindow {
    private sealed record ActionDef(string Id, FontAwesomeIcon Icon, string Label, string Command, Action<Plugin> Execute);

    private static readonly ActionDef EnterHouse =
        new("enterHouse", FontAwesomeIcon.HouseCircleCheck, "Enter House", "/mop enterhouse", p => p.IpcProvider.ExecuteEnterHouse());
    private static readonly ActionDef ExitHouse =
        new("exitHouse", FontAwesomeIcon.HouseCircleXmark, "Exit House", "/mop exithouse", p => p.IpcProvider.ExecuteExitHouse());
    private static readonly ActionDef FrontDoor =
        new("frontDoor", FontAwesomeIcon.DoorClosed, "Front Door", "/mop movefrontdoor", p => p.IpcProvider.ExecuteMoveToFrontDoor());
    private static readonly ActionDef ExitInn =
        new("exitInn", FontAwesomeIcon.DoorOpen, "Exit Inn Room", "/mop exitinn", p => p.IpcProvider.ExecuteExitInn());

    private Plugin Plugin { get; }
    private readonly List<ActionDef> _actions = new(2);

    public HousingOverlayWindow(Plugin plugin) : base("Housing###MopHousingOverlay", OverlayBar.BaseFlags) {
        Plugin = plugin;
        IsOpen = true;
        RespectCloseHotkey = false;
        ShowCloseButton = false;
    }

    public override bool DrawConditions() {
        _actions.Clear();
        var config = Plugin.Config;
        if (!config.ShowHousingOverlay) return false;
        if (!DalamudApi.PlayerState.IsLoaded) return false;
        if (!OverlayBar.IsEnabledForCurrentCharacter(config)) return false;

        var condition = DalamudApi.Condition;
        if (condition[ConditionFlag.InCombat]
            || condition[ConditionFlag.BetweenAreas]
            || condition[ConditionFlag.BetweenAreas51]
            || condition[ConditionFlag.OccupiedInCutSceneEvent]
            || condition[ConditionFlag.WatchingCutscene]) return false;

        var range = Math.Max(1f, config.HousingOverlayRange);

        if (GameDoorManager.IsInnTerritory()) {
            if (config.HousingOverlayShowInnExit
                && GameDoorManager.FindNearestDoor(range, out _, out _, DoorKind.InnExit) != null)
                _actions.Add(ExitInn);
            return _actions.Count > 0;
        }

        // doors only exist in housing / inn territories: skip the object scan everywhere else
        if (!GameDoorManager.IsHousingTerritory(out var indoor)) return false;

        if (!indoor) {
            var door = GameDoorManager.FindNearestDoor(range, out _, out _, DoorKind.HouseEntrance);
            if (door != null)
                _actions.Add(EnterHouse);
        } else if (GameDoorManager.IsInsideHouse()) {
            var exit = GameDoorManager.FindNearestDoor(range, out _, out _, DoorKind.HouseExit);
            if (exit != null) {
                _actions.Add(ExitHouse);
            } else if (config.HousingOverlayShowAnywhereInside) {
                // the exit flow moves to the front door by itself when the exit is out of range
                _actions.Add(ExitHouse);
                _actions.Add(FrontDoor);
            }
        }

        return _actions.Count > 0;
    }

    public override void PreDraw() {
        Flags = OverlayBar.ApplyDock(Plugin.Config.HousingOverlayDock, Plugin.Config.HousingOverlayOffsetY);
        base.PreDraw();
    }

    public override void Draw() {
        var iconsOnly = Plugin.Config.HousingOverlayIconsOnly;
        foreach (var action in _actions) {
            if (OverlayBar.DrawButton(action.Id, action.Icon, action.Label, Style.Colors.White, iconsOnly))
                action.Execute(Plugin);
            ImGuiUtil.ToolTip(iconsOnly ? $"{action.Label}\n{action.Command}" : action.Command);
        }
    }
}
