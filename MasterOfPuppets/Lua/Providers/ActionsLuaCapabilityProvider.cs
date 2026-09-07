using System;
using System.Collections.Generic;
using System.Numerics;

using Lua;

using MasterOfPuppets.LuaScripting.Automation;
using MasterOfPuppets.LuaScripting.Runtime;

namespace MasterOfPuppets.LuaScripting.Providers;

public sealed class ActionsLuaCapabilityProvider : ILuaCapabilityProvider {
    private static readonly LuaCapabilityDescriptor Capability = new(
        "mop.actions",
        "4.0.0",
        "Validated local/current-PC commands, typed actions, jump/sprint helpers, script-selected gearsets, poses, appearance requests, walk mode, and movement stop.",
        ["chat.action", "game.action", "movement"]);

    public LuaCapabilityDescriptor Descriptor => Capability;

    public void Register(LuaApiRegistrationContext registration) {
        var commands = registration.GetOrCreateModule("commands");
        commands["execute"] = new LuaFunction(async (call, cancellationToken) => {
            var text = call.GetArgument<string>(0);
            var scope = call.ArgumentCount > 1 ? call.GetArgument<string>(1) : "local";
            registration.Quota.ConsumeChatAction();
            return call.Return(ToLua(await Facade(registration).ExecuteTextAsync(text, scope, cancellationToken)));
        });

        var gearsets = registration.GetOrCreateModule("gearsets");
        gearsets["list"] = new LuaFunction(async (call, cancellationToken) => {
            var classJobId = OptionalClassJobId(call, 0);
            return call.Return(ToLua(await Facade(registration).ListGearsetsAsync(classJobId, cancellationToken)));
        });
        gearsets["find"] = new LuaFunction(async (call, cancellationToken) => {
            var selector = ReadGearsetSelector(call.GetArgument<LuaValue>(0));
            var classJobId = OptionalClassJobId(call, 1);
            return call.Return(ToLua(await Facade(registration).FindGearsetAsync(
                selector, classJobId, cancellationToken)));
        });
        gearsets["equip"] = new LuaFunction(async (call, cancellationToken) => {
            var selector = ReadGearsetSelector(call.GetArgument<LuaValue>(0));
            var classJobId = OptionalClassJobId(call, 1);
            registration.Quota.ConsumeGameAction();
            return call.Return(ToLua(await Facade(registration).EquipGearsetAsync(
                selector, classJobId, cancellationToken)));
        });

        var actions = registration.GetOrCreateModule("actions");
        actions["jump"] = new LuaFunction(async (call, cancellationToken) => {
            registration.Quota.ConsumeGameAction();
            return call.Return(ToLua(await Facade(registration).UseExactActionAsync(
                "general_action", 2, "local", null, cancellationToken)));
        });
        actions["sprint"] = new LuaFunction(async (call, cancellationToken) => {
            registration.Quota.ConsumeGameAction();
            return call.Return(ToLua(await Facade(registration).UseExactActionAsync(
                "general_action", 3, "local", null, cancellationToken)));
        });
        actions["use"] = new LuaFunction(async (call, cancellationToken) => {
            var kind = call.GetArgument<string>(0);
            var id = ReadUInt(call.GetArgument<double>(1), "action ID");
            var scope = call.ArgumentCount > 2 ? call.GetArgument<string>(2) : "local";
            var persistent = OptionalBoolean(call, 3);
            registration.Quota.ConsumeGameAction();
            return call.Return(ToLua(await Facade(registration).UseActionAsync(kind, id, scope, persistent, cancellationToken)));
        });
        actions["use_on"] = new LuaFunction(async (call, cancellationToken) => {
            var kind = call.GetArgument<string>(0);
            var id = ReadUInt(call.GetArgument<double>(1), "action ID");
            var targetId = ReadULongString(call.GetArgument<string>(2), "target ID");
            var persistent = OptionalBoolean(call, 3);
            registration.Quota.ConsumeGameAction();
            return call.Return(ToLua(await Facade(registration).UseActionOnAsync(kind, id, targetId, persistent, cancellationToken)));
        });
        actions["use_exact"] = new LuaFunction(async (call, cancellationToken) => {
            var kind = call.GetArgument<string>(0);
            var id = ReadUInt(call.GetArgument<double>(1), "action ID");
            var scope = call.ArgumentCount > 2 ? call.GetArgument<string>(2) : "local";
            var persistent = OptionalBoolean(call, 3);
            registration.Quota.ConsumeGameAction();
            return call.Return(ToLua(await Facade(registration).UseExactActionAsync(kind, id, scope, persistent, cancellationToken)));
        });
        actions["use_exact_on"] = new LuaFunction(async (call, cancellationToken) => {
            var kind = call.GetArgument<string>(0);
            var id = ReadUInt(call.GetArgument<double>(1), "action ID");
            var targetId = ReadULongString(call.GetArgument<string>(2), "target ID");
            var persistent = OptionalBoolean(call, 3);
            registration.Quota.ConsumeGameAction();
            return call.Return(ToLua(await Facade(registration).UseExactActionOnAsync(kind, id, targetId, persistent, cancellationToken)));
        });
        actions["use_ground_on"] = new LuaFunction(async (call, cancellationToken) => {
            var kind = call.GetArgument<string>(0);
            var id = ReadUInt(call.GetArgument<double>(1), "action ID");
            var targetId = ReadULongStringAllowZero(call.GetArgument<string>(2), "target ID");
            Vector3? fallback = null;
            if (call.ArgumentCount >= 6) {
                var x = call.GetArgument<double>(3);
                var y = call.GetArgument<double>(4);
                var z = call.GetArgument<double>(5);
                if (!double.IsFinite(x) || !double.IsFinite(y) || !double.IsFinite(z)
                    || Math.Abs(x) > 100_000 || Math.Abs(y) > 100_000 || Math.Abs(z) > 100_000)
                    throw new ArgumentOutOfRangeException("ground position", "ground position must contain finite world coordinates");
                fallback = new Vector3((float)x, (float)y, (float)z);
            }
            registration.Quota.ConsumeGameAction();
            return call.Return(ToLua(await Facade(registration).UseGroundActionOnAsync(
                kind, id, targetId, fallback, cancellationToken)));
        });
        actions["fallback_candidates"] = new LuaFunction(async (call, cancellationToken) => {
            var kind = call.GetArgument<string>(0);
            var id = ReadUInt(call.GetArgument<double>(1), "action ID");
            var persistent = OptionalBoolean(call, 2);
            var result = await Facade(registration).GetFallbackCandidatesAsync(kind, id, persistent, cancellationToken);
            var candidates = new LuaTable();
            for (var index = 0; index < result.CandidateIds.Count; index++)
                candidates[index + 1] = (double)result.CandidateIds[index];
            var universe = new LuaTable();
            for (var index = 0; index < result.UniverseIds.Count; index++)
                universe[index + 1] = (double)result.UniverseIds[index];
            return call.Return(new LuaTable {
                ["ok"] = result.Success,
                ["kind"] = result.Kind,
                ["requested_id"] = (double)result.RequestedId,
                ["category"] = result.Category,
                ["candidates"] = candidates,
                ["universe"] = universe,
                ["eligibility"] = result.EligibilityToken,
                ["universe_signature"] = result.UniverseSignature,
                ["message"] = result.Message,
            });
        });
        actions["stop_cosmetic"] = new LuaFunction(async (call, cancellationToken) => {
            var kind = call.GetArgument<string>(0);
            registration.Quota.ConsumeGameAction();
            return call.Return(ToLua(await Facade(registration).StopCosmeticAsync(kind, cancellationToken)));
        });
        actions["stop_emote"] = new LuaFunction(async (call, cancellationToken) => {
            registration.Quota.ConsumeGameAction();
            return call.Return(ToLua(await Facade(registration).StopEmoteAsync(cancellationToken)));
        });
        actions["pose"] = new LuaFunction(async (call, cancellationToken) => {
            var poseType = ReadByte(call.GetArgument<double>(0), "pose type");
            var poseState = ReadByte(call.GetArgument<double>(1), "pose state");
            registration.Quota.ConsumeGameAction();
            return call.Return(ToLua(await Facade(registration).SetPoseAsync(poseType, poseState, cancellationToken)));
        });
        actions["target"] = new LuaFunction(async (call, cancellationToken) => {
            var targetId = ReadULongStringAllowZero(call.GetArgument<string>(0), "target ID");
            registration.Quota.ConsumeGameAction();
            return call.Return(ToLua(await Facade(registration).SetTargetAsync(targetId, cancellationToken)));
        });
        actions["target_of"] = new LuaFunction(async (call, cancellationToken) => {
            var actorName = call.GetArgument<string>(0);
            registration.Quota.ConsumeGameAction();
            return call.Return(ToLua(await Facade(registration).SetTargetOfActorAsync(actorName, cancellationToken)));
        });
        actions["target_via_leader"] = new LuaFunction(async (call, cancellationToken) => {
            var targetId = ReadULongStringAllowZero(call.GetArgument<string>(0), "target ID");
            registration.Quota.ConsumeGameAction();
            return call.Return(ToLua(await Facade(registration).MirrorTargetViaLeaderAsync(targetId, cancellationToken)));
        });
        actions["weapon"] = new LuaFunction(async (call, cancellationToken) => {
            var drawn = call.GetArgument<bool>(0);
            registration.Quota.ConsumeGameAction();
            return call.Return(ToLua(await Facade(registration).SetWeaponDrawnAsync(drawn, cancellationToken)));
        });
        actions["headgear_visible"] = new LuaFunction(async (call, cancellationToken) => {
            var visible = call.GetArgument<bool>(0);
            registration.Quota.ConsumeGameAction();
            return call.Return(ToLua(await Facade(registration).SetHeadgearVisibleAsync(visible, cancellationToken)));
        });
        actions["visor"] = new LuaFunction(async (call, cancellationToken) => {
            var enabled = call.GetArgument<bool>(0);
            registration.Quota.ConsumeGameAction();
            return call.Return(ToLua(await Facade(registration).SetVisorAsync(enabled, cancellationToken)));
        });
        actions["online_status"] = new LuaFunction(async (call, cancellationToken) => {
            var statusId = ReadNonNegativeUInt(call.GetArgument<double>(0), "online status ID");
            var statusName = call.GetArgument<string>(1);
            registration.Quota.ConsumeGameAction();
            return call.Return(ToLua(await Facade(registration).SetOnlineStatusAsync(statusId, statusName, cancellationToken)));
        });
        actions["job"] = new LuaFunction(async (call, cancellationToken) => {
            var classJobId = ReadUInt(call.GetArgument<double>(0), "class/job ID");
            if (classJobId > byte.MaxValue)
                throw new ArgumentOutOfRangeException("class/job ID", "class/job ID must be between 1 and 255");
            if (call.ArgumentCount < 2)
                throw new ArgumentException("mop.actions.job requires an exact gearset name or 1-based gearset number");
            var selector = ReadGearsetSelector(call.GetArgument<LuaValue>(1));
            registration.Quota.ConsumeGameAction();
            return call.Return(ToLua(await Facade(registration).ChangeJobAsync(
                classJobId, selector, cancellationToken)));
        });
        actions["walk"] = new LuaFunction(async (call, cancellationToken) => {
            var mode = call.GetArgument<string>(0);
            var scope = call.ArgumentCount > 1 ? call.GetArgument<string>(1) : "local";
            if (string.Equals(scope, "current_pc", StringComparison.OrdinalIgnoreCase))
                registration.Quota.ConsumeChatAction();
            else
                registration.Quota.ConsumeGameAction();
            return call.Return(ToLua(await Facade(registration).SetWalkingAsync(mode, scope, cancellationToken)));
        });
        actions["stop_movement"] = new LuaFunction(async (call, cancellationToken) => {
            var scope = call.ArgumentCount > 0 ? call.GetArgument<string>(0) : "local";
            return call.Return(ToLua(await Facade(registration).StopMovementAsync(scope, cancellationToken)));
        });
        actions["place_pet"] = new LuaFunction(async (call, cancellationToken) => {
            var x = call.GetArgument<double>(0);
            var y = call.GetArgument<double>(1);
            var z = call.GetArgument<double>(2);
            var anchor = call.ArgumentCount > 3 ? call.GetArgument<string>(3) : "target";
            registration.Quota.ConsumeChatAction();
            return call.Return(ToLua(await Facade(registration).PlacePetAsync(new Vector3((float)x, (float)y, (float)z), anchor, cancellationToken)));
        });
        actions["place_pet_formation"] = new LuaFunction(async (call, cancellationToken) => {
            var formationName = call.GetArgument<string>(0);
            var pointNumber = ReadInt(call.GetArgument<double>(1), "point number");
            var anchor = call.ArgumentCount > 2 ? call.GetArgument<string>(2) : "self";
            registration.Quota.ConsumeChatAction();
            return call.Return(ToLua(await Facade(registration).PlacePetFormationAsync(formationName, pointNumber, anchor, cancellationToken)));
        });
    }

