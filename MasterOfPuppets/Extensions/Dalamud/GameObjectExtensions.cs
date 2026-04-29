using System.Numerics;

using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;

namespace MasterOfPuppets.Extensions.Dalamud;

public static class GameObjectExtensions {

    // Returns "Name@World" for player objects, or just "Name" when the world is unavailable.
    // Returns null when the object is not a player.
    public static string? GetPlayerNameWorld(this IGameObject? self) {
        if (self == null) return null;
        if (self.ObjectKind != ObjectKind.Pc) return null;

        var name = self.Name.TextValue;
        var world = (self as IPlayerCharacter)?.HomeWorld.ValueNullable?.Name.ToString();

        return world != null ? $"{name}@{world}" : name;
    }

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
