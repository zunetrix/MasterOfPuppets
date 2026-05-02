// <summary>
using System;
using System.Numerics;

namespace MasterOfPuppets.Extensions;

/// Conversion helpers between FFXIV world-space and plot-space (canonical math space).
///
/// FFXIV world:  X = east,  Y = up,  Z = south
/// Plot-space:   X = east,  Y = north
///
/// To go from world to plot: plotX = worldX, plotY = -worldZ
/// To go from plot to world: worldX = plotX, worldZ = -plotY
/// </summary>
internal static class FormationConventions {
    /// <summary>Converts a saved formation offset (world XZ) to plot-space XY.</summary>
    public static Vector2 OffsetToPlot(this Vector3 offset)
    => new(offset.X, -offset.Z);

    /// <summary>Converts a plot-space XY back to a world-space XZ offset.</summary>
    public static Vector3 PlotToOffset(this Vector2 plot)
    => new(plot.X, 0f, -plot.Y);

    /// <summary>Rotates a formation offset by the leader's FFXIV rotation and returns the world position.</summary>
    public static Vector3 ApplyLeaderRotation(this Vector3 offset, float leaderRotRad, Vector3 leaderPos) {
        var matrix = Matrix4x4.CreateRotationY(leaderRotRad + MathF.PI);
        return Vector3.Transform(offset, matrix) + leaderPos;
    }

    public static float Distance2D(this Vector3 a, Vector3 b) =>
    new Vector2(a.X - b.X, a.Z - b.Z).Length();
}
