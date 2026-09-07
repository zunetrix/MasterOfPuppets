using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace MasterOfPuppets.LuaScripting.Mirror.Coordination;

/// <summary>
/// A compact mask over the fixed, ordered Mirror roster. Bit N always refers to
/// roster entry N; masks must never be reinterpreted against another roster.
/// </summary>
public readonly record struct MirrorParticipantMask(uint Bits) {
    public static MirrorParticipantMask Empty => new(0);

    public int Count => BitOperations.PopCount(Bits);
    public bool IsEmpty => Bits == 0;

    public static MirrorParticipantMask All(int participantCount) {
        if (participantCount is < 0 or > MirrorRoster.MaximumParticipants)
            throw new ArgumentOutOfRangeException(nameof(participantCount));
        return participantCount == MirrorRoster.MaximumParticipants
            ? new(uint.MaxValue)
            : new((1u << participantCount) - 1u);
    }

    public static MirrorParticipantMask One(int participantIndex) {
        ValidateIndex(participantIndex);
        return new(1u << participantIndex);
    }

    public bool Contains(int participantIndex) {
        ValidateIndex(participantIndex);
        return (Bits & (1u << participantIndex)) != 0;
    }

    public bool IsSubsetOf(MirrorParticipantMask other) => (Bits & ~other.Bits) == 0;
    public MirrorParticipantMask Except(MirrorParticipantMask other) => new(Bits & ~other.Bits);
    public MirrorParticipantMask Intersect(MirrorParticipantMask other) => new(Bits & other.Bits);
    public MirrorParticipantMask Union(MirrorParticipantMask other) => new(Bits | other.Bits);

    public IReadOnlyList<int> ToIndices() {
        var result = new List<int>(Count);
        for (var index = 0; index < MirrorRoster.MaximumParticipants; index++)
            if (Contains(index))
                result.Add(index);
        return result;
    }

    private static void ValidateIndex(int participantIndex) {
        if (participantIndex is < 0 or >= MirrorRoster.MaximumParticipants)
            throw new ArgumentOutOfRangeException(nameof(participantIndex));
    }
}

/// <summary>
/// Immutable, exact-order roster. Construction rejects truncation, duplicate
/// content IDs, and zero IDs rather than silently changing participant identity.
/// </summary>
public sealed class MirrorRoster {
    public const int MaximumParticipants = 32;

    private readonly ulong[] _contentIds;
    private readonly IReadOnlyList<ulong> _contentIdsView;
    private readonly Dictionary<ulong, int> _indexByContentId;

    public MirrorRoster(IEnumerable<ulong> orderedContentIds) {
        ArgumentNullException.ThrowIfNull(orderedContentIds);
        _contentIds = orderedContentIds.ToArray();
        if (_contentIds.Length is < 1 or > MaximumParticipants)
            throw new ArgumentException($"Mirror roster must contain 1 to {MaximumParticipants} participants.", nameof(orderedContentIds));
        if (_contentIds.Any(contentId => contentId == 0))
            throw new ArgumentException("Mirror roster content IDs must be non-zero.", nameof(orderedContentIds));
        if (_contentIds.Distinct().Count() != _contentIds.Length)
            throw new ArgumentException("Mirror roster content IDs must be unique.", nameof(orderedContentIds));

        _contentIdsView = Array.AsReadOnly(_contentIds);
        _indexByContentId = _contentIds
            .Select((contentId, index) => (contentId, index))
            .ToDictionary(value => value.contentId, value => value.index);
    }

    public int Count => _contentIds.Length;
    public MirrorParticipantMask AllParticipants => MirrorParticipantMask.All(Count);
    public IReadOnlyList<ulong> ContentIds => _contentIdsView;

    public ulong GetContentId(int participantIndex) {
        if (participantIndex < 0 || participantIndex >= Count)
            throw new ArgumentOutOfRangeException(nameof(participantIndex));
        return _contentIds[participantIndex];
    }

    public bool TryGetIndex(ulong contentId, out int participantIndex) =>
        _indexByContentId.TryGetValue(contentId, out participantIndex);

    public int GetIndex(ulong contentId) => _indexByContentId.TryGetValue(contentId, out var index)
        ? index
        : throw new ArgumentException($"Content ID {contentId} is not in the authoritative Mirror roster.", nameof(contentId));

    public MirrorParticipantMask MaskOf(IEnumerable<ulong> contentIds) {
        ArgumentNullException.ThrowIfNull(contentIds);
        var bits = 0u;
        foreach (var contentId in contentIds)
            bits |= MirrorParticipantMask.One(GetIndex(contentId)).Bits;
        return new MirrorParticipantMask(bits);
    }

    public void RequireSubset(MirrorParticipantMask mask, string parameterName) {
        if (!mask.IsSubsetOf(AllParticipants))
            throw new ArgumentException("Participant mask contains bits outside the authoritative Mirror roster.", parameterName);
    }
}
