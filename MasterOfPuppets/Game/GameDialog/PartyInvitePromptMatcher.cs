using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Lumina.Text.Payloads;
using Lumina.Text.ReadOnly;

using MasterOfPuppets.Ipc;

namespace MasterOfPuppets;

public static class PartyInvitePromptMatcher {
    private static readonly TimeSpan DefaultConnectedPeerMaxAge = TimeSpan.FromSeconds(5);

    public static bool ShouldAcceptPartyInvite(
        string promptText,
        bool onlyFromCharacters,
        IEnumerable<string> characterNames) {
        if (!TryGetPartyInviteInviterSegment(promptText, string.Empty, out _))
            return false;

        return !onlyFromCharacters || IsInviterInCharacterList(promptText, characterNames);
    }

    public static bool IsInviterInCharacterList(string promptText, IEnumerable<string> characterNames) {
        if (!TryGetPartyInviteInviterSegment(promptText, string.Empty, out var inviterSegment))
            return false;

        return characterNames
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Any(name => MatchesCharacter(inviterSegment, name));
    }

    public static bool IsInviterInConnectedConfiguredCharacters(
        string promptText,
        IEnumerable<Character> configuredCharacters,
        IEnumerable<PeerCharacterInfo> connectedPeers,
        DateTime? now = null,
        TimeSpan? maxPeerAge = null) {
        return EvaluateConnectedConfiguredInvite(
            promptText,
            string.Empty,
            configuredCharacters,
            connectedPeers,
            now,
            maxPeerAge).ShouldAccept;
    }

    public static bool TryGetPartyInviteInviterSegment(string promptText, string addonRowText, out string inviterSegment) {
        var result = ParsePartyInvitePrompt(promptText, addonRowText);
        inviterSegment = result.InviterSegment;
        return result.IsPartyInvite;
    }

    public static PartyInviteDecision EvaluateConnectedConfiguredInvite(
        string promptText,
        string addonRowText,
        IEnumerable<Character> configuredCharacters,
        IEnumerable<PeerCharacterInfo> connectedPeers,
        DateTime? now = null,
        TimeSpan? maxPeerAge = null) {
        return EvaluateConnectedConfiguredInvite(
            ParsePartyInvitePrompt(promptText, addonRowText),
            configuredCharacters,
            connectedPeers,
            now,
            maxPeerAge);
    }

    internal static PartyInviteDecision EvaluateConnectedConfiguredInvite(
        string promptText,
        ReadOnlySeString addonRowText,
        IEnumerable<Character> configuredCharacters,
        IEnumerable<PeerCharacterInfo> connectedPeers,
        DateTime? now = null,
        TimeSpan? maxPeerAge = null) {
        return EvaluateConnectedConfiguredInvite(
            ParsePartyInvitePrompt(promptText, addonRowText),
            configuredCharacters,
            connectedPeers,
            now,
            maxPeerAge);
    }

    private static PartyInviteDecision EvaluateConnectedConfiguredInvite(
        PartyInvitePromptParseResult parse,
        IEnumerable<Character> configuredCharacters,
        IEnumerable<PeerCharacterInfo> connectedPeers,
        DateTime? now = null,
        TimeSpan? maxPeerAge = null) {
        var currentTime = now ?? DateTime.UtcNow;
        var peerMaxAge = maxPeerAge ?? DefaultConnectedPeerMaxAge;
        var peers = connectedPeers.ToList();
        var freshPeers = peers
            .Where(peer => IsFreshPeer(peer, currentTime, peerMaxAge))
            .ToList();
        if (!parse.IsPartyInvite)
            return new PartyInviteDecision(
                ShouldAccept: false,
                Parse: parse,
                ConnectedPeerCount: peers.Count,
                FreshPeerCount: freshPeers.Count,
                MatchedPeer: string.Empty,
                ConfigMatch: string.Empty,
                Reason: "prompt did not match party invite expression");

        var configured = configuredCharacters
            .Where(character => character != null)
            .ToList();

        foreach (var peer in freshPeers) {
            if (!MatchesPeer(parse.InviterSegment, peer, out var peerMatchDetails))
                continue;

            if (IsPeerConfigured(peer, configured, out var configMatchDetails))
                return new PartyInviteDecision(
                    ShouldAccept: true,
                    Parse: parse,
                    ConnectedPeerCount: peers.Count,
                    FreshPeerCount: freshPeers.Count,
                    MatchedPeer: FormatPeer(peer),
                    ConfigMatch: configMatchDetails,
                    Reason: $"matched peer ({peerMatchDetails}) and configured character ({configMatchDetails})");

            return new PartyInviteDecision(
                ShouldAccept: false,
                Parse: parse,
                ConnectedPeerCount: peers.Count,
                FreshPeerCount: freshPeers.Count,
                MatchedPeer: FormatPeer(peer),
                ConfigMatch: string.Empty,
                Reason: $"matched peer ({peerMatchDetails}) but peer is not configured");
        }

        return new PartyInviteDecision(
            ShouldAccept: false,
            Parse: parse,
            ConnectedPeerCount: peers.Count,
            FreshPeerCount: freshPeers.Count,
            MatchedPeer: string.Empty,
            ConfigMatch: string.Empty,
            Reason: "no fresh connected peer matched inviter segment");
    }

