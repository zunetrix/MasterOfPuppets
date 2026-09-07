using MasterOfPuppets.LuaScripting.Mirror.Core;
using MasterOfPuppets.LuaScripting.Mirror.Runtime;
using MasterOfPuppets.LuaScripting.Watches;

using Xunit;

namespace MasterOfPuppetsTests.Mirror.Native;

public sealed class MirrorBoundTargetWatchTests {
    [Fact]
    public void ActorWatchAppearanceChanges_AreFlaggedAndIncludedInEventPayload() {
        var previous = WatchState();
        var current = WatchState() with {
            IsHeadgearVisible = true,
            IsVisorToggled = true,
        };
        var payload = new Dictionary<string, string>();

        LuaActorWatchService.AddState(payload, current);
        var changes = current.ChangesFrom(previous);

        Assert.True(changes.HasFlag(LuaActorWatchChange.Headgear));
        Assert.True(changes.HasFlag(LuaActorWatchChange.Visor));
        Assert.Equal("true", payload["is_headgear_visible"]);
        Assert.Equal("true", payload["is_visor_toggled"]);
    }

    [Fact]
    public void MatchingObservation_RemainsActive() {
        using var watch = Watch();

        var transition = watch.Observe(MirrorBoundTargetObservation.Observed(900, 901));

        Assert.Equal(MirrorBoundTargetWatchStatus.Observing, transition.Status);
        Assert.False(transition.EmittedTerminalLoss);
        Assert.False(watch.GenerationCancellation.IsCancellationRequested);
    }

    [Fact]
    public void FirstMissingObservation_EmitsOnce_AndCancelsGeneration() {
        using var watch = Watch();
        var emissions = new List<MirrorBoundTargetLoss>();
        watch.TargetLost += emissions.Add;

        var first = watch.Observe(MirrorBoundTargetObservation.Lost("actor left local observation"));
        var repeated = watch.Observe(MirrorBoundTargetObservation.Lost("still absent"));

        Assert.True(first.EmittedTerminalLoss);
        Assert.False(repeated.EmittedTerminalLoss);
        Assert.Single(emissions);
        Assert.Same(first.Loss, repeated.Loss);
        Assert.Equal(7, first.Loss.Generation);
        Assert.True(watch.GenerationCancellation.IsCancellationRequested);
        Assert.Equal(MirrorBoundTargetWatchStatus.TargetLost, watch.Status);
    }

    [Fact]
    public void ReplacementActor_IsTerminal_AndCannotReacquireOriginal() {
        using var watch = Watch();

        var replacement = watch.Observe(MirrorBoundTargetObservation.Observed(902, 903));
        var originalReturns = watch.Observe(MirrorBoundTargetObservation.Observed(900, 901));

        Assert.True(replacement.EmittedTerminalLoss);
        Assert.Contains("reacquisition is not permitted", replacement.Loss.Reason);
        Assert.False(originalReturns.EmittedTerminalLoss);
        Assert.Equal(MirrorBoundTargetWatchStatus.TargetLost, originalReturns.Status);
        Assert.True(watch.GenerationCancellation.IsCancellationRequested);
    }

    private static MirrorBoundTargetWatch Watch() {
        var player = MirrorPlayerIdentity.CreateValidated(
            1234,
            21,
            "Observed Player",
            isRealPlayerCharacter: true);
        return new MirrorBoundTargetWatch(
            generation: 7,
            new MirrorBoundTargetIdentity(player, gameObjectId: 900, entityId: 901));
    }

    private static LuaActorWatchState WatchState() => new(
        GameObjectId: 900,
        EntityId: 901,
        TargetGameObjectId: 0,
        PositionX: 0,
        PositionY: 0,
        PositionZ: 0,
        IsTargetable: true,
        IsDead: false,
        IsLoaded: true,
        IsMoving: false,
        IsJumping: false,
        JumpSequence: 0,
        MountId: 0,
        CompanionId: 0,
        EmoteId: 0,
        EmoteTargetGameObjectId: 0,
        IsEmoteLooping: false,
        PoseType: 0,
        PoseState: 0,
        OrnamentId: 0,
        FacewearId: 0,
        IsSprinting: false,
        IsWeaponDrawn: false,
        OnlineStatusId: 0,
        ClassJobId: 1);
}
