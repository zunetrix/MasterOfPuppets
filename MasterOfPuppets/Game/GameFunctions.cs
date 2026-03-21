using System;
using System.Numerics;
using System.Runtime.InteropServices;

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

    private delegate void SetFacingDelegate(GameObjectStruct* obj, float radians);
    private static SetFacingDelegate? _setFacing;

    private static nint _followStructPtr;

    internal static void Initialize() {
        try {
            var callSite = DalamudApi.SigScanner.ScanText("E8 ?? ?? ?? ?? 48 8B 8B ?? 24 00 00 45 33 C0 33 D2");
            var funcAddr = callSite + 5 + *(int*)(callSite + 1);
            _setFacing = Marshal.GetDelegateForFunctionPointer<SetFacingDelegate>(funcAddr);
        } catch (Exception ex) {
            DalamudApi.PluginLog.Error(ex, "GameFunctions: failed to resolve SetFacing");
        }

        try {
            var lea = DalamudApi.SigScanner.ScanText("48 8D 15 ?? ?? ?? ?? 48 8D 0D ?? ?? ?? ?? E8 ?? ?? ?? ?? 0F B6 47 3E");
            _followStructPtr = lea + 7 + *(int*)(lea + 3);
        } catch (Exception ex) {
            DalamudApi.PluginLog.Error(ex, "GameFunctions: failed to resolve FollowStructPtr");
        }
    }

    /// <summary>Instantly rotates the local player to face the given angle.</summary>
    public static void SetFacing(Angle angle) {
        if (_setFacing == null) return;
        var player = DalamudApi.ObjectTable.LocalPlayer;
        if (player == null) return;
        DalamudApi.Framework.RunOnTick(() =>
            _setFacing((GameObjectStruct*)player.Address, angle.Rad));
    }

    /// <summary>Instantly rotates the local player to face a world position.</summary>
    public static void SetFacing(Vector3 target) {
        var player = DalamudApi.ObjectTable.LocalPlayer;
        if (player == null) return;
        SetFacing(MathF.Atan2(target.X - player.Position.X, target.Z - player.Position.Z).Radians());
    }

    /// <summary>Activates the game's native follow mode targeting the given entity ID.</summary>
    public static void Follow(uint entityId) {
        if (_followStructPtr == 0) return;
        var ptr = (uint*)_followStructPtr;
        ptr[0] = entityId;
        ptr[2] = 4;
    }

    /// <summary>Deactivates the game's native follow mode.</summary>
    public static void StopFollow() {
        if (_followStructPtr == 0) return;
        var ptr = (uint*)_followStructPtr;
        ptr[0] = 0xE000_0000;
        ptr[2] = 1;
    }
}
