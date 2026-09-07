using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using MasterOfPuppets.LuaScripting.Mirror.Coordination;
using MasterOfPuppets.LuaScripting.Mirror.Core;

namespace MasterOfPuppets.LuaScripting.Mirror.Membership;

public enum MirrorMembershipSessionStatus { Active, Stopped }
public enum MirrorGlobalStopSource { ManualOwnerStop, LocalTargetLostOrInvalid }

public enum MirrorMembershipAdmission {
    Accepted,
    AlreadyAcknowledged,
    Rejoined,
    ReplayedMessage,
    WrongSession,
    StaleGeneration,
    FutureGeneration,
    StaleSequence,
    UnknownRecipient,
    RecipientInactive,
    IdentityConflict,
    CapacityReached,
    SessionStopped,
}

public sealed record MirrorMembershipStart(
    Guid MessageId,
    Guid SessionId,
    long Generation,
    ulong ControlSequence,
    DateTimeOffset IssuedAt) {

    public MirrorMembershipStart Validate() {
        if (MessageId == Guid.Empty)
            throw new ArgumentException("A START message ID is required.", nameof(MessageId));
        if (SessionId == Guid.Empty)
            throw new ArgumentException("A Mirror session ID is required.", nameof(SessionId));
        if (Generation <= 0)
            throw new ArgumentOutOfRangeException(nameof(Generation));
        if (ControlSequence == 0)
            throw new ArgumentOutOfRangeException(nameof(ControlSequence));
        return this;
    }
}

public sealed record MirrorMembershipAck(
    Guid MessageId, Guid SessionId, long Generation,
    MirrorPlayerIdentity Recipient, ulong RecipientSequence, DateTimeOffset ObservedAt);

public sealed record MirrorMembershipHeartbeat(
    Guid MessageId, Guid SessionId, long Generation,
    ulong RecipientContentId, ulong RecipientSequence, DateTimeOffset ObservedAt);

public sealed record MirrorMembershipLeave(
    Guid MessageId, Guid SessionId, long Generation,
    ulong RecipientContentId, ulong RecipientSequence, string Reason, DateTimeOffset ObservedAt);

public sealed record MirrorMembershipStop(
    Guid MessageId, Guid SessionId, long Generation,
    ulong ControlSequence, MirrorGlobalStopSource Source, string Reason, DateTimeOffset ObservedAt);

public sealed record MirrorMembershipStopAck(
    Guid MessageId, Guid SessionId, long Generation,
    ulong RecipientContentId, ulong RecipientSequence, DateTimeOffset ObservedAt);

public sealed record MirrorRecipientSnapshot(
    int ParticipantIndex,
    ulong AdmissionEpoch,
    MirrorPlayerIdentity Identity,
    bool IsActive,
    ulong LastSequence,
    DateTimeOffset LastObservedAt,
    string DepartureReason,
    DateTimeOffset? DepartedAt);

public sealed record MirrorLeaseExpiryObservation(
    int ParticipantIndex,
    ulong AdmissionEpoch,
    MirrorPlayerIdentity Identity,
    DateTimeOffset LastObservedAt,
    DateTimeOffset LeaseDeadline,
    DateTimeOffset DetectedAt);

public sealed record MirrorStopTombstoneSnapshot(
    MirrorGlobalStopSource Source,
    string Reason,
    DateTimeOffset FirstObservedAt,
    DateTimeOffset LastBroadcastAt,
    int BroadcastCount,
    ImmutableArray<ulong> RequiredRecipientContentIds,
    ImmutableArray<ulong> AcknowledgedRecipientContentIds,
    ImmutableArray<ulong> PendingRecipientContentIds);

public sealed record MirrorMembershipSnapshot(
    Guid SessionId,
    long Generation,
    MirrorMembershipSessionStatus Status,
    long MembershipRevision,
    ImmutableArray<MirrorRecipientSnapshot> RecipientSlots,
    MirrorStopTombstoneSnapshot StopTombstone);

public sealed record MirrorEventParticipant(
    int ParticipantIndex,
    ulong AdmissionEpoch,
    MirrorPlayerIdentity Identity);

