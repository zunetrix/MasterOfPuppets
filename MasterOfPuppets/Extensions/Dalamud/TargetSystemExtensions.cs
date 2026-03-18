using Dalamud.Game.ClientState.Objects.Types;

using FFXIVClientStructs.FFXIV.Client.Game.Control;
using FFXIVClientStructs.FFXIV.Client.Game.Object;

namespace MasterOfPuppets.Extensions.Dalamud;

public static class TargetSystemExtensions {

    internal static unsafe void Interact(this IGameObject gameObject, bool checkLineOfSight = false) {
        if (gameObject == null) return;
        TargetSystem.Instance()->InteractWithObject((GameObject*)gameObject.Address, checkLineOfSight);
    }
}
