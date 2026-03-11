using System;
using System.Numerics;
using System.Runtime.InteropServices;

using FFXIVClientStructs.FFXIV.Client.Game.Object;

using Dalamud.Hooking;
using System.Threading;

namespace MasterOfPuppets.Cam;

public unsafe class CamHack : IDisposable {
    private static readonly Lazy<CamHack> LazyInstance = new(static () => new CamHack());

    private CamHack() { }

    public static CamHack Instance => LazyInstance.Value;

    private static readonly CameraManager* CameraManager = (CameraManager*)FFXIVClientStructs.FFXIV.Client.Game.Control.CameraManager.Instance();

    private static bool Active { get; set; } = false;

    public delegate void GetCameraPositionDelegate(GameCamera* camera, GameObject* target, Vector3* position, bool swapPerson);
    private static Hook<GetCameraPositionDelegate> _getPositionDeltaHook;
    private static void GetCameraPositionDetour(GameCamera* camera, GameObject* target, Vector3* position, bool swapPerson) {
        if (!Active) {
            camera->resetLookatHeightOffset = 1;
        } else {
            camera->lookAtHeightOffset += 10;
            position->Y += 3000f;
        }
    }

    public void Initialize() {
        var vtbl = CameraManager->worldCamera->vtbl;
        _getPositionDeltaHook = DalamudApi.GameInteropProvider.HookFromAddress<GetCameraPositionDelegate>(vtbl[15], GetCameraPositionDetour);

        Active = false;
    }

    public void Enable() {
        if (Active)
            return;
        _getPositionDeltaHook.Enable();
        Active = true;
    }

    public void EnableOthers() {
        // Plugin.IpcProvider.EnableCamHack();
    }

    public void Disable() {
        if (!Active)
            return;

        Active = false;
        DalamudApi.Framework.RunOnTick(delegate {
            _getPositionDeltaHook?.Disable();
        }, default(TimeSpan), 10, default(CancellationToken));

    }

    public void Dispose() {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing) {
        if (!disposing) return;
        _getPositionDeltaHook?.Dispose();
    }
}

/// <summary>
/// https://github.com/UnknownX7/Cammy/blob/master/Structures/CameraManager.cs
/// </summary>
[StructLayout(LayoutKind.Explicit)]
internal unsafe struct CameraManager {
    [FieldOffset(0x0)] public GameCamera* worldCamera;
    [FieldOffset(0x8)] public GameCamera* idleCamera;
    [FieldOffset(0x10)] public GameCamera* menuCamera;
    [FieldOffset(0x18)] public GameCamera* spectatorCamera;
}

/// <summary>
/// https://github.com/UnknownX7/Cammy/blob/master/Structures/GameCamera.cs
/// </summary>
[StructLayout(LayoutKind.Explicit)]
public unsafe struct GameCamera {
    [FieldOffset(0x0)] public nint* vtbl;
    [FieldOffset(0x60)] public float x;
    [FieldOffset(0x64)] public float y;
    [FieldOffset(0x68)] public float z;
    [FieldOffset(0x90)] public float lookAtX; // Position that the camera is focused on (Actual position when zoom is 0)
    [FieldOffset(0x94)] public float lookAtY;
    [FieldOffset(0x98)] public float lookAtZ;
    [FieldOffset(0x114)] public float currentZoom; // 6
    [FieldOffset(0x118)] public float minZoom; // 1.5
    [FieldOffset(0x11C)] public float maxZoom; // 20
    [FieldOffset(0x120)] public float maxFoV; // 0.78
    [FieldOffset(0x124)] public float minFoV; // 0.69
    [FieldOffset(0x128)] public float currentFoV; // 0.78
    [FieldOffset(0x12C)] public float addedFoV; // 0
    [FieldOffset(0x130)] public float currentHRotation; // -pi -> pi, default is pi
    [FieldOffset(0x134)] public float currentVRotation; // -0.349066
    [FieldOffset(0x138)] public float hRotationDelta;
    [FieldOffset(0x148)] public float minVRotation; // -1.483530, should be -+pi/2 for straight down/up but camera breaks so use -+1.569
    [FieldOffset(0x14C)] public float maxVRotation; // 0.785398 (pi/4)
    [FieldOffset(0x160)] public float tilt;
    [FieldOffset(0x170)] public int mode; // Camera mode? (0 = 1st person, 1 = 3rd person, 2+ = weird controller mode? cant look up/down)
    [FieldOffset(0x174)] public int controlType; // 0 first person, 1 legacy, 2 standard, 4 talking to npc in first person (with option enabled), 5 talking to npc (with option enabled), 3/6 ???
    [FieldOffset(0x17C)] public float interpolatedZoom;
    [FieldOffset(0x190)] public float transition; // Seems to be related to the 1st <-> 3rd camera transition
    [FieldOffset(0x1B0)] public float viewX;
    [FieldOffset(0x1B4)] public float viewY;
    [FieldOffset(0x1B8)] public float viewZ;
    [FieldOffset(0x1E4)] public byte isFlipped; // 1 while holding the keybind
    [FieldOffset(0x21C)] public float interpolatedY;
    [FieldOffset(0x224)] public float lookAtHeightOffset; // No idea what to call this (0x230 is the interpolated value)
    [FieldOffset(0x228)] public byte resetLookatHeightOffset; // No idea what to call this
    [FieldOffset(0x230)] public float interpolatedLookAtHeightOffset;
    [FieldOffset(0x2B0)] public byte lockPosition;
    [FieldOffset(0x2C4)] public float lookAtY2;
}