/// <summary>
/// Immutable membership for one event. A mask bit is interpreted only through
/// this snapshot's identity and admission epoch, never through a later roster.
/// </summary>
public sealed record MirrorEventMembershipSnapshot(
    MirrorEventStamp Stamp,
    long MembershipRevision,
    ImmutableArray<MirrorEventParticipant> Participants,
    MirrorParticipantMask ParticipantMask);

/// <summary>
/// A safe in-flight transition after ordinary recipient departure. Joined or
/// rejoined recipients are deliberately absent until a future event.
/// </summary>
public sealed record MirrorEventMembershipTransition(
    MirrorEventMembershipSnapshot Previous,
    MirrorEventMembershipSnapshot Current,
    MirrorParticipantMask RetainedParticipants,
    MirrorParticipantMask DepartedParticipants);

/// <summary>
/// Coordinator-side membership for a dynamic broadcast-recipient run. No CidsGroup
/// is consulted. START acknowledgements create active recipients, ordinary departure
/// affects only that recipient, and each event freezes its identity/index mapping.
/// </summary>
public sealed class MirrorRecipientMembership {
    public const int MaximumRecipients = 32;
    public const int DefaultReplayCapacity = 2048;

    private sealed class RecipientState(
        int participantIndex,
        ulong admissionEpoch,
        MirrorPlayerIdentity identity,
        ulong lastSequence,
        DateTimeOffset lastObservedAt) {
        public int ParticipantIndex { get; } = participantIndex;
        public ulong AdmissionEpoch { get; } = admissionEpoch;
        public MirrorPlayerIdentity Identity { get; } = identity;
        public bool IsActive { get; set; } = true;
        public ulong LastSequence { get; set; } = lastSequence;
        public DateTimeOffset LastObservedAt { get; set; } = lastObservedAt;
        public string DepartureReason { get; set; } = string.Empty;
        public DateTimeOffset? DepartedAt { get; set; }
    }

    private sealed class StopTombstone(
        MirrorGlobalStopSource source,
        string reason,
        DateTimeOffset firstObservedAt,
        HashSet<ulong> requiredRecipients) {
        public MirrorGlobalStopSource Source { get; } = source;
        public string Reason { get; } = reason;
        public DateTimeOffset FirstObservedAt { get; } = firstObservedAt;
        public DateTimeOffset LastBroadcastAt { get; set; } = firstObservedAt;
        public int BroadcastCount { get; set; } = 1;
        public HashSet<ulong> RequiredRecipients { get; } = requiredRecipients;
        public HashSet<ulong> AcknowledgedRecipients { get; } = [];
    }

    private readonly object gate = new();
    private readonly int replayCapacity;
    private readonly RecipientState[] slots = new RecipientState[MaximumRecipients];
    private readonly Dictionary<ulong, RecipientState> currentByContentId = [];
    private readonly Dictionary<ulong, MirrorPlayerIdentity> knownIdentities = [];
    private readonly Dictionary<ulong, ulong> lastSequenceByContentId = [];
    private readonly HashSet<Guid> seenMessageIds = [];
    private readonly Queue<Guid> seenMessageOrder = [];
    private ulong lastControlSequence;
    private ulong lastAdmissionEpoch;
    private long membershipRevision;
    private MirrorMembershipSessionStatus status = MirrorMembershipSessionStatus.Active;
    private StopTombstone stopTombstone;

    public MirrorRecipientMembership(
        MirrorMembershipStart start,
        int replayCapacity = DefaultReplayCapacity) {
        Start = (start ?? throw new ArgumentNullException(nameof(start))).Validate();
        if (replayCapacity < MaximumRecipients)
            throw new ArgumentOutOfRangeException(nameof(replayCapacity));
        this.replayCapacity = replayCapacity;
        lastControlSequence = Start.ControlSequence;
        RememberMessage(Start.MessageId);
    }

    public MirrorMembershipStart Start { get; }

    public MirrorMembershipSnapshot Snapshot {
        get {
            lock (gate) {
                return new MirrorMembershipSnapshot(
                    Start.SessionId,
                    Start.Generation,
                    status,
                    membershipRevision,
                    slots.Where(value => value is not null).Select(ToSnapshot).ToImmutableArray(),
                    stopTombstone is null ? null : ToSnapshot(stopTombstone));
            }
        }
    }

