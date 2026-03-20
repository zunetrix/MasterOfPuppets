using Dalamud.Game.ClientState.Objects.Enums;

using Lumina.Excel.Sheets;

using MasterOfPuppets.Util;

namespace MasterOfPuppets;

/// <summary>
/// Handles housing-related game interactions such as teleporting to a specific residential ward.
/// </summary>
internal static class ResidentialTeleportManager {

    // Aetheryte menu list index: "Residential District Aethernet."
    private const int IndexResidentialDistrict = 1;

    // Row 6349 in the Addon sheet contains "Go to specified ward. (Review Tabs)" localised
    // to the client language automatically by DataManager.
    private static string TextGoToWard =>
        DalamudApi.DataManager.GetExcelSheet<Addon>(DalamudApi.ClientState.ClientLanguage)
            ?.GetRow(6349).Text.ExtractText()
        ?? "Go to specified ward. (Review Tabs)";


    // flow:
    // Interact with aetheryte > SelectString > "Residential District Aethernet." > SelectString > "Go to specified ward. (Review Tabs)" > HousingSelectBlock > Select ward number > Confirm button Id 34 > Yes
    /// <summary>
    /// Initiates the full aetheryte-menu flow to teleport to the given residential ward (1-indexed, 1-30).
    /// Requires the player to be near a main-city aetheryte.
    /// </summary>
    public static void TeleportToWard(int ward) {
        if (ward < 1 || ward > 30) {
            DalamudApi.PluginLog.Warning($"[GameHousingManager] invalid ward {ward}, expected 1-30");
            return;
        }
        DalamudApi.Framework.RunOnFrameworkThread(() => {
            GameTargetManager.TargetNearestAetheryte();
            Step_WaitForAetheryteTarget(ward);
        });
    }

    //  Step 1
    //  Wait until the nearest aetheryte is targeted, then interact.

    private static void Step_WaitForAetheryteTarget(int ward) {
        var ok = false;
        Coroutine.StartRunOnFramework(
            runFunction: () => { },
            stopWhen: () => ok = DalamudApi.TargetManager.Target?.ObjectKind == ObjectKind.Aetheryte,
            callback: () => {
                if (!ok) { DalamudApi.PluginLog.Warning("[TeleportToWard] step1 timeout: no aetheryte targeted"); return; }
                GameTargetManager.InteractWithTarget();
                Step_WaitForSelectStringMenu1(ward);
            },
            timeoutMs: 3000);
    }

    //  Step 2
    //  Poll every frame until SelectString entry at index 1 ("Residential District Aethernet.") is visible and clicked.
    //  Index-based to be locale-independent.

    private static void Step_WaitForSelectStringMenu1(int ward) {
        var ok = false;
        Coroutine.StartRunOnFramework(
            runFunction: () => { },
            stopWhen: () => ok = GameDialogManager.SelectStringAtIndex(IndexResidentialDistrict),
            // stopWhen: () => GameDialogManager.SelectStringByText(TextGoToWard),
            callback: () => {
                if (!ok) { DalamudApi.PluginLog.Warning("[TeleportToWard] step2 timeout: SelectString1 not clicked"); return; }
                Step_WaitForSelectStringMenu2(ward);
            },
            timeoutMs: 3000);
    }

    //  Step 3
    //  Poll every frame until the "Go to specified ward." entry appears and is clicked.
    //  Text is read from the DataManager (locale-aware).

    private static void Step_WaitForSelectStringMenu2(int ward) {
        var ok = false;
        Coroutine.StartRunOnFramework(
            runFunction: () => { },
            stopWhen: () => ok = GameDialogManager.SelectStringByText(TextGoToWard),
            callback: () => {
                if (!ok) { DalamudApi.PluginLog.Warning("[TeleportToWard] step3 timeout: 'Go to ward' not clicked"); return; }
                Step_WaitForHousingSelectBlock(ward);
            },
            timeoutMs: 3000);
    }

    //  Step 4
    //  Wait for HousingSelectBlock addon, select ward, then start confirm polling.

    private static void Step_WaitForHousingSelectBlock(int ward) {
        var ok = false;
        Coroutine.StartRunOnFramework(
            runFunction: () => { },
            stopWhen: () => ok = GameDialogManager.IsAddonVisible(GameDialogManager.AddonName.HousingSelectBlock),
            callback: () => {
                if (!ok) { DalamudApi.PluginLog.Warning("[TeleportToWard] step4 timeout: HousingSelectBlock not visible"); return; }
                GameDialogManager.SelectWardInHousingBlock(ward);
                Step_WaitForConfirmButton();
            },
            timeoutMs: 5000);
    }

    //  Step 5
    //  Every frame: if the confirm button is enabled, click it.
    //  HousingSelectBlock stays open while SelectYesno is shown on top - stop when YesNo appears.

    private static void Step_WaitForConfirmButton() {
        var ok = false;
        Coroutine.StartRunOnFramework(
            runFunction: () => {
                if (GameDialogManager.IsHousingBlockConfirmEnabled())
                    GameDialogManager.ClickHousingBlockConfirm();
            },
            stopWhen: () => ok = GameDialogManager.IsAddonVisible(GameDialogManager.AddonName.SelectYesno),
            callback: () => {
                if (!ok) { DalamudApi.PluginLog.Warning("[TeleportToWard] step5 timeout: SelectYesno did not appear after confirm"); return; }
                Step_WaitForYesNoAndConfirm();
            },
            timeoutMs: 5000);
    }

    //  Step 6
    //  Wait for the "Travel to Ward X?" SelectYesno, then click Yes.

    private static void Step_WaitForYesNoAndConfirm() {
        var ok = false;
        Coroutine.StartRunOnFramework(
            runFunction: () => { },
            stopWhen: () => ok = GameDialogManager.IsAddonVisible(GameDialogManager.AddonName.SelectYesno),
            callback: () => {
                if (!ok) { DalamudApi.PluginLog.Warning("[TeleportToWard] step6 timeout: SelectYesno not visible"); return; }
                GameDialogManager.ClickYes();
            },
            timeoutMs: 5000);
    }
}
