using MasterOfPuppets.LuaScripting.Mirror.Coordination;

using Xunit;

namespace MasterOfPuppetsTests.Mirror.Coordination;

public sealed class MirrorCoordinationPrimitivesTests {
    [Fact]
    public void Roster_PreservesExactOrderAndSupportsAllThirtyTwoBits() {
        var contentIds = Enumerable.Range(1, 32).Select(index => (ulong)index).ToArray();
        var roster = new MirrorRoster(contentIds);

        Assert.Equal(32, roster.Count);
        Assert.Equal(uint.MaxValue, roster.AllParticipants.Bits);
        Assert.Equal(31, roster.GetIndex(32));
        Assert.Equal(32ul, roster.GetContentId(31));
        Assert.Throws<ArgumentException>(() => new MirrorRoster(contentIds.Append(33ul)));
        Assert.Throws<ArgumentException>(() => new MirrorRoster([1, 1]));
    }

    [Fact]
    public void EventClock_IssuesGlobalEpochsAndPerModuleDesiredRevisions() {
        var clock = new MirrorAuthoritativeEventClock(Guid.NewGuid());

        var firstEmote = clock.Issue("Emote");
        var mount = clock.Issue("mount");
        var secondEmote = clock.Issue("EMOTE");

        Assert.Equal((1ul, 1ul, "emote"), (firstEmote.Epoch, firstEmote.DesiredRevision, firstEmote.Module));
        Assert.Equal((2ul, 1ul), (mount.Epoch, mount.DesiredRevision));
        Assert.Equal((3ul, 2ul), (secondEmote.Epoch, secondEmote.DesiredRevision));
    }

    [Fact]
    public void FallbackResolver_PreservesExactUsersAndDeterministicallyMaximizesSharedCoverage() {
        var stamp = new MirrorAuthoritativeEventClock(Guid.NewGuid()).Issue("mount");
        var participants = MirrorParticipantMask.All(4);
        var exact = new MirrorParticipantMask(0b0011);
        var request = new MirrorFallbackRequest(
            stamp,
            "mount",
            "exact-a",
            participants,
            exact,
            [
                new MirrorFallbackCandidate("fallback-z", new MirrorParticipantMask(0b1100), 2),
                new MirrorFallbackCandidate("fallback-a", new MirrorParticipantMask(0b1100), 1),
            ]);

        var selected = MirrorFallbackResolver.Select(request);

        Assert.Equal(exact, selected.ExactParticipants);
        Assert.Equal("fallback-a", selected.FallbackObjectId);
        Assert.Equal(new MirrorParticipantMask(0b1100), selected.FallbackParticipants);
        Assert.True(selected.UnresolvedParticipants.IsEmpty);
    }

    [Fact]
    public void FallbackResolver_ExposesUnresolvedParticipantsWithoutChoosingOdBOutcome() {
        var stamp = new MirrorAuthoritativeEventClock(Guid.NewGuid()).Issue("emote");
        var selected = MirrorFallbackResolver.Select(new MirrorFallbackRequest(
            stamp,
            "emote",
            "desired",
            MirrorParticipantMask.All(4),
            MirrorParticipantMask.One(0),
            [new MirrorFallbackCandidate("widest", new MirrorParticipantMask(0b0110), 0)]));

        Assert.Equal(new MirrorParticipantMask(0b0110), selected.FallbackParticipants);
        Assert.Equal(MirrorParticipantMask.One(3), selected.UnresolvedParticipants);
        Assert.True(selected.RequiresOwnerDefinedNoUniversalFallbackOutcome);
    }

