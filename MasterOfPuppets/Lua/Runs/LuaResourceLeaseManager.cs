using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace MasterOfPuppets.LuaScripting.Runs;

public sealed class LuaResourceLeaseManager {
    private readonly object _lock = new();
    private readonly Dictionary<LuaResourceKind, string> _owners = new();

    public bool TryAcquire(
        string runId,
        LuaResourceKind resources,
        out LuaResourceLease? lease,
        out LuaResourceConflict? conflict) {
        if (string.IsNullOrWhiteSpace(runId))
            throw new ArgumentException("Run ID is required.", nameof(runId));
        resources = LuaResourceKinds.ValidateMask(resources);
        var requested = Enumerate(resources).ToArray();
        lock (_lock) {
            foreach (var resource in requested) {
                if (_owners.TryGetValue(resource, out var owner)
                    && !owner.Equals(runId, StringComparison.Ordinal)) {
                    lease = null;
                    conflict = new LuaResourceConflict(resource, owner);
                    return false;
                }
            }
            foreach (var resource in requested)
                _owners[resource] = runId;
        }
        conflict = null;
        lease = new LuaResourceLease(this, runId, resources);
        return true;
    }

    public IReadOnlyDictionary<LuaResourceKind, string> SnapshotOwners() {
        lock (_lock)
            return new Dictionary<LuaResourceKind, string>(_owners);
    }

    internal void Release(string runId, LuaResourceKind resources) {
        lock (_lock) {
            foreach (var resource in Enumerate(resources)) {
                if (_owners.TryGetValue(resource, out var owner)
                    && owner.Equals(runId, StringComparison.Ordinal))
                    _owners.Remove(resource);
            }
        }
    }

    private static IEnumerable<LuaResourceKind> Enumerate(LuaResourceKind resources) {
        foreach (var value in Enum.GetValues<LuaResourceKind>()) {
            if (value != LuaResourceKind.None && (resources & value) == value)
                yield return value;
        }
    }
}

public sealed record LuaResourceConflict(LuaResourceKind Resource, string OwnerRunId) {
    public string Message => $"resource '{Resource}' is owned by Lua run {OwnerRunId}";
}

public sealed class LuaResourceConflictException : InvalidOperationException {
    public LuaResourceConflictException(LuaResourceConflict conflict)
        : base($"Lua run rejected: {conflict.Message}.") => Conflict = conflict;
    public LuaResourceConflict Conflict { get; }
}

public sealed class LuaResourceLease : IDisposable {
    private LuaResourceLeaseManager? _manager;

    internal LuaResourceLease(LuaResourceLeaseManager manager, string runId, LuaResourceKind resources) {
        _manager = manager;
        RunId = runId;
        Resources = resources;
    }

    public string RunId { get; }
    public LuaResourceKind Resources { get; }

    public void Dispose() =>
        Interlocked.Exchange(ref _manager, null)?.Release(RunId, Resources);
}
