using System;
using System.Threading;

namespace MasterOfPuppets.LuaScripting.Mirror.Core;

/// <summary>
/// A generation identifies one immutable Mirror session; a revision identifies an
/// authoritative observation within that session.
/// </summary>
public readonly record struct MirrorVersionStamp {
    public MirrorVersionStamp(long generation, long revision) : this() {
        if (generation <= 0)
            throw new ArgumentOutOfRangeException(nameof(generation), "Generation must be positive.");
        if (revision < 0)
            throw new ArgumentOutOfRangeException(nameof(revision), "Revision cannot be negative.");

        Generation = generation;
        Revision = revision;
    }

    public long Generation { get; }
    public long Revision { get; }

    public bool IsOlderThan(MirrorVersionStamp other) =>
        Generation < other.Generation
        || (Generation == other.Generation && Revision < other.Revision);
}

/// <summary>
/// Process-local monotonic generation allocator. A distributed adapter may seed it
/// from a persisted or coordinator-issued generation without changing core semantics.
/// </summary>
public sealed class MirrorGenerationSource {
    private long lastGeneration;

    public MirrorGenerationSource(long lastIssuedGeneration = 0) {
        if (lastIssuedGeneration < 0)
            throw new ArgumentOutOfRangeException(nameof(lastIssuedGeneration));
        lastGeneration = lastIssuedGeneration;
    }

    public long Next() {
        var generation = Interlocked.Increment(ref lastGeneration);
        if (generation <= 0)
            throw new InvalidOperationException("Mirror generation space was exhausted.");
        return generation;
    }
}
