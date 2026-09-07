using System;
using System.Collections.Generic;
using System.Linq;

using MasterOfPuppets.LuaScripting.Snapshots;

namespace MasterOfPuppets.LuaScripting.Events;

public sealed record LuaTrackedGameEvent(string Name, IReadOnlyDictionary<string, string> Data);

/// <summary>
/// Converts immutable game snapshots into a small deterministic change stream.
/// Snapshot capture remains a framework-thread responsibility; this tracker is
/// game-independent and never invokes Lua.
/// </summary>
public sealed class LuaGameEventTracker {
    private readonly TimeSpan _minimumInterval;
    private LuaGameSnapshot? _previous;
    private DateTimeOffset _nextObservationAt;

    public LuaGameEventTracker(TimeSpan? minimumInterval = null) {
        _minimumInterval = minimumInterval ?? TimeSpan.FromMilliseconds(250);
        if (_minimumInterval <= TimeSpan.Zero || _minimumInterval > TimeSpan.FromSeconds(5))
            throw new ArgumentOutOfRangeException(nameof(minimumInterval));
    }

    public bool ShouldObserve(DateTimeOffset now) {
        if (now < _nextObservationAt)
            return false;
        _nextObservationAt = now + _minimumInterval;
        return true;
    }

    public IReadOnlyList<LuaTrackedGameEvent> Observe(LuaGameSnapshot current) {
        ArgumentNullException.ThrowIfNull(current);
        if (_previous == null) {
            _previous = current;
            return Array.Empty<LuaTrackedGameEvent>();
        }

        var events = new List<LuaTrackedGameEvent>();
        AddTargetChange(events, "selected", _previous.SelectedTarget, current.SelectedTarget);
        AddTargetChange(events, "focus", _previous.FocusTarget, current.FocusTarget);
        AddConditionChanges(events, _previous.Conditions, current.Conditions);
        AddParticipantVisibilityChanges(events, _previous.Participants, current.Participants);
        _previous = current;
        return events;
    }

    private static void AddTargetChange(
        ICollection<LuaTrackedGameEvent> events,
        string kind,
        LuaActorSnapshot? previous,
        LuaActorSnapshot? current) {
        if (string.Equals(previous?.GameObjectId, current?.GameObjectId, StringComparison.Ordinal))
            return;
        events.Add(new LuaTrackedGameEvent("target.changed", Fields(
            ("kind", kind),
            ("previous_id", previous?.GameObjectId ?? string.Empty),
            ("previous_name", previous?.Name ?? string.Empty),
            ("current_id", current?.GameObjectId ?? string.Empty),
            ("current_name", current?.Name ?? string.Empty))));
    }

    private static void AddConditionChanges(
        ICollection<LuaTrackedGameEvent> events,
        IReadOnlyDictionary<string, bool> previous,
        IReadOnlyDictionary<string, bool> current) {
        foreach (var name in previous.Keys.Concat(current.Keys).Distinct(StringComparer.Ordinal).OrderBy(name => name, StringComparer.Ordinal)) {
            var wasActive = previous.GetValueOrDefault(name);
            var isActive = current.GetValueOrDefault(name);
            if (wasActive == isActive)
                continue;
            events.Add(new LuaTrackedGameEvent("condition.changed", Fields(
                ("condition", name),
                ("active", isActive ? "true" : "false"))));
        }
    }

    private static void AddParticipantVisibilityChanges(
        ICollection<LuaTrackedGameEvent> events,
        IReadOnlyList<LuaParticipantSnapshot> previous,
        IReadOnlyList<LuaParticipantSnapshot> current) {
        var previousByCid = previous.Where(item => item.ContentId != 0).ToDictionary(item => item.ContentId);
        foreach (var participant in current.Where(item => item.ContentId != 0).OrderBy(item => item.Slot)) {
            var wasVisible = previousByCid.TryGetValue(participant.ContentId, out var old) && old.Actor != null;
            var isVisible = participant.Actor != null;
            if (wasVisible == isVisible)
                continue;
            events.Add(new LuaTrackedGameEvent(
                isVisible ? "participant.visible" : "participant.lost",
                Fields(
                    ("content_id", participant.ContentId.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                    ("slot", participant.Slot.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                    ("name", participant.Name),
                    ("authority", participant.Authority))));
        }
    }

    private static IReadOnlyDictionary<string, string> Fields(params (string Key, string Value)[] fields) =>
        fields.ToDictionary(field => field.Key, field => field.Value, StringComparer.Ordinal);
}
