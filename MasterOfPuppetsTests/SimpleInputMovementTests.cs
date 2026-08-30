using System.Numerics;

using MasterOfPuppets.Formations;
using MasterOfPuppets.Movement;

using Xunit;

public class SimpleInputMovementTests {
    [Theory]
    [InlineData(SimpleMovementMode.Natural, true)]
    [InlineData(SimpleMovementMode.Continuous, false)]
    [InlineData(SimpleMovementMode.Precise, false)]
    [InlineData(SimpleMovementMode.Forward, false)]
    public void PreservesWalkState_ForLiveToggleModes(SimpleMovementMode mode, bool expected) {
        Assert.Equal(expected, SimpleInputMovement.PreservesWalkState(mode));
    }

    [Theory]
    [InlineData(SimpleMovementMode.Natural, true)]
    [InlineData(SimpleMovementMode.Continuous, false)]
    [InlineData(SimpleMovementMode.Precise, false)]
    [InlineData(SimpleMovementMode.Forward, false)]
    public void UsesLiveFormationTracking_ForPersistentAnchorModes(SimpleMovementMode mode, bool expected) {
        Assert.Equal(expected, SimpleInputMovement.UsesLiveFormationTracking(mode));
    }

    [Fact]
    public void CaptureControlBaseline_PreservesOriginalModesAcrossReplacementMoves() {
        var original = new MovementControlState(MoveMode: 1, PadMode: 1);
        var transient = new MovementControlState(MoveMode: 0, PadMode: 0);

        var baseline = SimpleInputMovement.CaptureControlBaseline(null, original);
        baseline = SimpleInputMovement.CaptureControlBaseline(baseline, transient);

        Assert.Equal(original, baseline);
    }

    [Fact]
    public void CaptureWalkBaseline_PreservesOriginalStateAcrossReplacementMoves() {
        var baseline = SimpleInputMovement.CaptureWalkBaseline(null, currentState: false);
        baseline = SimpleInputMovement.CaptureWalkBaseline(baseline, currentState: true);

        Assert.False(baseline);
    }

    [Fact]
    public void FormationTargetTracker_ResetsVelocityAfterTeleport() {
        var tracker = new FormationTargetTracker();
        tracker.Reset(Vector3.Zero, nowMs: 0);
        tracker.UpdateTarget(new Vector3(1f, 0f, 0f), nowMs: 100);
        tracker.UpdateTarget(new Vector3(20f, 0f, 0f), nowMs: 200);

        Assert.Equal(Vector3.Zero, tracker.Velocity);
        Assert.Equal(new Vector3(20f, 0f, 0f), tracker.Target);
    }

    [Fact]
    public void FormationTargetTracker_IgnoresStationaryTransformNoise() {
        var tracker = new FormationTargetTracker();
        tracker.Reset(Vector3.Zero, nowMs: 0);
        tracker.UpdateTarget(new Vector3(0.005f, 0f, 0f), nowMs: 16);

        Assert.False(tracker.IsSlotMoving);
    }

    [Fact]
    public void FormationTargetTracker_HoldUsesResumeHysteresis() {
        Assert.True(FormationTargetTracker.ShouldHold(0.09f, 0.1f, wasHolding: false, slotMoving: false));
        Assert.True(FormationTargetTracker.ShouldHold(0.15f, 0.1f, wasHolding: true, slotMoving: false));
        Assert.False(FormationTargetTracker.ShouldHold(0.19f, 0.1f, wasHolding: true, slotMoving: false));
        Assert.True(FormationTargetTracker.ShouldHold(0.01f, 0.1f, wasHolding: true, slotMoving: true));
    }

    [Fact]
    public void FormationTargetTracker_FacingUpdatesOnlyAfterMeaningfulChange() {
        Assert.True(FormationTargetTracker.ShouldUpdateFacing(null, 0f));
        Assert.False(FormationTargetTracker.ShouldUpdateFacing(0f, 0.01f));
        Assert.True(FormationTargetTracker.ShouldUpdateFacing(0f, 0.02f));
        Assert.False(FormationTargetTracker.ShouldUpdateFacing(MathF.PI, -MathF.PI + 0.01f));
    }

