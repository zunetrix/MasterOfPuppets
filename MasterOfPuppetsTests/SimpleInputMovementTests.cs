using System.Numerics;

using MasterOfPuppets.Movement;

using Xunit;

public class SimpleInputMovementTests {
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
