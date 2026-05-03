using System.Collections.Generic;
using System.Linq;

namespace MasterOfPuppets.Formations;

public static class FormationExecution {
    public static FormationPoint? GetAssignedPoint(
        Formation formation,
        ulong contentId,
        IReadOnlyList<CidGroup>? groups = null) =>
        formation.Points.FirstOrDefault(p => p.GetEffectiveCids(groups).Contains(contentId));

    public static int GetAssignedPointIndex(
        Formation formation,
        ulong contentId,
        IReadOnlyList<CidGroup>? groups = null) =>
        formation.Points.FindIndex(p => p.GetEffectiveCids(groups).Contains(contentId));
}
