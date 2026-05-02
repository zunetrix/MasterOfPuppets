using System;
using System.Numerics;
using System.Threading;

using Dalamud.Game.ClientState.Conditions;
using Dalamud.Hooking;
using Dalamud.Utility.Signatures;

using FFXIVClientStructs.FFXIV.Client.Game.Control;

using MasterOfPuppets.Extensions;
using MasterOfPuppets.Util;

namespace MasterOfPuppets.Movement;

public unsafe class SimpleInputMovement : IDisposable {

    [Signature("74 0C 48 8D 0D ?? ?? ?? ?? E8 ?? ?? ?? ?? 48 8D 0D ?? ?? ?? ?? E8 ?? ?? ?? ?? 48 8D 0D ?? ?? ?? ??", ScanType = ScanType.StaticAddress)]
    private readonly nint _moveControllerSubMemberForMineInstance;

    [Signature("40 53 48 83 EC ?? 48 8B 41 20 48 8B D9 80 B8 02 02 00 00 ??", ScanType = ScanType.Text)]
    private readonly delegate* unmanaged<nint, long> _moveStop;

    /// <summary>Current injected direction. Set to None to stop injection.</summary>
    public MovementDirection Direction {
        get => _direction;
        set {
            _direction = value;
            // Only keep the hook enabled while we are actually injecting
            // When idle the hook is disabled so it has zero runtime cost
            if (value == MovementDirection.None)
                _playerMoveHook?.Disable();
            else
                _playerMoveHook?.Enable();
        }
    }

    public bool IsWalking {
        get {
            var control = Control.Instance();
            if (control == null) return false;
            return control->IsWalking;
        }
        set {
            var control = Control.Instance();
            if (control == null) return;

            if (control->IsWalking == value)
                return;

            control->IsWalking = value;
        }
    }

    // -------------------------------------------------------------------------
    // Hook
    //
    // Intercepts every movement input poll.  When our direction is active we
    // return 1 (key held) for that direction code, regardless of keyboard state.
    // This is identical to what the game sees when you physically hold a key.
    // The hook starts disabled and is enabled only while Direction != None.
    // -------------------------------------------------------------------------
    private delegate byte PlayerMoveDelegate(nint a1, int direction);
    private Hook<PlayerMoveDelegate>? _playerMoveHook;
    private byte PlayerMoveDetour(nint a1, int dir) {
        var original = _playerMoveHook.Original(a1, dir);

        if (_direction != MovementDirection.None && dir == (int)_direction)
            return 1;

        return original;
    }

    private MovementDirection _direction;

    // Active cancellation source for the current MoveTo coroutine
    private CancellationTokenSource? _cts;

    public SimpleInputMovement() {
        DalamudApi.GameInteropProvider.InitializeFromAttributes(this);
        // Hook starts disabled, enabled automatically when Direction is set.
        _playerMoveHook = DalamudApi.GameInteropProvider.HookFromSignature<PlayerMoveDelegate>(
            "E8 ?? ?? ?? ?? 4C 63 4B 04",
            PlayerMoveDetour
        );

        DalamudApi.PluginLog.Debug($"[SimpleInputMovement] MoveControllerInstance: {_moveControllerSubMemberForMineInstance:X}");
    }

    public void Dispose() {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;

        _direction = MovementDirection.None;

        _playerMoveHook?.Disable();
        _playerMoveHook?.Dispose();
        _playerMoveHook = null;
    }

    private void MoveStopNative() {
        if (_moveStop == null || _moveControllerSubMemberForMineInstance == 0)
            return;
        try {
            _moveStop(_moveControllerSubMemberForMineInstance);
        } catch {
            // ignored
        }
    }

    public void StopMove() {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;

        _direction = MovementDirection.None;
        _playerMoveHook?.Disable();

        // DalamudApi.Framework.RunOnFrameworkThread(() => {
        //     if (_playerMoveHook == null) return;
        //     if (DalamudApi.ObjectTable.LocalPlayer == null || !DalamudApi.ClientState.IsLoggedIn)
        //         return;
        //     try {
        //         MoveStopNative();
        //     } catch {
        //         // ignored
        //     }
        // });
    }

