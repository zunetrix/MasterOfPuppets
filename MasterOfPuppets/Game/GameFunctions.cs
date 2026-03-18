using System;
using System.Numerics;
using System.Runtime.InteropServices;

using Dalamud.Game.ClientState.Objects.Types;

using FFXIVClientStructs.FFXIV.Client.Game.Event;

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

    /// <summary>Instantly rotates an object to face the given angle in radians.</summary>
    public static void SetFacing(IGameObject obj, float radians) {
        if (_setFacing == null) return;
        DalamudApi.Framework.RunOnTick(() =>
            _setFacing((GameObjectStruct*)obj.Address, radians));
    }

    /// <summary>Instantly rotates an object to face a world position.</summary>
    public static void SetFacing(IGameObject obj, Vector3 target) =>
        SetFacing(obj, MathF.Atan2(target.X - obj.Position.X, target.Z - obj.Position.Z));

    /// <summary>Activates the game's native follow mode targeting the given entity ID.</summary>
    public static void FollowStart(uint entityId) {
        if (_followStructPtr == 0) return;
        var ptr = (uint*)_followStructPtr;
        ptr[0] = entityId;
        ptr[2] = 4;
    }

    /// <summary>Deactivates the game's native follow mode.</summary>
    public static void FollowStop() {
        if (_followStructPtr == 0) return;
        var ptr = (uint*)_followStructPtr;
        ptr[0] = 0xE000_0000;
        ptr[2] = 1;
    }
}
