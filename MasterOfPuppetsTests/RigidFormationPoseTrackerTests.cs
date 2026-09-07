using System.Numerics;

using MasterOfPuppets.LuaScripting;

using Xunit;

namespace MasterOfPuppetsTests;

public sealed class RigidFormationPoseTrackerTests {
    [Fact]
    public void FirstSampleUsesLeaderPoseExactly() {
        var tracker = new RigidFormationPoseTracker();

        var pose = tracker.Step(new Vector3(5f, 1f, 7f), 0.75f, 8f, 6f, 1000);

        Assert.Equal(new Vector3(5f, 1f, 7f), pose.Position);
        Assert.Equal(0.75f, pose.Rotation, precision: 5);
        Assert.False(pose.IsTurning);
    }

    [Fact]
    public void WideFormationRateLimitsRotationAsOneFrame() {
        var tracker = new RigidFormationPoseTracker();
        tracker.Step(Vector3.Zero, 0f, 10f, 6f, 1000);

        var pose = tracker.Step(Vector3.Zero, MathF.PI / 2f, 10f, 6f, 1100);

        Assert.True(pose.IsTurning);
        Assert.InRange(pose.Rotation, 0.059f, 0.061f);
    }

    [Fact]
    public void StationaryRotationNoiseDoesNotMoveTheFormationFrame() {
        var tracker = new RigidFormationPoseTracker();
        tracker.Step(Vector3.Zero, 0f, 10f, 6f, 1000);

        var pose = tracker.Step(Vector3.Zero, 0.1f, 10f, 6f, 1100);

        Assert.False(pose.IsTurning);
        Assert.Equal(0f, pose.Rotation, precision: 5);
    }

    [Fact]
    public void SuddenAboutFaceFlipsFrameSoSlotsTakeDirectPathThroughLeader() {
        var tracker = new RigidFormationPoseTracker();
        var leader = new Vector3(5f, 0f, 7f);
        tracker.Step(leader, 0f, 10f, 6f, 1000);

        var pose = tracker.Step(leader, MathF.PI, 10f, 6f, 1100);

        Assert.True(pose.IsTurning);
        Assert.InRange(MathF.Abs(pose.Rotation), MathF.PI - 0.0001f, MathF.PI + 0.0001f);
        var reversedSlot = LuaActorFollowController.GetWorldDestination(
            pose.Position,
            pose.Rotation,
            new Vector3(0f, 0f, -3f));
        Assert.Equal(10f, reversedSlot.Z, precision: 4);
    }

    [Fact]
    public void TurningReservesSpeedForOuterSlotsWithoutLettingFrameCenterTrail() {
        var tracker = new RigidFormationPoseTracker();
        tracker.Step(Vector3.Zero, 0f, 8f, 6f, 1000);

        var pose = tracker.Step(new Vector3(0f, 0f, 0.6f), 0.4f, 8f, 6f, 1100);

        Assert.True(pose.IsTurning);
        Assert.Equal(0.6f, pose.Position.Z, precision: 5);
        Assert.InRange(pose.Rotation, 0.046f, 0.048f);
    }

    [Fact]
    public void ClientsThatAttachAtDifferentTimesConvergeDuringStraightTravel() {
        var earlyClient = new RigidFormationPoseTracker();
        var lateClient = new RigidFormationPoseTracker();
        earlyClient.Step(Vector3.Zero, 0f, 8f, 6f, 1000);
        lateClient.Step(new Vector3(0f, 0f, 0.3f), 0f, 8f, 6f, 1050);

        var leader = new Vector3(0f, 0f, 0.6f);
        var earlyPose = earlyClient.Step(leader, 0f, 8f, 6f, 1100);
        var latePose = lateClient.Step(leader, 0f, 8f, 6f, 1100);

        Assert.Equal(leader, earlyPose.Position);
        Assert.Equal(earlyPose.Position, latePose.Position);
        Assert.Equal(earlyPose.Rotation, latePose.Rotation, precision: 5);
    }

    [Fact]
    public void TeleportResetsFrameInsteadOfRunningAcrossZone() {
        var tracker = new RigidFormationPoseTracker();
        tracker.Step(Vector3.Zero, 0f, 5f, 6f, 1000);

        var pose = tracker.Step(new Vector3(50f, 3f, 50f), 1.2f, 5f, 6f, 1100);

        Assert.Equal(new Vector3(50f, 3f, 50f), pose.Position);
        Assert.Equal(1.2f, pose.Rotation, precision: 5);
        Assert.False(pose.IsTurning);
    }
}
