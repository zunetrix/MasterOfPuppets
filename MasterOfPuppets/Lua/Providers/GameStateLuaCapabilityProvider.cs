using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;

using Dalamud.Game.ClientState.Objects.Enums;

using Lua;

using MasterOfPuppets.LuaScripting.Events;
using MasterOfPuppets.Formations;
using MasterOfPuppets.LuaScripting.Runtime;
using MasterOfPuppets.LuaScripting.Snapshots;
using MasterOfPuppets.LuaScripting.Watches;

namespace MasterOfPuppets.LuaScripting.Providers;

public sealed class GameStateLuaCapabilityProvider : ILuaCapabilityProvider {
    private static readonly TimeSpan ObservationPollInterval = TimeSpan.FromMilliseconds(100);
    private static readonly LuaCapabilityDescriptor Capability = new(
        "mop.game-state",
        "4.0.0",
        "Immutable snapshots plus arbitrary-player watches and source-scoped action, emote, movement, appearance, target, pose, and weapon reactions.",
        ["game-state.observe"]);

    public LuaCapabilityDescriptor Descriptor => Capability;

    public void Register(LuaApiRegistrationContext registration) {
        var self = registration.GetOrCreateModule("self");
        self["snapshot"] = SnapshotFunction(registration, snapshot => snapshot.Self);
        self["watch"] = WatchFunction(registration, _ => LuaActorWatchService.SelfQuery);

        var target = registration.GetOrCreateModule("target");
        target["snapshot"] = new LuaFunction(async (call, cancellationToken) => {
            var kind = ReadTargetKind(call, 0);
            var snapshot = await Capture(registration, cancellationToken);
            var actor = SelectTarget(snapshot, kind);
            return call.Return(actor == null ? LuaValue.Nil : ToLua(actor));
        });
        target["job"] = new LuaFunction(async (call, cancellationToken) => {
            var kind = ReadTargetKind(call, 0);
            var actor = SelectTarget(await Capture(registration, cancellationToken), kind);
            return call.Return(ToJobObservation(actor, kind));
        });
        target["wait_job_changed"] = new LuaFunction(async (call, cancellationToken) => {
            var previousClassJobId = ReadClassJobId(call.GetArgument<double>(0));
            var timeout = ReadTimeout(call, 1);
            var kind = ReadTargetKind(call, 2);
            var expectedGameObjectId = call.ArgumentCount > 3
                ? call.GetArgument<string>(3).Trim()
                : string.Empty;
            if (call.ArgumentCount > 3 && expectedGameObjectId.Length == 0)
                throw new ArgumentException("expected game-object ID cannot be empty");
            var initial = SelectTarget(await Capture(registration, cancellationToken), kind);
            if (initial == null)
                return call.Return(ToJobTerminalStatus("target_lost", kind, null, previousClassJobId));
            if (expectedGameObjectId.Length > 0
                && !initial.GameObjectId.Equals(expectedGameObjectId, StringComparison.Ordinal))
                return call.Return(ToJobTerminalStatus("target_changed", kind, initial, previousClassJobId));
            if (initial.ClassJobId > 0 && initial.ClassJobId != previousClassJobId)
                return call.Return(ToJobChanged(initial, kind, previousClassJobId));

            var boundGameObjectId = expectedGameObjectId.Length > 0
                ? expectedGameObjectId
                : initial.GameObjectId;
            var result = await WaitForSnapshotAsync(
                registration,
                timeout,
                cancellationToken,
                snapshot => {
                    var actor = SelectTarget(snapshot, kind);
                    if (actor == null)
                        return (true, (LuaValue)ToJobTerminalStatus("target_lost", kind, null, previousClassJobId));
                    if (!actor.GameObjectId.Equals(boundGameObjectId, StringComparison.Ordinal))
                        return (true, (LuaValue)ToJobTerminalStatus("target_changed", kind, actor, previousClassJobId));
                    return actor.ClassJobId > 0 && actor.ClassJobId != previousClassJobId
                        ? (true, (LuaValue)ToJobChanged(actor, kind, previousClassJobId))
                        : (false, LuaValue.Nil);
                });
            return call.Return(result);
        });

        // Actor-query APIs are deliberately independent from participant and
        // configured-character rosters. A query resolves any locally visible
        // real player; steady-state waits reuse LuaActorWatchService's cached
        // actor instead of rebuilding the broad world snapshot.
        var actors = registration.GetOrCreateModule("actors");
        actors["list"] = new LuaFunction(async (call, cancellationToken) => {
            var snapshot = await Capture(registration, cancellationToken);
            return call.Return(ToLua(snapshot.VisibleActors));
        });
        actors["find"] = new LuaFunction(async (call, cancellationToken) => {
            var query = call.GetArgument<string>(0).Trim();
            if (query.Length == 0)
                throw new ArgumentException("actor query cannot be empty");
            var snapshot = await Capture(registration, cancellationToken);
            return call.Return(ToLookupResult(FindActors(snapshot, query)));
        });
        actors["watch"] = WatchFunction(registration, call => RequiredQuery(call.GetArgument<string>(0)));
        actors["event_sources"] = new LuaFunction((call, _) => {
            var sources = registration.Script.GetActorEventSources?.Invoke()
                ?? new LuaActorEventSources(
                    StateChanges: registration.Script.WatchActor != null,
                    CombatActions: false,
                    Emotes: false);
            return new ValueTask<int>(call.Return(new LuaTable {
                ["state_changes"] = sources.StateChanges,
                ["combat_actions"] = sources.CombatActions,
                ["general_actions"] = sources.CombatActions,
                ["emotes"] = sources.Emotes,
            }));
        });
        actors["next_event"] = new LuaFunction(async (call, cancellationToken) => {
            var query = RequiredQuery(call.GetArgument<string>(0));
            var kind = RequiredQuery(call.GetArgument<string>(1)).ToLowerInvariant();
            var eventName = LuaActorEventNames.ResolveKind(kind);
            var timeout = ReadTimeout(call, 2);
            var sources = registration.Script.GetActorEventSources?.Invoke();
            if (sources.HasValue
                && ((LuaActorEventNames.IsActionKind(kind) && !sources.Value.CombatActions)
                    || (LuaActorEventNames.IsEmoteEdgeKind(kind) && !sources.Value.Emotes))) {
                return call.Return(new LuaTable {
                    ["status"] = "unavailable",
                    ["query"] = query,
                    ["kind"] = kind,
                    ["source"] = LuaActorEventNames.IsEmoteEdgeKind(kind) ? "emote_hook" : "combat_action_hook",
                });
            }
            var watched = await WatchOnce(registration, query, cancellationToken);
            if (!watched.Status.Equals("found", StringComparison.OrdinalIgnoreCase)
                || !watched.State.HasValue) {
                return call.Return(new LuaTable {
                    ["status"] = watched.Status,
                    ["query"] = watched.Query,
                    ["kind"] = kind,
                    ["watch_id"] = watched.WatchId,
                    ["count"] = (double)watched.MatchCount,
                });
            }

            var events = registration.Script.Events
                ?? throw new InvalidOperationException("actor event streaming is unavailable in this Lua host context");
            using var waiter = registration.Quota.EnterWaiter();
            await registration.ExecutionControl.WaitIfPausedAsync(cancellationToken);
            var item = await events.ReadAsync(
                eventName,
                new Dictionary<string, string>(1, StringComparer.Ordinal) {
                    ["watch_id"] = watched.WatchId,
                },
                timeout,
                cancellationToken);
            if (item != null)
                return call.Return(EventsLuaCapabilityProvider.ToLua(item));
            return call.Return(new LuaTable {
                ["status"] = "timeout",
                ["query"] = watched.Query,
                ["kind"] = kind,
                ["watch_id"] = watched.WatchId,
                ["revision"] = (double)watched.Revision,
            });
        });
        actors["wait_changed"] = new LuaFunction(async (call, cancellationToken) => {
            var query = RequiredQuery(call.GetArgument<string>(0));
            var afterRevision = ReadRevision(call.GetArgument<double>(1));
            var timeout = ReadTimeout(call, 2);
            var watch = registration.Script.WatchActor
                ?? throw new InvalidOperationException("actor watches are unavailable in this Lua host context");
            var attempts = PollAttempts(timeout);
            LuaActorWatchRegistration? latest = null;
            using var waiter = registration.Quota.EnterWaiter();
            for (var attempt = 0; attempt < attempts; attempt++) {
                await registration.ExecutionControl.WaitIfPausedAsync(cancellationToken);
                latest = await watch(query, cancellationToken);
                if (latest.Revision > afterRevision) {
                    var changed = ToLua(latest);
                    changed["actor_status"] = latest.Status;
                    changed["status"] = "changed";
                    return call.Return(changed);
                }
                if (attempt + 1 < attempts)
                    await registration.DelayAsync(ObservationPollInterval, cancellationToken);
            }
            var timedOut = latest == null ? new LuaTable() : ToLua(latest);
            timedOut["actor_status"] = latest?.Status ?? "missing";
            timedOut["status"] = "timeout";
            return call.Return(timedOut);
        });
        actors["job"] = new LuaFunction(async (call, cancellationToken) => {
            var query = RequiredQuery(call.GetArgument<string>(0));
            var registrationResult = await WatchOnce(registration, query, cancellationToken);
            return call.Return(ToActorJobObservation(registrationResult));
        });
        actors["wait_job_changed"] = new LuaFunction(async (call, cancellationToken) => {
            var query = RequiredQuery(call.GetArgument<string>(0));
            var previousClassJobId = ReadClassJobId(call.GetArgument<double>(1));
            var timeout = ReadTimeout(call, 2);
            var expectedGameObjectId = call.ArgumentCount > 3
                ? call.GetArgument<string>(3).Trim()
                : string.Empty;
            if (call.ArgumentCount > 3 && expectedGameObjectId.Length == 0)
                throw new ArgumentException("expected game-object ID cannot be empty");
            var watch = registration.Script.WatchActor
                ?? throw new InvalidOperationException("actor watches are unavailable in this Lua host context");
            var attempts = PollAttempts(timeout);
            LuaActorWatchRegistration? latest = null;
            using var waiter = registration.Quota.EnterWaiter();
            for (var attempt = 0; attempt < attempts; attempt++) {
                await registration.ExecutionControl.WaitIfPausedAsync(cancellationToken);
                latest = await watch(query, cancellationToken);
                var terminal = ObserveActorJob(latest, previousClassJobId, expectedGameObjectId);
                if (terminal != null)
                    return call.Return(terminal);
                if (attempt + 1 < attempts)
                    await registration.DelayAsync(ObservationPollInterval, cancellationToken);
            }
            return call.Return(ToActorJobStatus("timeout", latest, previousClassJobId));
        });
        actors["unwatch"] = new LuaFunction(async (call, cancellationToken) => {
            var watchId = RequiredQuery(call.GetArgument<string>(0));
            var unwatch = registration.Script.UnwatchActor
                ?? throw new InvalidOperationException("actor watches are unavailable in this Lua host context");
            using var waiter = registration.Quota.EnterWaiter();
            await registration.ExecutionControl.WaitIfPausedAsync(cancellationToken);
            return call.Return(await unwatch(watchId, cancellationToken));
        });
        actors["wait_visible"] = new LuaFunction(async (call, cancellationToken) => {
            var query = RequiredQuery(call.GetArgument<string>(0));
            var timeout = ReadTimeout(call, 1);
            if (registration.Script.WatchActor != null) {
                var watched = await WaitForActorWatchAsync(
                    registration,
                    query,
                    timeout,
                    cancellationToken,
                    value => (value.Status.Equals("found", StringComparison.OrdinalIgnoreCase)
                            && value.State.HasValue)
                        || value.Status.Equals("ambiguous", StringComparison.OrdinalIgnoreCase));
                return call.Return(watched == null
                    ? new LuaTable { ["status"] = "timeout" }
                    : ToActorLookupResult(watched));
            }
            var result = await WaitForSnapshotAsync(
                registration,
                timeout,
                cancellationToken,
                snapshot => {
                    var matches = FindActors(snapshot, query);
                    return matches.Length > 0
                        ? (true, (LuaValue)ToLookupResult(matches))
                        : (false, LuaValue.Nil);
                });
            return call.Return(result);
        });
        actors["wait_lost"] = new LuaFunction(async (call, cancellationToken) => {
            var query = RequiredQuery(call.GetArgument<string>(0));
            var timeout = ReadTimeout(call, 1);
            if (registration.Script.WatchActor != null) {
                var watched = await WaitForActorWatchAsync(
                    registration,
                    query,
                    timeout,
                    cancellationToken,
                    value => value.Status.Equals("missing", StringComparison.OrdinalIgnoreCase)
                        || value.Status.Equals("ambiguous", StringComparison.OrdinalIgnoreCase));
                return call.Return(watched == null
                    ? new LuaTable { ["status"] = "timeout" }
                    : watched.Status.Equals("ambiguous", StringComparison.OrdinalIgnoreCase)
                        ? ToActorLookupResult(watched)
                    : new LuaTable {
                        ["status"] = "lost",
                        ["query"] = watched.Query,
                        ["revision"] = (double)watched.Revision,
                        ["actor_status"] = watched.Status,
                    });
            }
            var result = await WaitForSnapshotAsync(
                registration,
                timeout,
                cancellationToken,
                snapshot => {
                    var matches = FindActors(snapshot, query);
                    if (matches.Length == 0)
                        return (true, (LuaValue)new LuaTable { ["status"] = "lost" });
                    return matches.Length > 1
                        ? (true, (LuaValue)ToLookupResult(matches))
                        : (false, LuaValue.Nil);
                });
            return call.Return(result);
        });
        actors["wait_proximity"] = new LuaFunction(async (call, cancellationToken) => {
            var query = RequiredQuery(call.GetArgument<string>(0));
            var distance = call.GetArgument<double>(1);
            if (!double.IsFinite(distance) || distance < 0 || distance > 1000)
                throw new ArgumentOutOfRangeException(nameof(distance), "proximity distance must be between 0 and 1000 yalms");
            var timeout = ReadTimeout(call, 2);
            if (registration.Script.WatchActor != null) {
                var watch = registration.Script.WatchActor;
                var attempts = PollAttempts(timeout);
                using var waiter = registration.Quota.EnterWaiter();
                for (var attempt = 0; attempt < attempts; attempt++) {
                    await registration.ExecutionControl.WaitIfPausedAsync(cancellationToken);
                    var targetState = await watch(query, cancellationToken);
                    var selfState = await watch(LuaActorWatchService.SelfQuery, cancellationToken);
                    if (targetState.Status.Equals("ambiguous", StringComparison.OrdinalIgnoreCase))
                        return call.Return(ToActorLookupResult(targetState));
                    if (targetState.Status.Equals("found", StringComparison.OrdinalIgnoreCase)
                        && targetState.State.HasValue
                        && selfState.Status.Equals("found", StringComparison.OrdinalIgnoreCase)
                        && selfState.State.HasValue) {
                        var targetActor = targetState.State.Value;
                        var selfActor = selfState.State.Value;
                        var dx = targetActor.PositionX - selfActor.PositionX;
                        var dy = targetActor.PositionY - selfActor.PositionY;
                        var dz = targetActor.PositionZ - selfActor.PositionZ;
                        var actual = Math.Sqrt(dx * dx + dy * dy + dz * dz);
                        if (actual <= distance) {
                            var value = ToActorLookupResult(targetState);
                            value["distance"] = actual;
                            return call.Return(value);
                        }
                    }
                    if (attempt + 1 < attempts)
                        await registration.DelayAsync(ObservationPollInterval, cancellationToken);
                }
                return call.Return(new LuaTable { ["status"] = "timeout" });
            }
            var result = await WaitForSnapshotAsync(
                registration,
                timeout,
                cancellationToken,
                snapshot => {
                    var matches = FindActors(snapshot, query);
                    if (matches.Length > 1)
                        return (true, (LuaValue)ToLookupResult(matches));
                    if (snapshot.Self == null || matches.Length == 0)
                        return (false, LuaValue.Nil);
                    var actual = Vector3.Distance(snapshot.Self.Position, matches[0].Position);
                    if (actual > distance)
                        return (false, LuaValue.Nil);
                    var value = ToLookupResult(matches);
                    value["distance"] = actual;
                    return (true, (LuaValue)value);
                });
            return call.Return(result);
        });

        var participants = registration.GetOrCreateModule("participants");
        participants["list"] = new LuaFunction(async (call, cancellationToken) => {
            var snapshot = await Capture(registration, cancellationToken);
            var result = new LuaTable();
            for (var index = 0; index < snapshot.Participants.Count; index++)
                result[index + 1] = ToLua(snapshot.Participants[index]);
            return call.Return(result);
        });

        var player = registration.GetOrCreateModule("player");
        player["profile"] = new LuaFunction(async (call, cancellationToken) => {
            var snapshot = await Capture(registration, cancellationToken);
            return call.Return(snapshot.PlayerProfile == null ? LuaValue.Nil : ToLua(snapshot.PlayerProfile));
        });

        var party = registration.GetOrCreateModule("party");
        party["list"] = new LuaFunction(async (call, cancellationToken) => {
            var snapshot = await Capture(registration, cancellationToken);
            return call.Return(ToLua(snapshot.Party));
        });

        var buddies = registration.GetOrCreateModule("buddies");
        buddies["list"] = new LuaFunction(async (call, cancellationToken) => {
            var snapshot = await Capture(registration, cancellationToken);
            return call.Return(ToLua(snapshot.Buddies));
        });

        var game = registration.GetOrCreateModule("game");
        game["snapshot"] = new LuaFunction(async (call, cancellationToken) => {
            var snapshot = await Capture(registration, cancellationToken);
            var conditions = new LuaTable();
            foreach (var (name, active) in snapshot.Conditions)
                conditions[name] = active;
            return call.Return(new LuaTable {
                ["territory_id"] = (double)snapshot.TerritoryId,
                ["map_id"] = (double)snapshot.MapId,
                ["instance_id"] = (double)snapshot.InstanceId,
                ["logged_in"] = snapshot.IsLoggedIn,
                ["gpose"] = snapshot.IsGPosing,
                ["pvp"] = snapshot.IsPvP,
                ["conditions"] = conditions,
            });
        });
        game["wait_condition"] = new LuaFunction(async (call, cancellationToken) => {
            var name = RequiredQuery(call.GetArgument<string>(0));
            var expected = call.ArgumentCount <= 1 || call.GetArgument<bool>(1);
            var timeout = ReadTimeout(call, 2);
            var result = await WaitForSnapshotAsync(
                registration,
                timeout,
                cancellationToken,
                snapshot => {
                    var condition = snapshot.Conditions.FirstOrDefault(pair =>
                        pair.Key.Equals(name, StringComparison.OrdinalIgnoreCase));
                    return !string.IsNullOrEmpty(condition.Key) && condition.Value == expected
                        ? (true, (LuaValue)new LuaTable {
                            ["status"] = "matched",
                            ["condition"] = condition.Key,
                            ["active"] = condition.Value,
                        })
                        : (false, LuaValue.Nil);
                });
            return call.Return(result);
        });

        target["wait_changed"] = new LuaFunction(async (call, cancellationToken) => {
            var timeout = ReadTimeout(call, 0);
            var initial = await Capture(registration, cancellationToken);
            var initialId = initial.SelectedTarget?.GameObjectId ?? string.Empty;
            var result = await WaitForSnapshotAsync(
                registration,
                timeout,
                cancellationToken,
                snapshot => {
                    var currentId = snapshot.SelectedTarget?.GameObjectId ?? string.Empty;
                    if (currentId.Equals(initialId, StringComparison.Ordinal))
                        return (false, LuaValue.Nil);
                    return (true, (LuaValue)new LuaTable {
                        ["status"] = "changed",
                        ["target"] = snapshot.SelectedTarget == null ? LuaValue.Nil : ToLua(snapshot.SelectedTarget),
                    });
                });
            return call.Return(result);
        });
    }