    private static ILuaActionFacade Facade(LuaApiRegistrationContext registration) =>
        registration.Script.Actions
        ?? throw new InvalidOperationException("game actions are unavailable in this Lua host context");

    private static bool? OptionalBoolean(LuaFunctionExecutionContext call, int index) {
        if (call.ArgumentCount <= index)
            return null;
        var value = call.GetArgument<LuaValue>(index);
        return value.Type == LuaValueType.Nil ? null : value.Read<bool>();
    }

    private static uint? OptionalClassJobId(LuaFunctionExecutionContext call, int index) {
        if (call.ArgumentCount <= index)
            return null;
        var value = call.GetArgument<LuaValue>(index);
        if (value.Type == LuaValueType.Nil)
            return null;
        if (value.Type != LuaValueType.Number)
            throw new ArgumentException("class/job ID must be a number");
        var parsed = ReadUInt(value.Read<double>(), "class/job ID");
        if (parsed > byte.MaxValue)
            throw new ArgumentOutOfRangeException("class/job ID", "class/job ID must be between 1 and 255");
        return parsed;
    }

    private static GearsetSelector ReadGearsetSelector(LuaValue value) {
        if (value.Type == LuaValueType.Number)
            return GearsetSelector.ByNumber(ReadInt(value.Read<double>(), "gearset number"));
        if (value.Type == LuaValueType.String) {
            var name = value.Read<string>().Trim();
            if (name.Length == 0)
                throw new ArgumentException("gearset name cannot be empty");
            if (name.Length > 100)
                throw new ArgumentException("gearset name cannot exceed 100 characters");
            return GearsetSelector.ByName(name);
        }
        throw new ArgumentException("gearset selector must be an exact name string or 1-based number");
    }

