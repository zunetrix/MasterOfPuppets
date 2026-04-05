// <summary>
using System;
using System.Numerics;

namespace MasterOfPuppets.Extensions;

/// Conversion helpers between FFXIV world-space and plot-space (canonical math space).
///
/// FFXIV world:  X = east,  Y = up,  Z = south  (rotation 0 = north/−Z, increases CW)
/// Plot-space:   X = west,  Y = north            (rotation 0 = north/+Y, increases CCW)
///
/// To go from world to plot: plotX = −worldX, plotY = worldZ
/// To go from plot to world: worldX = −plotX, worldZ = plotY
/// </summary>
internal static class FormationConventions {
    /// <summary>Converts a saved formation offset (world XZ) to plot-space XY.</summary>
    public static Vector2 OffsetToPlot(this Vector3 offset)
    => new(-offset.X, offset.Z);

    /// <summary>Converts a plot-space XY back to a world-space XZ offset.</summary>
    public static Vector3 PlotToOffset(this Vector2 plot)
    => new(-plot.X, 0f, plot.Y);

    /// <summary>
    /// Rotates a world-space offset by the leader's FFXIV rotation and returns the world position.
    /// FFXIV rotation: 0 = north (−Z), CW positive → converted to CCW radians for math.
    /// </summary>
    public static Vector3 ApplyLeaderRotation(this Vector3 offset, float leaderRotRad, Vector3 leaderPos) {
        // Convert FFXIV CW rotation to CCW for standard math rotation on XZ plane
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
