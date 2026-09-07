using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

using Lua;

using MasterOfPuppets.Formations;
using MasterOfPuppets.LuaScripting.Runtime;

namespace MasterOfPuppets.LuaScripting.Providers;

/// <summary>Compatibility provider for every V1 flat mop function.</summary>
public sealed class LegacyLuaCapabilityProvider : ILuaCapabilityProvider {
    private static readonly LuaCapabilityDescriptor Capability = new(
        "legacy.flat-api",
        "1.0.0",
        "The original 18 flat Master of Puppets Lua functions.",
        ["runtime", "movement", "chat"]);

    public LuaCapabilityDescriptor Descriptor => Capability;

    public void Register(LuaApiRegistrationContext registration) {
        var mop = registration.Mop;
        var context = registration.Script;

        mop["log"] = new LuaFunction((call, _) => {
            registration.Quota.WriteLog(call.GetArgument<string>(0), context.Log);
            return new ValueTask<int>(call.Return());
        });

        mop["wait"] = new LuaFunction(async (call, cancellationToken) => {
            var seconds = call.GetArgument<double>(0);
            ValidateSeconds(seconds, 0, 10, "wait");
            using var waiter = registration.Quota.EnterWaiter();
            await registration.ExecutionControl.WaitIfPausedAsync(cancellationToken);
            await registration.DelayAsync(TimeSpan.FromSeconds(seconds), cancellationToken);
            await registration.ExecutionControl.WaitIfPausedAsync(cancellationToken);
            return call.Return();
        });

        mop["time"] = new LuaFunction((call, _) =>
            new ValueTask<int>(call.Return(registration.ChoreographySeconds)));

        mop["get_slot"] = new LuaFunction((call, _) =>
            new ValueTask<int>(call.Return((double)context.Slot)));

        mop["get_count"] = new LuaFunction((call, _) =>
            new ValueTask<int>(call.Return((double)context.CharacterCount)));

        mop["get_seed"] = new LuaFunction((call, _) =>
            new ValueTask<int>(call.Return((double)context.Seed)));

        mop["get_name"] = new LuaFunction((call, _) =>
            new ValueTask<int>(call.Return(context.CharacterName)));

        mop["get_run_target"] = new LuaFunction((call, _) =>
            new ValueTask<int>(call.Return(
                string.IsNullOrWhiteSpace(context.RunTargetName)
                    ? LuaValue.Nil
                    : context.RunTargetName)));

        mop["get_target_speed"] = new LuaFunction((call, _) =>
            new ValueTask<int>(call.Return(context.GetTargetSpeed?.Invoke() ?? 0.0)));

        mop["get_group"] = new LuaFunction((call, _) => {
            var groupName = call.GetArgument<string>(0);
            var group = context.GetGroup?.Invoke(groupName);
            var table = new LuaTable();
            if (group != null) {
                for (var i = 0; i < group.Count; i++) {
                    table[i + 1] = group[i];
                }
            }
            return new ValueTask<int>(call.Return(table));
        });

        mop["is_walking"] = new LuaFunction((call, _) =>
            new ValueTask<int>(call.Return(context.IsWalking?.Invoke() ?? true)));

        mop["visible_roster"] = new LuaFunction((call, _) => {
            var (slot, count) = context.GetVisibleRoster?.Invoke()
                ?? (context.Slot, Math.Max(1, context.CharacterCount));
            return new ValueTask<int>(call.Return(new LuaTable {
                ["slot"] = (double)Math.Max(0, slot),
                ["count"] = (double)Math.Max(1, count),
            }));
        });

        mop["get_var"] = new LuaFunction((call, _) => {
            var name = call.GetArgument<string>(0);
            var value = context.Variables?
                .FirstOrDefault(pair => pair.Key.Equals(name, StringComparison.OrdinalIgnoreCase))
                .Value;
            return new ValueTask<int>(call.Return(value == null ? LuaValue.Nil : value));
        });

        mop["get_number"] = new LuaFunction((call, _) => {
            var name = call.GetArgument<string>(0);
            var fallback = call.GetArgument<double>(1);
            var raw = context.Variables?
                .FirstOrDefault(pair => pair.Key.Equals(name, StringComparison.OrdinalIgnoreCase))
                .Value;
            var value = double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
                && double.IsFinite(parsed)
                    ? parsed
                    : fallback;
            return new ValueTask<int>(call.Return(value));
        });

        mop["names_match"] = new LuaFunction((call, _) =>
            new ValueTask<int>(call.Return(
                FormationCharacterName.Matches(
                    call.GetArgument<string>(0),
                    call.GetArgument<string>(1)))));

        mop["set_anchor"] = new LuaFunction(async (call, cancellationToken) => {
            if (context.SetAnchor == null)
                throw new InvalidOperationException("movement anchors are unavailable in this Lua context");

            var anchorName = call.GetArgument<string>(0);
            if (string.IsNullOrWhiteSpace(anchorName))
                throw new ArgumentException("anchor name cannot be empty", nameof(anchorName));
            await context.SetAnchor(anchorName.Trim(), cancellationToken);
            return call.Return();
        });

        mop["follow_actor"] = new LuaFunction(async (call, cancellationToken) => {
            if (context.FollowActor == null)
                throw new InvalidOperationException("actor following is unavailable in this Lua context");

            var options = call.GetArgument<LuaTable>(0);
            var request = new LuaActorFollowRequest(
                ReadAnchorCandidates(options),
                new Vector3(
                    ReadNumber(options, "offset_x", 0f),
                    ReadNumber(options, "offset_y", 0f),
                    ReadNumber(options, "offset_z", 0f)),
                FaceAnchor: ReadBoolean(options, "face_anchor", true),
                FacingOffsetRadians: ReadNumber(options, "facing_offset", 0f),
                Precision: ReadNumber(options, "precision", 0.1f),
                BrakeAtPosition: ReadBoolean(options, "brake_at_position", true),
                PursuitPrediction: ReadBoolean(options, "pursuit_prediction", false),
                ImmediateSteering: ReadBoolean(options, "immediate_steering", true),
                RigidFormation: ReadBoolean(options, "rigid_formation", false),
                FormationRadius: ReadNumber(options, "formation_radius", 0f),
                FormationNeighbors: ReadFormationNeighbors(options),
                NeighborCorrectionWeight: ReadNumber(options, "neighbor_correction", 0.25f),
                MaximumNeighborCorrection: ReadNumber(options, "maximum_neighbor_correction", 0.35f),
                MirrorWalkRun: ReadBoolean(options, "mirror_walk_run", false),
                MirrorSprint: ReadBoolean(options, "mirror_sprint", false)).Validate();
            await context.FollowActor(request, cancellationToken);
            return call.Return();
        });

        mop["is_running"] = new LuaFunction((call, cancellationToken) =>
            new ValueTask<int>(call.Return(
                !cancellationToken.IsCancellationRequested
                && !registration.ExecutionControl.IsPaused)));

        mop["chat"] = new LuaFunction((call, _) => {
            QueueChat(registration, call.GetArgument<string>(0));
            return new ValueTask<int>(call.Return());
        });

        mop["say"] = new LuaFunction((call, _) => {
            QueueChat(registration, $"/say {call.GetArgument<string>(0)}");
            return new ValueTask<int>(call.Return());
        });

        mop["wait_for_chat"] = new LuaFunction(async (call, cancellationToken) => {
            if (context.WaitForChat == null)
                throw new InvalidOperationException("chat observation is unavailable in this Lua context");

            var speaker = call.GetArgument<string>(0);
            var message = call.GetArgument<string>(1);
            var timeoutSeconds = call.GetArgument<double>(2);
            if (string.IsNullOrWhiteSpace(speaker))
                throw new ArgumentException("expected speaker cannot be empty", nameof(speaker));
            if (string.IsNullOrWhiteSpace(message))
                throw new ArgumentException("expected chat message cannot be empty", nameof(message));
            ValidateSeconds(timeoutSeconds, double.Epsilon, 60, "chat timeout");

            using var waiter = registration.Quota.EnterWaiter();
            var observed = await context.WaitForChat(
                speaker,
                message,
                TimeSpan.FromSeconds(timeoutSeconds),
                cancellationToken);
            return call.Return(observed);
        });

        mop["trajectory_update"] = new LuaFunction((call, _) => {
            if (!LuaTrajectorySample.TryCreate(
                    call.GetArgument<double>(0),
                    call.GetArgument<double>(1),
                    call.GetArgument<double>(2),
                    registration.ChoreographySeconds,
                    out var sample))
                throw new ArgumentException("trajectory values must be finite numbers");

            context.PublishTrajectory(sample);
            return new ValueTask<int>(call.Return());
        });
    }

