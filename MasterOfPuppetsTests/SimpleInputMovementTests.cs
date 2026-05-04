using System.Numerics;

using MasterOfPuppets.Movement;

using Xunit;

public class SimpleInputMovementTests {
    [Fact]
    public void GetArrivalState_Runs_When_Outside_Walk_Buffer() {
        var state = SimpleInputMovement.GetArrivalState(distance: 0.61f, precision: 0.1f);

        Assert.Equal(ArrivalMovementState.Run, state);
    }

    [Fact]
    public void GetArrivalState_Walks_When_Inside_Walk_Buffer_But_Outside_Stop_Radius() {
        var state = SimpleInputMovement.GetArrivalState(distance: 0.5f, precision: 0.1f);

        Assert.Equal(ArrivalMovementState.Walk, state);
    }

    [Fact]
    public void GetArrivalState_Stops_When_Inside_Precision_Radius() {
        var state = SimpleInputMovement.GetArrivalState(distance: 0.09f, precision: 0.1f);

        Assert.Equal(ArrivalMovementState.Stop, state);
    }

    [Fact]
    public void GetArrivalState_Uses_Configured_Precision() {
        Assert.Equal(ArrivalMovementState.Stop, SimpleInputMovement.GetArrivalState(distance: 0.24f, precision: 0.25f));
        Assert.Equal(ArrivalMovementState.Walk, SimpleInputMovement.GetArrivalState(distance: 0.7f, precision: 0.25f));
        Assert.Equal(ArrivalMovementState.Run, SimpleInputMovement.GetArrivalState(distance: 0.76f, precision: 0.25f));
    }

    [Fact]
    public void GetArrivalState_Continuous_Runs_Inside_Walk_Buffer() {
        var state = SimpleInputMovement.GetArrivalState(
            distance: 0.5f,
            precision: 0.1f,
            MovementArrivalMode.Continuous);

        Assert.Equal(ArrivalMovementState.Run, state);
    }

    [Fact]
    public void GetArrivalState_Continuous_Stops_When_Inside_Precision_Radius() {
        var state = SimpleInputMovement.GetArrivalState(
            distance: 0.09f,
            precision: 0.1f,
            MovementArrivalMode.Continuous);

        Assert.Equal(ArrivalMovementState.Stop, state);
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