    private static LuaFunction SnapshotFunction(
        LuaApiRegistrationContext registration,
        Func<LuaGameSnapshot, LuaActorSnapshot?> select) =>
        new(async (call, cancellationToken) => {
            var actor = select(await Capture(registration, cancellationToken));
            return call.Return(actor == null ? LuaValue.Nil : ToLua(actor));
        });

    private static LuaFunction WatchFunction(
        LuaApiRegistrationContext registration,
        Func<LuaFunctionExecutionContext, string> readQuery) =>
        new(async (call, cancellationToken) => {
            var watch = registration.Script.WatchActor
                ?? throw new InvalidOperationException("actor watches are unavailable in this Lua host context");
            using var waiter = registration.Quota.EnterWaiter();
            await registration.ExecutionControl.WaitIfPausedAsync(cancellationToken);
            var result = await watch(readQuery(call), cancellationToken);
            return call.Return(ToLua(result));
        });

    private static async Task<LuaGameSnapshot> Capture(
        LuaApiRegistrationContext registration,
        System.Threading.CancellationToken cancellationToken) {
        var capture = registration.Script.CaptureGameSnapshot
            ?? throw new InvalidOperationException("game-state snapshots are unavailable in this Lua host context");
        using var waiter = registration.Quota.EnterWaiter();
        await registration.ExecutionControl.WaitIfPausedAsync(cancellationToken);
        return await capture(cancellationToken);
    }

