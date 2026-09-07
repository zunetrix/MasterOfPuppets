using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace MasterOfPuppets.LuaScripting;

public sealed record LuaActorFollowRequest(
    IReadOnlyList<string> AnchorCandidates,
    Vector3 RelativeOffset,
    bool FaceAnchor = true,
    float FacingOffsetRadians = 0f,
    float Precision = 0.1f,
    bool BrakeAtPosition = true,
    bool PursuitPrediction = false,
    bool ImmediateSteering = true,
    bool RigidFormation = false,
    float FormationRadius = 0f,
    IReadOnlyList<LuaFormationNeighbor>? FormationNeighbors = null,
    float NeighborCorrectionWeight = 0.25f,
    float MaximumNeighborCorrection = 0.35f,
    bool MirrorWalkRun = false,
    bool MirrorSprint = false) {

    public LuaActorFollowRequest Validate() {
        var candidates = AnchorCandidates?
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray() ?? [];
        if (candidates.Length == 0)
            throw new ArgumentException("follow_actor requires at least one anchor candidate");
        if (candidates.Length > 64)
            throw new ArgumentException("follow_actor accepts at most 64 anchor candidates");
        if (!IsFinite(RelativeOffset) || RelativeOffset.Length() > 100f)
            throw new ArgumentException("follow_actor offset must be finite and within 100 yalms");
        if (!float.IsFinite(FacingOffsetRadians))
            throw new ArgumentException("follow_actor facing offset must be finite");
        if (!float.IsFinite(Precision) || Precision < 0.02f || Precision > 5f)
            throw new ArgumentException("follow_actor precision must be between 0.02 and 5 yalms");

        if (!float.IsFinite(FormationRadius) || FormationRadius < 0f || FormationRadius > 100f)
            throw new ArgumentException("follow_actor formation radius must be between 0 and 100 yalms");
        if (RigidFormation && FormationRadius + 0.001f < Length2D(RelativeOffset))
            throw new ArgumentException("follow_actor formation radius cannot be smaller than the assigned slot radius");
        if (!float.IsFinite(NeighborCorrectionWeight) || NeighborCorrectionWeight < 0f || NeighborCorrectionWeight > 0.5f)
            throw new ArgumentException("follow_actor neighbor correction weight must be between 0 and 0.5");
        if (!float.IsFinite(MaximumNeighborCorrection) || MaximumNeighborCorrection < 0f || MaximumNeighborCorrection > 2f)
            throw new ArgumentException("follow_actor maximum neighbor correction must be between 0 and 2 yalms");

        var neighbors = (FormationNeighbors ?? [])
            .Where(neighbor => neighbor != null && !string.IsNullOrWhiteSpace(neighbor.Name))
            .Select(neighbor => neighbor with { Name = neighbor.Name.Trim() })
            .DistinctBy(neighbor => neighbor.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (neighbors.Length > 4)
            throw new ArgumentException("follow_actor accepts at most four rigid-formation neighbors");
        foreach (var neighbor in neighbors) {
            if (!IsFinite(neighbor.RelativeOffset) || neighbor.RelativeOffset.Length() > 100f)
                throw new ArgumentException("follow_actor neighbor offsets must be finite and within 100 yalms");
            if (RigidFormation && FormationRadius + 0.001f < Length2D(neighbor.RelativeOffset))
                throw new ArgumentException("follow_actor formation radius cannot be smaller than a neighbor slot radius");
        }

        return this with {
            AnchorCandidates = candidates,
            FormationNeighbors = neighbors,
        };
    }

    private static bool IsFinite(Vector3 value) =>
        float.IsFinite(value.X) && float.IsFinite(value.Y) && float.IsFinite(value.Z);

    private static float Length2D(Vector3 value) =>
        MathF.Sqrt(value.X * value.X + value.Z * value.Z);
}

public sealed record LuaFormationNeighbor(string Name, Vector3 RelativeOffset);
