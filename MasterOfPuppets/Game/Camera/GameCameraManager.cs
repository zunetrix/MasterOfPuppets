using System;
using System.Numerics;

using Dalamud.Hooking;

namespace MasterOfPuppets.Camera;

internal static unsafe class GameCameraManager {
    private static Hook<GetCameraPositionDelegate>? hook;
    private static bool _enabled;
    private static float _yOffset;
    private static float _currentY;
    public static bool Enabled => _enabled;
    public static float CurrentY => _currentY;
    public static float YOffset => _yOffset;
    public static float MaxYOffset = 100000000f;

    private delegate void GetCameraPositionDelegate(
        GameCamera* camera,
        IntPtr target,
        Vector3* position,
        bool swapPerson
    );

    public static void Initialize() {
        var camManager = (CameraManager*)
            FFXIVClientStructs.FFXIV.Client.Game.Control.CameraManager.Instance();

        if (camManager == null || camManager->worldCamera == null)
            return;

        var vtbl = camManager->worldCamera->vtbl;

        // set position
        hook = DalamudApi.GameInteropProvider.HookFromAddress<GetCameraPositionDelegate>(vtbl[15], Detour);

        hook.Enable();
    }

    public static void Enable(float offset) {
        _yOffset = offset;
        _enabled = true;
        DalamudApi.ChatGui.Print("", "MOP: Camhack ON", Style.Colors.SeGreen);
    }

    public static void Disable() {
        _enabled = false;
        DalamudApi.ChatGui.Print("", "MOP: Camhack OFF", Style.Colors.SeRed);
    }

    public static void EnableCamHighHeight() {
        Enable(MaxYOffset);
    }

    public static void SetHeight(float offset, bool autoEnable = false) {
        _yOffset = offset;

        if (autoEnable)
            _enabled = true;
    }

    private static void Detour(
        GameCamera* camera,
        IntPtr target,
        Vector3* position,
        bool swapPerson) {
        hook.Original(camera, target, position, swapPerson);

        if (position == null)
            return;

        _currentY = position->Y;

        if (!_enabled)
            return;

        position->Y += _yOffset;
    }

    public static void Dispose() {
        if (hook == null)
            return;

        hook.Disable();

        hook.Dispose();
        hook = null;

        _enabled = false;
        _yOffset = 0f;
    }
}