    [Theory]
    [InlineData(0f, 0f, 1f, MovementDirection.Forward)]
    [InlineData(0f, 0f, -1f, MovementDirection.Backward)]
    [InlineData(0f, -1f, 0f, MovementDirection.StrafeRight)]
    [InlineData(0f, 1f, 0f, MovementDirection.StrafeLeft)]
    [InlineData(1.5707964f, 1f, 0f, MovementDirection.Forward)]
    [InlineData(1.5707964f, -1f, 0f, MovementDirection.Backward)]
    [InlineData(1.5707964f, 0f, 1f, MovementDirection.StrafeRight)]
    [InlineData(1.5707964f, 0f, -1f, MovementDirection.StrafeLeft)]
    public void FormationTargetTracker_SelectsMovementRelativeToFormationFacing(
        float facing,
        float targetX,
        float targetZ,
        MovementDirection expected) {
        var result = FormationTargetTracker.SelectRelativeMovementDirection(
            Vector3.Zero,
            new Vector3(targetX, 0f, targetZ),
            facing);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void FormationTargetTracker_DiagonalErrorUsesDominantRelativeAxis() {
        var mostlyBehind = FormationTargetTracker.SelectRelativeMovementDirection(
            Vector3.Zero,
            new Vector3(-0.25f, 0f, -1f),
            facingRadians: 0f);
        var mostlyRight = FormationTargetTracker.SelectRelativeMovementDirection(
            Vector3.Zero,
            new Vector3(-1f, 0f, -0.25f),
            facingRadians: 0f);

        Assert.Equal(MovementDirection.Backward, mostlyBehind);
        Assert.Equal(MovementDirection.StrafeRight, mostlyRight);
    }

    [Theory]
    [InlineData(0f, 0f, 1f, false)]
    [InlineData(0f, 0f, -1f, true)]
    [InlineData(0f, -1f, 0f, true)]
    [InlineData(0f, 1f, 0f, true)]
    [InlineData(0f, -0.25f, 1f, false)]
    [InlineData(0f, -1f, 0.25f, true)]
    public void FormationAnchorLocomotion_ClassifiesOnlyBackwardOrStrafe(
        float facing,
        float moveX,
        float moveZ,
        bool expected) {
        Assert.Equal(
            expected,
            FormationAnchorLocomotionTracker.IsBackwardOrStrafe(
                new Vector3(moveX, 0f, moveZ),
                facing));
    }

    [Fact]
    public void FormationAnchorLocomotion_RotationWithoutTranslationUsesOriginalMovement() {
        var tracker = new FormationAnchorLocomotionTracker(Vector3.Zero, 0f, nowMs: 0);

        var result = tracker.Update(Vector3.Zero, 0.1f, nowMs: 16);

        Assert.False(result);
    }

    [Fact]
    public void FormationAnchorLocomotion_BackupUsesRelativeMovement() {
        var tracker = new FormationAnchorLocomotionTracker(Vector3.Zero, 0f, nowMs: 0);

        var result = tracker.Update(new Vector3(0f, 0f, -0.1f), 0f, nowMs: 16);

        Assert.True(result);
    }

    [Fact]
    public void FormationAnchorLocomotion_RotationOverridesTranslationNoise() {
        var tracker = new FormationAnchorLocomotionTracker(Vector3.Zero, 0f, nowMs: 0);

        var result = tracker.Update(new Vector3(-0.02f, 0f, 0f), 0.1f, nowMs: 16);

        Assert.False(result);
    }

    [Fact]
    public void FormationAnchorPoseTracker_FreezesStationaryTransformNoise() {
        var tracker = new FormationAnchorPoseTracker(Vector3.Zero, 0f, nowMs: 0);

        tracker.Update(new Vector3(0.01f, 0f, 0f), 0.01f, nowMs: 16);

        Assert.False(tracker.IsMoving);
        Assert.Equal(Vector3.Zero, tracker.AcceptedPosition);
        Assert.Equal(0f, tracker.AcceptedRotation);
    }

    [Fact]
    public void FormationAnchorPoseTracker_TracksMeaningfulRotation() {
        var tracker = new FormationAnchorPoseTracker(Vector3.Zero, 0f, nowMs: 0);
        var rotation = FormationAnchorPoseTracker.RotationDeadZoneRadians + 0.001f;

        tracker.Update(Vector3.Zero, rotation, nowMs: 16);

        Assert.True(tracker.IsMoving);
        Assert.Equal(rotation, tracker.AcceptedRotation);
    }

    [Fact]
    public void FormationAnchorPoseTracker_FreezesAgainAfterQuietPeriod() {
        var tracker = new FormationAnchorPoseTracker(Vector3.Zero, 0f, nowMs: 0);
        tracker.Update(new Vector3(0.03f, 0f, 0f), 0f, nowMs: 16);
        tracker.Update(new Vector3(0.031f, 0f, 0f), 0f, nowMs: 216);
        var acceptedPosition = tracker.AcceptedPosition;

        tracker.Update(new Vector3(0.035f, 0f, 0f), 0.005f, nowMs: 232);

        Assert.False(tracker.IsMoving);
        Assert.Equal(acceptedPosition, tracker.AcceptedPosition);
        Assert.Equal(0f, tracker.AcceptedRotation);
    }

    [Fact]
    public void FormationTrackingSession_RecomputesSlotFromCurrentAnchorRotation() {
        var formation = new Formation {
            Name = "Line",
            Points = [
                new FormationPoint { Offset = Vector3.Zero },
                new FormationPoint { Offset = new Vector3(1f, 0f, 0f) },
            ],
        };

        Assert.True(FormationTrackingSession.TryComputeTarget(
            formation, 1, 0, Vector3.Zero, 0f, out var beforeTurn));
        Assert.True(FormationTrackingSession.TryComputeTarget(
            formation, 1, 0, Vector3.Zero, MathF.PI / 2f, out var afterTurn));

        Assert.Equal(new Vector3(1f, 0f, 0f), beforeTurn.Position);
        Assert.True(Vector3.Distance(new Vector3(0f, 0f, -1f), afterTurn.Position) < 0.0001f);
    }

    [Fact]
    public void GetArrivalState_Runs_When_Outside_Walk_Buffer() {
        var state = SimpleInputMovement.GetArrivalState(distance: 0.61f, precision: 0.1f, SimpleMovementMode.Forward);

        Assert.Equal(ArrivalMovementState.Run, state);
    }

    [Fact]
    public void GetArrivalState_Walks_When_Inside_Walk_Buffer_But_Outside_Stop_Radius() {
        var state = SimpleInputMovement.GetArrivalState(distance: 0.5f, precision: 0.1f, SimpleMovementMode.Forward);

        Assert.Equal(ArrivalMovementState.Walk, state);
    }

    [Fact]
    public void GetArrivalState_Stops_When_Inside_Precision_Radius() {
        var state = SimpleInputMovement.GetArrivalState(distance: 0.09f, precision: 0.1f, SimpleMovementMode.Forward);

        Assert.Equal(ArrivalMovementState.Stop, state);
    }

    [Fact]
    public void GetArrivalState_Uses_Configured_Precision() {
        Assert.Equal(ArrivalMovementState.Stop, SimpleInputMovement.GetArrivalState(distance: 0.24f, precision: 0.25f, SimpleMovementMode.Forward));
        Assert.Equal(ArrivalMovementState.Walk, SimpleInputMovement.GetArrivalState(distance: 0.7f, precision: 0.25f, SimpleMovementMode.Forward));
        Assert.Equal(ArrivalMovementState.Run, SimpleInputMovement.GetArrivalState(distance: 0.76f, precision: 0.25f, SimpleMovementMode.Forward));
    }

    [Fact]
    public void GetArrivalState_Continuous_Runs_Inside_Walk_Buffer() {
        var state = SimpleInputMovement.GetArrivalState(
            distance: 0.5f,
            precision: 0.1f,
            SimpleMovementMode.Continuous);

        Assert.Equal(ArrivalMovementState.Run, state);
    }

    [Fact]
    public void GetArrivalState_Continuous_Stops_When_Inside_Precision_Radius() {
        var state = SimpleInputMovement.GetArrivalState(
            distance: 0.09f,
            precision: 0.1f,
            SimpleMovementMode.Continuous);

        Assert.Equal(ArrivalMovementState.Stop, state);
    }

    [Fact]
    public void GetArrivalState_Precise_Uses_Forward_Walk_Radius() {
        Assert.Equal(ArrivalMovementState.Run, SimpleInputMovement.GetArrivalState(distance: 0.61f, precision: 0.1f, SimpleMovementMode.Precise));
        Assert.Equal(ArrivalMovementState.Walk, SimpleInputMovement.GetArrivalState(distance: 0.6f, precision: 0.1f, SimpleMovementMode.Precise));
        Assert.Equal(ArrivalMovementState.Stop, SimpleInputMovement.GetArrivalState(distance: 0.1f, precision: 0.1f, SimpleMovementMode.Precise));
    }

    [Fact]
    public void UpdateContinuousProgress_Completes_Inside_Precision() {
        var progress = SimpleInputMovement.UpdateContinuousProgress(
            distance: 0.09f,
            precision: 0.1f,
            previousDistance: 0.2f,
            hasApproached: true);

        Assert.True(progress.Complete);
    }

    [Fact]
    public void UpdateContinuousProgress_Keeps_Running_While_Approaching() {
        var progress = SimpleInputMovement.UpdateContinuousProgress(
            distance: 1.5f,
            precision: 0.1f,
            previousDistance: 2f,
            hasApproached: false);

        Assert.False(progress.Complete);
        Assert.True(progress.HasApproached);
    }

    [Fact]
    public void UpdateContinuousProgress_Completes_After_Passing_Closest_Approach() {
        var progress = SimpleInputMovement.UpdateContinuousProgress(
            distance: 1.61f,
            precision: 0.1f,
            previousDistance: 1.5f,
            hasApproached: true);

        Assert.True(progress.Complete);
    }

    [Fact]
    public void UpdateContinuousProgress_Does_Not_Complete_On_First_Large_Sample() {
        var progress = SimpleInputMovement.UpdateContinuousProgress(
            distance: 5f,
            precision: 0.1f,
            previousDistance: null,
            hasApproached: false);

        Assert.False(progress.Complete);
        Assert.False(progress.HasApproached);
    }

    [Fact]
    public void UpdateContinuousProgress_Does_Not_Complete_On_Increase_Before_Approach() {
        var progress = SimpleInputMovement.UpdateContinuousProgress(
            distance: 6f,
            precision: 0.1f,
            previousDistance: 5f,
            hasApproached: false);

        Assert.False(progress.Complete);
        Assert.False(progress.HasApproached);
    }

    [Fact]
    public void ProgressTracker_Does_Not_Report_Stuck_On_First_Sample() {
        var tracker = new SimpleMovementProgressTracker();

        var stuck = tracker.Update(Vector3.Zero, nowMs: 1_000, movementTolerance: 0.05f, timeoutMs: 500);

        Assert.False(stuck);
    }

    [Fact]
    public void ProgressTracker_Reports_Stuck_After_Timeout_Without_Significant_Movement() {
        var tracker = new SimpleMovementProgressTracker();

        Assert.False(tracker.Update(Vector3.Zero, nowMs: 1_000, movementTolerance: 0.05f, timeoutMs: 500));

        var stuck = tracker.Update(new Vector3(0.01f, 0, 0), nowMs: 1_500, movementTolerance: 0.05f, timeoutMs: 500);

        Assert.True(stuck);
    }

    [Fact]
    public void ProgressTracker_Resets_Timeout_When_Position_Changes_Significantly() {
        var tracker = new SimpleMovementProgressTracker();

        Assert.False(tracker.Update(Vector3.Zero, nowMs: 1_000, movementTolerance: 0.05f, timeoutMs: 500));
        Assert.False(tracker.Update(new Vector3(0.10f, 0, 0), nowMs: 1_400, movementTolerance: 0.05f, timeoutMs: 500));

        var stuck = tracker.Update(new Vector3(0.11f, 0, 0), nowMs: 1_800, movementTolerance: 0.05f, timeoutMs: 500);

        Assert.False(stuck);
    }
}
