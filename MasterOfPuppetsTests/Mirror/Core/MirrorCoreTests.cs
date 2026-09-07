using System;
using System.Linq;

using MasterOfPuppets.LuaScripting.Mirror.Core;

using Xunit;

public class MirrorCoreTests {
    [Fact]
    public void Identity_Requires_RuntimeValidated_RealPlayer() {
        Assert.Throws<ArgumentException>(() =>
            MirrorPlayerIdentity.CreateValidated(1, 21, "Not A Player", isRealPlayerCharacter: false));

        var player = Player(1);

        Assert.Equal((ulong)1, player.ContentId);
        Assert.Equal((uint)21, player.HomeWorldId);
    }

    [Fact]
    public void Roster_IsOrderedImmutableUnique_AndCappedAt32() {
        var ordered = Enumerable.Range(1, 32).Select(index => Player((ulong)index)).ToArray();
        var roster = MirrorRosterSnapshot.Create(ordered);

        Assert.Equal(32, roster.Count);
        Assert.Equal((ulong)1, roster[0].ContentId);
        Assert.Equal((ulong)32, roster[31].ContentId);
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            MirrorRosterSnapshot.Create(Enumerable.Range(1, 33).Select(index => Player((ulong)index))));
        Assert.Throws<ArgumentException>(() =>
            MirrorRosterSnapshot.Create([Player(1), Player(1)]));
    }

    [Fact]
    public void NewerDirective_SupersedesOnlyItsOwnModule_AndRejectsStaleWork() {
        using var session = Session();
        var emote = new MirrorModuleId("emote");
        var mount = new MirrorModuleId("mount");

        var emoteV1 = session.NextRevision();
        Assert.Equal(MirrorDirectiveAdmission.Accepted, session.TryBeginDirective(emote, emoteV1, out var firstEmote));
        var mountV1 = session.NextRevision();
        Assert.Equal(MirrorDirectiveAdmission.Accepted, session.TryBeginDirective(mount, mountV1, out var firstMount));
        var emoteV2 = session.NextRevision();
        Assert.Equal(MirrorDirectiveAdmission.Accepted, session.TryBeginDirective(emote, emoteV2, out var secondEmote));

        Assert.True(firstEmote.CancellationToken.IsCancellationRequested);
        Assert.Equal(MirrorCancellationReason.Superseded, firstEmote.CancellationReason);
        Assert.False(firstMount.CancellationToken.IsCancellationRequested);
        Assert.True(session.IsCurrent(secondEmote));
        Assert.Equal(
            MirrorDirectiveAdmission.StaleOrDuplicateRevision,
            session.TryBeginDirective(emote, emoteV1, out _));
    }

    [Fact]
    public void FirstTargetObservationLoss_IsTerminal_AndCancelsAllModules() {
        using var session = Session();
        var revision = session.NextRevision();
        session.TryBeginDirective(new MirrorModuleId("target"), revision, out var lease);

        Assert.True(session.ReportTargetLocalObservationLost(session.Target, "actor left local observation"));
        Assert.True(lease.CancellationToken.IsCancellationRequested);
        Assert.True(session.CancellationToken.IsCancellationRequested);
        Assert.Equal(MirrorCancellationReason.TargetLost, lease.CancellationReason);
        Assert.Equal(MirrorSessionStatus.TargetLost, session.Snapshot.Status);
        Assert.False(session.ReportTargetLocalObservationLost(session.Target, "second signal"));
        Assert.Throws<InvalidOperationException>(() => session.NextRevision());
    }

    [Fact]
    public void Outcome_ExposesOnlyRetryableFollowers_AsRetryCandidates() {
        using var session = Session();
        var stamp = session.NextRevision();
        var directive = MirrorDirective<string>.DesiredState(new MirrorModuleId("visor"), stamp, "enabled");
        var succeeded = session.Roster[0];
        var failed = session.Roster[1];
        var outcome = MirrorDirectiveOutcome.Create(directive.ToReference(), [
            new MirrorParticipantOutcome(succeeded, MirrorExecutionDisposition.Succeeded, 1),
            new MirrorParticipantOutcome(failed, MirrorExecutionDisposition.RetryableFailure, 1, "animation lock"),
        ]);

        Assert.Equal(failed, Assert.Single(outcome.RetryCandidates()));
    }

    private static MirrorSession Session() {
        var target = Player(100);
        return new MirrorSession(
            Guid.NewGuid(),
            generation: 7,
            target,
            MirrorRosterSnapshot.Create([Player(1), Player(2)]));
    }

    private static MirrorPlayerIdentity Player(ulong contentId) =>
        MirrorPlayerIdentity.CreateValidated(contentId, 21, $"Player {contentId}", isRealPlayerCharacter: true);
}
