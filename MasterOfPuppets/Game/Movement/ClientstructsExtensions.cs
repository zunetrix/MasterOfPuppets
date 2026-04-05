using FFXIVClientStructs.FFXIV.Client.Game.Control;

using System.Runtime.InteropServices;

namespace MasterOfPuppets;

// from: bossmod
// Offsets confirmed from BossMod for patch 7.3+.
// The PlayerMove offset was 0x1E0 in 7.25 - if the game updates, check here first.

[StructLayout(LayoutKind.Explicit, Size = 0x22E0)]
internal partial struct PlayerMove {
    [FieldOffset(0x1E0)] public MoveContainer Move;
}

[StructLayout(LayoutKind.Explicit, Size = 0x430)]
internal partial struct MoveContainer {

    [StructLayout(LayoutKind.Explicit, Size = 0x88)]
    public partial struct InterpolationState {
        // The angle the interpolator is currently rotating toward.
        // Must be reset alongside any SetFacing / AutoFaceTargetPosition call
        // to prevent the interpolator from undoing the new rotation on the
        // next frame.
        [FieldOffset(0x10)] public float DesiredRotation;

        // The angle the interpolation started from.
        [FieldOffset(0x14)] public float OriginalRotation;

        // True while the interpolator is actively rotating the character.
        [FieldOffset(0x40)] public bool RotationInterpolationInProgress;
    }

    // Offset was 0x1C0 in 7.25.
    [FieldOffset(0x1D0)] public InterpolationState Interpolation;
}

[StructLayout(LayoutKind.Explicit, Size = 0x76F0)]
internal unsafe partial struct ControlEx {
    // Base movement speed scalar.  Modify with care - the game reads this
    // every frame to scale all movement input.
    [FieldOffset(0x7118)] public float BaseMoveSpeed;

    public static ControlEx* Instance() => (ControlEx*)Control.Instance();
}
