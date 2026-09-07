using System;
using System.Collections.Generic;
using System.Linq;

namespace MasterOfPuppets.LuaScripting.Synchronization;

public sealed class LuaReplayWindow {
    public static readonly TimeSpan DefaultRetention = TimeSpan.FromMinutes(5);
    public static readonly TimeSpan MaximumMessageAge = TimeSpan.FromMinutes(2);
    public static readonly TimeSpan MaximumFutureSkew = TimeSpan.FromSeconds(15);
    public const int DefaultMaximumEntries = 1024;

    private readonly object _sync = new();
    private readonly Dictionary<Guid, DateTimeOffset> _accepted = new();
    private readonly TimeSpan _retention;
    private readonly int _maximumEntries;

    public LuaReplayWindow(TimeSpan? retention = null, int maximumEntries = DefaultMaximumEntries) {
        _retention = retention ?? DefaultRetention;
        if (_retention <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(retention));
        if (maximumEntries <= 0)
            throw new ArgumentOutOfRangeException(nameof(maximumEntries));
        _maximumEntries = maximumEntries;
    }

    public bool TryAccept(string messageId, long createdUnixMilliseconds, DateTimeOffset now, out string reason) {
        if (!Guid.TryParse(messageId, out var id) || id == Guid.Empty) {
            reason = "the synchronization message ID is invalid";
            return false;
        }
        DateTimeOffset created;
        try {
            created = DateTimeOffset.FromUnixTimeMilliseconds(createdUnixMilliseconds);
        } catch (ArgumentOutOfRangeException) {
            reason = "the synchronization timestamp is invalid";
            return false;
        }
        if (now - created > MaximumMessageAge) {
            reason = "the synchronization message is stale";
            return false;
        }
        if (created - now > MaximumFutureSkew) {
            reason = "the synchronization message timestamp is too far in the future";
            return false;
        }

        lock (_sync) {
            Prune(now);
            if (_accepted.ContainsKey(id)) {
                reason = "the synchronization message is a replay";
                return false;
            }
            if (_accepted.Count >= _maximumEntries)
                RemoveOldest();
            _accepted[id] = now;
        }
        reason = string.Empty;
        return true;
    }

    public int Count {
        get { lock (_sync) return _accepted.Count; }
    }

    private void Prune(DateTimeOffset now) {
        foreach (var (id, acceptedAt) in _accepted.ToArray())
            if (now - acceptedAt > _retention)
                _accepted.Remove(id);
    }

    private void RemoveOldest() {
        Guid oldestId = default;
        var oldest = DateTimeOffset.MaxValue;
        foreach (var (id, acceptedAt) in _accepted)
            if (acceptedAt < oldest) {
                oldest = acceptedAt;
                oldestId = id;
            }
        if (oldestId != default)
            _accepted.Remove(oldestId);
    }
}
