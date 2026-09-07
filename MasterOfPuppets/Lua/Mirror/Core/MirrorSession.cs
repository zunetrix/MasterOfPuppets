using System;
using System.Collections.Generic;
using System.Threading;

namespace MasterOfPuppets.LuaScripting.Mirror.Core;

public enum MirrorSessionStatus {
    Running,
    TargetLost,
    Stopped,
}

public enum MirrorCancellationReason {
    None,
    Superseded,
    TargetLost,
    SessionStopped,
}

public enum MirrorDirectiveAdmission {
    Accepted,
    SessionTerminal,
    StaleGeneration,
    FutureGeneration,
    StaleOrDuplicateRevision,
}

public sealed record MirrorSessionSnapshot(
    Guid SessionId,
    MirrorPlayerIdentity Target,
    MirrorRosterSnapshot Roster,
    long Generation,
    long LatestRevision,
    MirrorSessionStatus Status,
    string StopReason);

/// <summary>
/// Cancellation lease for an accepted module directive. It is cancelled by a newer
/// directive for the same module or by terminal session cancellation.
/// </summary>
public sealed class MirrorDirectiveLease {
    private readonly CancellationTokenSource cancellation;
    private readonly CancellationToken token;
    private int cancellationReason;

    internal MirrorDirectiveLease(MirrorModuleId module, MirrorVersionStamp stamp, CancellationToken sessionToken) {
        Module = module;
        Stamp = stamp;
        cancellation = CancellationTokenSource.CreateLinkedTokenSource(sessionToken);
        token = cancellation.Token;
    }

    public MirrorModuleId Module { get; }
    public MirrorVersionStamp Stamp { get; }
    public CancellationToken CancellationToken => token;
    public MirrorCancellationReason CancellationReason =>
        (MirrorCancellationReason)Volatile.Read(ref cancellationReason);

    internal void Cancel(MirrorCancellationReason reason) {
        if (reason == MirrorCancellationReason.None)
            throw new ArgumentOutOfRangeException(nameof(reason));

        Interlocked.CompareExchange(ref cancellationReason, (int)reason, (int)MirrorCancellationReason.None);
        cancellation.Cancel();
    }

    internal void DisposeSource() => cancellation.Dispose();
}

/// <summary>
/// Pure coordination core for a single immutable-target Mirror session.
/// It contains no Dalamud or transport dependencies.
/// </summary>
public sealed class MirrorSession : IDisposable {
    private readonly object gate = new();
    private readonly CancellationTokenSource sessionCancellation = new();
    private readonly Dictionary<MirrorModuleId, MirrorDirectiveLease> currentByModule = new();
    private long latestRevision;
    private MirrorSessionStatus status = MirrorSessionStatus.Running;
    private string stopReason;
    private bool disposed;

    public MirrorSession(
        Guid sessionId,
        long generation,
        MirrorPlayerIdentity target,
        MirrorRosterSnapshot roster) {
        if (sessionId == Guid.Empty)
            throw new ArgumentException("A non-empty session ID is required.", nameof(sessionId));
        if (generation <= 0)
            throw new ArgumentOutOfRangeException(nameof(generation), "Generation must be positive.");

        SessionId = sessionId;
        Generation = generation;
        Target = target ?? throw new ArgumentNullException(nameof(target));
        Roster = roster ?? throw new ArgumentNullException(nameof(roster));
    }

    public Guid SessionId { get; }
    public long Generation { get; }
    public MirrorPlayerIdentity Target { get; }
    public MirrorRosterSnapshot Roster { get; }
    public CancellationToken CancellationToken => sessionCancellation.Token;

    public MirrorSessionSnapshot Snapshot {
        get {
            lock (gate) {
                return new MirrorSessionSnapshot(
                    SessionId,
                    Target,
                    Roster,
                    Generation,
                    latestRevision,
                    status,
                    stopReason);
            }
        }
    }

    /// <summary>Allocates the next globally monotonic observation revision.</summary>
    public MirrorVersionStamp NextRevision() {
        lock (gate) {
            ThrowIfDisposed();
            EnsureRunning();
            latestRevision = checked(latestRevision + 1);
            return new MirrorVersionStamp(Generation, latestRevision);
        }
    }

