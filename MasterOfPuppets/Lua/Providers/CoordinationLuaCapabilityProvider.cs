using System;
using System.Threading;
using System.Threading.Tasks;

using Lua;

using MasterOfPuppets.LuaScripting.Coordination;
using MasterOfPuppets.LuaScripting.Runtime;

namespace MasterOfPuppets.LuaScripting.Providers;

public sealed class CoordinationLuaCapabilityProvider : ILuaCapabilityProvider {
    private static readonly LuaCapabilityDescriptor Capability = new(
        "mop.coordination",
        "2.1.0",
        "Bounded conductor-owned shared variables and ordered versioned participant messages.",
        ["coordination.read", "coordination.write"]);

    public LuaCapabilityDescriptor Descriptor => Capability;

    public void Register(LuaApiRegistrationContext registration) {
        var shared = registration.GetOrCreateModule("shared");
        shared["get"] = new LuaFunction((call, _) => {
            var key = call.GetArgument<string>(0);
            return new ValueTask<int>(call.Return(State(registration).TryGetShared(key, out var value)
                ? ToLua(value!)
                : LuaValue.Nil));
        });
        shared["list"] = new LuaFunction((call, _) => {
            var values = State(registration).SharedVariables;
            var result = new LuaTable();
            for (var index = 0; index < values.Count; index++)
                result[index + 1] = ToLua(values[index]);
            return new ValueTask<int>(call.Return(result));
        });
        shared["set"] = new LuaFunction((call, _) =>
            new ValueTask<int>(call.Return(ToLua(State(registration).SetShared(
                call.GetArgument<string>(0),
                call.GetArgument<string>(1))))));
        shared["wait"] = new LuaFunction(async (call, cancellationToken) => {
            var key = call.GetArgument<string>(0);
            var timeout = ReadTimeout(call, 1);
            var afterSequence = call.ArgumentCount > 2 ? ReadInteger(call, 2, "after_sequence") : 0;
            using var waiter = registration.Quota.EnterWaiter();
            var result = await PollAsync(
                registration,
                timeout,
                cancellationToken,
                () => State(registration).TryGetShared(key, out var value) && value!.Sequence > afterSequence
                    ? ToLua(value)
                    : null);
            return call.Return(result ?? new LuaTable { ["status"] = "timeout" });
        });

        var messages = registration.GetOrCreateModule("messages");
        messages["send"] = new LuaFunction((call, _) => {
            var state = State(registration);
            var targetContentId = 0UL;
            if (call.ArgumentCount > 2) {
                var targetSlot = ReadInteger(call, 2, "target_slot");
                if (targetSlot < 0 || targetSlot >= state.ParticipantContentIds.Count)
                    throw new ArgumentOutOfRangeException(nameof(targetSlot), "target slot is outside the authoritative roster");
                targetContentId = state.ParticipantContentIds[(int)targetSlot];
            }
            var schemaVersion = call.ArgumentCount > 3 ? ReadInteger(call, 3, "schema_version") : 1;
            return new ValueTask<int>(call.Return(ToLua(state.SendMessage(
                call.GetArgument<string>(0),
                call.GetArgument<string>(1),
                checked((int)schemaVersion),
                targetContentId))));
        });
        messages["broadcast"] = new LuaFunction((call, _) => {
            var state = State(registration);
            var schemaVersion = call.ArgumentCount > 2 ? ReadInteger(call, 2, "schema_version") : 1;
            return new ValueTask<int>(call.Return(ToLua(state.SendMessage(
                call.GetArgument<string>(0),
                call.GetArgument<string>(1),
                checked((int)schemaVersion),
                0))));
        });
        messages["poll"] = new LuaFunction((call, _) => {
            var topic = OptionalString(call, 0);
            return new ValueTask<int>(call.Return(State(registration).TryReadMessage(topic, out var message)
                ? ToLua(message!)
                : LuaValue.Nil));
        });
        messages["next"] = new LuaFunction(async (call, cancellationToken) => {
            var topic = OptionalString(call, 0);
            var timeout = ReadTimeout(call, 1);
            using var waiter = registration.Quota.EnterWaiter();
            var result = await PollAsync(
                registration,
                timeout,
                cancellationToken,
                () => State(registration).TryReadMessage(topic, out var message) ? ToLua(message!) : null);
            return call.Return(result ?? new LuaTable { ["status"] = "timeout" });
        });
        messages["info"] = new LuaFunction((call, _) => {
            var state = State(registration);
            return new ValueTask<int>(call.Return(new LuaTable {
                ["distributed"] = state.IsDistributed,
                ["conductor"] = state.IsConductor,
                ["local_content_id"] = state.LocalContentId.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["participant_count"] = (double)state.ParticipantContentIds.Count,
            }));
        });
    }