    public MirrorMembershipAdmission TryAcknowledgeStart(
        MirrorMembershipAck acknowledgement,
        out MirrorRecipientSnapshot recipient,
        out string reason) {
        ArgumentNullException.ThrowIfNull(acknowledgement);
        lock (gate) {
            recipient = null;
            var common = ValidateActiveEnvelope(
                acknowledgement.MessageId, acknowledgement.SessionId, acknowledgement.Generation, out reason);
            if (common is { } rejected)
                return rejected;
            if (acknowledgement.Recipient is null) {
                reason = "START acknowledgement requires a validated real-player identity.";
                return MirrorMembershipAdmission.IdentityConflict;
            }
            if (!ValidateIdentity(acknowledgement.Recipient, out reason))
                return MirrorMembershipAdmission.IdentityConflict;
            if (!TryValidateSequence(
                    acknowledgement.Recipient.ContentId,
                    acknowledgement.RecipientSequence,
                    acknowledgement.ObservedAt,
                    out reason))
                return MirrorMembershipAdmission.StaleSequence;

            var contentId = acknowledgement.Recipient.ContentId;
            if (currentByContentId.TryGetValue(contentId, out var existing) && existing.IsActive) {
                Advance(existing, acknowledgement.RecipientSequence, acknowledgement.ObservedAt);
                RememberMessage(acknowledgement.MessageId);
                recipient = ToSnapshot(existing);
                return MirrorMembershipAdmission.AlreadyAcknowledged;
            }

            if (slots.Count(value => value is { IsActive: true }) == MaximumRecipients) {
                reason = $"Mirror session already has the maximum {MaximumRecipients} active recipients.";
                return MirrorMembershipAdmission.CapacityReached;
            }

            var index = existing?.ParticipantIndex ?? Array.FindIndex(slots, value => value is null or { IsActive: false });
            if (index < 0)
                throw new InvalidOperationException("An inactive Mirror recipient slot was expected.");
            if (slots[index] is { } replaced)
                currentByContentId.Remove(replaced.Identity.ContentId);

            var rejoining = knownIdentities.ContainsKey(contentId);
            var admitted = new RecipientState(
                index,
                checked(++lastAdmissionEpoch),
                acknowledgement.Recipient,
                acknowledgement.RecipientSequence,
                acknowledgement.ObservedAt);
            slots[index] = admitted;
            currentByContentId[contentId] = admitted;
            knownIdentities[contentId] = acknowledgement.Recipient;
            lastSequenceByContentId[contentId] = acknowledgement.RecipientSequence;
            membershipRevision = checked(membershipRevision + 1);
            RememberMessage(acknowledgement.MessageId);
            recipient = ToSnapshot(admitted);
            reason = string.Empty;
            return rejoining ? MirrorMembershipAdmission.Rejoined : MirrorMembershipAdmission.Accepted;
        }
    }

    public MirrorMembershipAdmission TryObserveHeartbeat(
        MirrorMembershipHeartbeat heartbeat,
        out MirrorRecipientSnapshot recipient,
        out string reason) {
        ArgumentNullException.ThrowIfNull(heartbeat);
        lock (gate) {
            recipient = null;
            var common = ValidateActiveEnvelope(
                heartbeat.MessageId, heartbeat.SessionId, heartbeat.Generation, out reason);
            if (common is { } rejected)
                return rejected;
            if (!currentByContentId.TryGetValue(heartbeat.RecipientContentId, out var existing)) {
                reason = "Heartbeat sender has not acknowledged START for this session.";
                return MirrorMembershipAdmission.UnknownRecipient;
            }
            if (!existing.IsActive) {
                reason = "Inactive recipient must re-acknowledge START before sending heartbeats.";
                return MirrorMembershipAdmission.RecipientInactive;
            }
            if (!TryValidateSequence(
                    heartbeat.RecipientContentId, heartbeat.RecipientSequence, heartbeat.ObservedAt, out reason))
                return MirrorMembershipAdmission.StaleSequence;
            Advance(existing, heartbeat.RecipientSequence, heartbeat.ObservedAt);
            RememberMessage(heartbeat.MessageId);
            recipient = ToSnapshot(existing);
            return MirrorMembershipAdmission.Accepted;
        }
    }

