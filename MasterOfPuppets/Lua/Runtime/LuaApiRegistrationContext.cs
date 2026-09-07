using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;

using Lua;

using MasterOfPuppets.LuaScripting.Runs;

namespace MasterOfPuppets.LuaScripting.Runtime;

public sealed class LuaApiRegistrationContext {
    internal LuaApiRegistrationContext(
        LuaState state,
        LuaTable mop,
        LuaScriptContext script,
        Stopwatch clock,
        LuaRunQuota quota,
        LuaExecutionControl executionControl,
        Func<IReadOnlyList<LuaCapabilityDescriptor>> getCapabilities) {
        State = state;
        Mop = mop;
        Script = script;
        Clock = clock;
        Quota = quota;
        ExecutionControl = executionControl;
        GetCapabilities = getCapabilities;
    }

    public LuaState State { get; }
    public LuaTable Mop { get; }
    public LuaScriptContext Script { get; }
    public Stopwatch Clock { get; }
    public LuaRunQuota Quota { get; }
    public LuaExecutionControl ExecutionControl { get; }
    public Func<IReadOnlyList<LuaCapabilityDescriptor>> GetCapabilities { get; }
    public double LocalMonotonicSeconds {
        get {
            var value = Script.LocalTimeSeconds?.Invoke() ?? Clock.Elapsed.TotalSeconds;
            return double.IsFinite(value) ? value : Clock.Elapsed.TotalSeconds;
        }
    }
    public double ChoreographySeconds {
        get {
            var value = Script.SharedTimeSeconds?.Invoke() ?? Clock.Elapsed.TotalSeconds;
            return double.IsFinite(value) ? value : Clock.Elapsed.TotalSeconds;
        }
    }

    public ValueTask DelayAsync(TimeSpan delay, System.Threading.CancellationToken cancellationToken) =>
        (Script.Scheduler ?? SystemLuaScheduler.Instance).DelayAsync(delay, cancellationToken);

    public LuaTable GetOrCreateModule(string name) {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (Mop.TryGetValue(name, out var existing) && existing.TryRead<LuaTable>(out var table))
            return table;
        table = new LuaTable();
        Mop[name] = table;
        return table;
    }
}
