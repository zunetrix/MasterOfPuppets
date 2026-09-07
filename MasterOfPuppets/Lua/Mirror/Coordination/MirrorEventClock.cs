using System;
using System.Collections.Generic;

namespace MasterOfPuppets.LuaScripting.Mirror.Coordination;

/// <summary>
/// Globally ordered event epoch plus a monotonic desired-state revision for one
/// module. A changed desired state always receives a new stamp.
/// </summary>
public readonly record struct MirrorEventStamp(
    Guid RunId,
    ulong Epoch,
    string Module,
    ulong DesiredRevision) {

    public MirrorEventStamp Validate() {
        if (RunId == Guid.Empty)
            throw new ArgumentException("Mirror run ID is required.", nameof(RunId));
        if (Epoch == 0)
            throw new ArgumentOutOfRangeException(nameof(Epoch));
        if (string.IsNullOrWhiteSpace(Module))
            throw new ArgumentException("Mirror module ID is required.", nameof(Module));
        if (DesiredRevision == 0)
            throw new ArgumentOutOfRangeException(nameof(DesiredRevision));
        return this with { Module = Module.Trim().ToLowerInvariant() };
    }

    public bool SameStream(MirrorEventStamp other) =>
        RunId == other.RunId && string.Equals(Module, other.Module, StringComparison.Ordinal);

    public int CompareWithinStream(MirrorEventStamp other) {
        if (!SameStream(other))
            throw new InvalidOperationException("Mirror event stamps belong to different streams.");
        var epoch = Epoch.CompareTo(other.Epoch);
        return epoch != 0 ? epoch : DesiredRevision.CompareTo(other.DesiredRevision);
    }
}

/// <summary>
/// Coordinator-owned source of authoritative event stamps. It contains no wall
/// clock and therefore cannot decide roster-loss or timeout policy.
/// </summary>
public sealed class MirrorAuthoritativeEventClock {
    private readonly Guid _runId;
    private readonly Dictionary<string, ulong> _moduleRevisions = new(StringComparer.Ordinal);
    private ulong _epoch;

    public MirrorAuthoritativeEventClock(Guid runId) {
        if (runId == Guid.Empty)
            throw new ArgumentException("Mirror run ID is required.", nameof(runId));
        _runId = runId;
    }

    public Guid RunId => _runId;
    public ulong CurrentEpoch => _epoch;

    public MirrorEventStamp Issue(string module) {
        var normalized = NormalizeModule(module);
        if (_epoch == ulong.MaxValue)
            throw new InvalidOperationException("Mirror event epoch is exhausted.");
        var currentRevision = _moduleRevisions.GetValueOrDefault(normalized);
        if (currentRevision == ulong.MaxValue)
            throw new InvalidOperationException($"Mirror desired-state revision is exhausted for module '{normalized}'.");

        _epoch++;
        currentRevision++;
        _moduleRevisions[normalized] = currentRevision;
        return new MirrorEventStamp(_runId, _epoch, normalized, currentRevision);
    }

    private static string NormalizeModule(string module) {
        if (string.IsNullOrWhiteSpace(module))
            throw new ArgumentException("Mirror module ID is required.", nameof(module));
        return module.Trim().ToLowerInvariant();
    }
}
