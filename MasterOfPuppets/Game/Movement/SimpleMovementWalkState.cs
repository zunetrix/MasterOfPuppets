using FFXIVClientStructs.FFXIV.Client.Game.Control;

namespace MasterOfPuppets.Movement;

internal static unsafe class SimpleMovementWalkState {
    public static bool IsWalking {
        get {
            var control = Control.Instance();
            if (control == null) return false;
            return control->IsWalking;
        }
        set {
            var control = Control.Instance();
            if (control == null || control->IsWalking == value)
                return;

            control->IsWalking = value;
        }
    }
}
