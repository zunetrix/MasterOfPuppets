using System.Collections.Generic;

namespace MasterOfPuppets.Formations;

/// <summary>
/// Pure policy for how point 1 behaves as a formation anchor.
/// Kept free of Dalamud dependencies so the semantics can be unit-tested headless.
/// </summary>
public static class FormationAnchorRules {
    public static bool IsPointOneAssigned(
        Formation formation,
        IReadOnlyList<CidGroup>? groups = null) =>
        formation.Points.Count > FormationPointMovement.AnchorPointIndex
        && formation.Points[FormationPointMovement.AnchorPointIndex].GetEffectiveCids(groups).Count > 0;

    /// <summary>
    /// Whether point 1 is a raw empty slot, meaning no direct CIDs and no group references.
    /// This is the "wildcard origin" case.
    /// </summary>
    public static bool IsPointOneUnassigned(Formation formation) {
        if (formation.Points.Count == 0)
            return false;

        var p1 = formation.Points[FormationPointMovement.AnchorPointIndex];
        return (p1.Cids == null || p1.Cids.Count == 0)
            && (p1.GroupIds == null || p1.GroupIds.Count == 0);
    }

    /// <summary>
    /// Whether a target or focus-target anchor may fall back to the command leader.
    /// This is independent of point-1 assignment so every command transport has
    /// the same deterministic targetless behavior.
    /// </summary>
    public static bool ShouldUseLeaderFallbackOnTargetlessAnchor(
        Formation formation,
        FormationAnchorKind anchorKind) =>
        anchorKind is FormationAnchorKind.Target or FormationAnchorKind.FocusTarget;

    /// <summary>
    /// Whether the issuer should be rejected because they have no role in the formation.
    /// If point 1 is unassigned, any sender may act as the wildcard-origin leader.
    /// </summary>
    public static bool ShouldRejectIssuer(
        Formation formation,
        ulong issuerCid,
        IReadOnlyList<CidGroup>? groups = null) =>
        FormationExecution.GetAssignedPoint(formation, issuerCid, groups) == null
        && !IsPointOneUnassigned(formation);

    public static ulong SelectAnchorCid(
        Formation formation,
        FormationAnchorKind effectiveAnchorKind,
        ulong resolvedContentId,
        ulong issuerCid,
        IReadOnlyList<CidGroup>? groups = null) {
        if (effectiveAnchorKind is FormationAnchorKind.Target or FormationAnchorKind.FocusTarget)
            return 0;

        if (IsPointOneUnassigned(formation))
            return 0;

        if (effectiveAnchorKind == FormationAnchorKind.Self)
            return issuerCid;

        return resolvedContentId != 0
            && FormationExecution.GetAssignedPoint(formation, resolvedContentId, groups) != null
                ? resolvedContentId
                : 0;
    }

    public static int ResolveMoveDestinationPointIndex(
        ulong anchorCid,
        int playerPointIndex,
        int anchorPointIndex,
        IReadOnlyList<int> marchSequence,
        int sequenceIndex) {
        if (anchorCid == 0 && playerPointIndex >= 0 && playerPointIndex == anchorPointIndex)
            return playerPointIndex;

        if (marchSequence == null || marchSequence.Count == 0)
            return -1;

        var index = sequenceIndex % marchSequence.Count;
        if (index < 0)
            index += marchSequence.Count;
        return marchSequence[index];
    }
}
