using System;
using System.Collections.Generic;
using System.Linq;

namespace MasterOfPuppets;

public static class PartyInvitePromptMatcher {
    private const string PartyInvitePrefix = "Join ";
    private const string PartyInviteSuffix = "'s party?";

    public static bool ShouldAcceptPartyInvite(
        string promptText,
        bool onlyFromCharacters,
        IEnumerable<string> characterNames) {
        if (!TryGetPartyInviteInviterSegment(promptText, out _))
            return false;

        return !onlyFromCharacters || IsInviterInCharacterList(promptText, characterNames);
    }

    public static bool IsInviterInCharacterList(string promptText, IEnumerable<string> characterNames) {
        if (!TryGetPartyInviteInviterSegment(promptText, out var inviterSegment))
            return false;

        return characterNames
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Any(name => MatchesCharacter(inviterSegment, name));
    }

    public static bool TryGetPartyInviteInviterSegment(string promptText, out string inviterSegment) {
        inviterSegment = string.Empty;

        if (string.IsNullOrWhiteSpace(promptText))
            return false;

        var text = promptText.Trim();
        if (!text.StartsWith(PartyInvitePrefix, StringComparison.OrdinalIgnoreCase))
            return false;

        var end = text.IndexOf(PartyInviteSuffix, PartyInvitePrefix.Length, StringComparison.OrdinalIgnoreCase);
        if (end <= PartyInvitePrefix.Length)
            return false;

        inviterSegment = text[PartyInvitePrefix.Length..end].Trim();
        return !string.IsNullOrWhiteSpace(inviterSegment);
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

    private static (string Name, string World) SplitCharacterName(string characterName) {
        var trimmed = characterName.Trim();
        var separatorIndex = trimmed.LastIndexOf('@');
        if (separatorIndex <= 0 || separatorIndex == trimmed.Length - 1)
            return (trimmed, string.Empty);

        return (trimmed[..separatorIndex].Trim(), trimmed[(separatorIndex + 1)..].Trim());
    }
}
