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
}