    /// <summary>
    /// Accepts a directive only for this active generation and only when it is newer
    /// than the currently accepted directive for its module. A newer directive
    /// supersedes that module's previous lease without disturbing other modules.
    /// </summary>
    public MirrorDirectiveAdmission TryBeginDirective(
        MirrorModuleId module,
        MirrorVersionStamp stamp,
        out MirrorDirectiveLease lease) {
        lock (gate) {
            ThrowIfDisposed();
            lease = null;

            if (string.IsNullOrWhiteSpace(module.Value))
                throw new ArgumentException("A valid Mirror module ID is required.", nameof(module));

            if (status != MirrorSessionStatus.Running)
                return MirrorDirectiveAdmission.SessionTerminal;
            if (stamp.Generation < Generation)
                return MirrorDirectiveAdmission.StaleGeneration;
            if (stamp.Generation > Generation)
                return MirrorDirectiveAdmission.FutureGeneration;
            if (stamp.Revision == 0)
                return MirrorDirectiveAdmission.StaleOrDuplicateRevision;
            if (currentByModule.TryGetValue(module, out var current)
                && stamp.Revision <= current.Stamp.Revision)
                return MirrorDirectiveAdmission.StaleOrDuplicateRevision;

            if (stamp.Revision > latestRevision)
                latestRevision = stamp.Revision;

            var accepted = new MirrorDirectiveLease(module, stamp, sessionCancellation.Token);
            if (current is not null) {
                current.Cancel(MirrorCancellationReason.Superseded);
                current.DisposeSource();
            }
            currentByModule[module] = accepted;
            lease = accepted;
            return MirrorDirectiveAdmission.Accepted;
        }
    }

    public bool IsCurrent(MirrorDirectiveLease lease) {
        ArgumentNullException.ThrowIfNull(lease);
        lock (gate) {
            return status == MirrorSessionStatus.Running
                && currentByModule.TryGetValue(lease.Module, out var current)
                && ReferenceEquals(current, lease)
                && current.Stamp == lease.Stamp;
        }
    }

    /// <summary>
    /// The first confirmed loss of the immutable local target is terminal. A stopped
    /// session has no transition back to Running; reacquisition requires a new session.
    /// </summary>
    public bool ReportTargetLocalObservationLost(MirrorPlayerIdentity target, string reason) {
        ArgumentNullException.ThrowIfNull(target);
        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("A target-loss reason is required.", nameof(reason));

        lock (gate) {
            ThrowIfDisposed();
            if (!Target.Equals(target) || status != MirrorSessionStatus.Running)
                return false;

            status = MirrorSessionStatus.TargetLost;
            stopReason = reason.Trim();
            CancelSession(MirrorCancellationReason.TargetLost);
            return true;
        }
    }

    public bool Stop(string reason) {
        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("A stop reason is required.", nameof(reason));

        lock (gate) {
            ThrowIfDisposed();
            if (status != MirrorSessionStatus.Running)
                return false;

            status = MirrorSessionStatus.Stopped;
            stopReason = reason.Trim();
            CancelSession(MirrorCancellationReason.SessionStopped);
            return true;
        }
    }

    public void Dispose() {
        lock (gate) {
            if (disposed)
                return;
            if (status == MirrorSessionStatus.Running) {
                status = MirrorSessionStatus.Stopped;
                stopReason = "Session disposed.";
                CancelSession(MirrorCancellationReason.SessionStopped);
            }
            foreach (var lease in currentByModule.Values)
                lease.DisposeSource();
            currentByModule.Clear();
            sessionCancellation.Dispose();
            disposed = true;
        }
    }

    private void CancelSession(MirrorCancellationReason reason) {
        foreach (var lease in currentByModule.Values)
            lease.Cancel(reason);
        sessionCancellation.Cancel();
    }

    private void EnsureRunning() {
        if (status != MirrorSessionStatus.Running)
            throw new InvalidOperationException($"The Mirror session is terminal ({status}).");
    }

    private void ThrowIfDisposed() {
        ObjectDisposedException.ThrowIf(disposed, this);
    }
}
