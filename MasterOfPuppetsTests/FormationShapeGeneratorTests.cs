using System;
using System.Numerics;

using MasterOfPuppets.Formations;

using Xunit;

public class FormationShapeGeneratorTests {
    [Fact]
    public void Circle_Anchors_Northernmost_Point_At_Origin() {
        var points = FormationShapeGenerator.Generate(new FormationShapeSpec {
            Type = FormationShapeType.Circle,
            Count = 4,
            Radius = 2f,
            FaceMode = FormationShapeFaceMode.Outward,
        });

        Assert.Equal(4, points.Count);
        AssertClose(Vector3.Zero, points[0].Offset);
        AssertClose(new Vector3(2f, 0f, 2f), points[1].Offset);
        AssertClose(new Vector3(0f, 0f, 4f), points[2].Offset);
        AssertClose(new Vector3(-2f, 0f, 2f), points[3].Offset);
    }

    [Fact]
    public void Line_Uses_Existing_Formation_Spacing() {
        var points = FormationShapeGenerator.Generate(new FormationShapeSpec {
            Type = FormationShapeType.Line,
            Count = 3,
            Spacing = 2f,
            FaceMode = FormationShapeFaceMode.North,
        });

        Assert.Equal(3, points.Count);
        AssertClose(new Vector3(-2f, 0f, 0f), points[0].Offset);
        AssertClose(Vector3.Zero, points[1].Offset);
        AssertClose(new Vector3(2f, 0f, 0f), points[2].Offset);
        Assert.All(points, p => AssertClose(0f, p.Angle));
    }

    [Fact]
    public void Rectangle_Returns_No_Points_When_Count_Is_Too_Small() {
        var points = FormationShapeGenerator.Generate(new FormationShapeSpec {
            Type = FormationShapeType.Rectangle,
            Count = 3,
            Width = 4f,
            Depth = 2f,
        });

        Assert.Empty(points);
    }

    [Fact]
    public void RingWithCenter_Is_Shifted_With_The_Generated_Ring() {
        var points = FormationShapeGenerator.Generate(new FormationShapeSpec {
            Type = FormationShapeType.RingWithCenter,
            Count = 5,
            Radius = 2f,
            IntParameter = 1,
        });

        Assert.Equal(5, points.Count);
        AssertClose(new Vector3(0f, 0f, 2f), points[0].Offset);
        AssertClose(Vector3.Zero, points[1].Offset);
    }

    [Fact]
    public void AnchorAtCenter_Circle_Puts_First_Point_At_Origin_And_Ring_Around_It() {
        var points = FormationShapeGenerator.Generate(new FormationShapeSpec {
            Type = FormationShapeType.Circle,
            Count = 5,
            Radius = 2f,
            AnchorMode = FormationShapeAnchorMode.AnchorAtCenter,
            FaceMode = FormationShapeFaceMode.Outward,
        });

        Assert.Equal(5, points.Count);
        AssertClose(Vector3.Zero, points[0].Offset);
        AssertClose(new Vector3(0f, 0f, -2f), points[1].Offset);
        AssertClose(new Vector3(2f, 0f, 0f), points[2].Offset);
        AssertClose(new Vector3(0f, 0f, 2f), points[3].Offset);
        AssertClose(new Vector3(-2f, 0f, 0f), points[4].Offset);
    }

    [Fact]
    public void AnchorAtCenter_Assigns_First_Cid_To_Center() {
        var points = FormationShapeGenerator.Generate(new FormationShapeSpec {
            Type = FormationShapeType.Circle,
            Count = 3,
            Radius = 2f,
            AnchorMode = FormationShapeAnchorMode.AnchorAtCenter,
            AssignedCids = [1001, 1002, 1003],
        });

        Assert.Equal([1001UL], points[0].Cids);
        Assert.Equal([1002UL], points[1].Cids);
        Assert.Equal([1003UL], points[2].Cids);
        Assert.All(points, point => Assert.Empty(point.GroupIds));
    }

    [Fact]
    public void AnchorAtCenter_CountOne_GeneratesOnlyCenter() {
        var points = FormationShapeGenerator.Generate(new FormationShapeSpec {
            Type = FormationShapeType.Circle,
            Count = 1,
            Radius = 2f,
            AnchorMode = FormationShapeAnchorMode.AnchorAtCenter,
        });

        Assert.Single(points);
        AssertClose(Vector3.Zero, points[0].Offset);
    }

