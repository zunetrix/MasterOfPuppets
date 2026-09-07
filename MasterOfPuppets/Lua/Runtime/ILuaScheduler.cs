using System;
using System.Threading;
using System.Threading.Tasks;

namespace MasterOfPuppets.LuaScripting.Runtime;

/// <summary>
/// Awaitable scheduling boundary for Lua host APIs. Tests can supply virtual
/// time without making a Lua run sleep on the wall clock.
/// </summary>
public interface ILuaScheduler {
    ValueTask DelayAsync(TimeSpan delay, CancellationToken cancellationToken);
}

public sealed class SystemLuaScheduler : ILuaScheduler {
    public static SystemLuaScheduler Instance { get; } = new();

    private SystemLuaScheduler() { }

    public ValueTask DelayAsync(TimeSpan delay, CancellationToken cancellationToken) =>
        new(Task.Delay(delay, cancellationToken));
}
