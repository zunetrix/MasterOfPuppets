using System;
using System.Numerics;
using System.Runtime.InteropServices;

using Dalamud.Utility.Signatures;

using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Event;
// using GameObjectStruct = FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject;

using MasterOfPuppets.Movement;

namespace MasterOfPuppets;

// from: https://github.com/PunishXIV/Questionable/blob/new-main/Questionable/Functions/GameFunctions.cs
public static unsafe class GameFunctions {
    private sealed class Sigs {
        [Signature("48 8D 15 ?? ?? ?? ?? 48 8D 0D ?? ?? ?? ?? E8 ?? ?? ?? ?? 0F B6 47 3E", ScanType = ScanType.StaticAddress)]
        public readonly nint FollowStruct;

        public Sigs() => DalamudApi.GameInteropProvider.InitializeFromAttributes(this);
    }

    private static readonly Sigs _sigs = new();

    private delegate void AbandonDutyDelegate(bool a1);
    private static readonly AbandonDutyDelegate _abandonDuty =
        Marshal.GetDelegateForFunctionPointer<AbandonDutyDelegate>(EventFramework.Addresses.LeaveCurrentContent.Value);
    public static void AbandonDuty() => _abandonDuty(false);

    /// <summary>
    /// Rotates the local player to face the given angle reliably.
    /// Works without a selected target - AutoFaceTargetPosition takes a world
    /// position, not a target object.
    /// </summary>
    public static void FaceDirection(Angle angle) {
        var player = DalamudApi.ObjectTable.LocalPlayer;
        if (player == null) return;

        // Build a point 1 unit ahead in the desired direction.
        // The distance is irrelevant; only the direction is used internally.
        var targetPos = player.Position + angle.ToDirectionXZ();

        var am = ActionManager.Instance();
        if (am == null) return;
        am->AutoFaceTargetPosition(&targetPos);

        // Reset DesiredRotation so the interpolator does not undo our
        // facing on the very next frame.
        var pm = (PlayerMove*)player.Address;
        pm->Move.Interpolation.DesiredRotation = angle.Rad;
    }

    public static void FaceDirectionDeferred(Angle angle) {
        DalamudApi.Framework.RunOnTick(() => FaceDirection(angle));
    }

    /// <summary>Rotates the local player to face a world-space position.</summary>
    public static void FaceDirection(Vector3 target) {
        var player = DalamudApi.ObjectTable.LocalPlayer;
        if (player == null) return;
        FaceDirection(MathF.Atan2(
            target.X - player.Position.X,
            target.Z - player.Position.Z).Radians());
    }

    /// <summary>Activates the game's native follow mode targeting the given entity.</summary>
    public static void Follow(uint entityId) {
        if (_sigs.FollowStruct == 0) return;
        var ptr = (uint*)_sigs.FollowStruct;
        ptr[0] = entityId;
        ptr[2] = 4;
    }

    /// <summary>Deactivates the game's native follow mode.</summary>
    public static void StopFollow() {
        if (_sigs.FollowStruct == 0) return;
        var ptr = (uint*)_sigs.FollowStruct;
        ptr[0] = 0xE000_0000;
        ptr[2] = 1;
    }
}
