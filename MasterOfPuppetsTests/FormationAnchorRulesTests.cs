using MasterOfPuppets.Formations;

using Xunit;

public class FormationAnchorRulesTests {
    [Fact]
    public void PointOneWithoutAssignmentsIsTreatedAsUnassigned() {
        var formation = new Formation {
            Points = [
                new FormationPoint(),
            ],
        };

        Assert.True(FormationAnchorRules.IsPointOneUnassigned(formation));
        Assert.False(FormationAnchorRules.IsPointOneAssigned(formation));
    }

    [Fact]
    public void PointOneWithDirectCidAssignmentIsTreatedAsAssigned() {
        var formation = new Formation {
            Points = [
                new FormationPoint { Cids = [123UL] },
            ],
        };

        Assert.False(FormationAnchorRules.IsPointOneUnassigned(formation));
        Assert.True(FormationAnchorRules.IsPointOneAssigned(formation));
    }

    [Fact]
    public void TargetFallbackIsAllowedRegardlessOfPointOneAssignment() {
        var unassignedPointOne = new Formation {
            Points = [
                new FormationPoint(),
            ],
        };
        var assignedPointOne = new Formation {
            Points = [
                new FormationPoint { Cids = [123UL] },
            ],
        };

        Assert.True(FormationAnchorRules.ShouldUseLeaderFallbackOnTargetlessAnchor(unassignedPointOne, FormationAnchorKind.Target));
        Assert.True(FormationAnchorRules.ShouldUseLeaderFallbackOnTargetlessAnchor(assignedPointOne, FormationAnchorKind.Target));
        Assert.True(FormationAnchorRules.ShouldUseLeaderFallbackOnTargetlessAnchor(assignedPointOne, FormationAnchorKind.FocusTarget));
        Assert.False(FormationAnchorRules.ShouldUseLeaderFallbackOnTargetlessAnchor(assignedPointOne, FormationAnchorKind.Self));
    }

    [Fact]
    public void IssuerIsRejectedOnlyWhenTheyHaveNoRoleAndPointOneIsAssigned() {
        var formation = new Formation {
            Points = [
                new FormationPoint { Cids = [111UL] },
                new FormationPoint { Cids = [222UL] },
            ],
        };

        Assert.True(FormationAnchorRules.ShouldRejectIssuer(formation, 333UL));
        Assert.False(FormationAnchorRules.ShouldRejectIssuer(formation, 111UL));
    }

    [Fact]
    public void IssuerIsAcceptedWhenPointOneIsUnassigned() {
        var formation = new Formation {
            Points = [
                new FormationPoint(),
                new FormationPoint { Cids = [222UL] },
            ],
        };

        Assert.False(FormationAnchorRules.ShouldRejectIssuer(formation, 333UL));
    }

    [Fact]
    public void TargetAnchorUsesOriginSentinelEvenWhenPointOneIsAssigned() {
        var formation = new Formation {
            Points = [
                new FormationPoint { Cids = [111UL] },
                new FormationPoint { Cids = [222UL] },
            ],
        };

        Assert.Equal(0UL, FormationAnchorRules.SelectAnchorCid(
            formation,
            FormationAnchorKind.Target,
            resolvedContentId: 0,
            issuerCid: 111UL));
    }

    [Fact]
    public void WildcardOriginUsesOriginSentinelForSelfAnchor() {
        var formation = new Formation {
            Points = [
                new FormationPoint(),
                new FormationPoint { Cids = [222UL] },
            ],
        };

        Assert.Equal(0UL, FormationAnchorRules.SelectAnchorCid(
            formation,
            FormationAnchorKind.Self,
            resolvedContentId: 0,
            issuerCid: 333UL));
    }

    [Fact]
    public void OriginAnchorAllowsPointOneMemberToMoveToOwnPoint() {
        Assert.Equal(0, FormationAnchorRules.ResolveMoveDestinationPointIndex(
            anchorCid: 0,
            playerPointIndex: 0,
            anchorPointIndex: 0,
            marchSequence: [],
            sequenceIndex: 3));
    }

    [Fact]
    public void CharacterAnchorWithEmptySequenceDoesNotMovePivot() {
        Assert.Equal(-1, FormationAnchorRules.ResolveMoveDestinationPointIndex(
            anchorCid: 111UL,
            playerPointIndex: 0,
            anchorPointIndex: 0,
            marchSequence: [],
            sequenceIndex: 0));
    }
}
