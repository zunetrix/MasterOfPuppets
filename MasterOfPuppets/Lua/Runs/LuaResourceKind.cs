using System;

namespace MasterOfPuppets.LuaScripting.Runs;

[Flags]
public enum LuaResourceKind {
    None = 0,
    Movement = 1 << 0,
    ChatActionBudget = 1 << 1,
    MacroQueue = 1 << 2,
    FormationTracking = 1 << 3,
    SynchronizedControl = 1 << 4,
    GameActions = 1 << 5,
}

public static class LuaResourceKinds {
    public const LuaResourceKind LegacyExclusive =
        LuaResourceKind.Movement
        | LuaResourceKind.ChatActionBudget
        | LuaResourceKind.MacroQueue
        | LuaResourceKind.FormationTracking
        | LuaResourceKind.SynchronizedControl
        | LuaResourceKind.GameActions;

    public static LuaResourceKind ValidateMask(LuaResourceKind resources) {
        if ((resources & ~LegacyExclusive) != 0)
            throw new ArgumentOutOfRangeException(nameof(resources), $"Unknown Lua resource bits: {resources & ~LegacyExclusive}");
        return resources;
    }
}