    private static async Task<LuaActorWatchRegistration> WatchOnce(
        LuaApiRegistrationContext registration,
        string query,
        CancellationToken cancellationToken) {
        var watch = registration.Script.WatchActor
            ?? throw new InvalidOperationException("actor watches are unavailable in this Lua host context");
        using var waiter = registration.Quota.EnterWaiter();
        await registration.ExecutionControl.WaitIfPausedAsync(cancellationToken);
        return await watch(query, cancellationToken);
    }

    private static async Task<LuaActorWatchRegistration?> WaitForActorWatchAsync(
        LuaApiRegistrationContext registration,
        string query,
        TimeSpan timeout,
        CancellationToken cancellationToken,
        Func<LuaActorWatchRegistration, bool> complete) {
        var watch = registration.Script.WatchActor
            ?? throw new InvalidOperationException("actor watches are unavailable in this Lua host context");
        var attempts = PollAttempts(timeout);
        using var waiter = registration.Quota.EnterWaiter();
        for (var attempt = 0; attempt < attempts; attempt++) {
            await registration.ExecutionControl.WaitIfPausedAsync(cancellationToken);
            var observation = await watch(query, cancellationToken);
            if (complete(observation))
                return observation;
            if (attempt + 1 < attempts)
                await registration.DelayAsync(ObservationPollInterval, cancellationToken);
        }
        return null;
    }

