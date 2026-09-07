using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using Lua;

using MasterOfPuppets.LuaScripting.Automation;
using MasterOfPuppets.LuaScripting.Runtime;

namespace MasterOfPuppets.LuaScripting.Providers;

public sealed class AutomationLuaCapabilityProvider : ILuaCapabilityProvider {
    private static readonly LuaCapabilityDescriptor Capability = new(
        "mop.automation",
        "2.0.0",
        "Typed orchestration of saved Master of Puppets macros and formations with explicit scope.",
        ["macro.queue", "formation.control", "movement"]);

    public LuaCapabilityDescriptor Descriptor => Capability;

    public void Register(LuaApiRegistrationContext registration) {
        var macros = registration.GetOrCreateModule("macros");
        macros["list"] = new LuaFunction(async (call, cancellationToken) => {
            var assets = await Facade(registration).ListMacrosAsync(cancellationToken);
            var result = new LuaTable();
            for (var index = 0; index < assets.Count; index++) {
                var tags = new LuaTable();
                for (var tag = 0; tag < assets[index].Tags.Count; tag++)
                    tags[tag + 1] = assets[index].Tags[tag];
                result[index + 1] = new LuaTable {
                    ["index"] = (double)assets[index].Index,
                    ["name"] = assets[index].Name,
                    ["tags"] = tags,
                    ["command_count"] = (double)assets[index].CommandCount,
                };
            }
            return call.Return(result);
        });
        macros["status"] = new LuaFunction(async (call, cancellationToken) => {
            var status = await Facade(registration).GetMacroStatusAsync(cancellationToken);
            return call.Return(new LuaTable {
                ["active"] = status.Active,
                ["paused"] = status.Paused,
                ["current_id"] = status.CurrentId,
                ["action_index"] = (double)status.CurrentActionIndex,
                ["action_count"] = (double)status.CurrentActionCount,
                ["pending_count"] = (double)status.PendingCount,
            });
        });
        macros["run"] = new LuaFunction(async (call, cancellationToken) => {
            var selector = call.GetArgument<string>(0);
            var variables = call.ArgumentCount > 1 ? ReadStringMap(call.GetArgument<LuaValue>(1), "macro variables") : new Dictionary<string, string>();
            var scope = call.ArgumentCount > 2 ? call.GetArgument<string>(2) : "local";
            return call.Return(ToLua(await Facade(registration).RunMacroAsync(selector, variables, scope, cancellationToken)));
        });
        macros["await"] = new LuaFunction(async (call, cancellationToken) => {
            var timeout = ReadTimeout(call);
            using var waiter = registration.Quota.EnterWaiter();
            await registration.ExecutionControl.WaitIfPausedAsync(cancellationToken);
            var result = await Facade(registration).WaitForMacrosAsync(timeout, cancellationToken);
            PublishAutomationEvent(registration, "macro", result);
            return call.Return(ToLua(result));
        });
        foreach (var action in new[] { "pause", "resume", "stop" }) {
            var captured = action;
            macros[captured] = new LuaFunction(async (call, cancellationToken) => {
                var scope = call.ArgumentCount > 0 ? call.GetArgument<string>(0) : "local";
                return call.Return(ToLua(await Facade(registration).ControlMacrosAsync(captured, scope, cancellationToken)));
            });
        }

        var formations = registration.GetOrCreateModule("formations");
        formations["list"] = new LuaFunction(async (call, cancellationToken) => {
            var assets = await Facade(registration).ListFormationsAsync(cancellationToken);
            var result = new LuaTable();
            for (var index = 0; index < assets.Count; index++)
                result[index + 1] = ToLua(assets[index]);
            return call.Return(result);
        });
        formations["find"] = new LuaFunction(async (call, cancellationToken) => {
            var name = call.GetArgument<string>(0);
            var assets = await Facade(registration).ListFormationsAsync(cancellationToken);
            var asset = System.Linq.Enumerable.FirstOrDefault(assets, candidate =>
                candidate.Name.Equals(name.Trim(), StringComparison.OrdinalIgnoreCase));
            return call.Return(asset == null ? LuaValue.Nil : ToLua(asset));
        });
        formations["run"] = new LuaFunction(async (call, cancellationToken) => {
            var name = call.GetArgument<string>(0);
            var anchor = call.ArgumentCount > 1 ? call.GetArgument<string>(1) : "self";
            var movementMode = call.ArgumentCount > 2 ? call.GetArgument<string>(2) : "natural";
            var scope = call.ArgumentCount > 3 ? call.GetArgument<string>(3) : "current_pc";
            return call.Return(ToLua(await Facade(registration).RunFormationAsync(name, anchor, movementMode, scope, cancellationToken)));
        });
        formations["await"] = new LuaFunction(async (call, cancellationToken) => {
            var timeout = ReadTimeout(call);
            using var waiter = registration.Quota.EnterWaiter();
            await registration.ExecutionControl.WaitIfPausedAsync(cancellationToken);
            var result = await Facade(registration).WaitForFormationAsync(timeout, cancellationToken);
            PublishAutomationEvent(registration, "formation", result);
            return call.Return(ToLua(result));
        });
        formations["stop"] = new LuaFunction(async (call, cancellationToken) => {
            var scope = call.ArgumentCount > 0 ? call.GetArgument<string>(0) : "local";
            return call.Return(ToLua(await Facade(registration).StopFormationAsync(scope, cancellationToken)));
        });
    }

