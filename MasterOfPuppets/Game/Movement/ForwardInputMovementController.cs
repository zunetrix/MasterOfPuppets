using System;

using Dalamud.Hooking;

namespace MasterOfPuppets.Movement;

internal unsafe sealed class ForwardInputMovementController : IDisposable {
    private delegate byte PlayerMoveDelegate(nint a1, int direction);
    private Hook<PlayerMoveDelegate>? _playerMoveHook;
    private MovementDirection _direction;

    public MovementDirection Direction {
        get => _direction;
        set {
            _direction = value;
            if (value == MovementDirection.None)
                _playerMoveHook?.Disable();
            else
                _playerMoveHook?.Enable();
        }
    }

    public ForwardInputMovementController() {
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
