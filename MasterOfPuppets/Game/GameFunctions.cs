using System;
using System.Numerics;
using System.Runtime.InteropServices;

using Dalamud.Hooking;
using Dalamud.Utility.Signatures;

using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Event;

using MasterOfPuppets.Movement;

using GameObjectStruct = FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject;

namespace MasterOfPuppets;

// from: https://github.com/PunishXIV/Questionable/blob/new-main/Questionable/Functions/GameFunctions.cs
public static unsafe class GameFunctions {

    private delegate void AbandonDutyDelegate(bool a1);
    private static readonly AbandonDutyDelegate _abandonDuty =
        Marshal.GetDelegateForFunctionPointer<AbandonDutyDelegate>(
            EventFramework.Addresses.LeaveCurrentContent.Value);

    public static void AbandonDuty() => _abandonDuty(false);


    private sealed class Sigs {
        // SetFacing - writes player->Rotation directly.
        // Call-site signature: Dalamud follows the CALL rel32 offset and
        // returns the function address automatically via delegate* unmanaged.
        [Signature("E8 ?? ?? ?? ?? 48 8B 8B ?? 24 00 00 45 33 C0 33 D2")]
        public readonly delegate* unmanaged<GameObjectStruct*, float, void> SetFacing;

        // FollowStructPtr - LEA RDX, [RIP + offset32]  (48 8D 15 ??)
        // ScanType.StaticAddress resolves the RIP-relative offset and returns
        // the absolute address of the struct directly.
        [Signature("48 8D 15 ?? ?? ?? ?? 48 8D 0D ?? ?? ?? ?? E8 ?? ?? ?? ?? 0F B6 47 3E",
                   ScanType = ScanType.StaticAddress)]
        public readonly nint FollowStructPtr;

        public Sigs() => DalamudApi.GameInteropProvider.InitializeFromAttributes(this);
    }

    private static readonly Sigs _sigs = new();

    /// <summary>
    /// Rotates the local player to face the given angle reliably.
    /// Works without a selected target - AutoFaceTargetPosition takes a world
    /// position, not a target object.
    /// </summary>
    public static void FaceDirection(Angle angle) {
        DalamudApi.Framework.RunOnTick(() => {
            var player = DalamudApi.ObjectTable.LocalPlayer;
            if (player == null) return;

            // Build a point 1 unit ahead in the desired direction.
            // The distance is irrelevant; only the direction is used internally.
            var targetPos = player.Position + angle.ToDirectionXZ();

            ActionManager.Instance()->AutoFaceTargetPosition(&targetPos);

            // Reset DesiredRotation so the interpolator does not undo our
            // facing on the very next frame.
            var pm = (PlayerMove*)player.Address;
            pm->Move.Interpolation.DesiredRotation = angle.Rad;
        });
    }

    /// <summary>Rotates the local player to face a world-space position.</summary>
    public static void FaceDirection(Vector3 target) {
        var player = DalamudApi.ObjectTable.LocalPlayer;
        if (player == null) return;
        FaceDirection(MathF.Atan2(
            target.X - player.Position.X,
            target.Z - player.Position.Z).Radians());
    }

    // -------------------------------------------------------------------------
    // SetFacing  (kept for simple cases where interpolation is not a concern)
    // -------------------------------------------------------------------------

    /// <summary>Instantly writes player->Rotation. Prefer FaceDirection() when accuracy matters.</summary>
    public static void SetFacing(Angle angle) {
        if (_sigs.SetFacing == null) return;
        var player = DalamudApi.ObjectTable.LocalPlayer;
        if (player == null) return;
        DalamudApi.Framework.RunOnTick(() =>
            _sigs.SetFacing((GameObjectStruct*)player.Address, angle.Rad));
    }

    /// <summary>Instantly rotates the local player to face a world-space position.</summary>
    public static void SetFacing(Vector3 target) {
        var player = DalamudApi.ObjectTable.LocalPlayer;
        if (player == null) return;
        SetFacing(MathF.Atan2(target.X - player.Position.X, target.Z - player.Position.Z).Radians());
    }

    /// <summary>Activates the game's native follow mode targeting the given entity.</summary>
    public static void Follow(uint entityId) {
        if (_sigs.FollowStructPtr == 0) return;
        var ptr = (uint*)_sigs.FollowStructPtr;
        ptr[0] = entityId;
        ptr[2] = 4;
    }

    /// <summary>Deactivates the game's native follow mode.</summary>
    public static void StopFollow() {
        if (_sigs.FollowStructPtr == 0) return;
        var ptr = (uint*)_sigs.FollowStructPtr;
        ptr[0] = 0xE000_0000;
        ptr[2] = 1;
    }

    internal static void Initialize() => _ = _sigs;
}
