using System;

namespace MasterOfPuppets.LuaScripting.Synchronization;

/// <summary>
/// Keeps distributed Lua housekeeping off the render-rate hot path while
/// retaining responsive staging and conservative heartbeat timeout checks.
/// </summary>
internal sealed class LuaFrameworkUpdateCadence {
    internal const long LaunchUpdateIntervalMs = 50;
    internal const long SessionTickIntervalMs = 250;

    private long _nextLaunchUpdateMs;
    private long _nextSessionTickMs;

    public bool ShouldUpdateLaunches(long nowMs) =>
        ShouldRun(nowMs, LaunchUpdateIntervalMs, ref _nextLaunchUpdateMs);

    public bool ShouldTickSessions(long nowMs) =>
        ShouldRun(nowMs, SessionTickIntervalMs, ref _nextSessionTickMs);

    private static bool ShouldRun(long nowMs, long intervalMs, ref long nextMs) {
        if (nowMs < 0)
            throw new ArgumentOutOfRangeException(nameof(nowMs));
        if (nowMs < nextMs)
            return false;
        nextMs = nowMs + intervalMs;
        return true;
    }
}
