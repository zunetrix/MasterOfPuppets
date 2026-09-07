using System;

using Dalamud.Hooking;

namespace MasterOfPuppets.Movement;

internal sealed class ForwardInputMovementController : IDisposable {
    private delegate byte PlayerMoveDelegate(nint a1, int direction);
    private Hook<PlayerMoveDelegate>? _playerMoveHook;
    private MovementDirection _direction;

    public MovementDirection Direction {
        get => _direction;
        set {
            if (_direction == value)
                return;

            _direction = value;
            if (value == MovementDirection.None) {
                _playerMoveHook?.Disable();
                return;
            }

            EnsureHook();
            _playerMoveHook!.Enable();
        }
    }

    // Hook creation is intentionally lazy. Constructing native hooks while Dalamud
    // is replacing an old plugin instance can deadlock a hot reload. By waiting
    // until movement is actually requested, the old instance has completed its
    // unload and all of its hooks have already been disposed.
    private void EnsureHook() {
        if (_playerMoveHook != null)
            return;

        _playerMoveHook = DalamudApi.GameInteropProvider.HookFromSignature<PlayerMoveDelegate>(
            "E8 ?? ?? ?? ?? 4C 63 4B 04",
            PlayerMoveDetour);
    }

    public void Dispose() {
        Direction = MovementDirection.None;
        _playerMoveHook?.Disable();
        _playerMoveHook?.Dispose();
        _playerMoveHook = null;
    }

    public void MoveForward() {
        Direction = MovementDirection.Forward;
    }

    public void Move(MovementDirection direction) {
        Direction = direction;
    }

    public void Stop() {
        Direction = MovementDirection.None;
    }

    private byte PlayerMoveDetour(nint a1, int dir) {
        var original = _playerMoveHook!.Original(a1, dir);
        return _direction != MovementDirection.None && dir == (int)_direction
            ? (byte)1
            : original;
    }
}
