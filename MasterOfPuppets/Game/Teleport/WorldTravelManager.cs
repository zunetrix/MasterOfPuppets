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

    // Aetheryte menu list index: "Visit Another World Server."
    private const int IndexVisitAnotherWorld = 2;

    // Addon row 102338 = "Visit Another World Server." (locale-aware, icon stripped).
    private static string TextVisitAnotherWorld =>
        DalamudApi.DataManager.GetExcelSheet<Addon>(DalamudApi.ClientState.ClientLanguage)
            ?.GetRow(102338).Text.ExtractText()
        ?? "Visit Another World Server.";

    //flow:
    //Interact with aetheryte > SelectString > "Visit Another World Server." > WorldTravelSelect > Select World > Yes
    /// <summary>
    /// Initiates the aetheryte-menu flow to travel to the given world.
    /// Requires the player to be near a main-city aetheryte.
    /// </summary>
    public static void TravelToWorld(string world, Plugin plugin) {
        DalamudApi.Framework.RunOnFrameworkThread(() => {
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
    //  Text from DataManager row 102338 (locale-aware, icon stripped).
    private static void Step_WaitForVisitAnotherWorldOption(string world) {
        var ok = false;
        Coroutine.StartRunOnFramework(
            runFunction: () => { },
            stopWhen: () => ok = GameDialogManager.SelectStringAtIndex(IndexVisitAnotherWorld),
            // stopWhen: () => GameDialogManager.SelectStringByText(TextVisitAnotherWorld),
            callback: () => {
                if (!ok) { DalamudApi.PluginLog.Warning("[TravelToWorld] step2 timeout: 'Visit Another World' not clicked"); return; }
                Step_WaitForWorldTravelSelect(world);
            },
            timeoutMs: 7000);
    }

    //  Step 3
    //  Wait for WorldTravelSelect addon AND its world list to be populated, then fire selection.
    //  Addon becomes visible before worlds load - poll until list has entries.
    //  Ls: Callback.Fire(addon, true, index + 2)
    private static void Step_WaitForWorldTravelSelect(string world) {
        var ok = false;
        List<string> worlds = [];
        Coroutine.StartRunOnFramework(
            runFunction: () => { },
            stopWhen: () => { worlds = GameDialogManager.GetAvailableWorldDestinations(); return ok = worlds.Count > 0; },
            callback: () => {
                if (!ok) { DalamudApi.PluginLog.Warning("[TravelToWorld] step3 timeout: WorldTravelSelect worlds not loaded"); return; }
                int index = worlds.FindIndex(w => string.Equals(w, world, StringComparison.OrdinalIgnoreCase));
                if (index == -1) {
                    DalamudApi.PluginLog.Warning($"[WorldTravel] '{world}' not available. Found: [{string.Join(", ", worlds)}]");
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