    private static async Task<LuaGameSnapshot> CaptureWithoutWaiter(
        LuaApiRegistrationContext registration,
        CancellationToken cancellationToken) {
        var capture = registration.Script.CaptureGameSnapshot
            ?? throw new InvalidOperationException("game-state snapshots are unavailable in this Lua host context");
        await registration.ExecutionControl.WaitIfPausedAsync(cancellationToken);
        return await capture(cancellationToken);
    }

    private static async Task<LuaValue> WaitForSnapshotAsync(
        LuaApiRegistrationContext registration,
        TimeSpan timeout,
        CancellationToken cancellationToken,
        Func<LuaGameSnapshot, (bool Complete, LuaValue Result)> observe) {
        var attempts = PollAttempts(timeout);
        using var waiter = registration.Quota.EnterWaiter();
        for (var attempt = 0; attempt < attempts; attempt++) {
            var observation = observe(await CaptureWithoutWaiter(registration, cancellationToken));
            if (observation.Complete)
                return observation.Result;
            if (attempt + 1 < attempts)
                await registration.DelayAsync(ObservationPollInterval, cancellationToken);
        }
        return new LuaTable { ["status"] = "timeout" };
    }

    private static int PollAttempts(TimeSpan timeout) =>
        Math.Max(1, (int)Math.Ceiling(
            timeout.TotalMilliseconds / ObservationPollInterval.TotalMilliseconds) + 1);

