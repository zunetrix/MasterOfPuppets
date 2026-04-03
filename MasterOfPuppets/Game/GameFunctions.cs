using System;
using System.Numerics;
using System.Runtime.InteropServices;

using Dalamud.Utility.Signatures;

using FFXIVClientStructs.FFXIV.Client.Game.Event;

using MasterOfPuppets.Movement;

using GameObjectStruct = FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject;

namespace MasterOfPuppets;

// from: https://github.com/PunishXIV/Questionable/blob/new-main/Questionable/Functions/GameFunctions.cs
public static unsafe class GameFunctions {
    private sealed class Sigs {
        // SetFacing
        [Signature("E8 ?? ?? ?? ?? 48 8B 8B ?? 24 00 00 45 33 C0 33 D2")]
        public readonly delegate* unmanaged<GameObjectStruct*, float, void> SetFacing;

        // FollowStructPtr
        [Signature("48 8D 15 ?? ?? ?? ?? 48 8D 0D ?? ?? ?? ?? E8 ?? ?? ?? ?? 0F B6 47 3E",
                   ScanType = ScanType.StaticAddress)]
        public readonly nint FollowStructPtr;

        public Sigs() => DalamudApi.GameInteropProvider.InitializeFromAttributes(this);
    }

    private static readonly Sigs _sigs = new();

    private delegate void AbandonDutyDelegate(bool a1);
    private static readonly AbandonDutyDelegate _abandonDuty =
        Marshal.GetDelegateForFunctionPointer<AbandonDutyDelegate>(
            EventFramework.Addresses.LeaveCurrentContent.Value);

    public static void AbandonDuty() => _abandonDuty(false);
    // EventFramework.LeaveCurrentContent(false);


    /// <summary>Instantly rotates the local player to face the given angle.</summary>
    public static void SetFacing(Angle angle) {
        if (_sigs.SetFacing == null) return;
        var player = DalamudApi.ObjectTable.LocalPlayer;
        if (player == null) return;
        DalamudApi.Framework.RunOnTick(() =>
            _sigs.SetFacing((GameObjectStruct*)player.Address, angle.Rad));
    }

    /// <summary>Instantly rotates the local player to face a world position.</summary>
    public static void SetFacing(Vector3 target) {
        var player = DalamudApi.ObjectTable.LocalPlayer;
        if (player == null) return;
        SetFacing(MathF.Atan2(target.X - player.Position.X, target.Z - player.Position.Z).Radians());
    }

    /// <summary>Activates the game's native follow mode targeting the given entity ID.</summary>
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
}
