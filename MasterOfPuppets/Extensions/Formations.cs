// <summary>
using System.Numerics;

namespace MasterOfPuppets.Extensions;

/// Conversion helpers between saved FFXIV game-space formation data and the plot UI.
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

    public static float Distance2D(this Vector3 a, Vector3 b) =>
    new Vector2(a.X - b.X, a.Z - b.Z).Length();
}
