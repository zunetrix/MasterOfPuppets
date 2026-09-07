using System;

namespace MasterOfPuppets.LuaScripting.Runtime;

public sealed record LuaRuntimeLimits {
    public static LuaRuntimeLimits Default { get; } = new();

    public int MaximumLogLines { get; init; } = 1000;
    public int MaximumLogUtf8Bytes { get; init; } = 256 * 1024;
    public int MaximumPendingWaiters { get; init; } = 32;
    public int MaximumChatActions { get; init; } = 2000;
    public int MaximumChatActionsPerWindow { get; init; } = 30;
    public TimeSpan ChatActionWindow { get; init; } = TimeSpan.FromSeconds(5);
    public int MaximumGameActions { get; init; } = 50_000;
    public int MaximumGameActionsPerWindow { get; init; } = 200;
    public TimeSpan GameActionWindow { get; init; } = TimeSpan.FromSeconds(5);
    public long MaximumInstructions { get; init; } = 50_000_000;
    public int InstructionHookInterval { get; init; } = 10_000;
    public int MaximumCallStackFrames { get; init; } = 256;

    public LuaRuntimeLimits Validate() {
        if (MaximumLogLines <= 0 || MaximumLogUtf8Bytes <= 0 || MaximumPendingWaiters <= 0
            || MaximumChatActions <= 0 || MaximumChatActionsPerWindow <= 0
            || ChatActionWindow <= TimeSpan.Zero
            || MaximumGameActions <= 0 || MaximumGameActionsPerWindow <= 0
            || GameActionWindow <= TimeSpan.Zero || MaximumInstructions <= 0
            || InstructionHookInterval <= 0 || MaximumCallStackFrames <= 0)
            throw new ArgumentOutOfRangeException(nameof(LuaRuntimeLimits), "Lua runtime limits must all be positive.");
        return this;
    }
}