    public static PartyInvitePromptParseResult ParsePartyInvitePrompt(string promptText, string addonRowText) {
        if (!TryBuildExtractionExpression(addonRowText, out var expression, out var expressionReason))
            return PartyInvitePromptParseResult.NotMatched(
                promptText?.Trim() ?? string.Empty,
                addonRowText?.Trim() ?? string.Empty,
                expressionReason);

        return ParsePartyInvitePrompt(promptText, expression);
    }

    internal static PartyInvitePromptParseResult ParsePartyInvitePrompt(string promptText, ReadOnlySeString addonRowText) {
        if (!TryBuildExtractionExpression(addonRowText, out var expression, out var expressionReason))
            return PartyInvitePromptParseResult.NotMatched(
                promptText?.Trim() ?? string.Empty,
                addonRowText.IsEmpty ? string.Empty : addonRowText.ToMacroString(),
                expressionReason);

        return ParsePartyInvitePrompt(promptText, expression);
    }

    private static PartyInvitePromptParseResult ParsePartyInvitePrompt(
        string promptText,
        PartyInviteExtractionExpression expression) {
        var text = promptText?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(text))
            return PartyInvitePromptParseResult.NotMatched(text, expression, "prompt text is empty");

        if (!text.StartsWith(expression.Prefix, StringComparison.OrdinalIgnoreCase))
            return PartyInvitePromptParseResult.NotMatched(text, expression, "prompt does not start with expression prefix");

        var end = string.IsNullOrEmpty(expression.Suffix)
            ? text.Length
            : text.IndexOf(expression.Suffix, expression.Prefix.Length, StringComparison.OrdinalIgnoreCase);
        if (end <= expression.Prefix.Length)
            return PartyInvitePromptParseResult.NotMatched(text, expression, "expression suffix was not found after prefix");

        var inviterSegment = text[expression.Prefix.Length..end].Trim();
        if (string.IsNullOrWhiteSpace(inviterSegment))
            return PartyInvitePromptParseResult.NotMatched(text, expression, "inviter segment is empty");