    private static ILuaCoordinationFacade State(LuaApiRegistrationContext registration) =>
        registration.Script.Coordination
        ?? throw new InvalidOperationException("run coordination is unavailable in this Lua host context");

    private static async Task<LuaTable?> PollAsync(
        LuaApiRegistrationContext registration,
        TimeSpan timeout,
        CancellationToken cancellationToken,
        Func<LuaTable?> poll) {
        var interval = TimeSpan.FromMilliseconds(50);
        var attempts = Math.Max(1, (int)Math.Ceiling(timeout.TotalMilliseconds / interval.TotalMilliseconds) + 1);
        for (var attempt = 0; attempt < attempts; attempt++) {
            await registration.ExecutionControl.WaitIfPausedAsync(cancellationToken);
            var value = poll();
            if (value != null)
                return value;
            if (attempt + 1 < attempts)
                await registration.DelayAsync(interval, cancellationToken);
        }
        return null;
    }

    private static TimeSpan ReadTimeout(LuaFunctionExecutionContext call, int index) {
        var seconds = call.ArgumentCount <= index ? 30.0 : call.GetArgument<double>(index);
        if (!double.IsFinite(seconds) || seconds <= 0 || seconds > 120)
            throw new ArgumentOutOfRangeException(nameof(seconds), "timeout must be between 0 and 120 seconds");
        return TimeSpan.FromSeconds(seconds);
    }

    private static long ReadInteger(LuaFunctionExecutionContext call, int index, string name) {
        var value = call.GetArgument<double>(index);
        if (!double.IsFinite(value) || value != Math.Truncate(value) || value < 0 || value > long.MaxValue)
            throw new ArgumentOutOfRangeException(name, $"{name} must be a non-negative integer");
        return checked((long)value);
    }

    private static string? OptionalString(LuaFunctionExecutionContext call, int index) {
        if (call.ArgumentCount <= index)
            return null;
        var value = call.GetArgument<LuaValue>(index);
        return value.Type == LuaValueType.Nil ? null : value.Read<string>();
    }

    private static LuaTable ToLua(LuaCoordinationResult result) => new() {
        ["ok"] = result.Ok,
        ["status"] = result.Status,
        ["message"] = result.Message,
    };

    private static LuaTable ToLua(LuaSharedVariableSnapshot value) => new() {
        ["status"] = "value",
        ["key"] = value.Key,
        ["value"] = value.Value,
        ["sequence"] = (double)value.Sequence,
        ["sender_content_id"] = value.SenderContentId.ToString(System.Globalization.CultureInfo.InvariantCulture),
        ["updated_unix_ms"] = (double)value.UpdatedAt.ToUnixTimeMilliseconds(),
    };

    private static LuaTable ToLua(LuaParticipantMessageSnapshot message) => new() {
        ["status"] = "message",
        ["message_id"] = message.MessageId.ToString("D"),
        ["topic"] = message.Topic,
        ["payload"] = message.Payload,
        ["schema_version"] = (double)message.SchemaVersion,
        ["sequence"] = (double)message.Sequence,
        ["sender_content_id"] = message.SenderContentId.ToString(System.Globalization.CultureInfo.InvariantCulture),
        ["target_content_id"] = message.TargetContentId.ToString(System.Globalization.CultureInfo.InvariantCulture),
        ["received_unix_ms"] = (double)message.ReceivedAt.ToUnixTimeMilliseconds(),
    };
}
