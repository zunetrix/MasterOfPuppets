using System;
using System.Collections.Generic;
using System.Globalization;

using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.Types;

using MasterOfPuppets.Extensions.Dalamud;
using MasterOfPuppets.Formations;
using MasterOfPuppets.LuaScripting.Events;
using MasterOfPuppets.LuaScripting.Snapshots;

namespace MasterOfPuppets.LuaScripting.Watches;

/// <summary>
/// Shared target-scoped observation for all active Lua runs in one plugin
/// process. Actor discovery is performed only on registration or bounded
/// reacquisition; steady-state updates sample cached actors without enumerating
/// the world and publish only meaningful state transitions.
/// </summary>
internal sealed class LuaActorWatchService : IDisposable {
    internal const string SelfQuery = "$self";
    private const long ReacquireIntervalMs = 500;

    private readonly Dictionary<string, WatchEntry> _entries = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Subscription> _subscriptions = new(StringComparer.Ordinal);
    private long _nextWatchId;
    private bool _disposed;

    public LuaActorWatchRegistration Watch(string ownerId, string query, LuaEventHub events) {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerId);
        ArgumentNullException.ThrowIfNull(events);
        query = NormalizeQuery(query);
        var entryKey = query.Equals(SelfQuery, StringComparison.OrdinalIgnoreCase)
            ? SelfQuery
            : FormationCharacterName.NormalizeWorldSeparator(query).Trim();
        var subscriptionKey = ownerId + "\0" + entryKey.ToLowerInvariant();
        if (_subscriptions.TryGetValue(subscriptionKey, out var existing))
            return Registration(existing);

        if (!_entries.TryGetValue(entryKey, out var entry)) {
            entry = new WatchEntry(entryKey, query);
            _entries.Add(entryKey, entry);
            Acquire(entry, Environment.TickCount64);
        }