    private static IReadOnlyList<string> ReadAnchorCandidates(LuaTable options) {
        if (!options.TryGetValue("anchors", out var value)
            || !value.TryRead<LuaTable>(out var anchorsTable))
            throw new ArgumentException("follow_actor requires an anchors table");

        var anchors = new List<string>(anchorsTable.ArrayLength);
        for (var index = 1; index <= anchorsTable.ArrayLength; index++) {
            if (anchorsTable[index].TryRead<string>(out var anchor)
                && !string.IsNullOrWhiteSpace(anchor))
                anchors.Add(anchor);
        }
        return anchors;
    }

    private static IReadOnlyList<LuaFormationNeighbor> ReadFormationNeighbors(LuaTable options) {
        if (!options.TryGetValue("neighbors", out var value)
            || !value.TryRead<LuaTable>(out var neighborsTable))
            return [];

        var neighbors = new List<LuaFormationNeighbor>(Math.Min(4, neighborsTable.ArrayLength));
        for (var index = 1; index <= neighborsTable.ArrayLength; index++) {
            if (!neighborsTable[index].TryRead<LuaTable>(out var neighbor))
                throw new ArgumentException("follow_actor neighbors must contain option tables");
            if (!neighbor.TryGetValue("name", out var nameValue)
                || !nameValue.TryRead<string>(out var name)
                || string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("follow_actor neighbor name cannot be empty");
            neighbors.Add(new LuaFormationNeighbor(
                name.Trim(),
                new Vector3(
                    ReadNumber(neighbor, "offset_x", 0f),
                    ReadNumber(neighbor, "offset_y", 0f),
                    ReadNumber(neighbor, "offset_z", 0f))));
        }
        return neighbors;
    }

    private static float ReadNumber(LuaTable options, string key, float fallback) {
        if (!options.TryGetValue(key, out var value))
            return fallback;
        if (!value.TryRead<double>(out var number) || !double.IsFinite(number))
            throw new ArgumentException($"follow_actor {key} must be a finite number");
        return (float)number;
    }

    private static bool ReadBoolean(LuaTable options, string key, bool fallback) {
        if (!options.TryGetValue(key, out var value))
            return fallback;
        if (!value.TryRead<bool>(out var result))
            throw new ArgumentException($"follow_actor {key} must be true or false");
        return result;
    }

    private static void QueueChat(LuaApiRegistrationContext registration, string message) {
        var context = registration.Script;
        if (context.SendChat == null)
            throw new InvalidOperationException("chat is unavailable in this Lua context");
        if (string.IsNullOrWhiteSpace(message))
            throw new ArgumentException("chat message cannot be empty", nameof(message));
        if (message.Contains('\r') || message.Contains('\n') || message.Contains('\0'))
            throw new ArgumentException("chat message must contain exactly one line", nameof(message));
        if (Encoding.UTF8.GetByteCount(message) > 500)
            throw new ArgumentException("chat message cannot exceed 500 UTF-8 bytes", nameof(message));
        registration.Quota.ConsumeChatAction();
        context.SendChat(message);
    }

    private static void ValidateSeconds(double value, double minimumExclusiveOrInclusive, double maximum, string label) {
        var invalidMinimum = minimumExclusiveOrInclusive == double.Epsilon
            ? value <= 0
            : value < minimumExclusiveOrInclusive;
        if (!double.IsFinite(value) || invalidMinimum || value > maximum)
            throw new ArgumentOutOfRangeException(nameof(value), $"{label} must be between {(minimumExclusiveOrInclusive == double.Epsilon ? "more than 0" : minimumExclusiveOrInclusive)} and {maximum} seconds");
    }
}
