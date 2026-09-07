using System;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace MasterOfPuppets.LuaScripting.Mirror.Core;

/// <summary>
/// Immutable launch-time ordering of Mirror participants.
/// Admission and later lifecycle policy are intentionally outside this value object.
/// </summary>
public sealed class MirrorRosterSnapshot {
    public const int MaximumParticipants = 32;

    private MirrorRosterSnapshot(ImmutableArray<MirrorPlayerIdentity> participants) {
        Participants = participants;
    }

    public ImmutableArray<MirrorPlayerIdentity> Participants { get; }
    public int Count => Participants.Length;

    public MirrorPlayerIdentity this[int orderedIndex] => Participants[orderedIndex];

    public static MirrorRosterSnapshot Create(IEnumerable<MirrorPlayerIdentity> orderedParticipants) {
        ArgumentNullException.ThrowIfNull(orderedParticipants);

        var builder = ImmutableArray.CreateBuilder<MirrorPlayerIdentity>();
        var identities = new HashSet<MirrorPlayerIdentity>();
        foreach (var participant in orderedParticipants) {
            if (participant is null)
                throw new ArgumentException("The roster cannot contain a null participant.", nameof(orderedParticipants));
            if (builder.Count == MaximumParticipants)
                throw new ArgumentOutOfRangeException(
                    nameof(orderedParticipants),
                    $"A Mirror roster cannot exceed {MaximumParticipants} participants.");
            if (!identities.Add(participant))
                throw new ArgumentException($"Duplicate roster participant: {participant}.", nameof(orderedParticipants));

            builder.Add(participant);
        }

        if (builder.Count == 0)
            throw new ArgumentException("A Mirror roster must contain at least one participant.", nameof(orderedParticipants));

        return new MirrorRosterSnapshot(builder.ToImmutable());
    }

    public bool Contains(MirrorPlayerIdentity identity) {
        ArgumentNullException.ThrowIfNull(identity);
        foreach (var participant in Participants) {
            if (participant.Equals(identity))
                return true;
        }
        return false;
    }
}