    [Theory]
    [InlineData(MirrorRetryScope.FailedParticipantsOnly, 0b0100u)]
    [InlineData(MirrorRetryScope.WholeParticipatingRoster, 0b1111u)]
    public void Barrier_DerivesRetryMaskWithoutApplyingTimeoutOrDropoutPolicy(
        MirrorRetryScope retryScope,
        uint expectedRetryBits) {
        var stamp = new MirrorAuthoritativeEventClock(Guid.NewGuid()).Issue("emote");
        var barrier = new MirrorEventBarrier(stamp, MirrorParticipantMask.All(4), retryScope);
        Assert.True(barrier.MarkReady(MirrorParticipantMask.All(4), out _));
        Assert.True(barrier.TryRelease(out _));
        Assert.True(barrier.RecordOutcome(0, 1, true, out _));
        Assert.True(barrier.RecordOutcome(1, 1, true, out _));
        Assert.True(barrier.RecordOutcome(2, 1, false, out _));
        Assert.True(barrier.RecordOutcome(3, 1, true, out _));

        var failed = barrier.Snapshot;
        Assert.Equal(MirrorBarrierPhase.AwaitingRetryDecision, failed.Phase);
        Assert.Equal(expectedRetryBits, failed.RecommendedRetryParticipants.Bits);
        Assert.True(barrier.BeginRetry(out _));
        Assert.Equal(expectedRetryBits, barrier.Snapshot.AttemptParticipants.Bits);
        Assert.Equal(MirrorBarrierPhase.CollectingReady, barrier.Snapshot.Phase);
    }

    [Fact]
    public void Barrier_RejectsStaleAttemptsAndConvergesAfterRetry() {
        var stamp = new MirrorAuthoritativeEventClock(Guid.NewGuid()).Issue("action");
        var barrier = new MirrorEventBarrier(stamp, MirrorParticipantMask.All(2), MirrorRetryScope.FailedParticipantsOnly);
        barrier.MarkReady(MirrorParticipantMask.All(2), out _);
        barrier.TryRelease(out _);
        barrier.RecordOutcome(0, 1, true, out _);
        barrier.RecordOutcome(1, 1, false, out _);
        barrier.BeginRetry(out _);
        barrier.MarkReady(MirrorParticipantMask.One(1), out _);
        barrier.TryRelease(out _);

        Assert.False(barrier.RecordOutcome(1, 1, true, out var stale));
        Assert.Contains("stale", stale);
        Assert.True(barrier.RecordOutcome(1, 2, true, out _));
        Assert.Equal(MirrorBarrierPhase.Converged, barrier.Snapshot.Phase);
        Assert.Equal(MirrorParticipantMask.All(2), barrier.Snapshot.ConvergedParticipants);
    }

    [Fact]
    public void AdmissionGate_RejectsReplaysStaleSequencesAndOldEpochs() {
        var roster = new MirrorRoster([10, 20]);
        var clock = new MirrorAuthoritativeEventClock(Guid.NewGuid());
        var first = clock.Issue("mount");
        var gate = new MirrorMessageAdmissionGate(roster, first);
        var message = new MirrorCoordinationEnvelope(
            Guid.NewGuid(), first, 0, 1, MirrorCoordinationMessageKind.Ready, 1,
            MirrorParticipantMask.One(0), string.Empty);

        Assert.True(gate.TryAccept(message, out _));
        Assert.False(gate.TryAccept(message, out var replay));
        Assert.Contains("replay", replay);
        Assert.False(gate.TryAccept(message with { MessageId = Guid.NewGuid() }, out var sequence));
        Assert.Contains("sequence", sequence);

        var second = clock.Issue("mount");
        gate.MoveTo(second);
        Assert.False(gate.TryAccept(message with { MessageId = Guid.NewGuid(), SenderSequence = 2 }, out var stale));
        Assert.Contains("stale", stale);
    }

    [Fact]
    public void OutcomeState_ConvergesOnlyForwardOnCoordinatorRevision() {
        var stamp = new MirrorAuthoritativeEventClock(Guid.NewGuid()).Issue("visor");
        var state = new MirrorOutcomeConvergenceState(stamp);
        var outcome = new MirrorAuthoritativeOutcome(
            stamp,
            1,
            MirrorParticipantMask.All(2),
            MirrorParticipantMask.One(0),
            MirrorParticipantMask.One(1),
            1);

        Assert.True(state.TryApply(outcome, out _));
        Assert.False(state.TryApply(outcome, out var replay));
        Assert.Contains("stale or replayed", replay);
        Assert.True(state.TryApply(outcome with {
            SucceededParticipants = MirrorParticipantMask.All(2),
            FailedParticipants = MirrorParticipantMask.Empty,
            OutcomeRevision = 2,
        }, out _));
        Assert.Equal(MirrorParticipantMask.All(2), state.Latest.SucceededParticipants);
    }
}
