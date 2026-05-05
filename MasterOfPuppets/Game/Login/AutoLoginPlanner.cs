using System;
using System.Collections.Generic;
using System.Linq;

namespace MasterOfPuppets;

public readonly record struct AutoLoginCandidate(ulong ContentId, string Name, string HomeWorldName = "") {
    public static AutoLoginCandidate? FromCharacter(Character character) {
        var (name, homeWorldName) = ExtractLoginNameWorld(character.Name);
        return name == null ? null : new AutoLoginCandidate(character.Cid, name, homeWorldName);
    }

    public static string? ExtractLoginName(string fullName) {
        var (name, _) = ExtractLoginNameWorld(fullName);
        return name;
    }

    public static (string? Name, string HomeWorldName) ExtractLoginNameWorld(string fullName) {
        if (string.IsNullOrWhiteSpace(fullName))
            return (null, string.Empty);

        var separatorIndex = fullName.LastIndexOf('@');
        var name = separatorIndex > 0
            ? fullName[..separatorIndex].Trim()
            : fullName.Trim();
        var homeWorldName = separatorIndex > 0 && separatorIndex < fullName.Length - 1
            ? fullName[(separatorIndex + 1)..].Trim()
            : string.Empty;

        return string.IsNullOrWhiteSpace(name) ? (null, string.Empty) : (name, homeWorldName);
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
    public static List<AutoLoginCandidate> BuildCandidates(IEnumerable<Character> characters) {
        return characters
            .Where(character => character.AutoLoginEnabled)
            .Select(AutoLoginCandidate.FromCharacter)
            .Where(candidate => candidate != null)
            .Select(candidate => candidate!.Value)
            .ToList();
    }

    public static bool HasEnabledCandidates(IEnumerable<Character> characters) =>
        BuildCandidates(characters).Count > 0;

    public static List<string> BuildWorldQueue(
        IReadOnlyList<AutoLoginCandidate> candidates,
        IReadOnlyList<AutoLoginLobbyEntry> lobbyEntries,
        IReadOnlyList<string> visibleWorlds) {
        var worldQueue = new List<string>();
        var visibleWorldSet = visibleWorlds.ToHashSet(StringComparer.InvariantCultureIgnoreCase);

        foreach (var candidate in candidates) {
            var entry = FindCandidateEntry(candidate, lobbyEntries);
            if (entry == null)
                continue;

            AddWorld(worldQueue, visibleWorldSet, entry.Value.CurrentWorldName);
        }

        foreach (var world in visibleWorlds)
            AddWorld(worldQueue, visibleWorldSet, world);

        return worldQueue;
    }

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

        for (var i = 0; i < visibleCharacters.Count; i++) {
            var candidate = FindCandidateByVisibleName(candidates, visibleCharacters[i]);
            if (candidate == null)
                continue;

            var entry = FindCandidateEntry(candidate.Value, lobbyEntries);
            var worldName = !string.IsNullOrWhiteSpace(entry?.CurrentWorldName)
                ? entry.Value.CurrentWorldName
                : string.Empty;
            target = new AutoLoginTarget(candidate.Value.Name, worldName);
            index = i;
            reason = entry == null
                ? lobbyEntries.Count == 0
                    ? $"lobby data was unavailable; resolved visible whitelisted character '{candidate.Value.Name}'"
                    : $"resolved visible whitelisted character '{candidate.Value.Name}' without lobby confirmation"
                : string.IsNullOrWhiteSpace(entry.Value.CurrentWorldName)
                    ? $"resolved visible whitelisted character '{candidate.Value.Name}' from lobby data without current world"
                    : $"resolved visible whitelisted character '{candidate.Value.Name}' from lobby data";
            return true;
        }

        reason = lobbyEntries.Count == 0
            ? "lobby data was unavailable and no configured character was visible"
            : "no visible whitelisted character matched the configured whitelist";
        return false;
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

    private static void AddWorld(List<string> worldQueue, HashSet<string> visibleWorldSet, string worldName) {
        if (string.IsNullOrWhiteSpace(worldName) || !visibleWorldSet.Contains(worldName))
            return;

        if (!worldQueue.Contains(worldName, StringComparer.InvariantCultureIgnoreCase))
            worldQueue.Add(worldName);
    }
}
