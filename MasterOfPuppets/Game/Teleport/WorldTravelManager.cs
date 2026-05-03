using System.Collections.Generic;

using Dalamud.Game.ClientState.Objects.Enums;

using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;

using Lumina.Excel.Sheets;

using MasterOfPuppets.Extensions.Dalamud;
using MasterOfPuppets.Util;

namespace MasterOfPuppets;

/// <summary>
/// Handles world travel via the aetheryte menu <c>WorldTravelSelect</c> flow.
/// </summary>
internal static unsafe class WorldTravelManager {

    //// Aetheryte menu list index: "Visit Another World Server."
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
    public static void TravelToWorld(string world) {
        DalamudApi.Framework.RunOnFrameworkThread(() => {
            // leave party before travel
            if (DalamudApi.PartyList.IsInParty()) {
                Chat.SendMessage("/leave");
            }
            GameTargetManager.TargetNearestAetheryte();
            Step_WaitForAetheryteTarget(world);
        });
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
        string[] worlds = [];
        Coroutine.StartRunOnFramework(
            runFunction: () => { },
            stopWhen: () => { worlds = GameDialogManager.GetAvailableWorldDestinations(); return ok = worlds.Length > 0; },
            callback: () => {
                if (!ok) { DalamudApi.PluginLog.Warning("[TravelToWorld] step3 timeout: WorldTravelSelect worlds not loaded"); return; }
                var index = System.Array.IndexOf(worlds, world);
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
