using System;
using System.Collections.Generic;
using System.Globalization;

using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.Types;

using MasterOfPuppets.Extensions.Dalamud;
using MasterOfPuppets.Formations;

namespace MasterOfPuppets.LuaScripting.Watches;

/// <summary>
/// One ambiguity-safe query contract for Lua player observation and reactions.
/// Numeric live IDs are exact. Names must identify exactly one visible player;
/// callers never inherit object-table ordering as an accidental policy.
/// </summary>
internal static class LuaActorQueryResolver {
    internal static LuaActorQueryResolution ResolvePlayer(
        IEnumerable<IGameObject> actors,
        string query,
        ulong excludedGameObjectId = 0) {
        ArgumentNullException.ThrowIfNull(actors);
        query = FormationCharacterName.NormalizeWorldSeparator(query ?? string.Empty).Trim();
        if (query.Length == 0)
            return new LuaActorQueryResolution("missing", null, 0);

        var numeric = ulong.TryParse(query, NumberStyles.None, CultureInfo.InvariantCulture, out var id)
            ? id
            : 0;
        IGameObject? match = null;
        var matches = 0;
        foreach (var actor in actors) {
            if (!IsUsablePlayer(actor) || actor.GameObjectId == excludedGameObjectId)
                continue;
            if (numeric != 0 && (actor.GameObjectId == numeric || actor.EntityId == numeric))
                return new LuaActorQueryResolution("found", actor, 1);
            var actorName = actor.GetPlayerNameWorld() ?? actor.Name.TextValue;
            if (!MatchesPlayerName(query, actorName))
                continue;
            match = actor;
            matches++;
        }

        return matches switch {
            0 => new LuaActorQueryResolution("missing", null, 0),
            1 => new LuaActorQueryResolution("found", match, 1),
            _ => new LuaActorQueryResolution("ambiguous", null, matches),
        };
    }

    internal static bool MatchesPlayerName(string query, string actorName) {
        query = FormationCharacterName.NormalizeWorldSeparator(query ?? string.Empty).Trim();
        actorName = FormationCharacterName.NormalizeWorldSeparator(actorName ?? string.Empty).Trim();
        if (query.Length == 0 || actorName.Length == 0)
            return false;

        // Supplying a world is an explicit disambiguation request. Never
        // degrade Name@World back to a base-name match on another world.
        if (query.Contains('@'))
            return query.Equals(actorName, StringComparison.OrdinalIgnoreCase);

        var queryBase = FormationCharacterName.GetBaseCharacterName(query);
        var actorBase = FormationCharacterName.GetBaseCharacterName(actorName);
        return actorBase.Equals(queryBase, StringComparison.OrdinalIgnoreCase)
            || actorBase.Contains(queryBase, StringComparison.OrdinalIgnoreCase)
            || queryBase.Contains(actorBase, StringComparison.OrdinalIgnoreCase);
    }

    internal static bool IsUsablePlayer(IGameObject? actor) =>
        actor != null
        && actor.Address != nint.Zero
        && actor.ObjectKind == ObjectKind.Pc
        && actor.EntityId is not (0 or 0xE0000000);
}

internal readonly record struct LuaActorQueryResolution(
    string Status,
    IGameObject? Actor,
    int MatchCount);
