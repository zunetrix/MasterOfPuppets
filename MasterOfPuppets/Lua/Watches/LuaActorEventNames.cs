using System;

namespace MasterOfPuppets.LuaScripting.Watches;

/// <summary>
/// Stable names shared by native/state producers and the Lua convenience API.
/// Keeping this contract in one place prevents a supported Lua kind from
/// silently waiting on a differently named producer event.
/// </summary>
internal static class LuaActorEventNames {
    internal const string Action = "actor.action";
    internal const string CombatAction = "actor.combat_action";
    internal const string GeneralAction = "actor.general_action";
    internal const string EmotePlayed = "actor.emote_played";

    private static readonly (LuaActorWatchChange Change, string Name)[] StateEventMappings = [
        (LuaActorWatchChange.Jump, "actor.jump"),
        (LuaActorWatchChange.Sprint, "actor.sprint"),
        (LuaActorWatchChange.Emote, "actor.emote_state"),
        (LuaActorWatchChange.Mount, "actor.mount"),
        (LuaActorWatchChange.Ornament, "actor.fashion_accessory"),
        (LuaActorWatchChange.Facewear, "actor.facewear"),
        (LuaActorWatchChange.Target, "actor.target"),
        (LuaActorWatchChange.Pose, "actor.idle_pose"),
        (LuaActorWatchChange.Weapon, "actor.weapon"),
    ];

    internal static ReadOnlySpan<(LuaActorWatchChange Change, string Name)> StateEvents =>
        StateEventMappings;

    internal static string ResolveKind(string kind) => kind switch {
        "action" => Action,
        "combat_action" or "combat" or "skill" => CombatAction,
        "general_action" or "general" => GeneralAction,
        "jump" => "actor.jump",
        "sprint" => "actor.sprint",
        "emote" or "emote_played" => EmotePlayed,
        "emote_state" => "actor.emote_state",
        "fashion_accessory" or "accessory" or "ornament" => "actor.fashion_accessory",
        "mount" => "actor.mount",
        "facewear" => "actor.facewear",
        "target" => "actor.target",
        "idle_pose" or "pose" => "actor.idle_pose",
        "weapon" or "weapon_state" => "actor.weapon",
        _ => throw new ArgumentException(
            "actor event kind must be action, combat_action, general_action, jump, sprint, emote, emote_state, fashion_accessory, mount, facewear, target, idle_pose, or weapon"),
    };

    internal static bool IsActionKind(string kind) =>
        kind is "action" or "combat_action" or "combat" or "skill"
            or "general_action" or "general";

    internal static bool IsEmoteEdgeKind(string kind) =>
        kind is "emote" or "emote_played";
}
