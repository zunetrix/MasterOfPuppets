using System.Numerics;

using Dalamud.Game.ClientState.Objects.Types;

namespace MasterOfPuppets.Extensions.Dalamud;

public static class GameObjectExtensions {

    /// <summary>
    /// Returns the squared distance between two objects. Prefer this over
    /// <see cref="DistanceTo"/> for comparisons to avoid an unnecessary sqrt.
    /// </summary>
    public static float DistanceSquaredTo(this IGameObject self, IGameObject other) =>
        Vector3.DistanceSquared(self.Position, other.Position);

    /// <summary>
    /// Returns the euclidean distance between two objects.
    /// </summary>
    public static float DistanceTo(this IGameObject self, IGameObject other) =>
        Vector3.Distance(self.Position, other.Position);
}
