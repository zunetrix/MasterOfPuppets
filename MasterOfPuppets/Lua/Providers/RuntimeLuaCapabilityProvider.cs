using System;
using System.Linq;
using System.Threading.Tasks;

using Lua;

using MasterOfPuppets.LuaScripting.Runs;
using MasterOfPuppets.LuaScripting.Runtime;

namespace MasterOfPuppets.LuaScripting.Providers;

public sealed class RuntimeLuaCapabilityProvider : ILuaCapabilityProvider {
    public const string ApiVersion = "4.0.0";

    private static readonly LuaCapabilityDescriptor Capability = new(
        "mop.runtime",
        ApiVersion,
        "Versioned runtime metadata, lifecycle state, and capability discovery.",
        ["runtime"]);

    public LuaCapabilityDescriptor Descriptor => Capability;

    public void Register(LuaApiRegistrationContext registration) {
        var mop = registration.Mop;
        var context = registration.Script;
        mop["api_version"] = new LuaFunction((call, _) =>
            new ValueTask<int>(call.Return(ApiVersion)));

        var runtime = registration.GetOrCreateModule("runtime");
        runtime["cancelled"] = new LuaFunction((call, cancellationToken) =>
            new ValueTask<int>(call.Return(cancellationToken.IsCancellationRequested)));
        runtime["paused"] = new LuaFunction((call, _) =>
            new ValueTask<int>(call.Return(registration.ExecutionControl.IsPaused)));
        runtime["stop"] = new LuaFunction((call, _) => {
            if (context.RequestStop == null)
                throw new InvalidOperationException("stopping this Lua run is unavailable in the current host context");
            var reason = OptionalString(call, 0) ?? "stopped by script";
            context.RequestStop(reason);
            return new ValueTask<int>(call.Return());
        });
        runtime["global_stop"] = new LuaFunction(async (call, cancellationToken) => {
            if (context.RequestGlobalStop == null)
                throw new InvalidOperationException("global stopping is unavailable for this Lua run");
            var reason = OptionalString(call, 0) ?? "terminal Mirror stop";
            return call.Return(await context.RequestGlobalStop(reason, cancellationToken));
        });
        runtime["broadcast_emote_resync"] = new LuaFunction(async (call, cancellationToken) => {
            if (context.RequestEmoteResync == null)
                throw new InvalidOperationException("cross-PC emote resynchronization is unavailable for this Lua run");
            var emoteId = ReadUInt(call.GetArgument<double>(0), "emote ID");
            var persistent = call.GetArgument<bool>(1);
            var targetId = ReadULongStringAllowZero(call.GetArgument<string>(2), "emote target ID");
            return call.Return(await context.RequestEmoteResync(emoteId, persistent, targetId, cancellationToken));
        });
        runtime["local_time"] = new LuaFunction((call, _) =>
            new ValueTask<int>(call.Return(registration.LocalMonotonicSeconds)));
        runtime["shared_time"] = new LuaFunction((call, _) =>
            new ValueTask<int>(call.Return(registration.ChoreographySeconds)));
        runtime["log"] = new LuaFunction((call, _) => {
            registration.Quota.WriteLog(call.GetArgument<string>(0), context.Log);
            return new ValueTask<int>(call.Return());
        });
        runtime["variable"] = new LuaFunction((call, _) => {
            var name = call.GetArgument<string>(0);
            var value = context.Variables?
                .FirstOrDefault(pair => pair.Key.Equals(name, StringComparison.OrdinalIgnoreCase))
                .Value;
            return new ValueTask<int>(call.Return(value == null ? LuaValue.Nil : value));
        });
        runtime["dalamud_version"] = new LuaFunction((call, _) =>
            new ValueTask<int>(call.Return(NullToUnknown(context.DalamudVersion))));
        runtime["game_version"] = new LuaFunction((call, _) =>
            new ValueTask<int>(call.Return(NullToUnknown(context.GameVersion))));
        runtime["clientstructs_version"] = new LuaFunction((call, _) =>
            new ValueTask<int>(call.Return(NullToUnknown(context.ClientStructsVersion))));
        runtime["status"] = new LuaFunction((call, cancellationToken) => {
            var table = RuntimeInfo(
                context,
                registration.ChoreographySeconds,
                cancellationToken.IsCancellationRequested,
                registration.Quota,
                registration.ExecutionControl);
            table["state"] = cancellationToken.IsCancellationRequested
                ? "cancelled"
                : registration.ExecutionControl.IsPaused ? "paused" : "running";
            return new ValueTask<int>(call.Return(table));
        });
        runtime["info"] = new LuaFunction((call, cancellationToken) =>
            new ValueTask<int>(call.Return(RuntimeInfo(
                context,
                registration.ChoreographySeconds,
                cancellationToken.IsCancellationRequested,
                registration.Quota,
                registration.ExecutionControl))));

        var capabilities = registration.GetOrCreateModule("capabilities");
        capabilities["list"] = new LuaFunction((call, _) => {
            var result = new LuaTable();
            var descriptors = registration.GetCapabilities();
            for (var index = 0; index < descriptors.Count; index++)
                result[index + 1] = ToLua(descriptors[index]);
            return new ValueTask<int>(call.Return(result));
        });
        capabilities["has"] = new LuaFunction((call, _) => {
            var name = call.GetArgument<string>(0);
            var minimum = OptionalString(call, 1);
            var descriptor = Find(registration, name);
            return new ValueTask<int>(call.Return(descriptor?.MeetsMinimumVersion(minimum) == true));
        });
        capabilities["describe"] = new LuaFunction((call, _) => {
            var descriptor = Find(registration, call.GetArgument<string>(0));
            return new ValueTask<int>(call.Return(descriptor == null ? LuaValue.Nil : ToLua(descriptor)));
        });
        capabilities["require"] = new LuaFunction((call, _) => {
            var name = call.GetArgument<string>(0);
            var minimum = OptionalString(call, 1);
            var descriptor = Find(registration, name)
                ?? throw new InvalidOperationException($"Required Lua capability '{name}' is unavailable on this client.");
            if (!descriptor.MeetsMinimumVersion(minimum))
                throw new InvalidOperationException(
                    $"Lua capability '{descriptor.Name}' version {descriptor.Version} does not satisfy required version {minimum}.");
            return new ValueTask<int>(call.Return(ToLua(descriptor)));
        });
    }