    private static ILuaAutomationFacade Facade(LuaApiRegistrationContext registration) =>
        registration.Script.Automation
        ?? throw new InvalidOperationException("Master of Puppets automation is unavailable in this Lua host context");

    private static TimeSpan ReadTimeout(LuaFunctionExecutionContext call) {
        var seconds = call.ArgumentCount > 0 ? call.GetArgument<double>(0) : 120.0;
        if (!double.IsFinite(seconds) || seconds <= 0 || seconds > 600)
            throw new ArgumentOutOfRangeException(nameof(seconds), "automation timeout must be between 0 and 600 seconds");
        return TimeSpan.FromSeconds(seconds);
    }

    private static void PublishAutomationEvent(
        LuaApiRegistrationContext registration,
        string subject,
        LuaAutomationResult result) =>
        registration.Script.Events?.Publish($"{subject}.{result.Status}", new Dictionary<string, string> {
            ["ok"] = result.Success ? "true" : "false",
            ["message"] = result.Message,
        });

    private static IReadOnlyDictionary<string, string> ReadStringMap(LuaValue value, string label) {
        if (value.Type == LuaValueType.Nil)
            return new Dictionary<string, string>();
        if (!value.TryRead<LuaTable>(out var table))
            throw new ArgumentException($"{label} must be a table");
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, item) in table) {
            if (!key.TryRead<string>(out var name) || string.IsNullOrWhiteSpace(name))
                throw new ArgumentException($"{label} keys must be non-empty strings");
            if (!item.TryRead<string>(out var text))
                throw new ArgumentException($"{label} values must be strings");
            result[name.Trim()] = text;
        }
        return result;
    }

    private static LuaTable ToLua(LuaAutomationResult result) => new() {
        ["ok"] = result.Success,
        ["status"] = result.Status,
        ["message"] = result.Message,
    };

    private static LuaTable ToLua(LuaFormationAssetSnapshot formation) {
        var points = new LuaTable();
        for (var index = 0; index < formation.Points.Count; index++) {
            var point = formation.Points[index];
            var groups = new LuaTable();
            for (var group = 0; group < point.Groups.Count; group++)
                groups[group + 1] = point.Groups[group];
            points[index + 1] = new LuaTable {
                ["index"] = (double)point.Index,
                ["offset"] = new LuaTable { ["x"] = point.X, ["y"] = point.Y, ["z"] = point.Z },
                ["facing_degrees"] = point.FacingDegrees,
                ["groups"] = groups,
                ["assigned_character_count"] = (double)point.AssignedCharacterCount,
            };
        }
        return new LuaTable { ["name"] = formation.Name, ["points"] = points };
    }
}