        return PartyInvitePromptParseResult.Matched(text, expression, inviterSegment);
    }

    public static bool TryBuildExtractionExpression(
        string addonRowText,
        out PartyInviteExtractionExpression expression,
        out string reason) {
        expression = new PartyInviteExtractionExpression(addonRowText?.Trim() ?? string.Empty, string.Empty, string.Empty);
        reason = string.Empty;

        if (string.IsNullOrWhiteSpace(addonRowText)) {
            reason = "party invite sheet expression unavailable";
            return false;
        }

        var text = addonRowText.Trim();
        var placeholderStart = text.IndexOf("<string", StringComparison.OrdinalIgnoreCase);
        if (placeholderStart < 0) {
            reason = "party invite sheet expression has no string payload";
            return false;
        }

        var placeholderEnd = text.IndexOf('>', placeholderStart);
        if (placeholderEnd <= placeholderStart) {
            reason = "party invite sheet expression has unterminated string payload";
            return false;
        }

        var prefix = text[..placeholderStart];
        var suffix = text[(placeholderEnd + 1)..];
        if (string.IsNullOrEmpty(prefix) && string.IsNullOrEmpty(suffix)) {
            reason = "party invite sheet expression has no static text";
            return false;
        }

        expression = new PartyInviteExtractionExpression(text, prefix, suffix);
        reason = "party invite sheet expression parsed";
        return true;
    }

    internal static bool TryBuildExtractionExpression(
        ReadOnlySeString addonRowText,
        out PartyInviteExtractionExpression expression,
        out string reason) {
        var sourceText = addonRowText.IsEmpty ? string.Empty : addonRowText.ToMacroString();
        expression = new PartyInviteExtractionExpression(sourceText, string.Empty, string.Empty);
        reason = string.Empty;

        if (addonRowText.IsEmpty) {
            reason = "party invite sheet expression unavailable";
            return false;
        }

        var prefix = new StringBuilder();
        var suffix = new StringBuilder();
        var foundStringPayload = false;
        foreach (var payload in addonRowText) {
            if (payload.Type == ReadOnlySePayloadType.Text) {
                (foundStringPayload ? suffix : prefix).Append(payload.ToString());
                continue;
            }

            if (payload.Type != ReadOnlySePayloadType.Macro) {
                reason = $"party invite sheet expression has unsupported payload type {payload.Type}";
                return false;
            }

            if (payload.MacroCode != MacroCode.String) {
                reason = $"party invite sheet expression has unsupported macro payload {payload.MacroCode}";
                return false;
            }

            if (foundStringPayload) {
                reason = "party invite sheet expression has multiple string payloads";
                return false;
            }

            foundStringPayload = true;
        }

        if (!foundStringPayload) {
            reason = "party invite sheet expression has no string payload";
            return false;
        }

        if (prefix.Length == 0 && suffix.Length == 0) {
            reason = "party invite sheet expression has no static text";
            return false;
        }

        expression = new PartyInviteExtractionExpression(sourceText, prefix.ToString(), suffix.ToString());
        reason = "party invite sheet expression parsed";
        return true;
    }

    private static bool MatchesCharacter(string inviterSegment, string savedCharacterName) {
        var (name, world) = SplitCharacterName(savedCharacterName);
        if (string.IsNullOrWhiteSpace(name))
            return false;

        if (inviterSegment.Equals(savedCharacterName.Trim(), StringComparison.OrdinalIgnoreCase))
            return true;

        if (!string.IsNullOrWhiteSpace(world) && MatchesNameAndWorld(inviterSegment, name, world))
            return true;

        return inviterSegment.Equals(name, StringComparison.OrdinalIgnoreCase);
    }

    private static bool MatchesPeer(string inviterSegment, PeerCharacterInfo peer, out string details) {
        details = string.Empty;
        if (string.IsNullOrWhiteSpace(peer.CharacterName))
            return false;

        if (!inviterSegment.StartsWith(peer.CharacterName, StringComparison.OrdinalIgnoreCase))
            return false;

        var remainder = inviterSegment[peer.CharacterName.Length..].Trim();
        if (string.IsNullOrWhiteSpace(remainder)) {
            details = "name-only prompt";
            return true;
        }

        if (!string.IsNullOrWhiteSpace(peer.HomeWorld) &&
            ContainsWorldName(remainder, peer.HomeWorld)) {
            details = "home world in prompt";
            return true;
        }

        if (!string.IsNullOrWhiteSpace(peer.CurrentWorld) &&
            ContainsWorldName(remainder, peer.CurrentWorld)) {
            details = "current world in prompt";
            return true;
        }

        return false;
    }

    private static bool IsPeerConfigured(PeerCharacterInfo peer, IReadOnlyList<Character> configuredCharacters, out string details) {
        details = string.Empty;
        foreach (var character in configuredCharacters) {
            if (character.Cid != 0 && character.Cid == peer.ContentId) {
                details = $"cid:{character.Cid}";
                return true;
            }

            if (string.IsNullOrWhiteSpace(character.Name))
                continue;

            var (configuredName, configuredWorld) = SplitCharacterName(character.Name);
            if (!configuredName.Equals(peer.CharacterName, StringComparison.OrdinalIgnoreCase))
                continue;

            if (string.IsNullOrWhiteSpace(configuredWorld)) {
                details = $"name:{character.Name}";
                return true;
            }

            if (!string.IsNullOrWhiteSpace(peer.CurrentWorld) &&
                configuredWorld.Equals(peer.CurrentWorld, StringComparison.OrdinalIgnoreCase)) {
                details = $"name-current-world:{character.Name}";
                return true;
            }

            if (!string.IsNullOrWhiteSpace(peer.HomeWorld) &&
                configuredWorld.Equals(peer.HomeWorld, StringComparison.OrdinalIgnoreCase)) {
                details = $"name-home-world:{character.Name}";
                return true;
            }
        }

        return false;
    }

    private static bool IsFreshPeer(PeerCharacterInfo peer, DateTime now, TimeSpan maxAge) =>
        peer.LastSeen != default && now - peer.LastSeen <= maxAge;

    private static bool MatchesNameAndWorld(string inviterSegment, string name, string world) {
        if (inviterSegment.Length <= name.Length)
            return false;

        if (!inviterSegment.StartsWith(name, StringComparison.OrdinalIgnoreCase))
            return false;

        var remainder = inviterSegment[name.Length..].Trim();
        if (remainder.StartsWith('@'))
            remainder = remainder[1..].Trim();
        remainder = remainder.Trim('(', ')', '[', ']');

        return remainder.Equals(world, StringComparison.OrdinalIgnoreCase);
    }

    private static bool ContainsWorldName(string value, string world) {
        var normalizedValue = NormalizeNameWorldSegment(value);
        var normalizedWorld = NormalizeNameWorldSegment(world);
        return normalizedValue.Contains(normalizedWorld, StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeNameWorldSegment(string value) {
        var chars = value
            .Where(char.IsLetterOrDigit)
            .Select(char.ToLowerInvariant)
            .ToArray();
        return new string(chars);
    }

    private static string FormatPeer(PeerCharacterInfo peer) =>
        $"{peer.CharacterName}@{peer.HomeWorld}/{peer.CurrentWorld} cid={peer.ContentId}";

    private static (string Name, string World) SplitCharacterName(string characterName) {
        var trimmed = characterName.Trim();
        var separatorIndex = trimmed.LastIndexOf('@');
        if (separatorIndex <= 0 || separatorIndex == trimmed.Length - 1)
            return (trimmed, string.Empty);

        return (trimmed[..separatorIndex].Trim(), trimmed[(separatorIndex + 1)..].Trim());
    }
}

public sealed record PartyInviteExtractionExpression(
    string SourceText,
    string Prefix,
    string Suffix);

public sealed record PartyInvitePromptParseResult(
    bool IsPartyInvite,
    string PromptText,
    PartyInviteExtractionExpression Expression,
    string InviterSegment,
    bool SheetExpressionAvailable,
    string SheetExpressionReason,
    string Reason) {
    public static PartyInvitePromptParseResult Matched(
        string promptText,
        PartyInviteExtractionExpression expression,
        string inviterSegment) =>
        new(true, promptText, expression, inviterSegment, true, "party invite sheet expression parsed", "matched");

    public static PartyInvitePromptParseResult NotMatched(
        string promptText,
        PartyInviteExtractionExpression expression,
        string reason) =>
        new(false, promptText, expression, string.Empty, true, "party invite sheet expression parsed", reason);

    public static PartyInvitePromptParseResult NotMatched(
        string promptText,
        string addonRowText,
        string sheetExpressionReason) =>
        new(
            false,
            promptText,
            new PartyInviteExtractionExpression(addonRowText, string.Empty, string.Empty),
            string.Empty,
            false,
            sheetExpressionReason,
            sheetExpressionReason);
}

public sealed record PartyInviteDecision(
    bool ShouldAccept,
    PartyInvitePromptParseResult Parse,
    int ConnectedPeerCount,
    int FreshPeerCount,
    string MatchedPeer,
    string ConfigMatch,
    string Reason) {
    public static PartyInviteDecision Rejected(PartyInvitePromptParseResult parse, string reason) =>
        new(false, parse, 0, 0, string.Empty, string.Empty, reason);
}
