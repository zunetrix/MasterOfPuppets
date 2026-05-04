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
    public static bool TryResolveDirectCharacterListTarget(
        IReadOnlyList<AutoLoginCandidate> candidates,
        IReadOnlyList<AutoLoginLobbyEntry> lobbyEntries,
        IReadOnlyList<string> visibleCharacters,
        out AutoLoginTarget target,
        out int index,
        out string reason) {
        target = default;
        index = -1;

        if (candidates.Count == 0) {
            reason = "no enabled auto-login candidates were configured";
            return false;
        }

        if (lobbyEntries.Count == 0) {
            if (TryFindVisibleCandidate(candidates, visibleCharacters, out var visibleCandidate, out index)) {
                target = new AutoLoginTarget(visibleCandidate, string.Empty);
                reason = "lobby data was unavailable; fell back to visible configured character";
                return true;
            }

            reason = "lobby data was unavailable and no configured character was visible";
            return false;
        }

        var visibleConfiguredWithoutWorld = string.Empty;
        for (var i = 0; i < visibleCharacters.Count; i++) {
            var candidate = FindCandidateByVisibleName(candidates, visibleCharacters[i]);
            if (candidate == null)
                continue;

            var entry = FindCandidateEntry(candidate.Value, lobbyEntries);
            if (entry == null)
                continue;

            target = new AutoLoginTarget(entry.Value.Name, entry.Value.CurrentWorldName);
            if (string.IsNullOrWhiteSpace(entry.Value.CurrentWorldName)) {
                visibleConfiguredWithoutWorld = entry.Value.Name;
                continue;
            }

            index = i;
            reason = $"resolved visible whitelisted character '{entry.Value.Name}' from lobby data";
            return true;
        }

        if (!string.IsNullOrWhiteSpace(visibleConfiguredWithoutWorld)) {
            reason = $"lobby data for visible whitelisted character '{visibleConfiguredWithoutWorld}' did not include a current world";
            return false;
        }

        reason = "lobby data did not contain any visible whitelisted character";
        return false;
    }

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

    private static AutoLoginCandidate? FindCandidateByVisibleName(
        IReadOnlyList<AutoLoginCandidate> candidates,
        string visibleCharacterName) {
        foreach (var candidate in candidates) {
            if (candidate.Name.Equals(visibleCharacterName, StringComparison.InvariantCultureIgnoreCase))
                return candidate;
        }

        return null;
    }
}
