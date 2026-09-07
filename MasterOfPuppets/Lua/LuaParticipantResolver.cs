using System;
using System.Collections.Generic;
using System.Linq;

using Dalamud.Game.ClientState.Objects.Types;

using MasterOfPuppets.Extensions.Dalamud;
using MasterOfPuppets.Formations;

namespace MasterOfPuppets.LuaScripting;

public static class LuaParticipantResolver {
    public static bool TryResolve(
        LuaScriptDefinition script,
        IReadOnlyList<Formation> formations,
        IReadOnlyList<CidGroup> groups,
        out IReadOnlyList<ulong> participantCids,
        out string error) {
        var formationName = script.ParticipantFormation?.Trim() ?? string.Empty;
        if (formationName.Length == 0) {
            participantCids = Array.Empty<ulong>();
            error = $"Lua script '{script.Name}' has no Participant Formation assigned.";
            return false;
        }

        var formation = formations.FirstOrDefault(candidate =>
            candidate.Name.Equals(formationName, StringComparison.OrdinalIgnoreCase));
        if (formation == null) {
            participantCids = Array.Empty<ulong>();
            error = $"Participant Formation '{formationName}' was not found.";
            return false;
        }

        var ordered = new List<ulong>();
        var seen = new HashSet<ulong>();
        foreach (var point in formation.Points) {
            foreach (var cid in point.Cids ?? new List<ulong>())
                Add(cid);
            foreach (var groupName in point.GroupIds ?? new List<string>()) {
                var group = groups.FirstOrDefault(candidate =>
                    candidate.Name.Equals(groupName, StringComparison.OrdinalIgnoreCase));
                if (group == null)
                    continue;
                foreach (var cid in group.Cids)
                    Add(cid);
            }
        }

        participantCids = ordered;
        if (ordered.Count == 0) {
            error = $"Participant Formation '{formation.Name}' has no assigned characters.";
            return false;
        }
        error = string.Empty;
        return true;

        void Add(ulong cid) {
            if (cid != 0 && seen.Add(cid))
                ordered.Add(cid);
        }
    }

    /// <summary>
    /// Freezes a launch roster to members who are locally visible (or otherwise
    /// known online). Order is preserved so every client that receives this list
    /// assigns the same compact slots.
    /// </summary>
    public static IReadOnlyList<ulong> CompactVisible(
        IReadOnlyList<ulong> roster,
        IReadOnlyDictionary<ulong, string> configuredNames,
        IReadOnlyCollection<ulong> alwaysInclude) {
        var always = alwaysInclude
            .Where(cid => cid != 0)
            .ToHashSet();
        var compacted = new List<ulong>();
        var seen = new HashSet<ulong>();
        foreach (var cid in roster) {
            if (cid == 0 || !seen.Add(cid))
                continue;
            configuredNames.TryGetValue(cid, out var name);
            if (always.Contains(cid) || IsVisible(cid, name))
                compacted.Add(cid);
        }

        return compacted.Count > 0
            ? compacted
            : roster.Where(cid => cid != 0).Distinct().ToArray();
    }

    public static IReadOnlyDictionary<ulong, string> CharacterNames(IEnumerable<Character> characters) =>
        characters
            .Where(character => character.Cid != 0)
            .GroupBy(character => character.Cid)
            .ToDictionary(group => group.Key, group => group.First().Name ?? string.Empty);

    public static bool IsVisible(ulong cid, string? configuredName) {
        if (cid != 0) {
            var party = DalamudApi.PartyList.FirstOrDefault(member => member.ContentId == cid);
            if (party?.GameObject is { Address: not 0 })
                return true;
        }

        if (string.IsNullOrWhiteSpace(configuredName))
            return false;

        foreach (var actor in DalamudApi.ObjectTable) {
            if (actor is not { Address: not 0 } || actor.ObjectKind != Dalamud.Game.ClientState.Objects.Enums.ObjectKind.Pc)
                continue;
            var actorName = actor.GetPlayerNameWorld() ?? actor.Name.TextValue;
            if (FormationCharacterName.Matches(configuredName, actorName))
                return true;
        }

        return false;
    }
}
