using System.Collections.Generic;

namespace MasterOfPuppets.Formations;

/// <summary>
/// Pure policy for how an anchor resolves against a formation's point 1.
/// Kept free of Dalamud dependencies so the semantics can be unit-tested headless.
///
/// Point 1 is the formation's anchor/origin slot:
///   - configured -> normal formation membership rules apply. Character anchors use their
///                   assigned point when available; external target/focus-target anchors use
///                   point 1 as the origin frame.
///   - raw empty  -> a wildcard origin. Target/focus-target fallback may use the command
///                   leader, while receivers continue to use point 1 as the origin frame.
/// </summary>
public static class FormationAnchorRules {
    public static bool IsPointOneAssigned(
        Formation formation,
        IReadOnlyList<CidGroup>? groups = null) =>
        formation.Points.Count > 0
        && formation.Points[FormationPointMovement.AnchorPointIndex].GetEffectiveCids(groups).Count > 0;

    /// <summary>
    /// Whether point 1 is a raw empty slot (no direct cids and no group references).
    /// This matches what the editor shows as "unassigned" and what the local executor uses.
    /// </summary>
    public static bool IsPointOneUnassigned(Formation formation) {
        if (formation.Points.Count == 0)
            return false;
        var p1 = formation.Points[FormationPointMovement.AnchorPointIndex];
        return (p1.Cids == null || p1.Cids.Count == 0)
            && (p1.GroupIds == null || p1.GroupIds.Count == 0);
    }

    /// <summary>
    /// Whether a missing target/focus-target anchor may fall back to the command leader
    /// instead of doing nothing. Only true for the raw-empty point-1 wildcard-origin case.
    /// </summary>
    public static bool ShouldUseLeaderFallbackOnTargetlessAnchor(
        Formation formation,
        FormationAnchorKind anchorKind) =>
        anchorKind is FormationAnchorKind.Target or FormationAnchorKind.FocusTarget
        && IsPointOneUnassigned(formation);

    /// <summary>
    /// Whether the command sender should be rejected because they have no role in the formation.
    /// Only rejected when the sender is not assigned to any point AND point 1 is assigned — i.e.
    /// there is no unassigned origin they could lead from. If point 1 is unassigned, any sender
    /// may act as the wildcard-origin leader (they simply stay put as the anchor).
    /// </summary>
    public static bool ShouldRejectIssuer(
        Formation formation,
        ulong issuerCid,
        IReadOnlyList<CidGroup>? groups = null) =>
        FormationExecution.GetAssignedPoint(formation, issuerCid, groups) == null
        && !IsPointOneUnassigned(formation);

    /// <summary>
    /// Selects the anchor ContentId to broadcast.
    ///   - target/focus-target anchor -> 0, the origin-slot sentinel. The anchor frame is point 1
    ///                                    and the broadcast position is the target/issuer. 0 avoids
    ///                                    the "playerCid == anchorCid" skip, so an assigned point 1
    ///                                    participates and moves to the target like any member.
    ///   - self                      -> issuer cid when point 1 is configured.
    ///   - named/sender               -> resolved cid only when that character is a formation
    ///                                   member; otherwise 0 for an external anchor.
    ///   - raw-empty point 1          -> 0 for every anchor kind (wildcard origin).
    /// </summary>
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

    /// <summary>
    /// Resolves the rank destination point index for a marching member, given the broadcast
    /// anchor cid and the member's own point.
    ///
    /// When the anchor is a real character pivot (anchorCid != 0) the pivot does not march to
    /// itself, so we honor the (possibly empty) march sequence. When the anchor is an origin
    /// anchor (anchorCid == 0 — target/focus-target/unassigned-origin) there is no character
    /// pivot, so an assigned point-1 member at the origin participates like any member: its
    /// destination is its own point, which resolves to the origin anchor position.
    /// </summary>
    public static int ResolveMoveDestinationPointIndex(
        ulong anchorCid,
        int playerPointIndex,
        int anchorPointIndex,
        System.Collections.Generic.IReadOnlyList<int> marchSequence,
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