    private static LuaActorSnapshot[] FindActors(LuaGameSnapshot snapshot, string query) {
        var exactId = snapshot.VisibleActors
            .Where(actor => actor.ObjectKind.Equals(ObjectKind.Pc.ToString(), StringComparison.Ordinal)
                && (actor.GameObjectId.Equals(query, StringComparison.Ordinal)
                    || actor.EntityId.ToString(System.Globalization.CultureInfo.InvariantCulture).Equals(query, StringComparison.Ordinal)))
            .ToArray();
        return exactId.Length > 0
            ? exactId
            : snapshot.VisibleActors
                .Where(actor => actor.ObjectKind.Equals(ObjectKind.Pc.ToString(), StringComparison.Ordinal)
                    && LuaActorQueryResolver.MatchesPlayerName(query, actor.Name))
                .ToArray();
    }

    private static LuaActorSnapshot? SelectTarget(LuaGameSnapshot snapshot, string kind) => kind switch {
        "selected" => snapshot.SelectedTarget,
        "focus" => snapshot.FocusTarget,
        "run" => snapshot.RunTarget,
        "self" => snapshot.Self,
        _ => throw new ArgumentException("target kind must be selected, focus, run, or self"),
    };

    private static string ReadTargetKind(LuaFunctionExecutionContext call, int index) {
        if (call.ArgumentCount <= index)
            return "selected";
        var kind = call.GetArgument<string>(index).Trim().ToLowerInvariant();
        return kind switch {
            "selected" or "target" => "selected",
            "focus" => "focus",
            "run" or "run-target" => "run",
            "self" => "self",
            _ => throw new ArgumentException("target kind must be selected, focus, run, or self"),
        };
    }

    private static uint ReadClassJobId(double value) {
        if (!double.IsFinite(value) || value < 1 || value > byte.MaxValue || value != Math.Truncate(value))
            throw new ArgumentOutOfRangeException(nameof(value), "class/job ID must be an integer between 1 and 255");
        return (uint)value;
    }

