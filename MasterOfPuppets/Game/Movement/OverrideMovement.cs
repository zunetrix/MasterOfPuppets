using System;
using System.Numerics;
using System.Runtime.InteropServices;

using Dalamud.Game.Config;
using Dalamud.Hooking;
using Dalamud.Utility.Signatures;

using FFXIVClientStructs.FFXIV.Client.Game.Control;

namespace MasterOfPuppets.Movement;

public unsafe class OverrideMovement : IDisposable {
    public bool Enabled {
        get => _rmiWalkHook.IsEnabled;
        set {
            if (value) {
                _rmiWalkHook.Enable();
                _rmiFlyHook.Enable();
                _mcIsInputActiveHook.Enable();
            } else {
                UserInput = false;
                _forcedControlState = null;
                _rmiWalkHook.Disable();
                _rmiFlyHook.Disable();
                _mcIsInputActiveHook.Disable();
            }
        }
    }

    public bool IgnoreUserInput; // if true, override even when the user is pressing keys
    public Vector3 DesiredPosition;
    public float Precision = 0.01f;

    /// <summary>True if the player (or another plugin) is pressing movement keys.</summary>
    public bool UserInput { get; private set; }

    // -------------------------------------------------------------------------
    // Internal state
    // -------------------------------------------------------------------------

    private bool _legacyMode;

    // Tristate consumed by the MCIsInputActive detour:
    //   null  = let the game decide (default path, no override)
    //   true  = force "input active"  (we want the game to accept our injection)
    //   false = force "input inactive" (we want the game to treat input as zero)
    private bool? _forcedControlState;

    // -------------------------------------------------------------------------
    // "Is input enabled" helpers
    // These mirror the two internal checks the game runs before sampling input.
    // Both must return true for movement input to be accepted (cutscenes,
    // certain cast states, etc. cause them to return false).
    // -------------------------------------------------------------------------

    private delegate bool RMIWalkIsInputEnabled(void* self);
    private readonly RMIWalkIsInputEnabled _rmiWalkIsInputEnabled1;
    private readonly RMIWalkIsInputEnabled _rmiWalkIsInputEnabled2;

    // -------------------------------------------------------------------------
    // RMIWalk hook
    // self is now typed so we can read Spinning without a cast.
    // -------------------------------------------------------------------------

    private delegate void RMIWalkDelegate(
        MoveControllerSubMemberForMine* self,
        float* sumLeft, float* sumForward, float* sumTurnLeft,
        byte* haveBackwardOrStrafe, byte* a6, byte bAdditiveUnk);

    [Signature("E8 ?? ?? ?? ?? 80 7B 3E 00 48 8D 3D")]
    private readonly Hook<RMIWalkDelegate> _rmiWalkHook = null!;

    // -------------------------------------------------------------------------
    // RMIFly hook - unchanged from the original
    // -------------------------------------------------------------------------

    private delegate void RMIFlyDelegate(void* self, PlayerMoveControllerFlyInput* result);

    [Signature("E8 ?? ?? ?? ?? 0F B6 0D ?? ?? ?? ?? B8")]
    private readonly Hook<RMIFlyDelegate> _rmiFlyHook = null!;

    // -------------------------------------------------------------------------
    // MCIsInputActive hook  (new - was missing from the original)
    //
    // The game calls this function (inputSourceFlags: 1=kb/mouse, 2=gamepad)
    // to decide whether any input source is active BEFORE it applies
    // sumLeft/sumForward to actual movement.  If it returns 0 the injected
    // values are silently discarded, so the player never moves.
    //
    // By overriding the return value with _forcedControlState we ensure the
    // game always acts on whatever we wrote into sumLeft/sumForward.
    // -------------------------------------------------------------------------

    private delegate byte MoveControlIsInputActiveDelegate(void* self, byte inputSourceFlags);

    [Signature("E8 ?? ?? ?? ?? 84 C0 74 09 84 DB 74 1A")]
    private readonly Hook<MoveControlIsInputActiveDelegate> _mcIsInputActiveHook = null!;

    // -------------------------------------------------------------------------
    // Constructor / Dispose
    // -------------------------------------------------------------------------

    public OverrideMovement() {
        var rmiWalkIsInputEnabled1Addr = DalamudApi.SigScanner.ScanText("E8 ?? ?? ?? ?? 84 C0 75 10 38 43 3C");
        var rmiWalkIsInputEnabled2Addr = DalamudApi.SigScanner.ScanText("E8 ?? ?? ?? ?? 84 C0 75 03 88 47 3F");
        _rmiWalkIsInputEnabled1 = Marshal.GetDelegateForFunctionPointer<RMIWalkIsInputEnabled>(rmiWalkIsInputEnabled1Addr);
        _rmiWalkIsInputEnabled2 = Marshal.GetDelegateForFunctionPointer<RMIWalkIsInputEnabled>(rmiWalkIsInputEnabled2Addr);

        DalamudApi.GameInteropProvider.InitializeFromAttributes(this);
        DalamudApi.GameConfig.UiControlChanged += OnConfigChanged;
        UpdateLegacyMode();
    }

    public void Dispose() {
        DalamudApi.GameConfig.UiControlChanged -= OnConfigChanged;
        _rmiWalkHook.Dispose();
        _rmiFlyHook.Dispose();
        _mcIsInputActiveHook.Dispose();
    }

    // -------------------------------------------------------------------------
    // RMIWalk detour
    // -------------------------------------------------------------------------

