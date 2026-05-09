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

public enum AutoLoginMatchConfidence {
    None,
    ContentId,
    NameAndHomeWorld,
    NameOnConfiguredHomeWorld,
}

public readonly record struct AutoLoginCharacterMatch(
    AutoLoginCandidate Candidate,
    AutoLoginTarget Target,
    int Index,
    AutoLoginMatchConfidence Confidence,
    string Reason);

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
                var sameNameEntries = FindSameNameEntries(candidate, lobbyEntries);
                diagnostics.Add(sameNameEntries.Count == 0
                    ? $"No lobby current-world entry for {FormatCandidate(candidate)}."
                    : $"Ignored same-name lobby entries for {FormatCandidate(candidate)}: no content ID or home-world confirmation. Entries: {FormatLobbyEntries(sameNameEntries)}.");
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
        if (TryResolveDirectCharacterListMatch(candidates, lobbyEntries, visibleCharacters, out var match, out reason)) {
            target = match.Target;
            index = match.Index;
            return true;
        }

        target = default;
        index = -1;
        return false;
    }

    public static bool TryResolveDirectCharacterListMatch(
        IReadOnlyList<AutoLoginCandidate> candidates,
        IReadOnlyList<AutoLoginLobbyEntry> lobbyEntries,
        IReadOnlyList<string> visibleCharacters,
        out AutoLoginCharacterMatch match,
        out string reason) {
        if (TryResolveVisibleCandidate(
                candidates,
                lobbyEntries,
                visibleCharacters,
                selectedWorldName: string.Empty,
                allowConfiguredHomeWorldFallback: false,
                out match,
                out reason)) {
            return true;
        }

        match = default;
        return false;
    }

    public static bool TryFindVisibleCandidate(
        IReadOnlyList<AutoLoginCandidate> candidates,
        IReadOnlyList<AutoLoginLobbyEntry> lobbyEntries,
        IReadOnlyList<string> visibleCharacters,
        string selectedWorldName,
        out string characterName,
        out int index,
        out string reason) {
        if (TryFindVisibleCandidateMatch(
                candidates,
                lobbyEntries,
                visibleCharacters,
                selectedWorldName,
                out var match,
                out reason)) {
            characterName = match.Target.CharacterName;
            index = match.Index;
            return true;
        }

        characterName = string.Empty;
        index = -1;
        return false;
    }

    public static bool TryFindVisibleCandidateMatch(
        IReadOnlyList<AutoLoginCandidate> candidates,
        IReadOnlyList<AutoLoginLobbyEntry> lobbyEntries,
        IReadOnlyList<string> visibleCharacters,
        string selectedWorldName,
        out AutoLoginCharacterMatch match,
        out string reason) {
        if (TryResolveVisibleCandidate(
                candidates,
                lobbyEntries,
                visibleCharacters,
                selectedWorldName,
                allowConfiguredHomeWorldFallback: true,
                out match,
                out reason)) {
            return true;
        }

        match = default;
        return false;
    }

    public static bool TryResolveVisibleCandidate(
        IReadOnlyList<AutoLoginCandidate> candidates,
        IReadOnlyList<AutoLoginLobbyEntry> lobbyEntries,
        IReadOnlyList<string> visibleCharacters,
        string selectedWorldName,
        bool allowConfiguredHomeWorldFallback,
        out AutoLoginCharacterMatch match,
        out string reason) {
        match = default;

        if (candidates.Count == 0) {
            reason = "no enabled auto-login candidates were configured";
            return false;
        }

        var skipReasons = new List<string>();
        for (var i = 0; i < visibleCharacters.Count; i++) {
            var visibleName = visibleCharacters[i];
            foreach (var candidate in candidates.Where(candidate =>
                         candidate.Name.Equals(visibleName, StringComparison.InvariantCultureIgnoreCase))) {
                var entry = FindCandidateEntry(candidate, lobbyEntries);
                if (entry != null && IsConfirmedCandidate(candidate, entry.Value, out var confidence, out var confirmationReason)) {
                    var worldName = !string.IsNullOrWhiteSpace(entry.Value.CurrentWorldName)
                        ? entry.Value.CurrentWorldName
                        : selectedWorldName;
                    var target = new AutoLoginTarget(candidate.Name, worldName);
                    match = new AutoLoginCharacterMatch(candidate, target, i, confidence, confirmationReason);
                    reason = confirmationReason;
                    return true;
                }

                if (entry != null) {
                    skipReasons.Add($"skipped visible '{visibleName}': lobby entry did not match configured identity for {FormatCandidate(candidate)}. Entry: {FormatLobbyEntry(entry.Value)}");
                    continue;
                }

                var sameNameEntries = FindSameNameEntries(candidate, lobbyEntries);
                if (sameNameEntries.Count > 0) {
                    skipReasons.Add($"skipped visible '{visibleName}': same-name lobby entries did not match configured identity for {FormatCandidate(candidate)}. Entries: {FormatLobbyEntries(sameNameEntries)}");
                    continue;
                }

                if (allowConfiguredHomeWorldFallback &&
                    !string.IsNullOrWhiteSpace(candidate.HomeWorldName) &&
                    candidate.HomeWorldName.Equals(selectedWorldName, StringComparison.InvariantCultureIgnoreCase)) {
                    var target = new AutoLoginTarget(candidate.Name, selectedWorldName);
                    reason = $"resolved visible whitelisted character '{candidate.Name}' on configured home world '{selectedWorldName}' without lobby confirmation";
                    match = new AutoLoginCharacterMatch(candidate, target, i, AutoLoginMatchConfidence.NameOnConfiguredHomeWorld, reason);
                    return true;
                }

                skipReasons.Add(
                    string.IsNullOrWhiteSpace(candidate.HomeWorldName)
                        ? $"skipped visible '{visibleName}': no lobby confirmation and configured home world is unknown for {FormatCandidate(candidate)}"
                        : $"skipped visible '{visibleName}': no lobby confirmation and selected world '{selectedWorldName}' is not configured home world '{candidate.HomeWorldName}'");
            }
        }

        reason = skipReasons.Count == 0
            ? lobbyEntries.Count == 0
                ? "lobby data was unavailable and no configured character was visible"
                : "no visible whitelisted character matched the configured whitelist"
            : string.Join("; ", skipReasons.Distinct());
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
            if (entry.Name.Equals(candidate.Name, StringComparison.InvariantCultureIgnoreCase) &&
                !string.IsNullOrWhiteSpace(candidate.HomeWorldName) &&
                entry.HomeWorldName.Equals(candidate.HomeWorldName, StringComparison.InvariantCultureIgnoreCase))
                return entry;
        }

        return null;
    }

    private static List<AutoLoginLobbyEntry> FindSameNameEntries(
        AutoLoginCandidate candidate,
        IReadOnlyList<AutoLoginLobbyEntry> lobbyEntries) =>
        lobbyEntries
            .Where(entry => entry.Name.Equals(candidate.Name, StringComparison.InvariantCultureIgnoreCase))
            .ToList();

    private static bool IsConfirmedCandidate(
        AutoLoginCandidate candidate,
        AutoLoginLobbyEntry entry,
        out AutoLoginMatchConfidence confidence,
        out string reason) {
        confidence = AutoLoginMatchConfidence.None;
        reason = string.Empty;

        if (!entry.Name.Equals(candidate.Name, StringComparison.InvariantCultureIgnoreCase))
            return false;

        if (candidate.ContentId != 0 && entry.ContentId == candidate.ContentId) {
            confidence = AutoLoginMatchConfidence.ContentId;
            reason = $"resolved visible whitelisted character '{candidate.Name}' by content ID";
            return true;
        }

        if (!string.IsNullOrWhiteSpace(candidate.HomeWorldName) &&
            entry.HomeWorldName.Equals(candidate.HomeWorldName, StringComparison.InvariantCultureIgnoreCase)) {
            confidence = AutoLoginMatchConfidence.NameAndHomeWorld;
            reason = $"resolved visible whitelisted character '{candidate.Name}' by name and home world '{candidate.HomeWorldName}'";
            return true;
        }

        return false;
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

    private static string FormatLobbyEntries(IReadOnlyList<AutoLoginLobbyEntry> entries) =>
        entries.Count == 0
            ? "<empty>"
            : string.Join("; ", entries.Select(FormatLobbyEntry));

    private static string FormatLobbyEntry(AutoLoginLobbyEntry entry) =>
        $"{entry.Name}@{entry.HomeWorldName}/{entry.CurrentWorldName}({entry.ContentId})";
}
