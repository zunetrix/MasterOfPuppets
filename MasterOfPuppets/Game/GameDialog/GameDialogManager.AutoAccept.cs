using System;
using System.Collections.Generic;
using System.Linq;

using FFXIVClientStructs.FFXIV.Client.UI;

using Lumina.Excel.Sheets;
using Lumina.Text.ReadOnly;

using MasterOfPuppets.Ipc;

namespace MasterOfPuppets;

internal static unsafe partial class GameDialogManager {
    private static string? _lastPartyInviteDiagnosticKey;

    private static ReadOnlySeString TextAcceptJoinParty =>
        DalamudApi.DataManager.GetExcelSheet<Addon>(DalamudApi.ClientState.ClientLanguage)
            ?.GetRow(120).Text
        ?? default;

    private static string TextAcceptTeleport =>
            DalamudApi.DataManager.GetExcelSheet<Addon>(DalamudApi.ClientState.ClientLanguage)
                ?.GetRow(1800).Text.ExtractText()
            ?? "Accept Teleport to <ennoun(PlaceName,2,<sheet(Aetheryte,lnum1,8)>,2,1)>?";

    /// <summary>
    /// Called every framework tick. Auto-accepts party invites and/or teleport requests
    /// by matching the SelectYesno prompt text against known Addon row fragments.
    /// </summary>
    public static void AutoAcceptUpdate(
        bool acceptParty,
        bool acceptTeleport,
        bool acceptPartyOnlyFromCharacters = false,
        IEnumerable<Character>? partyInviteCharacters = null,
        IEnumerable<PeerCharacterInfo>? connectedPeers = null) {
        if (!acceptParty && !acceptTeleport) return;
        if (!IsAddonVisible(AddonName.SelectYesno)) {
            _lastPartyInviteDiagnosticKey = null;
            return;
        }

        var addon = (AddonSelectYesno*)GetAddonByName(AddonName.SelectYesno);
        if (addon == null || addon->PromptText == null) return;

        var rawText = addon->PromptText->NodeText.ToString();
        var text = new ReadOnlySeStringSpan(addon->PromptText->NodeText.AsSpan()).ExtractText();
        if (string.IsNullOrWhiteSpace(text))
            text = rawText;
        if (string.IsNullOrEmpty(text)) return;

        if (acceptParty) {
            var partyDecision = PartyInvitePromptMatcher.EvaluateConnectedConfiguredInvite(
                text,
                TextAcceptJoinParty,
                partyInviteCharacters ?? Enumerable.Empty<Character>(),
                connectedPeers ?? Enumerable.Empty<PeerCharacterInfo>());
            var isPartyInvite = partyDecision.Parse.IsPartyInvite;
            var shouldAccept = isPartyInvite && (!acceptPartyOnlyFromCharacters || partyDecision.ShouldAccept);
            LogPartyInviteDiagnostic(rawText, text, partyDecision, acceptPartyOnlyFromCharacters, shouldAccept);

            if (isPartyInvite) {
                if (shouldAccept)
                    ClickYes();
                return;
            }
        }

        if (acceptTeleport && ContainsAllFragments(text, TextAcceptTeleport)) { ClickYes(); return; }
    }

    private static void LogPartyInviteDiagnostic(
        string rawText,
        string text,
        PartyInviteDecision decision,
        bool onlyFromCharacters,
        bool shouldAccept) {
        var parse = decision.Parse;
        var expression = parse.Expression;
        var key = string.Join(
            "\u001f",
            rawText,
            text,
            expression.SourceText,
            parse.InviterSegment,
            parse.SheetExpressionAvailable,
            parse.SheetExpressionReason,
            decision.Reason,
            onlyFromCharacters,
            shouldAccept);
        if (_lastPartyInviteDiagnosticKey == key)
            return;

        _lastPartyInviteDiagnosticKey = key;
        DalamudApi.PluginLog.Verbose(
            $"[AutoAcceptPartyInvite] rawText=\"{rawText}\" cleanedText=\"{text}\" addonRow=\"{expression.SourceText}\" " +
            $"expression=\"prefix:{expression.Prefix}|suffix:{expression.Suffix}\" " +
            $"sheetExpressionAvailable={parse.SheetExpressionAvailable} sheetExpressionReason=\"{parse.SheetExpressionReason}\" " +
            $"extracted=\"{parse.InviterSegment}\" isPartyInvite={parse.IsPartyInvite} onlyFromCharacters={onlyFromCharacters} " +
            $"connectedPeers={decision.ConnectedPeerCount} freshPeers={decision.FreshPeerCount} matchedPeer=\"{decision.MatchedPeer}\" " +
            $"configMatch=\"{decision.ConfigMatch}\" accept={shouldAccept} reason=\"{decision.Reason}\"");
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
