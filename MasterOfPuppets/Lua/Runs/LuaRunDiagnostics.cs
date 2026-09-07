using System;
using System.Collections.Generic;

using MasterOfPuppets.LuaScripting.Events;

namespace MasterOfPuppets.LuaScripting.Runs;

public sealed record LuaRunLogEntry(
    long Sequence,
    DateTimeOffset Timestamp,
    string Level,
    string Message);

public sealed record LuaRunDiagnosticsSnapshot(
    LuaRunSnapshot Run,
    IReadOnlyList<LuaRunLogEntry> Logs,
    LuaEventHubStatistics Events) {
    public LuaTrajectoryDiagnosticsSnapshot? Trajectory { get; init; }
}

public sealed record LuaTrajectoryDiagnosticsSnapshot(
    string AnchorName,
    float RequestedSpeed,
    string Locomotion,
    float RadialError,
    float PhaseError,
    bool Recovering);

public sealed class LuaRunLogBuffer {
    public const int DefaultCapacity = 200;
    public const int MaximumMessageLength = 4_000;
    private readonly object _sync = new();
    private readonly Queue<LuaRunLogEntry> _entries;
    private readonly int _capacity;
    private long _sequence;

    public LuaRunLogBuffer(int capacity = DefaultCapacity) {
        if (capacity is < 1 or > 2_000)
            throw new ArgumentOutOfRangeException(nameof(capacity));
        _capacity = capacity;
        _entries = new Queue<LuaRunLogEntry>(capacity);
    }

    public void Append(string level, string message, DateTimeOffset? timestamp = null) {
        var normalizedLevel = string.IsNullOrWhiteSpace(level) ? "info" : level.Trim().ToLowerInvariant();
        var normalizedMessage = message?.Trim() ?? string.Empty;
        if (normalizedMessage.Length > MaximumMessageLength)
            normalizedMessage = normalizedMessage[..MaximumMessageLength];
        lock (_sync) {
            _entries.Enqueue(new LuaRunLogEntry(++_sequence, timestamp ?? DateTimeOffset.UtcNow, normalizedLevel, normalizedMessage));
            while (_entries.Count > _capacity)
                _entries.Dequeue();
        }
    }

    public IReadOnlyList<LuaRunLogEntry> Snapshot() {
        lock (_sync)
            return _entries.ToArray();
    }
}
