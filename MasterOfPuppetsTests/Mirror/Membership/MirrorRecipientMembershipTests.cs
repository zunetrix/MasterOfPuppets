using MasterOfPuppets.LuaScripting.Mirror.Coordination;
using MasterOfPuppets.LuaScripting.Mirror.Core;
using MasterOfPuppets.LuaScripting.Mirror.Membership;
using Xunit;

namespace MasterOfPuppetsTests.Mirror.Membership;

public sealed class MirrorRecipientMembershipTests {
    private static readonly DateTimeOffset StartedAt = new(2026, 8, 31, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void BroadcastWithoutAcknowledgementDoesNotInventParticipants() {
        var membership = Session();
        Assert.True(membership.TryCaptureEventSnapshot(Stamp(membership, 1), out var snapshot, out _));
        Assert.Empty(snapshot.Participants);
        Assert.True(snapshot.ParticipantMask.IsEmpty);
        Assert.Equal(MirrorMembershipAdmission.UnknownRecipient, membership.TryObserveHeartbeat(
            Heartbeat(membership, 99, 1, StartedAt.AddSeconds(1)), out _, out var reason));
        Assert.Contains("has not acknowledged START", reason);
    }

    [Fact]
    public void AckAssignsStableIndexesAndLateJoinAffectsOnlyFutureEvents() {
        var membership = Session();
        membership.TryAcknowledgeStart(Ack(membership, 200, 1, StartedAt), out var first, out _);
        membership.TryCaptureEventSnapshot(Stamp(membership, 1), out var beforeJoin, out _);
        membership.TryAcknowledgeStart(Ack(membership, 100, 1, StartedAt), out var late, out _);
        membership.TryCaptureEventSnapshot(Stamp(membership, 2), out var afterJoin, out _);

        Assert.Equal(0, first.ParticipantIndex);
        Assert.Equal(1, late.ParticipantIndex);
        Assert.Equal([200ul], beforeJoin.Participants.Select(value => value.Identity.ContentId));
        Assert.Equal([200ul, 100ul], afterJoin.Participants.Select(value => value.Identity.ContentId));
        Assert.Equal(0b1u, beforeJoin.ParticipantMask.Bits);
        Assert.Equal(0b11u, afterJoin.ParticipantMask.Bits);
    }

    [Fact]
    public void ThirtyThirdActiveRecipientIsRejectedButDepartedSlotIsSafelyReused() {
        var membership = Session();
        for (ulong contentId = 1; contentId <= 32; contentId++)
            Assert.Equal(MirrorMembershipAdmission.Accepted,
                membership.TryAcknowledgeStart(Ack(membership, contentId, 1, StartedAt), out _, out _));
        membership.TryCaptureEventSnapshot(Stamp(membership, 1), out var original, out _);
        Assert.Equal(MirrorMembershipAdmission.CapacityReached,
            membership.TryAcknowledgeStart(Ack(membership, 33, 1, StartedAt), out _, out _));

        Assert.Equal(MirrorMembershipAdmission.Accepted, membership.TryLeave(new MirrorMembershipLeave(
            Guid.NewGuid(), membership.Start.SessionId, membership.Start.Generation,
            1, 2, "recipient left", StartedAt.AddSeconds(1)), out _, out _));
        Assert.Equal(MirrorMembershipAdmission.Accepted,
            membership.TryAcknowledgeStart(Ack(membership, 33, 1, StartedAt.AddSeconds(2)), out var replacement, out _));

        Assert.Equal(0, replacement.ParticipantIndex);
        Assert.Equal(1ul, original.Participants[0].Identity.ContentId);
        Assert.NotEqual(original.Participants[0].AdmissionEpoch, replacement.AdmissionEpoch);
    }

    [Fact]
    public void ExpiredRecipientLeavesAloneAndTransitionRetainsStablePeers() {
        var membership = Session();
        membership.TryAcknowledgeStart(Ack(membership, 10, 1, StartedAt), out _, out _);
        membership.TryAcknowledgeStart(Ack(membership, 20, 1, StartedAt), out _, out _);
        membership.TryObserveHeartbeat(Heartbeat(membership, 10, 2, StartedAt.AddSeconds(20)), out _, out _);
        membership.TryCaptureEventSnapshot(Stamp(membership, 1), out var inFlight, out _);

        var expired = membership.DetectLeaseExpirations(
            StartedAt.AddSeconds(31), TimeSpan.FromSeconds(30));

        Assert.Equal(20ul, Assert.Single(expired).Identity.ContentId);
        Assert.Equal(MirrorMembershipSessionStatus.Active, membership.Snapshot.Status);
        Assert.True(membership.TryTransitionEventSnapshot(inFlight, out var transition, out _));
        Assert.Equal(0b01u, transition.RetainedParticipants.Bits);
        Assert.Equal(0b10u, transition.DepartedParticipants.Bits);
        Assert.Equal(10ul, Assert.Single(transition.Current.Participants).Identity.ContentId);
    }

    [Fact]
    public void RejoinRequiresFreshAckAndAdmissionEpochAndWaitsForFutureEvent() {
        var membership = Session();
        membership.TryAcknowledgeStart(Ack(membership, 10, 1, StartedAt), out var initial, out _);
        membership.TryCaptureEventSnapshot(Stamp(membership, 1), out var inFlight, out _);
        membership.DetectLeaseExpirations(StartedAt.AddSeconds(31), TimeSpan.FromSeconds(30));

        Assert.Equal(MirrorMembershipAdmission.RecipientInactive,
            membership.TryObserveHeartbeat(Heartbeat(membership, 10, 2, StartedAt.AddSeconds(32)), out _, out _));
        Assert.Equal(MirrorMembershipAdmission.Rejoined,
            membership.TryAcknowledgeStart(Ack(membership, 10, 3, StartedAt.AddSeconds(33)), out var rejoined, out _));
        Assert.Equal(initial.ParticipantIndex, rejoined.ParticipantIndex);
        Assert.NotEqual(initial.AdmissionEpoch, rejoined.AdmissionEpoch);

        Assert.True(membership.TryTransitionEventSnapshot(inFlight, out var transition, out _));
        Assert.True(transition.Current.ParticipantMask.IsEmpty);
        membership.TryCaptureEventSnapshot(Stamp(membership, 2), out var future, out _);
        Assert.Equal(rejoined.AdmissionEpoch, Assert.Single(future.Participants).AdmissionEpoch);
    }

    [Theory]
    [InlineData(MirrorGlobalStopSource.ManualOwnerStop)]
    [InlineData(MirrorGlobalStopSource.LocalTargetLostOrInvalid)]
    public void AuthoritativeGlobalStopIsTerminalAndSupportsRepeatAndAck(
        MirrorGlobalStopSource source) {
        var membership = Session();
        membership.TryAcknowledgeStart(Ack(membership, 10, 1, StartedAt), out _, out _);
        membership.TryAcknowledgeStart(Ack(membership, 20, 1, StartedAt), out _, out _);
        Assert.Equal(MirrorMembershipAdmission.Accepted,
            membership.TryStop(Stop(membership, 2, source, StartedAt.AddSeconds(1)), out _));
        Assert.Equal(MirrorMembershipAdmission.Accepted,
            membership.TryStop(Stop(membership, 3, source, StartedAt.AddSeconds(2)), out _));
        Assert.Equal(MirrorMembershipAdmission.Accepted, membership.TryAcknowledgeStop(new MirrorMembershipStopAck(
            Guid.NewGuid(), membership.Start.SessionId, membership.Start.Generation,
            10, 2, StartedAt.AddSeconds(3)), out _));

        var tombstone = membership.Snapshot.StopTombstone;
        Assert.Equal(source, tombstone.Source);
        Assert.Equal(2, tombstone.BroadcastCount);
        Assert.Equal([10ul, 20ul], tombstone.RequiredRecipientContentIds.ToArray());
        Assert.Equal([10ul], tombstone.AcknowledgedRecipientContentIds.ToArray());
        Assert.Equal([20ul], tombstone.PendingRecipientContentIds.ToArray());
        Assert.Equal(MirrorMembershipAdmission.SessionStopped,
            membership.TryAcknowledgeStart(Ack(membership, 10, 3, StartedAt.AddSeconds(4)), out _, out var reason));
        Assert.Contains("cannot resume", reason);
        Assert.False(membership.TryCaptureEventSnapshot(Stamp(membership, 2), out _, out _));
    }

    [Fact]
    public void AdmissionRejectsReplayStaleSequenceSessionAndGeneration() {
        var membership = Session();
        var acknowledgement = Ack(membership, 10, 1, StartedAt);
        Assert.Equal(MirrorMembershipAdmission.Accepted,
            membership.TryAcknowledgeStart(acknowledgement, out _, out _));
        Assert.Equal(MirrorMembershipAdmission.ReplayedMessage,
            membership.TryAcknowledgeStart(acknowledgement, out _, out _));
        Assert.Equal(MirrorMembershipAdmission.StaleSequence,
            membership.TryObserveHeartbeat(Heartbeat(membership, 10, 1, StartedAt.AddSeconds(1)), out _, out _));
        Assert.Equal(MirrorMembershipAdmission.WrongSession,
            membership.TryObserveHeartbeat(Heartbeat(membership, 10, 2, StartedAt.AddSeconds(1)) with {
                SessionId = Guid.NewGuid(),
            }, out _, out _));
        Assert.Equal(MirrorMembershipAdmission.StaleGeneration,
            membership.TryObserveHeartbeat(Heartbeat(membership, 10, 2, StartedAt.AddSeconds(1)) with {
                Generation = membership.Start.Generation - 1,
            }, out _, out _));
        Assert.Equal(MirrorMembershipAdmission.FutureGeneration,
            membership.TryObserveHeartbeat(Heartbeat(membership, 10, 2, StartedAt.AddSeconds(1)) with {
                Generation = membership.Start.Generation + 1,
            }, out _, out _));
    }

    private static MirrorRecipientMembership Session() => new(new MirrorMembershipStart(
        Guid.NewGuid(), Guid.NewGuid(), 7, 1, StartedAt));

    private static MirrorMembershipAck Ack(
        MirrorRecipientMembership membership,
        ulong contentId,
        ulong sequence,
        DateTimeOffset observedAt) => new(
            Guid.NewGuid(), membership.Start.SessionId, membership.Start.Generation,
            Player(contentId), sequence, observedAt);

    private static MirrorMembershipHeartbeat Heartbeat(
        MirrorRecipientMembership membership,
        ulong contentId,
        ulong sequence,
        DateTimeOffset observedAt) => new(
            Guid.NewGuid(), membership.Start.SessionId, membership.Start.Generation,
            contentId, sequence, observedAt);

    private static MirrorMembershipStop Stop(
        MirrorRecipientMembership membership,
        ulong sequence,
        MirrorGlobalStopSource source,
        DateTimeOffset observedAt) => new(
            Guid.NewGuid(), membership.Start.SessionId, membership.Start.Generation,
            sequence, source, source.ToString(), observedAt);

    private static MirrorPlayerIdentity Player(ulong contentId) =>
        MirrorPlayerIdentity.CreateValidated(contentId, 21, $"Recipient {contentId}", true);

    private static MirrorEventStamp Stamp(MirrorRecipientMembership membership, ulong epoch) =>
        new(membership.Start.SessionId, epoch, "emote", epoch);
}
