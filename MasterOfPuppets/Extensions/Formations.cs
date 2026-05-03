using System;
using System.Numerics;

namespace MasterOfPuppets.Extensions;

/// Conversion helpers between FFXIV world-space and plot-space.
///
/// FFXIV world:  X = east,  Y = up,  Z = south.
/// Plot-space:   X = west,  Y = north.
///
/// To go from world to plot: plotX = -worldX, plotY = worldZ.
/// To go from plot to world: worldX = -plotX, worldZ = plotY.
internal static class FormationConventions {
    /// <summary>Converts a saved formation offset (world XZ) to plot-space XY.</summary>
    public static Vector2 OffsetToPlot(this Vector3 offset)
    => new(-offset.X, offset.Z);

    /// <summary>Converts a plot-space XY back to a world-space XZ offset.</summary>
    public static Vector3 PlotToOffset(this Vector2 plot)
    => new(-plot.X, 0f, plot.Y);

    /// <summary>Rotates a saved formation offset by the anchor's FFXIV rotation.</summary>
    public static Vector3 ApplyLeaderRotation(this Vector3 offset, float leaderRotRad, Vector3 leaderPos) {
        float angle = -leaderRotRad;
        float cos = MathF.Cos(angle);
        float sin = MathF.Sin(angle);

        return leaderPos + new Vector3(
            offset.X * cos - offset.Z * sin,
            0f,
            offset.X * sin + offset.Z * cos
        );
    }

    public static float Distance2D(this Vector3 a, Vector3 b) =>
    new Vector2(a.X - b.X, a.Z - b.Z).Length();
}
