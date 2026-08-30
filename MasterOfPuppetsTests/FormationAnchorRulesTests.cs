using System.Collections.Generic;
using System.Numerics;

using MasterOfPuppets;
using MasterOfPuppets.Formations;

using Xunit;

namespace MasterOfPuppetsTests;

public class FormationAnchorRulesTests
{
    private const ulong IssuerCid = 1001;
    private const ulong AnchorCid = 2002;
    private const ulong GroupedCid = 3003;

    private static Formation Formation(params FormationPoint[] points) =>
        new() { Points = new List<FormationPoint>(points) };

    private static FormationPoint Point(params ulong[] cids) =>
        new() { Offset = Vector3.Zero, Cids = new List<ulong>(cids) };

    private static FormationPoint GroupPoint(string groupId) =>
        new() { Offset = Vector3.Zero, GroupIds = new List<string> { groupId } };

    private static Formation FormationWithAssignedPointOne() =>
        Formation(Point(IssuerCid), Point(AnchorCid));

    private static Formation FormationWithWildcardOrigin() =>
        Formation(Point(), Point(AnchorCid));

    // Point 1 assigned ------------------------------------------------------

    [Fact]
    public void IsPointOneAssigned_True_When_Direct_Cid_Assigned() {
        Assert.True(FormationAnchorRules.IsPointOneAssigned(FormationWithAssignedPointOne()));
    }

    [Fact]
    public void IsPointOneAssigned_True_When_Point_One_Has_Group() {
        var groups = new List<CidGroup> { new() { Name = "Leaders", Cids = new List<ulong> { GroupedCid } } };
        Assert.True(FormationAnchorRules.IsPointOneAssigned(Formation(GroupPoint("Leaders")), groups));
    }

    [Fact]
    public void IsPointOneAssigned_False_When_Point_One_Empty() {
        Assert.False(FormationAnchorRules.IsPointOneAssigned(FormationWithWildcardOrigin()));
    }

    [Fact]
    public void IsPointOneAssigned_False_When_No_Points() {
        Assert.False(FormationAnchorRules.IsPointOneAssigned(Formation()));
    }

    // Sender guard ----------------------------------------------------------

    [Fact]
    public void ShouldRejectIssuer_True_When_No_Role_And_PointOne_Assigned() {
        // Sender is not assigned anywhere and point 1 is assigned: no role and no origin to lead from.
        const ulong outsiderCid = 9999;
        Assert.True(FormationAnchorRules.ShouldRejectIssuer(FormationWithAssignedPointOne(), outsiderCid));
    }

    [Fact]
    public void ShouldRejectIssuer_False_When_Sender_Is_Member() {
        // Sender assigned to point 1 (or any point) always allowed.
        Assert.False(FormationAnchorRules.ShouldRejectIssuer(FormationWithAssignedPointOne(), IssuerCid));
    }

    [Fact]
    public void ShouldRejectIssuer_False_When_Unassigned_PointOne_And_Sender_Not_Member() {
        // Point 1 unassigned: any sender may act as the wildcard-origin leader (stays put).
        Assert.False(FormationAnchorRules.ShouldRejectIssuer(FormationWithWildcardOrigin(), IssuerCid));
    }

    [Fact]
    public void ShouldRejectIssuer_False_When_Unassigned_PointOne_And_Sender_On_Another_Point() {
        // Point 1 unassigned, sender assigned to point 2.
        var formation = Formation(Point(), Point(AnchorCid));
        Assert.False(FormationAnchorRules.ShouldRejectIssuer(formation, AnchorCid));
    }

    // Assigned point 1: no targetless fallback (legacy) --------------------

    [Fact]
    public void ShouldNot_FallBackToSelf_When_PointOneAssigned() {
        var formation = FormationWithAssignedPointOne();
        Assert.False(FormationAnchorRules.ShouldFallBackToSelfOnTargetlessAnchor(formation, FormationAnchorKind.Target));
        Assert.False(FormationAnchorRules.ShouldFallBackToSelfOnTargetlessAnchor(formation, FormationAnchorKind.FocusTarget));
    }

    [Fact]
    public void Should_FallBackToSelf_When_PointOneUnassigned_And_TargetRequested() {
        var formation = FormationWithWildcardOrigin();
        Assert.True(FormationAnchorRules.ShouldFallBackToSelfOnTargetlessAnchor(formation, FormationAnchorKind.Target));
        Assert.True(FormationAnchorRules.ShouldFallBackToSelfOnTargetlessAnchor(formation, FormationAnchorKind.FocusTarget));
    }

