using System;
using System.Threading;

using MasterOfPuppets.LuaScripting.Mirror.Core;

namespace MasterOfPuppets.LuaScripting.Mirror.Runtime;

/// <summary>
/// The stable player identity and transient local actor identifiers captured when
/// a Mirror generation binds its target. All fields are immutable for the life of
/// that generation.
/// </summary>
public sealed record MirrorBoundTargetIdentity {
    public MirrorBoundTargetIdentity(
        MirrorPlayerIdentity player,
        ulong gameObjectId,
        uint entityId) {
        Player = player ?? throw new ArgumentNullException(nameof(player));
        if (gameObjectId is 0 or 0xE0000000)
            throw new ArgumentOutOfRangeException(nameof(gameObjectId), "A loaded target game-object ID is required.");
        if (entityId is 0 or 0xE0000000)
            throw new ArgumentOutOfRangeException(nameof(entityId), "A loaded target entity ID is required.");

        GameObjectId = gameObjectId;
        EntityId = entityId;
    }

    public MirrorPlayerIdentity Player { get; }
    public ulong GameObjectId { get; }
    public uint EntityId { get; }

    internal bool Matches(in MirrorBoundTargetObservation observation) =>
        observation.IsLocallyObserved
        && observation.GameObjectId == GameObjectId
        && observation.EntityId == EntityId;
}

/// <summary>
/// One runtime sample for the already-bound actor. A missing sample or a sample
/// with different transient identifiers means the captured actor left local
/// observation; a same-name replacement is intentionally not reacquired.
/// </summary>
public readonly record struct MirrorBoundTargetObservation(
    bool IsLocallyObserved,
    ulong GameObjectId,
    uint EntityId,
    string Detail) {
    public static MirrorBoundTargetObservation Observed(ulong gameObjectId, uint entityId) =>
        new(true, gameObjectId, entityId, string.Empty);

    public static MirrorBoundTargetObservation Lost(string detail) =>
        new(false, 0, 0, detail?.Trim() ?? string.Empty);
}

public enum MirrorBoundTargetWatchStatus {
    Observing,
    TargetLost,
}

public sealed record MirrorBoundTargetLoss(
    long Generation,
    MirrorBoundTargetIdentity Target,
    string Reason);

public readonly record struct MirrorBoundTargetTransition(
    MirrorBoundTargetWatchStatus Status,
    bool EmittedTerminalLoss,
    MirrorBoundTargetLoss Loss);

/// <summary>
/// Pure terminal watch for one immutable target in one Mirror generation. The
/// first missing or identity-mismatched observation emits exactly one loss and
/// cancels the generation token. Terminal watches cannot return to Observing.
/// </summary>
public sealed class MirrorBoundTargetWatch : IDisposable {
    private readonly object gate = new();
    private readonly CancellationTokenSource generationCancellation = new();
    private MirrorBoundTargetWatchStatus status = MirrorBoundTargetWatchStatus.Observing;
    private MirrorBoundTargetLoss terminalLoss;
    private bool disposed;

    public MirrorBoundTargetWatch(long generation, MirrorBoundTargetIdentity target) {
        if (generation <= 0)
            throw new ArgumentOutOfRangeException(nameof(generation), "Generation must be positive.");
        Generation = generation;
        Target = target ?? throw new ArgumentNullException(nameof(target));
    }

    public long Generation { get; }
    public MirrorBoundTargetIdentity Target { get; }
    public CancellationToken GenerationCancellation => generationCancellation.Token;
    public event Action<MirrorBoundTargetLoss> TargetLost;

    public MirrorBoundTargetWatchStatus Status {
        get {
            lock (gate)
                return status;
        }
    }

    public MirrorBoundTargetTransition Observe(in MirrorBoundTargetObservation observation) {
        MirrorBoundTargetLoss emitted = null;
        lock (gate) {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (status == MirrorBoundTargetWatchStatus.TargetLost)
                return new MirrorBoundTargetTransition(status, false, terminalLoss);
            if (Target.Matches(observation))
                return new MirrorBoundTargetTransition(status, false, null);

            var reason = observation.IsLocallyObserved
                ? $"Bound actor identity changed from {Target.GameObjectId}/{Target.EntityId} "
                    + $"to {observation.GameObjectId}/{observation.EntityId}; reacquisition is not permitted."
                : string.IsNullOrWhiteSpace(observation.Detail)
                    ? "Bound actor left local observation."
                    : observation.Detail.Trim();
            terminalLoss = emitted = new MirrorBoundTargetLoss(Generation, Target, reason);
            status = MirrorBoundTargetWatchStatus.TargetLost;
            generationCancellation.Cancel();
        }

        TargetLost?.Invoke(emitted);
        return new MirrorBoundTargetTransition(status, true, emitted);
    }

    public void Dispose() {
        lock (gate) {
            if (disposed)
                return;
            if (!generationCancellation.IsCancellationRequested)
                generationCancellation.Cancel();
            generationCancellation.Dispose();
            disposed = true;
        }
    }
}
