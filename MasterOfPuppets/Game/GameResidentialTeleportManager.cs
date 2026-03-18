using Dalamud.Game.ClientState.Objects.Enums;

using Lumina.Excel.Sheets;

using MasterOfPuppets.Util;

namespace MasterOfPuppets;

/// <summary>
/// Handles housing-related game interactions such as teleporting to a specific residential ward.
/// </summary>
internal static class GameResidentialTeleportManager {

    private const string AddonHousingSelectBlock = "HousingSelectBlock";
    private const string AddonSelectYesno = "SelectYesno";

    // "Residential District Aethernet." is always at SelectString index 1 in every locale.
    private const int IndexResidentialDistrict = 1;

    // Row 6349 in the Addon sheet contains "Go to specified ward. (Review Tabs)" localised
    // to the client language automatically by DataManager.
    private static string TextGoToWard =>
        DalamudApi.DataManager.GetExcelSheet<Addon>(DalamudApi.ClientState.ClientLanguage)
            ?.GetRow(6349).Text.ExtractText()
        ?? "Go to specified ward. (Review Tabs)";

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
        Coroutine.StartRunOnFramework(
            runFunction: () => { },
            stopWhen: () => DalamudApi.TargetManager.Target?.ObjectKind == ObjectKind.Aetheryte,
            callback: () => {
                GameTargetManager.InteractWithTarget();
                Step_WaitForSelectStringMenu1(ward);
            },
            timeoutMs: 3000);
    }

    //  Step 2
    //  Poll every frame until SelectString entry at index 1 ("Residential District Aethernet.") is visible and clicked.
    //  Index-based to be locale-independent.

    private static void Step_WaitForSelectStringMenu1(int ward) {
        Coroutine.StartRunOnFramework(
            runFunction: () => { },
            stopWhen: () => GameDialogManager.SelectStringAtIndex(IndexResidentialDistrict),
            callback: () => Step_WaitForSelectStringMenu2(ward),
            timeoutMs: 3000);
    }


    //  Step 3
    //  Poll every frame until the "Go to specified ward." entry appears and is clicked.
    //  Text is read from the DataManager (locale-aware).

    private static void Step_WaitForSelectStringMenu2(int ward) {
        Coroutine.StartRunOnFramework(
            runFunction: () => { },
            stopWhen: () => GameDialogManager.SelectStringByText(TextGoToWard),
            callback: () => Step_WaitForHousingSelectBlock(ward),
            timeoutMs: 3000);
    }

    //  Step 4
    //  Wait for HousingSelectBlock addon, select ward, then start confirm polling.

    private static void Step_WaitForHousingSelectBlock(int ward) {
        Coroutine.StartRunOnFramework(
            runFunction: () => { },
            stopWhen: () => GameDialogManager.IsAddonVisible(AddonHousingSelectBlock),
            callback: () => {
                GameDialogManager.SelectWardInHousingBlock(ward);
                Step_WaitForConfirmButton();
            },
            timeoutMs: 1000);
    }

    //  Step 5
    //  Every frame: if the confirm button is enabled, click it.
    //  Stops when HousingSelectBlock closes (= confirm accepted), then waits for YesNo.

    private static void Step_WaitForConfirmButton() {
        Coroutine.StartRunOnFramework(
            runFunction: () => {
                if (GameDialogManager.IsHousingBlockConfirmEnabled())
                    GameDialogManager.ClickHousingBlockConfirm();
            },
            stopWhen: () => !GameDialogManager.IsAddonVisible(AddonHousingSelectBlock),
            callback: () => Step_WaitForYesNoAndConfirm(),
            timeoutMs: 1000);
    }

    //  Step 6
    //  Wait for the "Travel to Ward X?" SelectYesno, then click Yes.

    private static void Step_WaitForYesNoAndConfirm() {
        Coroutine.StartRunOnFramework(
            runFunction: () => { },
            stopWhen: () => GameDialogManager.IsAddonVisible(AddonSelectYesno),
            callback: () => GameDialogManager.ClickYes(),
            timeoutMs: 1000);
    }
}