    private static uint ReadUInt(double value, string label) {
        if (!double.IsFinite(value) || value < 1 || value > uint.MaxValue || value != Math.Truncate(value))
            throw new ArgumentOutOfRangeException(label, $"{label} must be a positive unsigned integer");
        return (uint)value;
    }

    private static uint ReadNonNegativeUInt(double value, string label) {
        if (!double.IsFinite(value) || value < 0 || value > uint.MaxValue || value != Math.Truncate(value))
            throw new ArgumentOutOfRangeException(label, $"{label} must be a non-negative unsigned integer");
        return (uint)value;
    }

    private static byte ReadByte(double value, string label) {
        if (!double.IsFinite(value) || value < 0 || value > byte.MaxValue || value != Math.Truncate(value))
            throw new ArgumentOutOfRangeException(label, $"{label} must be an unsigned byte");
        return (byte)value;
    }

    private static int ReadInt(double value, string label) {
        if (!double.IsFinite(value) || value < 1 || value > int.MaxValue || value != Math.Truncate(value))
            throw new ArgumentOutOfRangeException(label, $"{label} must be a positive integer");
        return (int)value;
    }

    private static ulong ReadULongString(string value, string label) {
        if (!ulong.TryParse(value, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var parsed)
            || parsed == 0)
            throw new ArgumentOutOfRangeException(label, $"{label} must be a positive unsigned integer string");
        return parsed;
    }