    private static long ReadRevision(double value) {
        if (!double.IsFinite(value) || value < 0 || value > long.MaxValue || value != Math.Truncate(value))
            throw new ArgumentOutOfRangeException(nameof(value), "actor revision must be a non-negative integer");
        return (long)value;
    }

    private static LuaTable ToActorJobObservation(LuaActorWatchRegistration registration) {
        if (!registration.State.HasValue)
            return ToActorJobStatus(registration.Status, registration, 0);
        if (registration.State.Value.ClassJobId == 0)
            return ToActorJobStatus("unavailable", registration, 0);
        var result = ToActorJobStatus("observed", registration, 0);
        result["class_job_id"] = (double)registration.State.Value.ClassJobId;
        return result;
    }

    private static LuaTable? ObserveActorJob(
        LuaActorWatchRegistration registration,
        uint previousClassJobId,
        string expectedGameObjectId) {
        if (registration.Status.Equals("ambiguous", StringComparison.OrdinalIgnoreCase))
            return ToActorJobStatus("ambiguous", registration, previousClassJobId);
        if (!registration.State.HasValue)
            return ToActorJobStatus("actor_lost", registration, previousClassJobId);
        var state = registration.State.Value;
        if (expectedGameObjectId.Length > 0
            && !state.GameObjectId.ToString(System.Globalization.CultureInfo.InvariantCulture)
                .Equals(expectedGameObjectId, StringComparison.Ordinal))
            return ToActorJobStatus("actor_changed", registration, previousClassJobId);
        if (state.ClassJobId == 0)
            return null;
        if (state.ClassJobId == previousClassJobId)
            return null;
        var changed = ToActorJobStatus("changed", registration, previousClassJobId);
        changed["class_job_id"] = (double)state.ClassJobId;
        return changed;
    }

    private static LuaTable ToActorJobStatus(
        string status,
        LuaActorWatchRegistration? registration,
        uint previousClassJobId) => new() {
        ["status"] = status,
        ["query"] = registration?.Query ?? string.Empty,
        ["count"] = registration?.State.HasValue == true
            ? 1d
            : (double)(registration?.MatchCount ?? 0),
        ["revision"] = (double)(registration?.Revision ?? 0),
        ["previous_class_job_id"] = (double)previousClassJobId,
        ["class_job_id"] = registration?.State.HasValue == true
            ? (double)registration.State.Value.ClassJobId
            : 0d,
        ["actor"] = registration?.State.HasValue == true
            ? ToLua(registration.State.Value, registration.Name, registration.OnlineStatusName)
            : LuaValue.Nil,
    };

    private static LuaTable ToJobObservation(LuaActorSnapshot? actor, string kind) {
        if (actor == null)
            return ToJobTerminalStatus("target_lost", kind, null, 0);
        if (actor.ClassJobId == 0)
            return ToJobTerminalStatus("unavailable", kind, actor, 0);
        return new LuaTable {
            ["status"] = "observed",
            ["target_kind"] = kind,
            ["class_job_id"] = (double)actor.ClassJobId,
            ["target"] = ToLua(actor),
        };
    }

    private static LuaTable ToJobChanged(LuaActorSnapshot actor, string kind, uint previousClassJobId) => new() {
        ["status"] = "changed",
        ["target_kind"] = kind,
        ["previous_class_job_id"] = (double)previousClassJobId,
        ["class_job_id"] = (double)actor.ClassJobId,
        ["target"] = ToLua(actor),
    };

    private static LuaTable ToJobTerminalStatus(
        string status,
        string kind,
        LuaActorSnapshot? actor,
        uint previousClassJobId) => new() {
        ["status"] = status,
        ["target_kind"] = kind,
        ["previous_class_job_id"] = (double)previousClassJobId,
        ["class_job_id"] = actor == null ? 0d : (double)actor.ClassJobId,
        ["target"] = actor == null ? LuaValue.Nil : ToLua(actor),
    };

    private static LuaTable ToLookupResult(IReadOnlyList<LuaActorSnapshot> matches) {
        var result = new LuaTable {
            ["status"] = matches.Count switch { 0 => "missing", 1 => "found", _ => "ambiguous" },
            ["count"] = (double)matches.Count,
        };
        if (matches.Count == 1)
            result["actor"] = ToLua(matches[0]);
        else if (matches.Count > 1)
            result["matches"] = ToLua(matches);
        return result;
    }

    private static LuaTable ToActorLookupResult(LuaActorWatchRegistration registration) => new() {
        ["status"] = registration.State.HasValue ? "found" : registration.Status,
        ["count"] = registration.State.HasValue ? 1d : (double)registration.MatchCount,
        ["query"] = registration.Query,
        ["revision"] = (double)registration.Revision,
        ["actor"] = registration.State.HasValue
            ? ToLua(registration.State.Value, registration.Name, registration.OnlineStatusName)
            : LuaValue.Nil,
    };

    private static LuaTable ToLua(LuaActorWatchRegistration registration) => new() {
        ["watch_id"] = registration.WatchId,
        ["query"] = registration.Query,
        ["status"] = registration.Status,
        ["count"] = registration.State.HasValue ? 1d : (double)registration.MatchCount,
        ["revision"] = (double)registration.Revision,
        ["changes"] = ToLua(registration.Changes),
        ["actor"] = registration.State.HasValue
            ? ToLua(registration.State.Value, registration.Name, registration.OnlineStatusName)
            : LuaValue.Nil,
    };

