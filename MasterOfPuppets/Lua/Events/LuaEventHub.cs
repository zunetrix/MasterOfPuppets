using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace MasterOfPuppets.LuaScripting.Events;

public sealed record LuaHostEvent(
    long Sequence,
    string Name,
    DateTimeOffset Timestamp,
    IReadOnlyDictionary<string, string> Data);

public readonly record struct LuaEventHubStatistics(
    int Capacity,
    long Published,
    long Consumed,
    long Dropped,
    bool Completed);

/// <summary>
/// One bounded, thread-safe stream per Lua run. Producers never invoke Lua;
/// the run consumes events sequentially on its own task.
/// </summary>
public sealed class LuaEventHub : IDisposable {
    public const int DefaultCapacity = 256;
    private readonly object _sync = new();
    private readonly LinkedList<LuaHostEvent> _queue = new();
    private readonly HashSet<string> _interests = new(StringComparer.Ordinal);
    private readonly Channel<bool> _signals;
    private readonly int _capacity;
    private long _sequence;
    private long _published;
    private long _consumed;
    private long _dropped;
    private bool _completed;

    public LuaEventHub(int capacity = DefaultCapacity) {
        if (capacity is <= 0 or > 4096)
            throw new ArgumentOutOfRangeException(nameof(capacity));
        _capacity = capacity;
        _signals = Channel.CreateBounded<bool>(new BoundedChannelOptions(capacity) {
            SingleReader = true,
            SingleWriter = false,
            FullMode = BoundedChannelFullMode.DropOldest,
            AllowSynchronousContinuations = false,
        });
    }

    public bool Publish(string name, IReadOnlyDictionary<string, string>? data = null, DateTimeOffset? timestamp = null) {
        name = NormalizeName(name);
        var payload = data == null
            ? new Dictionary<string, string>(StringComparer.Ordinal)
            : data.Where(pair => !string.IsNullOrWhiteSpace(pair.Key))
                .Take(64)
                .ToDictionary(pair => pair.Key.Trim(), pair => Limit(pair.Value, 2000), StringComparer.Ordinal);
        lock (_sync) {
            if (_completed)
                return false;
            var item = new LuaHostEvent(
                ++_sequence,
                name,
                timestamp ?? DateTimeOffset.UtcNow,
                payload);
            if (_queue.Count == _capacity) {
                _queue.RemoveFirst();
                _dropped++;
            }
            _queue.AddLast(item);
            _published++;
        }
        _signals.Writer.TryWrite(true);
        return true;
    }

    public bool TryRead(string? name, out LuaHostEvent? item) {
        var filter = NormalizeFilter(name);
        if (filter != null)
            RegisterInterest(filter);
        return TryTake(filter, null, out item);
    }

    public bool TryRead(
        string? name,
        IReadOnlyDictionary<string, string>? dataEquals,
        out LuaHostEvent? item) {
        var filter = NormalizeFilter(name);
        if (filter != null)
            RegisterInterest(filter);
        return TryTake(filter, NormalizeDataFilter(dataEquals), out item);
    }

    public async Task<LuaHostEvent?> ReadAsync(string? name, TimeSpan timeout, CancellationToken cancellationToken) {
        return await ReadAsync(name, null, timeout, cancellationToken);
    }

    public async Task<LuaHostEvent?> ReadAsync(
        string? name,
        IReadOnlyDictionary<string, string>? dataEquals,
        TimeSpan timeout,
        CancellationToken cancellationToken) {
        if (timeout <= TimeSpan.Zero || timeout > TimeSpan.FromMinutes(2))
            throw new ArgumentOutOfRangeException(nameof(timeout));
        var filter = NormalizeFilter(name);
        var normalizedData = NormalizeDataFilter(dataEquals);
        if (filter != null)
            RegisterInterest(filter);
        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(timeout);
        try {
            while (true) {
                if (TryTake(filter, normalizedData, out var item))
                    return item;
                if (!await _signals.Reader.WaitToReadAsync(timeoutSource.Token))
                    return null;
                _signals.Reader.TryRead(out _);
            }
        } catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) {
            return null;
        }
    }

    public LuaEventHubStatistics Snapshot() {
        lock (_sync)
            return new LuaEventHubStatistics(
                _capacity,
                _published,
                _consumed,
                _dropped,
                _completed);
    }

    /// <summary>
    /// Registers interest in a high-volume raw stream. Actor-scoped events do
    /// not require registration; producers use this only to avoid broadcasting
    /// unrelated world combat/emote traffic into every run.
    /// </summary>
    public void RegisterInterest(string name) {
        name = NormalizeName(name);
        lock (_sync) {
            if (!_completed)
                _interests.Add(name);
        }
    }

    public bool UnregisterInterest(string name) {
        name = NormalizeName(name);
        lock (_sync)
            return _interests.Remove(name);
    }

    public bool IsInterested(string name) {
        name = NormalizeName(name);
        lock (_sync)
            return !_completed && _interests.Contains(name);
    }

    public void Dispose() {
        lock (_sync) {
            if (_completed)
                return;
            _completed = true;
            _queue.Clear();
            _interests.Clear();
        }
        _signals.Writer.TryComplete();
    }

    private bool TryTake(
        string? filter,
        IReadOnlyDictionary<string, string>? dataEquals,
        out LuaHostEvent? item) {
        lock (_sync) {
            var node = _queue.First;
            while (node != null) {
                if ((filter == null || node.Value.Name.Equals(filter, StringComparison.Ordinal))
                    && DataMatches(node.Value.Data, dataEquals)) {
                    item = node.Value;
                    _queue.Remove(node);
                    _consumed++;
                    return true;
                }
                node = node.Next;
            }
        }
        item = null;
        return false;
    }

    private static IReadOnlyDictionary<string, string>? NormalizeDataFilter(
        IReadOnlyDictionary<string, string>? dataEquals) {
        if (dataEquals == null || dataEquals.Count == 0)
            return null;
        if (dataEquals.Count > 16)
            throw new ArgumentOutOfRangeException(nameof(dataEquals), "event filters accept at most 16 fields");
        var normalized = new Dictionary<string, string>(dataEquals.Count, StringComparer.Ordinal);
        foreach (var (key, value) in dataEquals) {
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException("event filter keys cannot be empty", nameof(dataEquals));
            normalized[key.Trim()] = value ?? string.Empty;
        }
        return normalized;
    }

    private static bool DataMatches(
        IReadOnlyDictionary<string, string> data,
        IReadOnlyDictionary<string, string>? expected) {
        if (expected == null)
            return true;
        foreach (var (key, value) in expected) {
            if (!data.TryGetValue(key, out var actual)
                || !actual.Equals(value, StringComparison.Ordinal))
                return false;
        }
        return true;
    }

    private static string NormalizeName(string? name) {
        var normalized = name?.Trim().ToLowerInvariant() ?? string.Empty;
        if (normalized.Length is 0 or > 64
            || normalized.Any(character => !(char.IsLetterOrDigit(character) || character is '.' or '-' or '_')))
            throw new ArgumentException("Lua event names must contain only letters, digits, '.', '-', or '_' and be at most 64 characters.", nameof(name));
        return normalized;
    }

    private static string? NormalizeFilter(string? name) =>
        string.IsNullOrWhiteSpace(name) ? null : NormalizeName(name);

    private static string Limit(string? value, int maximum) {
        value ??= string.Empty;
        return value.Length <= maximum ? value : value[..maximum];
    }
}
