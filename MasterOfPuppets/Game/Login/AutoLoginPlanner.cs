using System;
using System.Collections.Generic;
using System.Linq;

namespace MasterOfPuppets;

public readonly record struct AutoLoginCandidate(ulong ContentId, string Name) {
    public static AutoLoginCandidate? FromCharacter(Character character) {
        var name = ExtractLoginName(character.Name);
        return name == null ? null : new AutoLoginCandidate(character.Cid, name);
    }

    public static string? ExtractLoginName(string fullName) {
        if (string.IsNullOrWhiteSpace(fullName))
            return null;

        var separatorIndex = fullName.LastIndexOf('@');
        var name = separatorIndex > 0
            ? fullName[..separatorIndex].Trim()
            : fullName.Trim();

        return string.IsNullOrWhiteSpace(name) ? null : name;
    }
}

public readonly record struct AutoLoginLobbyEntry(
    ulong ContentId,
    string Name,
    string CurrentWorldName,
    ushort CurrentWorldId,
    string HomeWorldName,
    ushort HomeWorldId,
    string RawJson,
    string ClientSelectWorldName);

public readonly record struct AutoLoginTarget(string CharacterName, string WorldName);

public static class AutoLoginPlanner {
    public static AutoLoginTarget? ResolveTarget(
        IReadOnlyList<AutoLoginCandidate> candidates,
        IReadOnlyList<AutoLoginLobbyEntry> lobbyEntries) {
        foreach (var candidate in candidates) {
            var entry = FindCandidateEntry(candidate, lobbyEntries);
            if (entry == null || string.IsNullOrWhiteSpace(entry.Value.CurrentWorldName))
                continue;

            return new AutoLoginTarget(entry.Value.Name, entry.Value.CurrentWorldName);
        }

        return null;
    }

    public static bool TryFindVisibleCandidate(
        IReadOnlyList<AutoLoginCandidate> candidates,
        IReadOnlyList<string> visibleCharacters,
        out string characterName,
        out int index) {
        var candidateNames = candidates.Select(c => c.Name).ToHashSet(StringComparer.InvariantCultureIgnoreCase);
        index = -1;
        for (var i = 0; i < visibleCharacters.Count; i++) {
            if (!candidateNames.Contains(visibleCharacters[i]))
                continue;

            characterName = visibleCharacters[i];
            index = i;
            return true;
        }

        characterName = string.Empty;
        return false;
    }

    private static AutoLoginLobbyEntry? FindCandidateEntry(
        AutoLoginCandidate candidate,
        IReadOnlyList<AutoLoginLobbyEntry> lobbyEntries) {
        if (candidate.ContentId != 0) {
            var cidEntry = lobbyEntries.FirstOrDefault(entry => entry.ContentId == candidate.ContentId);
            if (cidEntry.ContentId != 0)
                return cidEntry;
        }

        foreach (var entry in lobbyEntries) {
            if (entry.Name.Equals(candidate.Name, StringComparison.InvariantCultureIgnoreCase))
                return entry;
        }

        return null;
    }
}