    private static LuaTable ToLua(LuaActorWatchChange changes) => new() {
        ["availability"] = changes.HasFlag(LuaActorWatchChange.Availability),
        ["movement"] = changes.HasFlag(LuaActorWatchChange.Movement),
        ["walk_mode"] = changes.HasFlag(LuaActorWatchChange.WalkMode),
        ["jump"] = changes.HasFlag(LuaActorWatchChange.Jump),
        ["target"] = changes.HasFlag(LuaActorWatchChange.Target),
        ["mount"] = changes.HasFlag(LuaActorWatchChange.Mount),
        ["companion"] = changes.HasFlag(LuaActorWatchChange.Companion),
        ["emote"] = changes.HasFlag(LuaActorWatchChange.Emote),
        ["pose"] = changes.HasFlag(LuaActorWatchChange.Pose),
        ["ornament"] = changes.HasFlag(LuaActorWatchChange.Ornament),
        ["facewear"] = changes.HasFlag(LuaActorWatchChange.Facewear),
        ["headgear"] = changes.HasFlag(LuaActorWatchChange.Headgear),
        ["visor"] = changes.HasFlag(LuaActorWatchChange.Visor),
        ["sprint"] = changes.HasFlag(LuaActorWatchChange.Sprint),
        ["weapon"] = changes.HasFlag(LuaActorWatchChange.Weapon),
        ["online_status"] = changes.HasFlag(LuaActorWatchChange.OnlineStatus),
        ["class_job"] = changes.HasFlag(LuaActorWatchChange.ClassJob),
    };

    private static LuaTable ToLua(
        in LuaActorWatchState state,
        string name,
        string onlineStatusName) => new() {
        ["name"] = name,
        ["game_object_id"] = state.GameObjectId.ToString(System.Globalization.CultureInfo.InvariantCulture),
        ["entity_id"] = (double)state.EntityId,
        ["target_game_object_id"] = state.TargetGameObjectId.ToString(System.Globalization.CultureInfo.InvariantCulture),
        ["position"] = new LuaTable {
            ["x"] = state.PositionX,
            ["y"] = state.PositionY,
            ["z"] = state.PositionZ,
        },
        ["position_x"] = state.PositionX,
        ["position_y"] = state.PositionY,
        ["position_z"] = state.PositionZ,
        ["is_targetable"] = state.IsTargetable,
        ["is_dead"] = state.IsDead,
        ["is_loaded"] = state.IsLoaded,
        ["is_moving"] = state.IsMoving,
        ["is_walking"] = state.IsWalking,
        ["is_jumping"] = state.IsJumping,
        ["jump_sequence"] = (double)state.JumpSequence,
        ["mount_id"] = (double)state.MountId,
        ["companion_id"] = (double)state.CompanionId,
        ["emote_id"] = (double)state.EmoteId,
        ["emote_target_game_object_id"] = state.EmoteTargetGameObjectId.ToString(System.Globalization.CultureInfo.InvariantCulture),
        ["is_emote_looping"] = state.IsEmoteLooping,
        ["pose_type"] = (double)state.PoseType,
        ["pose_state"] = (double)state.PoseState,
        ["ornament_id"] = (double)state.OrnamentId,
        ["facewear_id"] = (double)state.FacewearId,
        ["is_headgear_visible"] = state.IsHeadgearVisible,
        ["is_visor_toggled"] = state.IsVisorToggled,
        ["is_sprinting"] = state.IsSprinting,
        ["is_weapon_drawn"] = state.IsWeaponDrawn,
        ["online_status_id"] = (double)state.OnlineStatusId,
        ["online_status_name"] = onlineStatusName,
        ["class_job_id"] = (double)state.ClassJobId,
    };

    private static string RequiredQuery(string value) {
        value = value?.Trim() ?? string.Empty;
        return value.Length > 0 ? value : throw new ArgumentException("query cannot be empty");
    }

    private static TimeSpan ReadTimeout(LuaFunctionExecutionContext call, int index) {
        var seconds = call.ArgumentCount <= index ? 30.0 : call.GetArgument<double>(index);
        if (!double.IsFinite(seconds) || seconds <= 0 || seconds > 120)
            throw new ArgumentOutOfRangeException(nameof(seconds), "timeout must be between 0 and 120 seconds");
        return TimeSpan.FromSeconds(seconds);
    }

    private static LuaTable ToLua(System.Collections.Generic.IReadOnlyList<LuaActorSnapshot> actors) {
        var result = new LuaTable();
        for (var index = 0; index < actors.Count; index++)
            result[index + 1] = ToLua(actors[index]);
        return result;
    }

    private static LuaTable ToLua(IReadOnlyList<LuaBuddySnapshot> buddies) {
        var result = new LuaTable();
        for (var index = 0; index < buddies.Count; index++) {
            var buddy = buddies[index];
            result[index + 1] = new LuaTable {
                ["kind"] = buddy.Kind,
                ["game_object_id"] = buddy.GameObjectId,
                ["entity_id"] = (double)buddy.EntityId,
                ["data_id"] = (double)buddy.DataId,
                ["hp"] = new LuaTable {
                    ["current"] = (double)buddy.CurrentHp,
                    ["max"] = (double)buddy.MaxHp,
                },
                ["actor"] = buddy.Actor == null ? LuaValue.Nil : ToLua(buddy.Actor),
            };
        }
        return result;
    }