    private void RMIWalkDetour(
        MoveControllerSubMemberForMine* self,
        float* sumLeft, float* sumForward, float* sumTurnLeft,
        byte* haveBackwardOrStrafe, byte* a6, byte bAdditiveUnk) {

        // Reset forced state at the start of each primary input cycle.
        // bAdditiveUnk == 1 signals a secondary additive call (e.g. spinning)
        // where the previous state is still relevant, so we leave it alone.
        if (bAdditiveUnk == 0)
            _forcedControlState = null;

        _rmiWalkHook.Original(self, sumLeft, sumForward, sumTurnLeft, haveBackwardOrStrafe, a6, bAdditiveUnk);

        // When a boss mechanic is spinning the player the game operates in a
        // special mode: bAdditiveUnk == 1 and Spinning == 1.  Normal movement
        // input is ignored; only steering is accepted.  Skip injection here -
        // the caller can update DesiredPosition for the next normal frame.
        if (self->Spinning == 1 && bAdditiveUnk == 1)
            return;

        // Respect the same input-enabled guards the game uses internally.
        bool movementAllowed = bAdditiveUnk == 0
            && _rmiWalkIsInputEnabled1(self)
            && _rmiWalkIsInputEnabled2(self);

        UserInput = *sumLeft != 0 || *sumForward != 0;

        if (movementAllowed && (IgnoreUserInput || *sumLeft == 0 && *sumForward == 0)) {
            var relDir = DirectionToDestination(false);
            if (relDir != null) {
                var dir = relDir.Value.h.ToDirection();
                *sumLeft = dir.X;
                *sumForward = dir.Y;

                // Tell MCIsInputActive the input is active so the game does
                // not discard the values we just wrote.
                _forcedControlState = true;
            } else {
                // Destination reached - zero out the output and tell
                // MCIsInputActive there is no input so the player stops cleanly.
                *sumLeft = *sumForward = 0;
                _forcedControlState = false;
            }
        }
        // If the user is in control and we are not overriding, leave
        // _forcedControlState as null so the game decides normally.
    }

    // -------------------------------------------------------------------------
    // RMIFly detour
    // -------------------------------------------------------------------------

    private void RMIFlyDetour(void* self, PlayerMoveControllerFlyInput* result) {
        _forcedControlState = null;
        _rmiFlyHook.Original(self, result);

        if (result->Forward == 0 && result->Left == 0 && result->Up == 0) {
            var relDir = DirectionToDestination(true);
            if (relDir != null) {
                var dir = relDir.Value.h.ToDirection();
                result->Forward = dir.Y;
                result->Left = dir.X;
                result->Up = relDir.Value.v.Rad;
                _forcedControlState = true;
            }
        }
    }

    // -------------------------------------------------------------------------
    // MCIsInputActive detour
    // When _forcedControlState has a value we substitute it for the game's
    // answer; otherwise we fall through to the original function.
    // -------------------------------------------------------------------------

    private byte MCIsInputActiveDetour(void* self, byte inputSourceFlags) {
        if (_forcedControlState != null)
            return (byte)(_forcedControlState.Value ? 1 : 0);
        return _mcIsInputActiveHook.Original(self, inputSourceFlags);
    }

    // -------------------------------------------------------------------------
    // DirectionToDestination
    // Returns the (horizontal, vertical) direction to DesiredPosition relative
    // to the camera/player forward, or null when already within Precision.
    // -------------------------------------------------------------------------

    private (Angle h, Angle v)? DirectionToDestination(bool allowVertical) {
        var player = DalamudApi.ObjectTable.LocalPlayer;
        if (player == null)
            return null;

        var dist = DesiredPosition - player.Position;
        if (dist.LengthSquared() <= Precision * Precision)
            return null;

        var dirH = Angle.FromDirectionXZ(dist);
        var dirV = allowVertical
            ? Angle.FromDirection(new(dist.Y, new Vector2(dist.X, dist.Z).Length()))
            : default;

        var refDir = _legacyMode
            ? ((CameraEx*)CameraManager.Instance()->GetActiveCamera())->DirH.Radians() + 180.Degrees()
            : player.Rotation.Radians();

        return (dirH - refDir, dirV);
    }

    // -------------------------------------------------------------------------
    // Legacy mode (standard vs. type-to-turn control scheme)
    // -------------------------------------------------------------------------

    private void OnConfigChanged(object? sender, ConfigChangeEvent evt) => UpdateLegacyMode();
    private void UpdateLegacyMode() {
        _legacyMode = DalamudApi.GameConfig.UiControl.TryGetUInt("MoveMode", out var mode) && mode == 1;
    }
}

[StructLayout(LayoutKind.Explicit, Size = 0x18)]
public struct PlayerMoveControllerFlyInput {
    [FieldOffset(0x0)] public float Forward;
    [FieldOffset(0x4)] public float Left;
    [FieldOffset(0x8)] public float Up;
    [FieldOffset(0xC)] public float Turn;
    [FieldOffset(0x10)] public float u10;
    [FieldOffset(0x14)] public byte DirMode;
    [FieldOffset(0x15)] public byte HaveBackwardOrStrafe;
}

// Typed version of MoveControllerSubMemberForMine.
// Using the concrete type instead of void* lets us read the Spinning field
// without an unsafe cast, matching the BossMod approach.
// Size 0x140 confirmed from BossMod - update if the game patches the struct.
[StructLayout(LayoutKind.Explicit, Size = 0x140)]
public struct MoveControllerSubMemberForMine {
    // Set to 1 when a boss mechanic is forcibly spinning the player.
    // In that state (Spinning == 1 && bAdditiveUnk == 1) the game ignores
    // normal movement input and only accepts steering, so we skip injection.
    [FieldOffset(0x94)] public byte Spinning;
}