    public MirrorMembershipAdmission TryLeave(
        MirrorMembershipLeave leave,
        out MirrorRecipientSnapshot recipient,
        out string reason) {
        ArgumentNullException.ThrowIfNull(leave);
        lock (gate) {
            recipient = null;
            var common = ValidateActiveEnvelope(leave.MessageId, leave.SessionId, leave.Generation, out reason);
            if (common is { } rejected)
                return rejected;
            if (!currentByContentId.TryGetValue(leave.RecipientContentId, out var existing)) {
                reason = "LEAVE sender was never admitted to this session.";
                return MirrorMembershipAdmission.UnknownRecipient;
            }
            if (!existing.IsActive) {
                reason = "Recipient is already inactive.";
                return MirrorMembershipAdmission.RecipientInactive;
            }
            if (string.IsNullOrWhiteSpace(leave.Reason)) {
                reason = "LEAVE requires a reason.";
                return MirrorMembershipAdmission.StaleSequence;
            }
            if (!TryValidateSequence(leave.RecipientContentId, leave.RecipientSequence, leave.ObservedAt, out reason))
                return MirrorMembershipAdmission.StaleSequence;
            Advance(existing, leave.RecipientSequence, leave.ObservedAt);
            Deactivate(existing, leave.Reason.Trim(), leave.ObservedAt);
            RememberMessage(leave.MessageId);
            recipient = ToSnapshot(existing);
            return MirrorMembershipAdmission.Accepted;
        }
    }

    /// <summary>
    /// Fails closed: an expired recipient alone becomes inactive. The run and all
    /// other recipients continue; target loss must instead enter TryStop globally.
    /// </summary>
    public ImmutableArray<MirrorLeaseExpiryObservation> DetectLeaseExpirations(
        DateTimeOffset now,
        TimeSpan leaseDuration) {
        if (leaseDuration <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(leaseDuration));
        lock (gate) {
            var observations = ImmutableArray.CreateBuilder<MirrorLeaseExpiryObservation>();
            if (status == MirrorMembershipSessionStatus.Stopped)
                return observations.ToImmutable();
            foreach (var value in slots) {
                if (value is not { IsActive: true } recipient)
                    continue;
                var deadline = recipient.LastObservedAt + leaseDuration;
                if (now <= deadline)
                    continue;
                observations.Add(new MirrorLeaseExpiryObservation(
                    recipient.ParticipantIndex,
                    recipient.AdmissionEpoch,
                    recipient.Identity,
                    recipient.LastObservedAt,
                    deadline,
                    now));
                Deactivate(recipient, "heartbeat lease expired", now);
            }
            return observations.ToImmutable();
        }
    }

    public bool TryCaptureEventSnapshot(
        MirrorEventStamp stamp,
        out MirrorEventMembershipSnapshot snapshot,
        out string reason) {
        lock (gate) {
            snapshot = null;
            if (!TryValidateStamp(stamp, out var validated, out reason))
                return false;
            var participants = slots
                .Where(value => value is { IsActive: true })
                .Select(value => new MirrorEventParticipant(
                    value.ParticipantIndex, value.AdmissionEpoch, value.Identity))
                .ToImmutableArray();
            snapshot = CreateEventSnapshot(validated, membershipRevision, participants);
            reason = string.Empty;
            return true;
        }
    }

    /// <summary>
    /// Reconciles only departures from an in-flight event. A reused slot with a new
    /// admission epoch is removed, never reinterpreted. Joins wait for a future event.
    /// </summary>
    public bool TryTransitionEventSnapshot(
        MirrorEventMembershipSnapshot previous,
        out MirrorEventMembershipTransition transition,
        out string reason) {
        ArgumentNullException.ThrowIfNull(previous);
        lock (gate) {
            transition = null;
            if (!TryValidateStamp(previous.Stamp, out var validated, out reason))
                return false;
            var retained = previous.Participants.Where(participant =>
                participant.ParticipantIndex >= 0
                && participant.ParticipantIndex < MaximumRecipients
                && slots[participant.ParticipantIndex] is { IsActive: true } current
                && current.AdmissionEpoch == participant.AdmissionEpoch
                && current.Identity.Equals(participant.Identity)).ToImmutableArray();
            var currentSnapshot = CreateEventSnapshot(validated, membershipRevision, retained);
            var retainedMask = currentSnapshot.ParticipantMask;
            transition = new MirrorEventMembershipTransition(
                previous,
                currentSnapshot,
                retainedMask,
                previous.ParticipantMask.Except(retainedMask));
            reason = string.Empty;
            return true;
        }
    }