    // -------------------------------------------------------------------------
    // MoveTo
    // Returns the CancellationTokenSource so the caller can cancel externally,
    // or null if movement was refused because Performing is already active.
    // -------------------------------------------------------------------------
    public CancellationTokenSource? MoveTo(Vector3 destination, float precision = 0.3f, float? faceDirection = null) {
        // Refuse to start while the player is performing - movement is blocked
        // by the game anyway, so there is nothing useful we could do.
        if (DalamudApi.Condition[ConditionFlag.Performing])
            return null;

        // Cancel any previous move before starting a new one.
        StopMove();

        var cts = new CancellationTokenSource();
        _cts = cts;

        // Snapshot current control settings so we can restore them on exit,
        // regardless of whether we finish, are cancelled, or Performing fires.
        uint savedMoveMode = DalamudApi.GameConfig.UiControl.GetUInt("MoveMode");
        uint savedPadMode = DalamudApi.GameConfig.UiConfig.GetUInt("PadMode");

        DalamudApi.GameConfig.UiControl.Set("MoveMode", 0u);
        DalamudApi.GameConfig.UiConfig.Set("PadMode", 0u);

        Coroutine.StartRunOnFramework(
            runFunction: () => {
                var player = DalamudApi.ObjectTable.LocalPlayer;
                if (player == null) return;

                // If the player entered performance mode mid-move, cancel
                // everything immediately - Stop() will clean up direction and
                // native movement; the coroutine's callback restores settings.
                if (DalamudApi.Condition[ConditionFlag.Performing]) {
                    StopMove();
                    return;
                }

                // Re-face target every frame in case the player drifts.
                GameFunctions.FaceDirection(destination);

                Direction = MovementDirection.Forward;

                // Slow to walk when close for a cleaner arrival.
                IsWalking = player.Position.Distance2D(destination) < precision;
            },
            callback: () => {
                // Restore control settings regardless of how the loop ended.
                DalamudApi.GameConfig.UiControl.Set("MoveMode", savedMoveMode);
                DalamudApi.GameConfig.UiConfig.Set("PadMode", savedPadMode);

                Direction = MovementDirection.None;
                try {
                    MoveStopNative();
                } catch {
                    // ignored
                }
                IsWalking = false;

                // Apply endpoint rotation only on clean arrival (not on cancel
                // or Performing - the player may be mid-performance already).
                if (faceDirection is float rot
                    && !cts.IsCancellationRequested
                    && !DalamudApi.Condition[ConditionFlag.Performing]) {
                    GameFunctions.FaceDirectionDeferred(rot.Radians());
                }

                _cts = null;
            },
            stopWhen: () => {
                // var player = DalamudApi.ObjectTable.LocalPlayer;
                // if (player == null) { DalamudApi.PluginLog.Warning("stopWhen: player null"); return true; }
                // if (!DalamudApi.ClientState.IsLoggedIn) { DalamudApi.PluginLog.Warning("stopWhen: not logged in"); return true; }
                // if (cts.IsCancellationRequested) { DalamudApi.PluginLog.Warning("stopWhen: cancelled"); return true; }
                // if (DalamudApi.Condition[ConditionFlag.Performing]) { DalamudApi.PluginLog.Warning("stopWhen: performing"); return true; }
                // var dist = player.Position.Distance2D(destination);
                // DalamudApi.PluginLog.Warning($"stopWhen: dist={dist} precision={precision}");
                // return dist < precision;

                var player = DalamudApi.ObjectTable.LocalPlayer;
                // Stop when: arrived, logged out, cancelled, or Performing active.
                return player == null
                    || !DalamudApi.ClientState.IsLoggedIn
                    || cts.IsCancellationRequested
                    || DalamudApi.Condition[ConditionFlag.Performing]
                    || player.Position.Distance2D(destination) < precision;
            },
            cancellationToken: cts.Token);

        return cts;
    }
}

public enum MovementDirection : int {
    None = 0,
    Forward = 0x141,
    Backward = 0x142,
    RotateLeft = 0x143,
    RotateRight = 0x144,
    StrafeLeft = 0x145,
    StrafeRight = 0x146,
}
