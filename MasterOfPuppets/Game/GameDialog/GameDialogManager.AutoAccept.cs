using System;

using FFXIVClientStructs.FFXIV.Client.UI;

using Lumina.Excel.Sheets;

namespace MasterOfPuppets;

internal static unsafe partial class GameDialogManager {
    private static string TextAcceptJoinParty =>
        DalamudApi.DataManager.GetExcelSheet<Addon>(DalamudApi.ClientState.ClientLanguage)
            ?.GetRow(120).Text.ExtractText()
        ?? "Join <string(lstr1)>'s party?";

    private static string TextAcceptTeleport =>
            DalamudApi.DataManager.GetExcelSheet<Addon>(DalamudApi.ClientState.ClientLanguage)
                ?.GetRow(1800).Text.ExtractText()
            ?? "Accept Teleport to <ennoun(PlaceName,2,<sheet(Aetheryte,lnum1,8)>,2,1)>?";

    /// <summary>
    /// Called every framework tick. Auto-accepts party invites and/or teleport requests
    /// by matching the SelectYesno prompt text against known Addon row fragments.
    /// </summary>
    public static void AutoAcceptUpdate(bool acceptParty, bool acceptTeleport) {
        if (!acceptParty && !acceptTeleport) return;
        if (!IsAddonVisible(AddonName.SelectYesno)) return;

        var addon = (AddonSelectYesno*)GetAddonByName(AddonName.SelectYesno);
        if (addon == null || addon->PromptText == null) return;

        var text = addon->PromptText->NodeText.ToString();
        if (string.IsNullOrEmpty(text)) return;

        if (acceptParty && ContainsAllFragments(text, TextAcceptJoinParty)) { ClickYes(); return; }
        if (acceptTeleport && ContainsAllFragments(text, TextAcceptTeleport)) { ClickYes(); return; }
    }

    // Splits the extracted Addon row text (which has template vars stripped) into words ≥ 3 chars
    // and checks that the dialog text contains each one. Locale-aware since TextAccept* use ClientLanguage.
    private static bool ContainsAllFragments(string dialogText, string pattern) {
        foreach (var part in pattern.Split(' ', StringSplitOptions.RemoveEmptyEntries)) {
            if (part.Length < 3) continue;
            if (!dialogText.Contains(part, StringComparison.OrdinalIgnoreCase)) return false;
        }
        return true;
    }
}
