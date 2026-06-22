using System;
using System.Collections.Generic;
using System.Numerics;

using MasterOfPuppets;
using MasterOfPuppets.Formations;
using MasterOfPuppets.Movement;

using Xunit;

public class FormationExecutionTests
{
    [Fact]
    public void GetAssignedPoint_Finds_Direct_Cid_Assignments()
    {
        const ulong issuerCid = 1001;
        var formation = new Formation
        {
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
    public void GetAssignedPoint_Finds_Group_Cid_Assignments()
    {
        const ulong groupedCid = 2001;
        var formation = new Formation
        {
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
    public void Target_Anchor_Does_Not_Require_Target_Cid_To_Be_Assigned()
    {
        const ulong issuerCid = 3001;
        const ulong memberCid = 3002;
        const ulong unassignedTargetCid = 9999;
        var formation = new Formation
        {
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
    public void Target_Anchor_Places_Members_Relative_To_Issuer_Point_At_Target_Transform()
    {
        var issuerPoint = new FormationPoint
        {
            Offset = new Vector3(1f, 0f, 1f),
            Angle = 45f,
        };
        var memberPoint = new FormationPoint
        {
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
        AssertClose(targetRotation + 45f * Angle.DegToRad, issuerWorldRotation);

        AssertClose(12f, memberWorldPosition.X);
        AssertClose(20f, memberWorldPosition.Z);
        AssertClose(MathF.PI / 4f, memberWorldRotation);
    }

    [Fact]
    public void FormationPath_BuildDestinationSequence_SkipsAnchorAndWalksFromRecipientPoint()
    {
        var formation = new Formation
        {
            Points = [
                new FormationPoint { Offset = Vector3.Zero },
                new FormationPoint { Offset = new Vector3(1f, 0f, 0f) },
                new FormationPoint { Offset = new Vector3(2f, 0f, 0f) },
                new FormationPoint { Offset = new Vector3(3f, 0f, 0f) },
            ],
        };

        var forward = FormationPath.BuildDestinationSequence(formation, anchorPointIndex: 0, startPointIndex: 2, step: 1, reverse: false);
        var backward = FormationPath.BuildDestinationSequence(formation, anchorPointIndex: 0, startPointIndex: 2, step: 1, reverse: true);
        var strideTwo = FormationPath.BuildDestinationSequence(formation, anchorPointIndex: 0, startPointIndex: 1, step: 2, reverse: false);

        Assert.Equal([2, 3, 1], forward);
        Assert.Equal([2, 1, 3], backward);
        Assert.Equal([1, 3, 2], strideTwo);
    }

    [Fact]
    public void FormationPath_BuildWorldMove_UsesAnchorRelativeFormationMath()
    {
        var formation = new Formation
        {
            Points = [
                new FormationPoint { Offset = new Vector3(0f, 0f, 0f), Angle = 0f },
                new FormationPoint { Offset = new Vector3(0f, 0f, 2f), Angle = 180f },
                new FormationPoint { Offset = new Vector3(2f, 0f, 0f), Angle = 90f },
            ],
        };

        var move0 = FormationPath.BuildWorldMove(
            formation,
            anchorPointIndex: 0,
            startPointIndex: 1,
            anchorWorldPosition: new Vector3(10f, 0f, 20f),
            anchorWorldRotation: MathF.PI / 2f,
            step: 1,
            reverse: false,
            sequenceIndex: 0);
        var move1 = FormationPath.BuildWorldMove(
            formation,
            anchorPointIndex: 0,
            startPointIndex: 1,
            anchorWorldPosition: new Vector3(10f, 0f, 20f),
            anchorWorldRotation: MathF.PI / 2f,
            step: 1,
            reverse: false,
            sequenceIndex: 1);

        Assert.NotNull(move0);
        Assert.NotNull(move1);
        AssertClose(12f, move0.Value.Position.X);
        AssertClose(20f, move0.Value.Position.Z);
        AssertClose(MathF.PI / 2f + MathF.PI, move0.Value.Rotation);
        AssertClose(10f, move1.Value.Position.X);
        AssertClose(18f, move1.Value.Position.Z);
        AssertClose(MathF.PI, move1.Value.Rotation);
    }

    [Fact]
    public void FormationPath_BuildWorldMove_WrapsSequenceIndex()
    {
        var formation = new Formation
        {
            Points = [
                new FormationPoint { Offset = Vector3.Zero },
                new FormationPoint { Offset = new Vector3(1f, 0f, 0f) },
                new FormationPoint { Offset = new Vector3(2f, 0f, 0f) },
            ],
        };

        var move = FormationPath.BuildWorldMove(
            formation,
            anchorPointIndex: 0,
            startPointIndex: 1,
            anchorWorldPosition: Vector3.Zero,
            anchorWorldRotation: 0f,
            step: 1,
            reverse: false,
            sequenceIndex: 2);

        Assert.NotNull(move);
        AssertClose(1f, move.Value.Position.X);
    }

    [Fact]
    public void FormationPointMovement_UsesPointOneAsAnchor()
    {
        var formation = new Formation
        {
            Points = [
                new FormationPoint { Offset = new Vector3(5f, 0f, 5f), Angle = 45f },
                new FormationPoint { Offset = new Vector3(5f, 0f, 7f), Angle = -45f },
            ],
        };

        var move = FormationPointMovement.BuildAnchoredWorldMove(
            formation,
            destinationPointIndex: 1,
            anchorPointIndex: FormationPointMovement.AnchorPointIndex,
            anchorWorldPosition: new Vector3(10f, 0f, 20f),
            anchorWorldRotation: MathF.PI / 2f);

        Assert.NotNull(move);
        AssertClose(12f, move.Value.Position.X);
        AssertClose(20f, move.Value.Position.Z);
        AssertClose(MathF.PI / 4f, move.Value.Rotation);
    }

    [Fact]
    public void FormationPointMovement_MatchesExistingFormationMathForPointOneAnchor()
    {
        var formation = new Formation
        {
            Points = [
                new FormationPoint { Offset = new Vector3(1f, 0f, 1f), Angle = 30f },
                new FormationPoint { Offset = new Vector3(3f, 0f, -2f), Angle = -120f },
            ],
        };
        var anchorPosition = new Vector3(-5f, 0f, 8f);
        var anchorRotation = -MathF.PI / 3f;

        var direct = FormationMath.GetMopRelativeWorld(
            formation.Points[0],
            formation.Points[1],
            anchorPosition,
            anchorRotation);
        var helper = FormationPointMovement.BuildAnchoredWorldMove(
            formation,
            destinationPointIndex: 1,
            anchorPointIndex: FormationPointMovement.AnchorPointIndex,
            anchorPosition,
            anchorRotation);

        Assert.NotNull(helper);
        AssertClose(direct.Position.X, helper.Value.Position.X);
        AssertClose(direct.Position.Z, helper.Value.Position.Z);
        AssertClose(direct.Rotation, helper.Value.Rotation);
    }

    [Fact]
    public void FormationPointMovement_Gets_Single_PointOne_Anchor_Cid()
    {
        var formation = new Formation
        {
            Points = [
                new FormationPoint { Cids = [100] },
                new FormationPoint { Cids = [101] },
            ],
        };

        var resolved = FormationPointMovement.TryGetPointOneAnchorCid(formation, null, out var cid, out var failure);

        Assert.True(resolved, failure);
        Assert.Equal(100UL, cid);
    }

    [Fact]
    public void FormationPointMovement_Rejects_Ambiguous_PointOne_Anchor_Cid()
    {
        var formation = new Formation
        {
            Points = [
                new FormationPoint { Cids = [100, 101] },
                new FormationPoint { Cids = [102] },
            ],
        };

        var resolved = FormationPointMovement.TryGetPointOneAnchorCid(formation, null, out _, out var failure);

        Assert.False(resolved);
        Assert.Contains("exactly one", failure);
    }

    [Fact]
    public void FormationPointMovement_Builds_Assigned_Move_From_PointOne_Anchor()
    {
        var formation = new Formation
        {
            Points = [
                new FormationPoint { Offset = new Vector3(5f, 0f, 5f), Angle = 45f, Cids = [100] },
                new FormationPoint { Offset = new Vector3(5f, 0f, 7f), Angle = -45f, Cids = [101] },
            ],
        };

        var move = FormationPointMovement.BuildAssignedPointOneAnchoredWorldMove(
            formation,
            destinationContentId: 101,
            groups: null,
            anchorWorldPosition: new Vector3(10f, 0f, 20f),
            anchorWorldRotation: MathF.PI / 2f,
            out var destinationPointIndex);

        Assert.NotNull(move);
        Assert.Equal(1, destinationPointIndex);
        AssertClose(12f, move.Value.Position.X);
        AssertClose(20f, move.Value.Position.Z);
        AssertClose(MathF.PI / 4f, move.Value.Rotation);
    }

    [Fact]
    public void FormationPointMovement_UsesFlexibleAnchorPoint() {
        var formation = new Formation {
            Points = [
                new FormationPoint { Offset = new Vector3(0f, 0f, 0f), Angle = 0f },
                new FormationPoint { Offset = new Vector3(0f, 0f, 2f), Angle = 180f },
                new FormationPoint { Offset = new Vector3(2f, 0f, 0f), Angle = 90f },
            ],
        };

        var move = FormationPointMovement.BuildAnchoredWorldMove(
            formation,
            destinationPointIndex: 1,
            anchorPointIndex: 2,
            anchorWorldPosition: new Vector3(10f, 0f, 20f),
            anchorWorldRotation: 0f);

        Assert.NotNull(move);
        AssertClose(8f, move.Value.Position.X);
        AssertClose(22f, move.Value.Position.Z);
        AssertClose(MathF.PI, move.Value.Rotation);
    }

    [Fact]
    public void FormationPointMovement_FlexibleAnchor_KeepsAnchorAtItsOwnPosition() {
        var formation = new Formation {
            Points = [
                new FormationPoint { Offset = new Vector3(0f, 0f, 0f), Angle = 0f },
                new FormationPoint { Offset = new Vector3(0f, 0f, 2f), Angle = 180f },
                new FormationPoint { Offset = new Vector3(2f, 0f, 0f), Angle = 90f },
            ],
        };

        var anchorPosition = new Vector3(10f, 0f, 20f);
        var anchorRotation = MathF.PI / 4f;

        var move = FormationPointMovement.BuildAnchoredWorldMove(
            formation,
            destinationPointIndex: 2,
            anchorPointIndex: 2,
            anchorWorldPosition: anchorPosition,
            anchorWorldRotation: anchorRotation);

        Assert.NotNull(move);
        AssertClose(anchorPosition.X, move.Value.Position.X);
        AssertClose(anchorPosition.Z, move.Value.Position.Z);
        AssertClose(anchorRotation + MathF.PI / 2f, move.Value.Rotation);
    }

    [Fact]
    public void FormationPointMovement_FlexibleAnchor_PreservesPositionAndUsesMemberFacing() {
        var formation = new Formation {
            Points = [
                new FormationPoint { Offset = new Vector3(0f, 0f, 0f), Angle = 0f },
                new FormationPoint { Offset = new Vector3(0f, 0f, 2f), Angle = 180f },
                new FormationPoint { Offset = new Vector3(2f, 0f, 0f), Angle = 90f },
            ],
        };
        var anchorPosition = new Vector3(10f, 0f, 20f);
        var anchorRotation = 0.25f;

        var centerMove = FormationPointMovement.BuildAnchoredWorldMove(
            formation,
            destinationPointIndex: 0,
            anchorPointIndex: 2,
            anchorWorldPosition: anchorPosition,
            anchorWorldRotation: anchorRotation);
        var rearMove = FormationPointMovement.BuildAnchoredWorldMove(
            formation,
            destinationPointIndex: 1,
            anchorPointIndex: 2,
            anchorWorldPosition: anchorPosition,
            anchorWorldRotation: anchorRotation);

        var expectedCenterPosition = ApplyLeaderRotation(
            formation.Points[0].Offset - formation.Points[2].Offset,
            anchorRotation,
            anchorPosition);
        var expectedRearPosition = ApplyLeaderRotation(
            formation.Points[1].Offset - formation.Points[2].Offset,
            anchorRotation,
            anchorPosition);

        Assert.NotNull(centerMove);
        AssertClose(expectedCenterPosition.X, centerMove.Value.Position.X);
        AssertClose(expectedCenterPosition.Z, centerMove.Value.Position.Z);
        AssertClose(anchorRotation, centerMove.Value.Rotation);

        Assert.NotNull(rearMove);
        AssertClose(expectedRearPosition.X, rearMove.Value.Position.X);
        AssertClose(expectedRearPosition.Z, rearMove.Value.Position.Z);
        AssertClose(anchorRotation + MathF.PI, rearMove.Value.Rotation);
    }

    [Fact]
    public void FormationAnchorArgumentParser_ParsesSender() {
        var result = FormationAnchorArgumentParser.ParseAnchorAndArrival(
            ["sender"],
            FormationAnchorReference.Self);

        Assert.Equal(FormationAnchorKind.Sender, result.Anchor.Kind);
    }

    [Fact]
    public void FormationAnchorArgumentParser_DefaultsToSender() {
        var result = FormationAnchorArgumentParser.ParseAnchorAndArrival(
            [],
            FormationAnchorReference.Sender);

        Assert.Equal(FormationAnchorKind.Sender, result.Anchor.Kind);
    }

    [Fact]
    public void FormationAnchorArgumentParser_FormatsSenderForMacro() {
        Assert.Equal("sender", FormationAnchorArgumentParser.FormatForMacro(FormationAnchorReference.Sender));
    }

    [Fact]
    public void FormationCharacterName_Normalizes_CrossWorld_Separator() {
        Assert.Equal(
            "Alpha One@ServerA",
            FormationCharacterName.NormalizeWorldSeparator("Alpha One\uE0B2ServerA"));
    }

    [Fact]
    public void FormationCharacterName_Normalizes_CrossWorld_Prefix() {
        Assert.Equal(
            "Alpha One@ServerA",
            FormationCharacterName.NormalizeWorldSeparator("Alpha One\uE0B1ServerA"));
    }

    [Fact]
    public void FormationCharacterName_Collapses_Repeated_World_Separators() {
        Assert.Equal(
            "Alpha One@ServerA",
            FormationCharacterName.NormalizeWorldSeparator("Alpha One \uE0B1\uE0B2 ServerA"));
    }

    [Theory]
    [InlineData("Bravo Two", "Bravo Two")]
    [InlineData("Bravo Two@ServerB", "Bravo Two@ServerB")]
    public void FormationCharacterName_Keeps_Normal_Names(string input, string expected) {
        Assert.Equal(expected, FormationCharacterName.NormalizeWorldSeparator(input));
    }

    [Fact]
    public void FormationCharacterName_Matches_CrossWorld_Sender_To_Configured_Name() {
        var score = FormationCharacterName.MatchScore(
            "Alpha One@ServerA",
            "Alpha One\uE0B2ServerA");

        Assert.Equal(int.MaxValue, score);
    }

    [Fact]
    public void FormationCharacterName_Matches_SameWorld_Sender_By_Base_Name() {
        var score = FormationCharacterName.MatchScore(
            "Bravo Two@ServerB",
            "Bravo Two");

        Assert.Equal(int.MaxValue - 1, score);
    }

    [Fact]
    public void FormationCharacterName_Formats_PlayerPayload_Name_And_World() {
        Assert.Equal(
            "Alpha One@ServerA",
            FormationCharacterName.FormatPlayerNameWorld("Alpha One", "ServerA", "ignored"));
    }

    [Fact]
    public void FormationCharacterName_Formats_PlayerPayload_Fallback_When_World_Missing() {
        Assert.Equal(
            "Alpha One@ServerA",
            FormationCharacterName.FormatPlayerNameWorld("Alpha One", null, "Alpha One\uE0B2ServerA"));
    }

    [Fact]
    public void FormationLocalMovementExecutor_Resolves_Sender_Anchor_Point_From_Cid() {
        var formation = new Formation {
            Points = [
                new FormationPoint { Cids = [100] },
                new FormationPoint { Cids = [101] },
            ],
        };

        var anchorPointIndex = FormationLocalMovementExecutor.ResolveAnchorPointIndex(
            formation,
            groups: null,
            FormationAnchorReference.Sender with { Name = "Bravo Two@ServerB" },
            anchorCid: 101,
            localCid: 100);

        Assert.Equal(1, anchorPointIndex);
    }

    [Fact]
    public void FormationLocalMovementExecutor_Falls_Back_To_Point_One_When_Sender_Cid_Is_Unassigned() {
        var formation = new Formation {
            Points = [
                new FormationPoint { Cids = [100] },
                new FormationPoint { Cids = [101] },
            ],
        };

        var anchorPointIndex = FormationLocalMovementExecutor.ResolveAnchorPointIndex(
            formation,
            groups: null,
            FormationAnchorReference.Sender,
            anchorCid: 999,
            localCid: 100);

        Assert.Equal(FormationPointMovement.AnchorPointIndex, anchorPointIndex);
    }

    private static void AssertClose(float expected, float actual, float tolerance = 0.0001f) =>
        Assert.InRange(MathF.Abs(expected - actual), 0f, tolerance);

    private static Vector3 ApplyLeaderRotation(Vector3 offset, float leaderRotRad, Vector3 leaderPos) {
        float cos = MathF.Cos(leaderRotRad);
        float sin = MathF.Sin(leaderRotRad);
        return new Vector3(
            offset.X * cos + offset.Z * sin,
            offset.Y,
            -offset.X * sin + offset.Z * cos) + leaderPos;
    }
}