    /// <summary>
    /// Manual owner Stop and locally observed target loss/invalidity are the only
    /// global terminal sources. Later calls are repeat broadcasts for the tombstone.
    /// </summary>
    public MirrorMembershipAdmission TryStop(MirrorMembershipStop stop, out string reason) {
        ArgumentNullException.ThrowIfNull(stop);
        lock (gate) {
            var common = ValidateSessionEnvelope(stop.MessageId, stop.SessionId, stop.Generation, out reason);
            if (common is { } rejected)
                return rejected;
            if (stop.ControlSequence <= lastControlSequence) {
                reason = "STOP control sequence is stale or replayed.";
                return MirrorMembershipAdmission.StaleSequence;
            }
            if (!Enum.IsDefined(stop.Source) || string.IsNullOrWhiteSpace(stop.Reason)) {
                reason = "STOP requires a supported terminal source and reason.";
                return MirrorMembershipAdmission.StaleSequence;
            }
            if (stopTombstone is null) {
                stopTombstone = new StopTombstone(
                    stop.Source, stop.Reason.Trim(), stop.ObservedAt, knownIdentities.Keys.ToHashSet());
                status = MirrorMembershipSessionStatus.Stopped;
                foreach (var recipient in slots.Where(value => value is { IsActive: true }))
                    Deactivate(recipient, $"global STOP: {stop.Source}", stop.ObservedAt);
            } else {
                if (stop.Source != stopTombstone.Source) {
                    reason = "A terminal session cannot change its authoritative STOP source.";
                    return MirrorMembershipAdmission.StaleSequence;
                }
                stopTombstone.LastBroadcastAt = stop.ObservedAt;
                stopTombstone.BroadcastCount++;
            }
            lastControlSequence = stop.ControlSequence;
            RememberMessage(stop.MessageId);
            reason = string.Empty;
            return MirrorMembershipAdmission.Accepted;
        }
    }

    public MirrorMembershipAdmission TryAcknowledgeStop(
        MirrorMembershipStopAck acknowledgement,
        out string reason) {
        ArgumentNullException.ThrowIfNull(acknowledgement);
        lock (gate) {
            var common = ValidateSessionEnvelope(
                acknowledgement.MessageId,
                acknowledgement.SessionId,
                acknowledgement.Generation,
                out reason);
            if (common is { } rejected)
                return rejected;
            if (stopTombstone is null) {
                reason = "STOP has not been issued for this session.";
                return MirrorMembershipAdmission.RecipientInactive;
            }
            if (!stopTombstone.RequiredRecipients.Contains(acknowledgement.RecipientContentId)) {
                reason = "STOP acknowledgement sender was never admitted to this session.";
                return MirrorMembershipAdmission.UnknownRecipient;
            }
            if (acknowledgement.RecipientSequence
                <= lastSequenceByContentId.GetValueOrDefault(acknowledgement.RecipientContentId)) {
                reason = "STOP acknowledgement sequence is stale or replayed.";
                return MirrorMembershipAdmission.StaleSequence;
            }
            lastSequenceByContentId[acknowledgement.RecipientContentId] = acknowledgement.RecipientSequence;
            stopTombstone.AcknowledgedRecipients.Add(acknowledgement.RecipientContentId);
            RememberMessage(acknowledgement.MessageId);
            reason = string.Empty;
            return MirrorMembershipAdmission.Accepted;
        }
    }

    private MirrorMembershipAdmission? ValidateActiveEnvelope(
        Guid messageId, Guid sessionId, long generation, out string reason) {
        var common = ValidateSessionEnvelope(messageId, sessionId, generation, out reason);
        if (common is not null)
            return common;
        if (status == MirrorMembershipSessionStatus.Stopped) {
            reason = "Mirror membership session is stopped and cannot resume.";
            return MirrorMembershipAdmission.SessionStopped;
        }
        return null;
    }

