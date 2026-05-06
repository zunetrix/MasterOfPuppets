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

public readonly record struct AutoLoginWorldEntry(string Name, ushort WorldId, int SelectorIndex, int? CharacterCount);

public readonly record struct AutoLoginWorldQueueResult(List<string> Worlds, List<string> Diagnostics);

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
        IReadOnlyList<AutoLoginWorldEntry> visibleWorlds) =>
        BuildWorldQueueWithDiagnostics(candidates, lobbyEntries, visibleWorlds).Worlds;

    public static List<string> BuildWorldQueue(
        IReadOnlyList<AutoLoginCandidate> candidates,
        IReadOnlyList<AutoLoginLobbyEntry> lobbyEntries,
        IReadOnlyList<string> visibleWorlds) =>
        BuildWorldQueue(
            candidates,
            lobbyEntries,
            visibleWorlds.Select((world, index) => new AutoLoginWorldEntry(world, 0, index, null)).ToList());

    public static AutoLoginWorldQueueResult BuildWorldQueueWithDiagnostics(
        IReadOnlyList<AutoLoginCandidate> candidates,
        IReadOnlyList<AutoLoginLobbyEntry> lobbyEntries,
        IReadOnlyList<AutoLoginWorldEntry> visibleWorlds,
        IReadOnlyCollection<string>? fallbackWorldsToSkip = null) {
        var worldQueue = new List<string>();
        var diagnostics = new List<string>();
        var visibleWorldMap = BuildVisibleWorldMap(visibleWorlds);
        var fallbackWorldSkipSet = BuildWorldSet(fallbackWorldsToSkip);

        foreach (var candidate in candidates) {
            var entry = FindCandidateEntry(candidate, lobbyEntries);
            if (entry == null) {
                diagnostics.Add($"No lobby current-world entry for {FormatCandidate(candidate)}.");
                continue;
            }

            AddWorld(
                worldQueue,
                diagnostics,
                visibleWorldMap,
                entry.Value.CurrentWorldName,
                $"lobby current world for {FormatCandidate(candidate)}");
        }

        foreach (var candidate in candidates) {
            AddWorld(
                worldQueue,
                diagnostics,
                visibleWorldMap,
                candidate.HomeWorldName,
                $"configured home world for {FormatCandidate(candidate)}");
        }

        foreach (var world in visibleWorlds) {
            if (fallbackWorldSkipSet.Contains(world.Name)) {
                diagnostics.Add($"Skipped visible fallback '{world.Name}' at selector index {world.SelectorIndex}: already loaded as the current lobby world.");
                continue;
            }

            AddWorld(
                worldQueue,
                diagnostics,
                visibleWorldMap,
                world.Name,
                "visible fallback");
        }

        return new AutoLoginWorldQueueResult(worldQueue, diagnostics);
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

    private static Dictionary<string, AutoLoginWorldEntry> BuildVisibleWorldMap(IReadOnlyList<AutoLoginWorldEntry> visibleWorlds) {
        var result = new Dictionary<string, AutoLoginWorldEntry>(StringComparer.InvariantCultureIgnoreCase);
        foreach (var world in visibleWorlds) {
            if (string.IsNullOrWhiteSpace(world.Name) || result.ContainsKey(world.Name))
                continue;

            result.Add(world.Name, world);
        }

        return result;
    }

    private static HashSet<string> BuildWorldSet(IReadOnlyCollection<string>? worlds) {
        var result = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);
        if (worlds == null)
            return result;

        foreach (var world in worlds) {
            if (!string.IsNullOrWhiteSpace(world))
                result.Add(world);
        }

        return result;
    }

    private static void AddWorld(
        List<string> worldQueue,
        List<string> diagnostics,
        IReadOnlyDictionary<string, AutoLoginWorldEntry> visibleWorldMap,
        string worldName,
        string source) {
        if (string.IsNullOrWhiteSpace(worldName)) {
            diagnostics.Add($"Skipped blank world from {source}.");
            return;
        }

        if (!visibleWorldMap.TryGetValue(worldName, out var world)) {
            diagnostics.Add($"Skipped {source} '{worldName}': not present in the world selector.");
            return;
        }

        if (world.CharacterCount == 0) {
            diagnostics.Add($"Skipped {source} '{world.Name}' at selector index {world.SelectorIndex}: selector reports 0 characters.");
            return;
        }

        if (worldQueue.Contains(world.Name, StringComparer.InvariantCultureIgnoreCase)) {
            diagnostics.Add($"Skipped {source} '{world.Name}' at selector index {world.SelectorIndex}: already queued.");
            return;
        }

        worldQueue.Add(world.Name);
        var countText = world.CharacterCount == null ? "an unknown number of" : world.CharacterCount.ToString();
        diagnostics.Add($"Queued {source} '{world.Name}' at selector index {world.SelectorIndex} with {countText} characters.");
    }

    private static string FormatCandidate(AutoLoginCandidate candidate) =>
        string.IsNullOrWhiteSpace(candidate.HomeWorldName)
            ? $"{candidate.Name}({candidate.ContentId})"
            : $"{candidate.Name}@{candidate.HomeWorldName}({candidate.ContentId})";
}