        _nextWatchId++;
        var watchId = "actor-watch-" + _nextWatchId.ToString(CultureInfo.InvariantCulture);
        var subscription = new Subscription(subscriptionKey, watchId, ownerId, entry, events);
        entry.Subscriptions.Add(subscription);
        _subscriptions.Add(subscriptionKey, subscription);
        return Registration(subscription);
    }

    public bool Unwatch(string ownerId, string watchId) {
        if (_disposed || string.IsNullOrWhiteSpace(ownerId) || string.IsNullOrWhiteSpace(watchId))
            return false;
        Subscription? found = null;
        foreach (var subscription in _subscriptions.Values) {
            if (subscription.OwnerId.Equals(ownerId, StringComparison.Ordinal)
                && subscription.WatchId.Equals(watchId, StringComparison.Ordinal)) {
                found = subscription;
                break;
            }
        }
        if (found == null)
            return false;
        Remove(found);
        return true;
    }

    public bool HasObservedSource(uint entityId) {
        if (_disposed || entityId is 0 or 0xE0000000)
            return false;
        foreach (var entry in _entries.Values) {
            if (entry.State.HasValue && entry.State.Value.EntityId == entityId)
                return true;
        }
        return false;
    }

    public void ReleaseOwner(string ownerId) {
        if (_disposed || string.IsNullOrWhiteSpace(ownerId))
            return;
        List<Subscription>? remove = null;
        foreach (var subscription in _subscriptions.Values) {
            if (!subscription.OwnerId.Equals(ownerId, StringComparison.Ordinal))
                continue;
            remove ??= new List<Subscription>();
            remove.Add(subscription);
        }
        if (remove == null)
            return;
        foreach (var subscription in remove)
            Remove(subscription);
    }

    public void Update(long nowMs) {
        if (_disposed)
            return;
        foreach (var entry in _entries.Values) {
            if (!IsUsable(entry.CachedActor, entry.ExpectedGameObjectId)) {
                if (entry.Status.Equals("found", StringComparison.Ordinal))
                    MarkLost(entry, "missing");
                if (nowMs - entry.LastAcquireAttemptMs >= ReacquireIntervalMs)
                    Acquire(entry, nowMs);
                continue;
            }

            var raw = DalamudLuaGameSnapshotCapture.CaptureWatchState(entry.CachedActor!);
            if (!raw.IsLoaded) {
                MarkLost(entry, "missing");
                continue;
            }
            var current = entry.Jumps.Observe(entry.Motion.Observe(raw, nowMs), nowMs);
            if (!entry.State.HasValue) {
                entry.State = current;
                entry.Status = "found";
                RefreshLabels(entry);
                Advance(entry, LuaActorWatchChange.Availability);
                Publish(entry, "actor.found", LuaActorWatchChange.Availability);
                continue;
            }

            var previous = entry.State.Value;
            var changes = current.ChangesFrom(previous);
            entry.State = current;
            if (changes == LuaActorWatchChange.None)
                continue;
            if ((changes & LuaActorWatchChange.OnlineStatus) != 0)
                entry.OnlineStatusName = GetOnlineStatusName(entry.CachedActor);
            Advance(entry, changes);
            Publish(entry, "actor.state", changes);
            PublishStateEvents(entry, changes);
        }
    }

    /// <summary>
    /// Routes one native action edge only to watches bound to its real source
    /// entity. No participant/configuration lookup or object-table scan occurs.
    /// </summary>
    public void PublishCombatAction(
        global::MasterOfPuppets.CombatActionObservation observation,
        bool isGroundTargeted) {
        if (_disposed || observation.SourceEntityId == 0)
            return;
        foreach (var entry in _entries.Values) {
            if (!entry.State.HasValue || entry.State.Value.EntityId != observation.SourceEntityId)
                continue;
            foreach (var subscription in entry.Subscriptions) {
                var data = BuildPayload(subscription, LuaActorWatchChange.None);
                data["source_game_object_id"] = entry.State.Value.GameObjectId.ToString(CultureInfo.InvariantCulture);
                AddCombatActionFields(data, observation, isGroundTargeted);
                subscription.Events.Publish(LuaActorEventNames.Action, data, observation.Timestamp);
                var specialized = observation.ActionType == 5
                    ? LuaActorEventNames.GeneralAction
                    : observation.ActionType is 1 or 11 or 14
                        ? LuaActorEventNames.CombatAction
                        : null;
                if (specialized != null)
                    subscription.Events.Publish(specialized, data, observation.Timestamp);
            }
        }
    }

    /// <summary>
    /// Routes short native emote edges to matching actor watches. Persistent
    /// emote/stop state continues to be reconciled by the sampled watch.
    /// </summary>
    public void PublishEmote(global::MasterOfPuppets.EmoteObservation observation) {
        if (_disposed || observation.SourceEntityId == 0 || observation.EmoteId == 0)
            return;
        foreach (var entry in _entries.Values) {
            if (!entry.State.HasValue || entry.State.Value.EntityId != observation.SourceEntityId)
                continue;
            foreach (var subscription in entry.Subscriptions) {
                var data = BuildPayload(subscription, LuaActorWatchChange.Emote);
                data["source_game_object_id"] = entry.State.Value.GameObjectId.ToString(CultureInfo.InvariantCulture);
                AddEmoteFields(data, observation);
                subscription.Events.Publish(LuaActorEventNames.EmotePlayed, data, observation.Timestamp);
            }
        }
    }

    public void Dispose() {
        _disposed = true;
        _subscriptions.Clear();
        _entries.Clear();
    }

    private static string NormalizeQuery(string query) {
        query = query?.Trim() ?? string.Empty;
        if (query.Length == 0)
            throw new ArgumentException("actor watch query cannot be empty", nameof(query));
        if (query.Length > 200)
            throw new ArgumentException("actor watch query cannot exceed 200 characters", nameof(query));
        return query;
    }

    private void Acquire(WatchEntry entry, long nowMs) {
        entry.LastAcquireAttemptMs = nowMs;
        var previousStatus = entry.Status;
        var previousMatchCount = entry.MatchCount;
        var resolution = Resolve(entry.Query);
        entry.Status = resolution.Status;
        entry.MatchCount = resolution.MatchCount;
        entry.CachedActor = resolution.Actor;
        entry.ExpectedGameObjectId = resolution.Actor?.GameObjectId ?? 0;
        entry.State = null;
        entry.Jumps.Reset();
        entry.Motion.Reset();
        if (resolution.Actor == null) {
            if (!previousStatus.Equals(entry.Status, StringComparison.Ordinal)
                || previousMatchCount != entry.MatchCount) {
                Advance(entry, LuaActorWatchChange.Availability);
                Publish(entry, "actor.lost", LuaActorWatchChange.Availability);
            }
            return;
        }

        var raw = DalamudLuaGameSnapshotCapture.CaptureWatchState(resolution.Actor);
        if (!raw.IsLoaded) {
            MarkLost(entry, "missing");
            return;
        }
        entry.State = entry.Jumps.Observe(entry.Motion.Observe(raw, nowMs), nowMs);
        entry.Status = "found";
        RefreshLabels(entry);
        if (!previousStatus.Equals("found", StringComparison.Ordinal)) {
            Advance(entry, LuaActorWatchChange.Availability);
            Publish(entry, "actor.found", LuaActorWatchChange.Availability);
        }
    }

    private static Resolution Resolve(string query) {
        if (query.Equals(SelfQuery, StringComparison.OrdinalIgnoreCase)) {
            var local = DalamudApi.ObjectTable.LocalPlayer;
            return local == null ? new Resolution("missing", null, 0) : new Resolution("found", local, 1);
        }
        var resolution = LuaActorQueryResolver.ResolvePlayer(DalamudApi.ObjectTable, query);
        return new Resolution(resolution.Status, resolution.Actor, resolution.MatchCount);
    }

    private static bool IsPlayerCandidate(IGameObject? actor) =>
        LuaActorQueryResolver.IsUsablePlayer(actor);

    private static bool IsUsable(IGameObject? actor, ulong expectedGameObjectId) =>
        IsPlayerCandidate(actor)
        && expectedGameObjectId != 0
        && actor!.GameObjectId == expectedGameObjectId;

    private static void RefreshLabels(WatchEntry entry) {
        var actor = entry.CachedActor;
        entry.Name = actor?.GetPlayerNameWorld() ?? actor?.Name.TextValue ?? string.Empty;
        entry.OnlineStatusName = GetOnlineStatusName(actor);
    }

    private static string GetOnlineStatusName(IGameObject? actor) =>
        (actor as ICharacter)?.OnlineStatus.Value.Name.ToString() ?? string.Empty;

    private static LuaActorWatchRegistration Registration(Subscription subscription) {
        var entry = subscription.Entry;
        return new LuaActorWatchRegistration(
            subscription.WatchId,
            entry.Query,
            entry.Status,
            entry.Name,
            entry.OnlineStatusName,
            entry.State) {
            Revision = entry.Revision,
            Changes = entry.LastChanges,
            MatchCount = entry.MatchCount,
        };
    }

    private void MarkLost(WatchEntry entry, string status) {
        var wasFound = entry.Status.Equals("found", StringComparison.Ordinal);
        entry.Status = status;
        entry.CachedActor = null;
        entry.ExpectedGameObjectId = 0;
        entry.MatchCount = 0;
        entry.State = null;
        entry.Jumps.Reset();
        entry.Motion.Reset();
        if (wasFound) {
            Advance(entry, LuaActorWatchChange.Availability);
            Publish(entry, "actor.lost", LuaActorWatchChange.Availability);
        }
    }

    private static void Advance(WatchEntry entry, LuaActorWatchChange changes) {
        entry.Revision++;
        entry.LastChanges = changes;
    }

    private static void Publish(WatchEntry entry, string eventName, LuaActorWatchChange changes) {
        foreach (var subscription in entry.Subscriptions)
            subscription.Events.Publish(eventName, BuildPayload(subscription, changes));
    }

    private static void PublishStateEvents(WatchEntry entry, LuaActorWatchChange changes) {
        foreach (var (change, eventName) in LuaActorEventNames.StateEvents)
            PublishIfChanged(entry, changes, change, eventName);
    }

    private static void PublishIfChanged(
        WatchEntry entry,
        LuaActorWatchChange changes,
        LuaActorWatchChange expected,
        string eventName) {
        if ((changes & expected) != 0)
            Publish(entry, eventName, expected);
    }

    private static Dictionary<string, string> BuildPayload(
        Subscription subscription,
        LuaActorWatchChange changes) {
        var entry = subscription.Entry;
        var data = new Dictionary<string, string>(48, StringComparer.Ordinal) {
            ["watch_id"] = subscription.WatchId,
            ["query"] = entry.Query,
            ["status"] = entry.Status,
            ["name"] = entry.Name,
            ["online_status_name"] = entry.OnlineStatusName,
            ["revision"] = entry.Revision.ToString(CultureInfo.InvariantCulture),
            ["match_count"] = entry.MatchCount.ToString(CultureInfo.InvariantCulture),
            ["changes"] = changes.ToString(),
        };
        if (!entry.State.HasValue)
            return data;
        AddState(data, entry.State.Value);
        return data;
    }

    internal static string ActionKind(byte actionType) => actionType switch {
        1 => "action",
        5 => "general_action",
        11 => "pet_action",
        14 => "pvp_action",
        _ => "unknown",
    };

    internal static void AddCombatActionFields(
        IDictionary<string, string> data,
        global::MasterOfPuppets.CombatActionObservation observation,
        bool isGroundTargeted) {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(observation);
        data["source_entity_id"] = observation.SourceEntityId.ToString(CultureInfo.InvariantCulture);
        data["action_id"] = observation.ActionId.ToString(CultureInfo.InvariantCulture);
        data["action_type"] = observation.ActionType.ToString(CultureInfo.InvariantCulture);
        data["action_kind"] = ActionKind(observation.ActionType);
        data["global_sequence"] = observation.GlobalSequence.ToString(CultureInfo.InvariantCulture);
        data["source_sequence"] = observation.SourceSequence.ToString(CultureInfo.InvariantCulture);
        data["spell_id"] = observation.SpellId.ToString(CultureInfo.InvariantCulture);
        data["animation_target_id"] = observation.AnimationTargetId.ToString(CultureInfo.InvariantCulture);
        data["target_ids"] = string.Join(',', observation.TargetIds);
        data["is_ground_targeted"] = isGroundTargeted ? "true" : "false";
        if (observation.TargetPosition is not { } position)
            return;
        data["target_x"] = position.X.ToString("R", CultureInfo.InvariantCulture);
        data["target_y"] = position.Y.ToString("R", CultureInfo.InvariantCulture);
        data["target_z"] = position.Z.ToString("R", CultureInfo.InvariantCulture);
    }

    internal static void AddEmoteFields(
        IDictionary<string, string> data,
        global::MasterOfPuppets.EmoteObservation observation) {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(observation);
        data["source_entity_id"] = observation.SourceEntityId.ToString(CultureInfo.InvariantCulture);
        data["emote_id"] = observation.EmoteId.ToString(CultureInfo.InvariantCulture);
        data["target_id"] = observation.TargetId.ToString(CultureInfo.InvariantCulture);
        data["is_persistent"] = observation.IsPersistent ? "true" : "false";
    }

    internal static void AddState(IDictionary<string, string> data, in LuaActorWatchState state) {
        data["game_object_id"] = state.GameObjectId.ToString(CultureInfo.InvariantCulture);
        data["entity_id"] = state.EntityId.ToString(CultureInfo.InvariantCulture);
        data["target_game_object_id"] = state.TargetGameObjectId.ToString(CultureInfo.InvariantCulture);
        data["position_x"] = state.PositionX.ToString("R", CultureInfo.InvariantCulture);
        data["position_y"] = state.PositionY.ToString("R", CultureInfo.InvariantCulture);
        data["position_z"] = state.PositionZ.ToString("R", CultureInfo.InvariantCulture);
        data["is_targetable"] = Bool(state.IsTargetable);
        data["is_dead"] = Bool(state.IsDead);
        data["is_loaded"] = Bool(state.IsLoaded);
        data["is_moving"] = Bool(state.IsMoving);
        data["is_walking"] = Bool(state.IsWalking);
        data["is_jumping"] = Bool(state.IsJumping);
        data["jump_sequence"] = state.JumpSequence.ToString(CultureInfo.InvariantCulture);
        data["mount_id"] = state.MountId.ToString(CultureInfo.InvariantCulture);
        data["companion_id"] = state.CompanionId.ToString(CultureInfo.InvariantCulture);
        data["emote_id"] = state.EmoteId.ToString(CultureInfo.InvariantCulture);
        data["emote_target_game_object_id"] = state.EmoteTargetGameObjectId.ToString(CultureInfo.InvariantCulture);
        data["is_emote_looping"] = Bool(state.IsEmoteLooping);
        data["pose_type"] = state.PoseType.ToString(CultureInfo.InvariantCulture);
        data["pose_state"] = state.PoseState.ToString(CultureInfo.InvariantCulture);
        data["ornament_id"] = state.OrnamentId.ToString(CultureInfo.InvariantCulture);
        data["facewear_id"] = state.FacewearId.ToString(CultureInfo.InvariantCulture);
        data["is_headgear_visible"] = Bool(state.IsHeadgearVisible);
        data["is_visor_toggled"] = Bool(state.IsVisorToggled);
        data["is_sprinting"] = Bool(state.IsSprinting);
        data["is_weapon_drawn"] = Bool(state.IsWeaponDrawn);
        data["online_status_id"] = state.OnlineStatusId.ToString(CultureInfo.InvariantCulture);
        data["class_job_id"] = state.ClassJobId.ToString(CultureInfo.InvariantCulture);
    }

    private static string Bool(bool value) => value ? "true" : "false";

    private void Remove(Subscription subscription) {
        _subscriptions.Remove(subscription.SubscriptionKey);
        subscription.Entry.Subscriptions.Remove(subscription);
        if (subscription.Entry.Subscriptions.Count == 0)
            _entries.Remove(subscription.Entry.EntryKey);
    }

    private sealed class WatchEntry {
        public WatchEntry(string entryKey, string query) {
            EntryKey = entryKey;
            Query = query;
        }

        public string EntryKey { get; }
        public string Query { get; }
        public string Status { get; set; } = "missing";
        public string Name { get; set; } = string.Empty;
        public string OnlineStatusName { get; set; } = string.Empty;
        public IGameObject? CachedActor { get; set; }
        public ulong ExpectedGameObjectId { get; set; }
        public LuaActorWatchState? State { get; set; }
        public long Revision { get; set; }
        public LuaActorWatchChange LastChanges { get; set; }
        public int MatchCount { get; set; }
        public long LastAcquireAttemptMs { get; set; } = long.MinValue / 2;
        public LuaActorJumpTracker Jumps { get; } = new();
        public LuaActorMotionTracker Motion { get; } = new();
        public List<Subscription> Subscriptions { get; } = new();
    }

    private sealed record Subscription(
        string SubscriptionKey,
        string WatchId,
        string OwnerId,
        WatchEntry Entry,
        LuaEventHub Events);

    private readonly record struct Resolution(string Status, IGameObject? Actor, int MatchCount);
}