    private MirrorMembershipAdmission? ValidateSessionEnvelope(
        Guid messageId, Guid sessionId, long generation, out string reason) {
        if (sessionId != Start.SessionId) {
            reason = "Membership message belongs to another Mirror session.";
            return MirrorMembershipAdmission.WrongSession;
        }
        if (generation < Start.Generation) {
            reason = "Membership message generation is stale.";
            return MirrorMembershipAdmission.StaleGeneration;
        }
        if (generation > Start.Generation) {
            reason = "Membership message generation is from a future session.";
            return MirrorMembershipAdmission.FutureGeneration;
        }
        if (messageId == Guid.Empty || seenMessageIds.Contains(messageId)) {
            reason = "Membership message is a replay or lacks an ID.";
            return MirrorMembershipAdmission.ReplayedMessage;
        }
        reason = string.Empty;
        return null;
    }

    private bool TryValidateStamp(
        MirrorEventStamp stamp,
        out MirrorEventStamp validated,
        out string reason) {
        validated = default;
        try {
            validated = stamp.Validate();
        } catch (ArgumentException exception) {
            reason = exception.Message;
            return false;
        }
        if (validated.RunId != Start.SessionId) {
            reason = "Mirror event belongs to another session.";
            return false;
        }
        if (status == MirrorMembershipSessionStatus.Stopped) {
            reason = "Mirror membership session is stopped and cannot resume.";
            return false;
        }
        reason = string.Empty;
        return true;
    }

    private bool ValidateIdentity(MirrorPlayerIdentity identity, out string reason) {
        if (knownIdentities.TryGetValue(identity.ContentId, out var known) && !known.Equals(identity)) {
            reason = "Recipient content ID conflicts with its prior session identity.";
            return false;
        }
        reason = string.Empty;
        return true;
    }

    private bool TryValidateSequence(
        ulong contentId, ulong sequence, DateTimeOffset observedAt, out string reason) {
        if (sequence == 0 || sequence <= lastSequenceByContentId.GetValueOrDefault(contentId)) {
            reason = "Recipient sequence is stale or replayed.";
            return false;
        }
        if (currentByContentId.TryGetValue(contentId, out var current)
            && observedAt < current.LastObservedAt) {
            reason = "Recipient observation time moved backward.";
            return false;
        }
        reason = string.Empty;
        return true;
    }

    private void Advance(RecipientState recipient, ulong sequence, DateTimeOffset observedAt) {
        recipient.LastSequence = sequence;
        recipient.LastObservedAt = observedAt;
        lastSequenceByContentId[recipient.Identity.ContentId] = sequence;
    }

    private void Deactivate(RecipientState recipient, string reason, DateTimeOffset at) {
        if (!recipient.IsActive)
            return;
        recipient.IsActive = false;
        recipient.DepartureReason = reason;
        recipient.DepartedAt = at;
        membershipRevision = checked(membershipRevision + 1);
    }

    private void RememberMessage(Guid messageId) {
        seenMessageIds.Add(messageId);
        seenMessageOrder.Enqueue(messageId);
        while (seenMessageOrder.Count > replayCapacity)
            seenMessageIds.Remove(seenMessageOrder.Dequeue());
    }

    private static MirrorEventMembershipSnapshot CreateEventSnapshot(
        MirrorEventStamp stamp,
        long revision,
        ImmutableArray<MirrorEventParticipant> participants) {
        var bits = participants.Aggregate(0u, (current, participant) =>
            current | MirrorParticipantMask.One(participant.ParticipantIndex).Bits);
        return new MirrorEventMembershipSnapshot(
            stamp, revision, participants, new MirrorParticipantMask(bits));
    }

    private static MirrorRecipientSnapshot ToSnapshot(RecipientState recipient) => new(
        recipient.ParticipantIndex,
        recipient.AdmissionEpoch,
        recipient.Identity,
        recipient.IsActive,
        recipient.LastSequence,
        recipient.LastObservedAt,
        recipient.DepartureReason,
        recipient.DepartedAt);

    private static MirrorStopTombstoneSnapshot ToSnapshot(StopTombstone tombstone) {
        var required = tombstone.RequiredRecipients.Order().ToImmutableArray();
        var acknowledged = tombstone.AcknowledgedRecipients.Order().ToImmutableArray();
        return new MirrorStopTombstoneSnapshot(
            tombstone.Source,
            tombstone.Reason,
            tombstone.FirstObservedAt,
            tombstone.LastBroadcastAt,
            tombstone.BroadcastCount,
            required,
            acknowledged,
            required.Except(acknowledged).ToImmutableArray());
    }
}