    [Fact]
    public void ShouldNot_FallBackToSelf_For_Self_Or_Named_Anchors() {
        var formation = FormationWithWildcardOrigin();
        Assert.False(FormationAnchorRules.ShouldFallBackToSelfOnTargetlessAnchor(formation, FormationAnchorKind.Self));
        Assert.False(FormationAnchorRules.ShouldFallBackToSelfOnTargetlessAnchor(formation, FormationAnchorKind.Named));
    }

    // Anchor CID selection --------------------------------------------------

    [Fact]
    public void SelectAnchorCid_AssignedPointOne_Self_Is_IssuerCid() {
        var formation = FormationWithAssignedPointOne();
        Assert.Equal(IssuerCid, FormationAnchorRules.SelectAnchorCid(formation, FormationAnchorKind.Self, resolvedContentId: 0, issuerCid: IssuerCid));
    }

    [Fact]
    public void SelectAnchorCid_AssignedPointOne_Target_Real_Is_Zero() {
        // With a target/focus-target anchor, an assigned point 1 participates and moves to the
        // target like any member. Broadcasting 0 avoids the "playerCid == anchorCid" skip so the
        // point-1 character is not treated as a stay-put leader.
        var formation = FormationWithAssignedPointOne();
        Assert.Equal(0ul, FormationAnchorRules.SelectAnchorCid(formation, FormationAnchorKind.Target, resolvedContentId: 0, issuerCid: IssuerCid));
    }

    [Fact]
    public void SelectAnchorCid_AssignedPointOne_FocusTarget_Real_Is_Zero() {
        var formation = FormationWithAssignedPointOne();
        Assert.Equal(0ul, FormationAnchorRules.SelectAnchorCid(formation, FormationAnchorKind.FocusTarget, resolvedContentId: 0, issuerCid: IssuerCid));
    }

    [Fact]
    public void SelectAnchorCid_AssignedPointOne_Named_ResolvedChar_Is_That_Char() {
        var formation = FormationWithAssignedPointOne();
        Assert.Equal(AnchorCid, FormationAnchorRules.SelectAnchorCid(formation, FormationAnchorKind.Named, resolvedContentId: AnchorCid, issuerCid: IssuerCid));
    }

    [Fact]
    public void SelectAnchorCid_UnassignedPointOne_Self_Is_Zero() {
        // Wildcard origin: the issuer (leader) is deliberately not assigned to any point,
        // so broadcasting their cid would leave receivers unable to resolve an anchor point
        // (GetAssignedPointIndex returns -1). 0 makes them use the origin slot (point 1).
        var formation = FormationWithWildcardOrigin();
        Assert.Equal(0ul, FormationAnchorRules.SelectAnchorCid(formation, FormationAnchorKind.Self, resolvedContentId: 0, issuerCid: IssuerCid));
    }

    [Fact]
    public void SelectAnchorCid_UnassignedPointOne_Target_Real_Is_Zero() {
        // Wildcard origin: the raw target object cannot be re-resolved by receivers,
        // so the broadcast anchor cid is 0, which they map to point 1 (the origin).
        var formation = FormationWithWildcardOrigin();
        Assert.Equal(0ul, FormationAnchorRules.SelectAnchorCid(formation, FormationAnchorKind.Target, resolvedContentId: 0, issuerCid: IssuerCid));
    }

    [Fact]
    public void SelectAnchorCid_UnassignedPointOne_Named_ResolvedChar_Is_Zero() {
        // Uniform origin semantics: with point 1 unassigned the anchor is always the origin
        // slot, regardless of anchor kind. The leader's identity/position is carried by the
        // separately-broadcast game object id / name / position, not the anchor cid.
        var formation = FormationWithWildcardOrigin();
        Assert.Equal(0ul, FormationAnchorRules.SelectAnchorCid(formation, FormationAnchorKind.Named, resolvedContentId: AnchorCid, issuerCid: IssuerCid));
    }

    // Point 1 raw-empty ("unassigned") helper used by the editor color ------------------

    [Fact]
    public void IsPointOneUnassigned_True_When_Raw_Empty() {
        Assert.True(FormationAnchorRules.IsPointOneUnassigned(FormationWithWildcardOrigin()));
    }

    [Fact]
    public void IsPointOneUnassigned_False_When_Direct_Cid() {
        Assert.False(FormationAnchorRules.IsPointOneUnassigned(FormationWithAssignedPointOne()));
    }

