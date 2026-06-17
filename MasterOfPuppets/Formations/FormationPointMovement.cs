using System.Collections.Generic;
using System.Linq;
using System.Numerics;

using MasterOfPuppets.Extensions;

namespace MasterOfPuppets.Formations;

public static class FormationPointMovement {
    public const int AnchorPointIndex = 0;

    public static (Vector3 Position, float Rotation)? BuildAnchoredWorldMove(
        Formation formation,
        int destinationPointIndex,
        int anchorPointIndex,
        Vector3 anchorWorldPosition,
        float anchorWorldRotation) {
        if (!formation.Points.IndexExists(anchorPointIndex) || !formation.Points.IndexExists(destinationPointIndex))
            return null;

        return FormationMath.GetMopRelativeWorld(
            formation.Points[anchorPointIndex],
            formation.Points[destinationPointIndex],
            anchorWorldPosition,
            anchorWorldRotation);
    }

    public static bool TryGetPointOneAnchorCid(
        Formation formation,
        IReadOnlyList<CidGroup>? groups,
        out ulong contentId,
        out string failureReason) {
        contentId = default;
        failureReason = string.Empty;

        if (!formation.Points.IndexExists(AnchorPointIndex)) {
            failureReason = "formation does not have point 1";
            return false;
        }

        var cids = formation.Points[AnchorPointIndex].GetEffectiveCids(groups).ToList();
        if (cids.Count != 1) {
            failureReason = $"point 1 must have exactly one assigned character; found {cids.Count}";
            return false;
        }

        contentId = cids[0];
        return true;
    }

    public static (Vector3 Position, float Rotation)? BuildAssignedPointOneAnchoredWorldMove(
        Formation formation,
        ulong destinationContentId,
        IReadOnlyList<CidGroup>? groups,
        Vector3 anchorWorldPosition,
        float anchorWorldRotation,
        out int destinationPointIndex) {
        destinationPointIndex = FormationExecution.GetAssignedPointIndex(formation, destinationContentId, groups);
        return destinationPointIndex < 0
            ? null
            : BuildAnchoredWorldMove(formation, destinationPointIndex, AnchorPointIndex, anchorWorldPosition, anchorWorldRotation);
    }
}