    [Fact]
    public void Default_Facing_Is_Inward() {
        var points = FormationShapeGenerator.Generate(new FormationShapeSpec {
            Type = FormationShapeType.Circle,
            Count = 5,
            Radius = 2f,
            AnchorMode = FormationShapeAnchorMode.AnchorAtCenter,
        });

        AssertAngleClose(0f, points[1].Angle);
        AssertAngleClose(-90f, points[2].Angle);
        AssertAngleClose(180f, points[3].Angle);
        AssertAngleClose(90f, points[4].Angle);
    }

    [Fact]
    public void Outward_Facing_Uses_Plot_Directions() {
        var points = FormationShapeGenerator.Generate(new FormationShapeSpec {
            Type = FormationShapeType.Circle,
            Count = 5,
            Radius = 2f,
            AnchorMode = FormationShapeAnchorMode.AnchorAtCenter,
            FaceMode = FormationShapeFaceMode.Outward,
        });

        AssertAngleClose(180f, points[1].Angle);
        AssertAngleClose(90f, points[2].Angle);
        AssertAngleClose(0f, points[3].Angle);
        AssertAngleClose(-90f, points[4].Angle);
    }

    [Fact]
    public void Tangent_Facing_Uses_Plot_Directions() {
        var points = FormationShapeGenerator.Generate(new FormationShapeSpec {
            Type = FormationShapeType.Line,
            Count = 2,
            Spacing = 2f,
            FaceMode = FormationShapeFaceMode.Tangent,
        });

        AssertAngleClose(90f, points[0].Angle);
        AssertAngleClose(90f, points[1].Angle);
    }

    [Fact]
    public void AngleOffset_Rotates_Position_And_Facing() {
        var points = FormationShapeGenerator.Generate(new FormationShapeSpec {
            Type = FormationShapeType.Line,
            Count = 2,
            Spacing = 2f,
            AngleOffsetDegrees = 90f,
            FaceMode = FormationShapeFaceMode.Tangent,
        });

        Assert.Equal(2, points.Count);
        AssertClose(new Vector3(0f, 0f, 2f), points[0].Offset);
        AssertClose(new Vector3(0f, 0f, 0f), points[1].Offset);
        AssertAngleClose(180f, points[0].Angle);
    }

    [Fact]
    public void AssignedCids_Are_Applied_In_Order_Without_Group_Assignment() {
        var points = FormationShapeGenerator.Generate(new FormationShapeSpec {
            Type = FormationShapeType.Line,
            Count = 3,
            AssignedCids = [3003, 3001, 3002],
        });

        Assert.Equal([3003UL], points[0].Cids);
        Assert.Equal([3001UL], points[1].Cids);
        Assert.Equal([3002UL], points[2].Cids);
        Assert.All(points, point => Assert.Empty(point.GroupIds));
    }

    [Fact]
    public void AssignedCids_Leaves_Extra_Points_Unassigned() {
        var points = FormationShapeGenerator.Generate(new FormationShapeSpec {
            Type = FormationShapeType.Line,
            Count = 3,
            AssignedCids = [3001],
        });

        Assert.Equal([3001UL], points[0].Cids);
        Assert.Empty(points[1].Cids);
        Assert.Empty(points[2].Cids);
        Assert.All(points, point => Assert.Empty(point.GroupIds));
    }

    [Fact]
    public void AssignedCids_Ignores_Extra_Cids() {
        var points = FormationShapeGenerator.Generate(new FormationShapeSpec {
            Type = FormationShapeType.Line,
            Count = 2,
            AssignedCids = [3001, 3002, 3003],
        });

        Assert.Equal(2, points.Count);
        Assert.Equal([3001UL], points[0].Cids);
        Assert.Equal([3002UL], points[1].Cids);
    }

    private static void AssertClose(Vector3 expected, Vector3 actual, float tolerance = 0.0001f) {
        AssertClose(expected.X, actual.X, tolerance);
        AssertClose(expected.Y, actual.Y, tolerance);
        AssertClose(expected.Z, actual.Z, tolerance);
    }

    private static void AssertClose(float expected, float actual, float tolerance = 0.0001f) =>
        Assert.InRange(MathF.Abs(expected - actual), 0f, tolerance);

    private static void AssertAngleClose(float expected, float actual, float tolerance = 0.0001f) =>
        AssertClose(0f, FormationMath.NormalizeDegrees(actual - expected), tolerance);
}
