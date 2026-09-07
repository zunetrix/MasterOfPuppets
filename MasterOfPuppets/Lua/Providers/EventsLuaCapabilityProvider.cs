using System;
using System.Threading.Tasks;

using Lua;

using MasterOfPuppets.LuaScripting.Events;
using MasterOfPuppets.LuaScripting.Runtime;

namespace MasterOfPuppets.LuaScripting.Providers;

public sealed class EventsLuaCapabilityProvider : ILuaCapabilityProvider {
    private static readonly LuaCapabilityDescriptor Capability = new(
        "mop.events",
        "3.0.0",
        "Bounded non-destructive filtered events, actor-scoped reactions, and opt-in raw world streams.",
        ["events.observe"]);

    public LuaCapabilityDescriptor Descriptor => Capability;

    public void Register(LuaApiRegistrationContext registration) {
        var events = registration.GetOrCreateModule("events");
        events["subscribe"] = new LuaFunction((call, _) => {
            var name = RequiredString(call, 0);
            Hub(registration).RegisterInterest(name);
            return new ValueTask<int>(call.Return(true));
        });
        events["unsubscribe"] = new LuaFunction((call, _) => {
            var name = RequiredString(call, 0);
            return new ValueTask<int>(call.Return(Hub(registration).UnregisterInterest(name)));
        });
        events["poll"] = new LuaFunction((call, _) => {
            var name = OptionalString(call, 0);
            return new ValueTask<int>(call.Return(Hub(registration).TryRead(name, out var item)
                ? ToLua(item!)
                : LuaValue.Nil));
        });
        events["next"] = new LuaFunction(async (call, cancellationToken) => {
            var name = OptionalString(call, 0);
            var timeoutSeconds = call.ArgumentCount > 1 ? call.GetArgument<double>(1) : 30.0;
            if (!double.IsFinite(timeoutSeconds) || timeoutSeconds <= 0 || timeoutSeconds > 120)
                throw new ArgumentOutOfRangeException(nameof(timeoutSeconds), "event timeout must be between 0 and 120 seconds");
            using var waiter = registration.Quota.EnterWaiter();
            await registration.ExecutionControl.WaitIfPausedAsync(cancellationToken);
            var item = await Hub(registration).ReadAsync(name, TimeSpan.FromSeconds(timeoutSeconds), cancellationToken);
            return call.Return(item == null
                ? new LuaTable { ["status"] = "timeout" }
                : ToLua(item));
        });
        events["stats"] = new LuaFunction((call, _) => {
            var stats = Hub(registration).Snapshot();
            return new ValueTask<int>(call.Return(new LuaTable {
                ["capacity"] = (double)stats.Capacity,
                ["published"] = (double)stats.Published,
                ["consumed"] = (double)stats.Consumed,
                ["dropped"] = (double)stats.Dropped,
                ["completed"] = stats.Completed,
            }));
        });
    }

    private static LuaEventHub Hub(LuaApiRegistrationContext registration) =>
        registration.Script.Events
        ?? throw new InvalidOperationException("event streaming is unavailable in this Lua host context");

    private static string? OptionalString(LuaFunctionExecutionContext call, int index) {
        if (call.ArgumentCount <= index)
            return null;
        var value = call.GetArgument<LuaValue>(index);
        return value.Type == LuaValueType.Nil ? null : value.Read<string>();
    }

    private static string RequiredString(LuaFunctionExecutionContext call, int index) {
        var value = call.GetArgument<string>(index).Trim();
        return value.Length > 0 ? value : throw new ArgumentException("event name cannot be empty");
    }

    internal static LuaTable ToLua(LuaHostEvent item) {
        var data = new LuaTable();
        foreach (var (key, value) in item.Data)
            data[key] = value;
        return new LuaTable {
            ["status"] = "event",
            ["sequence"] = (double)item.Sequence,
            ["name"] = item.Name,
            ["timestamp_unix_ms"] = (double)item.Timestamp.ToUnixTimeMilliseconds(),
            ["data"] = data,
        };
    }
}