    private static LuaCapabilityDescriptor? Find(LuaApiRegistrationContext registration, string name) {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("capability name is required", nameof(name));
        return registration.GetCapabilities().FirstOrDefault(descriptor =>
            descriptor.Name.Equals(name.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    private static string? OptionalString(LuaFunctionExecutionContext call, int index) {
        if (call.ArgumentCount <= index)
            return null;
        var value = call.GetArgument<LuaValue>(index);
        return value.Type == LuaValueType.Nil ? null : value.Read<string>();
    }

    private static uint ReadUInt(double value, string label) {
        if (!double.IsFinite(value) || value < 1 || value > uint.MaxValue || value != Math.Truncate(value))
            throw new ArgumentOutOfRangeException(label, $"{label} must be an integer between 1 and {uint.MaxValue}");
        return (uint)value;
    }

    private static ulong ReadULongStringAllowZero(string value, string label) {
        if (!ulong.TryParse(value, out var parsed) || parsed == 0xE0000000)
            return 0;
        return parsed;
    }

    private static LuaTable RuntimeInfo(
        LuaScriptContext context,
        double elapsed,
        bool cancelled,
        LuaRunQuota quota,
        LuaExecutionControl executionControl) {
        var usage = quota.Snapshot();
        return new LuaTable {
            ["api_version"] = ApiVersion,
            ["run_id"] = context.RunId ?? string.Empty,
            ["script_name"] = context.ScriptName ?? string.Empty,
            ["conductor"] = context.ConductorName ?? string.Empty,
            ["character_name"] = context.CharacterName ?? string.Empty,
            ["slot"] = (double)context.Slot,
            ["participant_count"] = (double)context.CharacterCount,
            ["seed"] = (double)context.Seed,
            ["elapsed"] = elapsed,
            ["cancelled"] = cancelled,
            ["paused"] = executionControl.IsPaused,
            ["instructions"] = (double)executionControl.Instructions,
            ["dalamud_version"] = NullToUnknown(context.DalamudVersion),
            ["game_version"] = NullToUnknown(context.GameVersion),
            ["clientstructs_version"] = NullToUnknown(context.ClientStructsVersion),
            ["quota"] = new LuaTable {
                ["log_lines"] = (double)usage.LogLines,
                ["log_utf8_bytes"] = (double)usage.LogUtf8Bytes,
                ["pending_waiters"] = (double)usage.PendingWaiters,
                ["chat_actions"] = (double)usage.ChatActions,
                ["game_actions"] = (double)usage.GameActions,
            },
        };
    }

    private static LuaTable ToLua(LuaCapabilityDescriptor descriptor) {
        var permissions = new LuaTable();
        for (var index = 0; index < descriptor.Permissions.Count; index++)
            permissions[index + 1] = descriptor.Permissions[index];
        return new LuaTable {
            ["name"] = descriptor.Name,
            ["version"] = descriptor.Version,
            ["description"] = descriptor.Description,
            ["permissions"] = permissions,
        };
    }

    private static string NullToUnknown(string? value) => string.IsNullOrWhiteSpace(value) ? "unknown" : value.Trim();
}