    private static LuaTable ToLua(LuaParticipantSnapshot participant) => new() {
        ["slot"] = (double)participant.Slot,
        ["content_id"] = participant.ContentId.ToString(System.Globalization.CultureInfo.InvariantCulture),
        ["name"] = participant.Name,
        ["is_local"] = participant.IsLocal,
        ["is_visible"] = participant.IsVisible || participant.IsLocal || participant.Actor != null,
        ["authority"] = participant.Authority,
        ["actor"] = participant.Actor == null ? LuaValue.Nil : ToLua(participant.Actor),
    };

    private static LuaTable ToLua(LuaPlayerProfileSnapshot profile) => new() {
        ["content_id"] = profile.ContentId,
        ["character_name"] = profile.CharacterName,
        ["entity_id"] = (double)profile.EntityId,
        ["current_world_id"] = (double)profile.CurrentWorldId,
        ["home_world_id"] = (double)profile.HomeWorldId,
        ["class_job_id"] = (double)profile.ClassJobId,
        ["level"] = (double)profile.Level,
        ["effective_level"] = (double)profile.EffectiveLevel,
        ["race_id"] = (double)profile.RaceId,
        ["tribe_id"] = (double)profile.TribeId,
        ["grand_company_id"] = (double)profile.GrandCompanyId,
        ["sex"] = profile.Sex,
        ["is_loaded"] = profile.IsLoaded,
        ["is_level_synced"] = profile.IsLevelSynced,
        ["is_mentor"] = profile.IsMentor,
        ["is_battle_mentor"] = profile.IsBattleMentor,
        ["is_trade_mentor"] = profile.IsTradeMentor,
        ["is_novice"] = profile.IsNovice,
        ["is_returner"] = profile.IsReturner,
        ["attributes"] = new LuaTable {
            ["strength"] = (double)profile.Strength,
            ["dexterity"] = (double)profile.Dexterity,
            ["vitality"] = (double)profile.Vitality,
            ["intelligence"] = (double)profile.Intelligence,
            ["mind"] = (double)profile.Mind,
            ["piety"] = (double)profile.Piety,
        },
    };

    private static LuaTable ToLua(LuaActorSnapshot actor) => new() {
        ["name"] = actor.Name,
        ["authority"] = actor.Authority,
        ["object_kind"] = actor.ObjectKind,
        ["game_object_id"] = actor.GameObjectId,
        ["entity_id"] = (double)actor.EntityId,
        ["target_game_object_id"] = actor.TargetGameObjectId,
        ["position"] = new LuaTable { ["x"] = actor.Position.X, ["y"] = actor.Position.Y, ["z"] = actor.Position.Z },
        ["rotation"] = actor.Rotation,
        ["is_local"] = actor.IsLocal,
        ["is_targetable"] = actor.IsTargetable,
        ["is_dead"] = actor.IsDead,
        ["is_loaded"] = actor.IsLoaded,
        ["is_moving"] = actor.IsMoving,
        ["is_walking"] = actor.IsWalking,
        ["is_jumping"] = actor.IsJumping,
        ["mount_id"] = (double)actor.MountId,
        ["companion_id"] = (double)actor.CompanionId,
        ["emote_id"] = (double)actor.EmoteId,
        ["emote_target_game_object_id"] = actor.EmoteTargetGameObjectId,
        ["is_emote_looping"] = actor.IsEmoteLooping,
        ["pose_type"] = (double)actor.PoseType,
        ["pose_state"] = (double)actor.PoseState,
        ["ornament_id"] = (double)actor.OrnamentId,
        ["facewear_id"] = (double)actor.FacewearId,
        ["is_headgear_visible"] = actor.IsHeadgearVisible,
        ["is_visor_toggled"] = actor.IsVisorToggled,
        ["is_weapon_drawn"] = actor.IsWeaponDrawn,
        ["online_status_id"] = (double)actor.OnlineStatusId,
        ["online_status_name"] = actor.OnlineStatusName,
        ["class_job_id"] = (double)actor.ClassJobId,
        ["level"] = (double)actor.Level,
        ["hp"] = new LuaTable { ["current"] = (double)actor.CurrentHp, ["max"] = (double)actor.MaxHp },
        ["mp"] = new LuaTable { ["current"] = (double)actor.CurrentMp, ["max"] = (double)actor.MaxMp },
        ["cp"] = new LuaTable { ["current"] = (double)actor.CurrentCp, ["max"] = (double)actor.MaxCp },
        ["gp"] = new LuaTable { ["current"] = (double)actor.CurrentGp, ["max"] = (double)actor.MaxGp },
        ["status_flags"] = actor.StatusFlags,
        ["cast"] = new LuaTable {
            ["is_casting"] = actor.IsCasting,
            ["action_id"] = (double)actor.CastActionId,
            ["target_game_object_id"] = actor.CastTargetGameObjectId,
            ["current_time"] = actor.CurrentCastTime,
            ["total_time"] = actor.TotalCastTime,
        },
    };
}
