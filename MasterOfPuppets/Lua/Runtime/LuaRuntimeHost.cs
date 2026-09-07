using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

using Lua;
using Lua.Standard;

using MasterOfPuppets.LuaScripting.Runs;

namespace MasterOfPuppets.LuaScripting.Runtime;

/// <summary>
/// Creates and owns one sandboxed Lua state. Providers are discovered once per
/// host and register typed functions; user Lua never receives reflection or CLR access.
/// </summary>
internal sealed class LuaRuntimeHost : IDisposable {
    private readonly LuaState _state;
    private readonly Stopwatch _clock = new();

    public LuaRuntimeHost(LuaScriptContext context, LuaCapabilityRegistry? registry = null) {
        ArgumentNullException.ThrowIfNull(context);
        _state = LuaState.Create();
        _state.ModuleLoader = new LuaInMemoryModuleLoader(context.Modules);
        var quota = new LuaRunQuota(context.Limits);
        var limits = (context.Limits ?? LuaRuntimeLimits.Default).Validate();
        var executionControl = context.ExecutionControl ?? new LuaExecutionControl();
        OpenSafeLibraries(_state, context, quota);
        InstallExecutionHook(_state, executionControl, limits);

        registry = (registry ?? LuaCapabilityRegistry.Discover()).SelectForRun(context.DeclaredCapabilities);
        var mop = new LuaTable();
        var registration = new LuaApiRegistrationContext(
            _state,
            mop,
            context,
            _clock,
            quota,
            executionControl,
            () => registry.Descriptors);
        registry.RegisterAll(registration);
        _state.Environment["mop"] = mop;
    }

    public async Task RunAsync(string source, CancellationToken cancellationToken) {
        ArgumentException.ThrowIfNullOrWhiteSpace(source);
        LuaScriptValidator.ThrowIfInvalid(source);
        _clock.Restart();
        await _state.DoStringAsync(source, LuaScriptValidator.DefaultChunkName, cancellationToken);
    }

    private static void OpenSafeLibraries(LuaState state, LuaScriptContext context, LuaRunQuota quota) {
        state.OpenBasicLibrary();
        state.OpenBitwiseLibrary();
        state.OpenCoroutineLibrary();
        state.OpenMathLibrary();
        state.OpenModuleLibrary();
        state.OpenStringLibrary();
        state.OpenTableLibrary();
        InstallDeterministicRandom(state, context.Seed);

        // BasicLibrary includes disk-backed and dynamic compilation helpers. The
        // V2 sandbox deliberately removes them after opening the safe base subset.
        state.Environment["dofile"] = LuaValue.Nil;
        state.Environment["loadfile"] = LuaValue.Nil;
        state.Environment["load"] = LuaValue.Nil;
        if (state.Environment.TryGetValue("package", out var packageValue)
            && packageValue.TryRead<LuaTable>(out var package))
            package["searchers"] = new LuaTable();
        state.Environment["package"] = LuaValue.Nil;
        state.Environment["print"] = new LuaFunction((call, _) => {
            var values = new string[call.ArgumentCount];
            for (var index = 0; index < values.Length; index++)
                values[index] = call.GetArgument<LuaValue>(index).ToString();
            quota.WriteLog(string.Join('\t', values), context.Log);
            return new ValueTask<int>(call.Return());
        });
    }

    private static void InstallDeterministicRandom(LuaState state, int seed) {
        if (!state.Environment.TryGetValue("math", out var mathValue)
            || !mathValue.TryRead<LuaTable>(out var math))
            throw new InvalidOperationException("Lua math library did not initialize.");

        var random = new LuaDeterministicRandom(seed);
        math["random"] = new LuaFunction((call, _) => {
            if (call.ArgumentCount == 0)
                return new ValueTask<int>(call.Return(random.NextDouble()));
            var first = ReadInteger(call, 0, "math.random");
            if (call.ArgumentCount == 1) {
                if (first < 1)
                    throw new ArgumentOutOfRangeException(nameof(first), "math.random upper bound must be at least 1");
                return new ValueTask<int>(call.Return((double)random.NextInt64(1, first)));
            }
            if (call.ArgumentCount == 2) {
                var second = ReadInteger(call, 1, "math.random");
                return new ValueTask<int>(call.Return((double)random.NextInt64(first, second)));
            }
            throw new ArgumentException("math.random accepts zero, one, or two arguments");
        });
        math["randomseed"] = new LuaFunction((call, _) => {
            random.Reset(ReadInteger(call, 0, "math.randomseed"));
            return new ValueTask<int>(call.Return());
        });
    }

    private static void InstallExecutionHook(
        LuaState state,
        LuaExecutionControl control,
        LuaRuntimeLimits limits) {
        state.SetHook(new LuaFunction(async (call, cancellationToken) => {
            await control.CheckpointAsync(
                limits.InstructionHookInterval,
                state.CallStackFrameCount,
                limits,
                cancellationToken);
            return call.Return();
        }), string.Empty, limits.InstructionHookInterval);
    }

    private static long ReadInteger(LuaFunctionExecutionContext call, int index, string function) {
        var number = call.GetArgument<double>(index);
        if (!double.IsFinite(number) || number != Math.Truncate(number) || number < long.MinValue || number > long.MaxValue)
            throw new ArgumentException($"{function} requires integer arguments");
        return (long)number;
    }

    public void Dispose() {
        _clock.Stop();
        _state.Dispose();
    }
}
