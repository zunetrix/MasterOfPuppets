using System;
using System.Collections.Generic;

using Dalamud.Game.ClientState.Conditions;
using Dalamud.Interface;

using MasterOfPuppets.Util.ImGuiExt;

namespace MasterOfPuppets;

/// <summary>
/// Action bar docked to the screen side while standing near a house entrance (residential districts)
/// or near the exit inside a house. Every button broadcasts, so all clients enter / leave through their nearest door.
/// </summary>
public class HousingOverlayWindow : ThemedWindow {
    private sealed record ActionDef(string Id, FontAwesomeIcon Icon, string Label, string Command, Action<Plugin> Execute);

    private static readonly ActionDef EnterHouse =
        new("enterHouse", FontAwesomeIcon.HouseCircleCheck, "Enter House", "/mop enterhouse", p => p.IpcProvider.ExecuteEnterHouse());
    private static readonly ActionDef ExitHouse =
        new("exitHouse", FontAwesomeIcon.HouseCircleXmark, "Exit House", "/mop exithouse", p => p.IpcProvider.ExecuteExitHouse());
    private static readonly ActionDef FrontDoor =
        new("frontDoor", FontAwesomeIcon.DoorClosed, "Front Door", "/mop movefrontdoor", p => p.IpcProvider.ExecuteMoveToFrontDoor());

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

        // doors only exist in housing territories: skip the object scan everywhere else
        if (!GameHousingManager.IsHousingTerritory(out var indoor)) return false;

        var range = Math.Max(1f, config.HousingOverlayRange);
        if (!indoor) {
            var door = GameHousingManager.FindNearestDoor(range, out _, out _, HousingDoorKind.HouseEntrance);
            if (door != null)
                _actions.Add(EnterHouse);
        } else if (GameHousingManager.IsInsideHouse()) {
            var exit = GameHousingManager.FindNearestDoor(range, out _, out _, HousingDoorKind.HouseExit);
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
