using System;
using System.Collections.Generic;
using System.Linq;

namespace MasterOfPuppets;

public readonly record struct AutoLoginCandidate(ulong ContentId, string Name) {
    public static AutoLoginCandidate? FromCharacter(global::Character character) {
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

public static class AutoLoginPlanner {
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

        foreach (var entry in lobbyEntries)
            AddWorld(worldQueue, visibleWorldSet, entry.CurrentWorldName);

        if (lobbyEntries.Count == 0)
            foreach (var world in visibleWorlds)
                AddWorld(worldQueue, visibleWorldSet, world);

        return worldQueue;
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

    private static void AddWorld(List<string> worldQueue, HashSet<string> visibleWorldSet, string worldName) {
        if (string.IsNullOrWhiteSpace(worldName) || !visibleWorldSet.Contains(worldName))
            return;

        if (!worldQueue.Contains(worldName, StringComparer.InvariantCultureIgnoreCase))
            worldQueue.Add(worldName);
    }
}
