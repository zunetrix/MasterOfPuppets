using System;
using System.Collections.Generic;
using System.Numerics;

using MasterOfPuppets.Formations;
using MasterOfPuppets.Movement;

using Xunit;

public class FormationExecutionTests {
    [Fact]
    public void GetAssignedPoint_Finds_Direct_Cid_Assignments() {
        const ulong issuerCid = 1001;
        var formation = new Formation {
            Points = [
                new FormationPoint { Offset = Vector3.Zero, Cids = [issuerCid] },
                new FormationPoint { Offset = new Vector3(0f, 0f, 2f), Cids = [1002] },
            ],
        };

        var point = FormationExecution.GetAssignedPoint(formation, issuerCid);

        Assert.NotNull(point);
        Assert.Equal(Vector3.Zero, point.Offset);
    }

    [Fact]
    public void GetAssignedPoint_Finds_Group_Cid_Assignments() {
        const ulong groupedCid = 2001;
        var formation = new Formation {
            Points = [
                new FormationPoint { Offset = new Vector3(1f, 0f, 0f), GroupIds = ["Group A"] },
            ],
        };
        var groups = new List<CidGroup> {
            new() { Name = "Group A", Cids = [groupedCid] },
        };

        var point = FormationExecution.GetAssignedPoint(formation, groupedCid, groups);

        Assert.NotNull(point);
        Assert.Equal(new Vector3(1f, 0f, 0f), point.Offset);
    }

    [Fact]
    public void Target_Anchor_Does_Not_Require_Target_Cid_To_Be_Assigned() {
        const ulong issuerCid = 3001;
        const ulong memberCid = 3002;
        const ulong unassignedTargetCid = 9999;
        var formation = new Formation {
            Points = [
                new FormationPoint { Offset = Vector3.Zero, Angle = 0f, Cids = [issuerCid] },
                new FormationPoint { Offset = new Vector3(0f, 0f, 2f), Angle = 180f, Cids = [memberCid] },
            ],
        };

        var issuerPoint = FormationExecution.GetAssignedPoint(formation, issuerCid);
        var memberPoint = FormationExecution.GetAssignedPoint(formation, memberCid);
        var targetPoint = FormationExecution.GetAssignedPoint(formation, unassignedTargetCid);

        Assert.NotNull(issuerPoint);
        Assert.NotNull(memberPoint);
        Assert.Null(targetPoint);

        var targetPosition = new Vector3(100f, 0f, 200f);
        var targetRotation = 0f;
        var (memberWorldPosition, memberWorldRotation) = FormationMath.GetMopRelativeWorld(
            issuerPoint,
            memberPoint,
            targetPosition,
            targetRotation);

        AssertClose(100f, memberWorldPosition.X);
        AssertClose(202f, memberWorldPosition.Z);
        AssertClose(MathF.PI, memberWorldRotation);
    }

    [Fact]
    public void Target_Anchor_Places_Members_Relative_To_Issuer_Point_At_Target_Transform() {
        var issuerPoint = new FormationPoint {
            Offset = new Vector3(1f, 0f, 1f),
            Angle = 45f,
        };
        var memberPoint = new FormationPoint {
            Offset = new Vector3(1f, 0f, 3f),
            Angle = -45f,
        };
        var targetPosition = new Vector3(10f, 0f, 20f);
        var targetRotation = MathF.PI / 2f;

        var (issuerWorldPosition, issuerWorldRotation) = FormationMath.GetMopRelativeWorld(
            issuerPoint,
            issuerPoint,
            targetPosition,
            targetRotation);
        var (memberWorldPosition, memberWorldRotation) = FormationMath.GetMopRelativeWorld(
            issuerPoint,
            memberPoint,
            targetPosition,
            targetRotation);

        AssertClose(targetPosition.X, issuerWorldPosition.X);
        AssertClose(targetPosition.Z, issuerWorldPosition.Z);
        AssertClose(targetRotation, issuerWorldRotation);

        AssertClose(12f, memberWorldPosition.X);
        AssertClose(20f, memberWorldPosition.Z);
        AssertClose(0f, memberWorldRotation);
    }

    private static void AssertClose(float expected, float actual, float tolerance = 0.0001f) =>
        Assert.InRange(MathF.Abs(expected - actual), 0f, tolerance);
}