    private static ulong ReadULongStringAllowZero(string value, string label) {
        if (!ulong.TryParse(value, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var parsed))
            throw new ArgumentOutOfRangeException(label, $"{label} must be an unsigned integer string");
        return parsed;
    }

    private static LuaTable ToLua(LuaAutomationResult result) => new() {
        ["ok"] = result.Success,
        ["status"] = result.Status,
        ["message"] = result.Message,
    };

    private static LuaTable ToLua(IReadOnlyList<GearsetDescriptor> gearsets) {
        var result = new LuaTable();
        for (var index = 0; index < gearsets.Count; index++)
            result[index + 1] = ToLua(gearsets[index]);
        return result;
    }

    private static LuaTable ToLua(GearsetDescriptor gearset) => new() {
        ["number"] = (double)gearset.Number,
        ["name"] = gearset.Name,
        ["class_job_id"] = (double)gearset.ClassJobId,
    };

    private static LuaTable ToLua(GearsetResolution resolution) => new() {
        ["ok"] = resolution.Success,
        ["status"] = resolution.Status,
        ["message"] = resolution.Message,
        ["gearset"] = resolution.Gearset == null ? LuaValue.Nil : ToLua(resolution.Gearset),
        ["candidates"] = ToLua(resolution.Candidates),
    };
}
