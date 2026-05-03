using System;
using System.Collections.Generic;
using System.Numerics;

using MasterOfPuppets.Extensions;

namespace MasterOfPuppets.Formations;

public static class FormationPath {
    public static List<int> BuildDestinationSequence(
        Formation formation,
        int anchorPointIndex,
        int startPointIndex,
        int step,
        bool reverse) {
        if (!formation.Points.IndexExists(anchorPointIndex)
            || !formation.Points.IndexExists(startPointIndex)
            || anchorPointIndex == startPointIndex)
            return [];

        var destinations = new List<int>();
        for (var i = 0; i < formation.Points.Count; i++) {
            if (i != anchorPointIndex)
                destinations.Add(i);
        }

        var startListIndex = destinations.IndexOf(startPointIndex);
        if (startListIndex < 0)
            return [];

        step = Math.Max(1, step);
        var direction = reverse ? -1 : 1;
        var sequence = new List<int>();
        var seen = new HashSet<int>();
        var idx = startListIndex;

        while (seen.Add(idx)) {
            sequence.Add(destinations[idx]);
            idx = PositiveMod(idx + direction * step, destinations.Count);
        }

        while (sequence.Count < destinations.Count)
            sequence.AddRange(sequence);

        return sequence.GetRange(0, destinations.Count);
    }

    public static (Vector3 Position, float Rotation)? BuildWorldMove(
        Formation formation,
        int anchorPointIndex,
        int startPointIndex,
        Vector3 anchorWorldPosition,
        float anchorWorldRotation,
        int step,
        bool reverse,
        int sequenceIndex) {
        var sequence = BuildDestinationSequence(formation, anchorPointIndex, startPointIndex, step, reverse);
        if (sequence.Count == 0)
            return null;

        var anchorPoint = formation.Points[anchorPointIndex];
        var pointIndex = sequence[PositiveMod(sequenceIndex, sequence.Count)];
        return FormationMath.GetMopRelativeWorld(
            anchorPoint,
            formation.Points[pointIndex],
            anchorWorldPosition,
            anchorWorldRotation);
    }

    private static int PositiveMod(int value, int mod) =>
        (value % mod + mod) % mod;
}
