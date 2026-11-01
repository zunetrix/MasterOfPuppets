using System;
using System.Collections.Generic;

using System.Linq;
using System.Numerics;

using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.Types;

using Lumina.Excel.Sheets;

using MasterOfPuppets.Extensions.Dalamud;
using MasterOfPuppets.Util;

namespace MasterOfPuppets;

/// <summary>
/// Handles world travel via the aetheryte menu <c>WorldTravelSelect</c> flow.
/// </summary>
internal static class WorldTravelManager {

    //flow:
    //Interact with aetheryte > SelectString > "Visit Another World Server." > WorldTravelSelect > Select World > Yes
    /// <summary>
    /// Initiates the aetheryte-menu flow to travel to the given world.
    /// Requires the player to be near a main-city aetheryte.
    /// </summary>
    public static void TravelToWorld(string world, Plugin plugin) {
        // normalize to the sheet name when it resolves (case / prefix tolerant: "tonb" -> "Tonberry")
        if (WorldCatalog.TryResolve(world, out var resolved)) world = resolved.Name.ExtractText();

        DalamudApi.Framework.RunOnFrameworkThread(() => {
            if (world.Equals(DalamudApi.PlayerState.CurrentWorld.Value.Name.ToString(), StringComparison.OrdinalIgnoreCase)) return;

            // leave party before travel
            if (DalamudApi.PartyList.IsInParty()) {
                Chat.SendMessage("/leave");
            }

            var player = DalamudApi.ObjectTable.LocalPlayer;
            if (player == null) return;

            var aetheryte = DalamudApi.ObjectTable
                .Where(x => x != null && x.ObjectKind == ObjectKind.Aetheryte)
                .OrderBy(x => Vector3.DistanceSquared(player.Position, x.Position))
                .FirstOrDefault();

            if (aetheryte != null && Vector3.Distance(aetheryte.Position, player.Position) > 10f) {
                plugin.MovementManager.MoveTo(aetheryte.GameObjectId);
                Step_WaitUntilCloseAndTargetAetheryte(world, aetheryte, plugin);
            } else {
                GameTargetManager.TargetNearestAetheryte();
                Step_WaitForAetheryteTarget(world);
            }
        });
    }

    private static void Step_WaitUntilCloseAndTargetAetheryte(string world, IGameObject aetheryte, Plugin plugin) {
        var ok = false;
        Coroutine.StartRunOnFramework(
            runFunction: () => { },
            stopWhen: () => {
                var player = DalamudApi.ObjectTable.LocalPlayer;
                if (player == null || aetheryte == null) return false;
                ok = Vector3.Distance(aetheryte.Position, player.Position) <= 10f;
                return ok;
            },
            callback: () => {
                if (!ok) {
                    DalamudApi.PluginLog.Warning("[TravelToWorld] move timeout: Aetheryte distance still > 10");
                    plugin.MovementManager.StopMove();
                    return;
                }
                plugin.MovementManager.StopMove();
                GameTargetManager.TargetNearestAetheryte();
                Step_WaitForAetheryteTarget(world);
            },
            timeoutMs: 30000);
    }

    //  Step 1
    //  Wait until the nearest aetheryte is targeted, then interact.
    private static void Step_WaitForAetheryteTarget(string world) {
        var ok = false;
        Coroutine.StartRunOnFramework(
            runFunction: () => { },
            stopWhen: () => ok = DalamudApi.TargetManager.Target?.ObjectKind == ObjectKind.Aetheryte && !DalamudApi.PartyList.IsInParty(),
            callback: () => {
                if (!ok) { DalamudApi.PluginLog.Warning("[TravelToWorld] step1 timeout: no aetheryte targeted"); return; }
                GameTargetManager.InteractWithTarget();
                Step_WaitForVisitAnotherWorldOption(world);
            },
            timeoutMs: 3000);
    }

    //  Step 2
    //  Poll every frame until "Visit Another World Server." is clicked in SelectString.
    //  Text from quest dialogue transport/Aetheryte (locale-aware), index as fallback.
    private static void Step_WaitForVisitAnotherWorldOption(string world) {
        var ok = false;
        Coroutine.StartRunOnFramework(
            runFunction: () => { },
            stopWhen: () => ok = GameDialogManager.SelectStringByTextOrIndex(AetheryteMenuText.VisitAnotherWorld, AetheryteMenuText.IndexVisitAnotherWorld),
            callback: () => {
                if (!ok) { DalamudApi.PluginLog.Warning("[TravelToWorld] step2 timeout: 'Visit Another World' not clicked"); return; }
                Step_WaitForWorldTravelSelect(world);
            },
            timeoutMs: 7000);
    }

    //  Step 3
    //  Wait for WorldTravelSelect addon AND its world list to be populated, then fire selection.
    //  Addon becomes visible before worlds load - poll until list has entries.
    //  Merged DCs (JP today) show one tab per original DC: when the world is not in the open tab, switch tab and poll again.
    //  Ls 2.6: Callback.Fire(addon, true, 0, index + 2) / tab: Callback.Fire(addon, true, 1, tab)
    private static void Step_WaitForWorldTravelSelect(string world) {
        const long TabSwitchIntervalMs = 300;

        var ok = false;
        var index = -1;
        long lastTabSwitch = 0;
        List<string> worlds = [];
        var tab = WorldCatalog.IsCurrentDataCenterMerged() && WorldCatalog.TryResolve(world, out var row)
            ? WorldCatalog.GetWorldTravelTab(row)
            : -1;

        Coroutine.StartRunOnFramework(
            runFunction: () => {
                if (worlds.Count == 0 || index != -1 || tab == -1) return;
                var now = Environment.TickCount64;
                if (now - lastTabSwitch < TabSwitchIntervalMs) return;
                lastTabSwitch = now;
                GameDialogManager.SelectWorldTravelTab(tab);
            },
            stopWhen: () => {
                if (!GameDialogManager.IsAddonVisible(GameDialogManager.AddonName.WorldTravelSelect)) return false;
                worlds = GameDialogManager.GetAvailableWorldDestinations();
                index = worlds.FindIndex(w => string.Equals(w, world, StringComparison.OrdinalIgnoreCase));
                // without a tab to switch to, a loaded list that lacks the world is final
                return ok = index != -1 || (tab == -1 && worlds.Count > 0);
            },
            callback: () => {
                if (!ok || index == -1) {
                    DalamudApi.PluginLog.Warning($"[WorldTravel] '{world}' not available. Found: [{string.Join(", ", worlds)}]");
                    GameDialogManager.CloseAddon(GameDialogManager.AddonName.WorldTravelSelect);
                    return;
                }
                GameDialogManager.SelectWorldTravelEntry(index);
                Step_WaitForYesNoAndConfirm();
            },
            timeoutMs: 5000);
    }

    //  Step 4
    //  Wait for SelectYesno confirmation and click Yes.
    private static void Step_WaitForYesNoAndConfirm() {
        var ok = false;
        Coroutine.StartRunOnFramework(
            runFunction: () => { },
            stopWhen: () => ok = GameDialogManager.IsAddonVisible("SelectYesno"),
            callback: () => {
                if (!ok) { DalamudApi.PluginLog.Warning("[TravelToWorld] step4 timeout: SelectYesno not visible"); return; }
                GameDialogManager.ClickYes();
            },
            timeoutMs: 5000);
    }
}