    [Fact]
    public void IsPointOneUnassigned_False_When_Group_Reference() {
        Assert.False(FormationAnchorRules.IsPointOneUnassigned(Formation(GroupPoint("Leaders"), Point(AnchorCid))));
    }

    [Fact]
    public void IsPointOneUnassigned_False_When_No_Points() {
        Assert.False(FormationAnchorRules.IsPointOneUnassigned(Formation()));
    }

    // Mirror of the reported "Surround In" shape: point 1 empty, members 2-8 assigned -----

    [Fact]
    public void SurroundIn_Shape_Uses_Origin_Anchor_And_Zero_Cid() {
        var formation = Formation(
            Point(),                       // point 1: wildcard origin (empty)
            Point(AnchorCid),              // member 1
            Point(GroupedCid));
        Assert.True(FormationAnchorRules.IsPointOneUnassigned(formation));
        Assert.False(FormationAnchorRules.IsPointOneAssigned(formation));
        Assert.Equal(0ul, FormationAnchorRules.SelectAnchorCid(formation, FormationAnchorKind.Self, resolvedContentId: 0, issuerCid: IssuerCid));
        Assert.Equal(FormationPointMovement.AnchorPointIndex, FormationAnchorRules.IsPointOneUnassigned(formation) ? 0 : -1);
    }

    // Defect B: anchorCid == 0 must map to the anchor (origin) point index ----

    [Fact]
    public void AnchorPointIndex_Is_Zero() {
        // The wildcard-origin sentinel (anchorCid == 0) resolves to this slot on the
        // receiving client; if it were resolved via GetAssignedPointIndex it would be -1
        // and silently bail (the regression being fixed).
        Assert.Equal(0, FormationPointMovement.AnchorPointIndex);
    }

    // Assigned point-1 member: /mopformationmove destination resolution ---------------------

    [Fact]
    public void ResolveMoveDestination_PointOneMember_OriginAnchor_Moves_To_Own_Point() {
        // Origin anchor (anchorCid == 0, e.g. target/ftarget) has no character pivot: an
        // assigned point-1 member (playerPointIndex == anchorPointIndex == 0) participates and
        // returns to its own point, bypassing the empty march sequence. BuildAnchoredWorldMove
        // with destination==anchor==0 then resolves to the origin anchor position.
        var sequence = FormationPath.BuildDestinationSequence(
            FormationWithAssignedPointOne(), 0, 0, /*step*/ 1, /*reverse*/ false);
        Assert.Empty(sequence);
        Assert.Equal(0, FormationAnchorRules.ResolveMoveDestinationPointIndex(
            anchorCid: 0, playerPointIndex: 0, anchorPointIndex: 0, sequence, sequenceIndex: 3));
    }

    [Fact]
    public void ResolveMoveDestination_PivotMember_CharacterAnchor_Uses_Sequence() {
        // A real character pivot (anchorCid != 0) must NOT march to itself: the empty march
        // sequence is honored and yields no destination for the pivot member.
        var sequence = FormationPath.BuildDestinationSequence(
            FormationWithAssignedPointOne(), 0, 0, /*step*/ 1, /*reverse*/ false);
        Assert.Equal(-1, FormationAnchorRules.ResolveMoveDestinationPointIndex(
            anchorCid: IssuerCid, playerPointIndex: 0, anchorPointIndex: 0, sequence, sequenceIndex: 0));
    }

    [Fact]
    public void ResolveMoveDestination_NonPivotMember_OriginAnchor_Uses_Sequence() {
        // Member on point 2 (index 1) marching with an origin anchor still steps through the
        // march sequence relative to point-1 origin.
        var sequence = FormationPath.BuildDestinationSequence(
            FormationWithAssignedPointOne(), /*anchor*/ 0, /*start*/ 1, /*step*/ 1, /*reverse*/ false);
        Assert.Equal(1, FormationAnchorRules.ResolveMoveDestinationPointIndex(
            anchorCid: 0, playerPointIndex: 1, anchorPointIndex: 0, sequence, sequenceIndex: 0));
    }

    [Fact]
    public void ResolveMoveDestination_EmptySequence_CharacterAnchor_Is_NoMove() {
        Assert.Equal(-1, FormationAnchorRules.ResolveMoveDestinationPointIndex(
            anchorCid: IssuerCid, playerPointIndex: 0, anchorPointIndex: 0, marchSequence: [], sequenceIndex: 0));
    }
}
