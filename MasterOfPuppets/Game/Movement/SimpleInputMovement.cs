using System;
using System.Numerics;

using Dalamud.Hooking;
using Dalamud.Utility.Signatures;

namespace MasterOfPuppets.Movement;

public class SimpleInputMovement : IDisposable {

    /// <summary>Current injected direction. Set to None to stop.</summary>
    public MovementDirection Direction {
        get => _direction;
        set {
            _direction = value;
            if (value == MovementDirection.None)
                _hook?.Disable();
            else
                _hook?.Enable();
        }
    }

    // -------------------------------------------------------------------------
    // Hook
    // The hook intercepts every movement input poll.  When our direction is
    // active, we return 1 (key held) for that direction regardless of actual
    // keyboard state - identical to what the game sees when you hold a key.
    // -------------------------------------------------------------------------
    private delegate byte PlayerMoveDelegate(nint a1, int direction);

    [Signature("E8 ?? ?? ?? ?? 4C 63 4B 04")]
    private readonly nint _hookAddress;

    private Hook<PlayerMoveDelegate>? _hook;
    private MovementDirection _direction;

    public SimpleInputMovement() {
        DalamudApi.GameInteropProvider.InitializeFromAttributes(this);

        _hook = DalamudApi.GameInteropProvider.HookFromAddress<PlayerMoveDelegate>(
            _hookAddress,
            (a1, dir) => {
                var original = _hook.Original(a1, dir);
                // If this poll is asking about our injected direction, return 1
                // (pressed) regardless of what the keyboard actually says.
                if (_direction != MovementDirection.None && dir == (int)_direction)
                    return 1;
                return original;
            });

        // Hook starts disabled; enabled automatically when Direction is set.
    }

    public void Dispose() {
        Direction = MovementDirection.None;
        _hook?.Dispose();
        _hook = null;
    }

    // -------------------------------------------------------------------------
    // helpers
    // -------------------------------------------------------------------------

    /// <summary>
    /// Picks the best discrete direction to move toward <paramref name="destination"/>
    /// and sets it.  Call every frame while moving; call Stop() on arrival.
    /// </summary>
    public void MoveToward(Vector3 destination, bool legacyMode = false) {
        var player = DalamudApi.ObjectTable.LocalPlayer;
        if (player == null) return;

        var delta = destination - player.Position;
        if (new Vector2(delta.X, delta.Z).LengthSquared() < 0.01f) {
            Stop();
            return;
        }

        // Compute angle from player rotation to destination (XZ plane only).
        var targetAngle = Angle.FromDirectionXZ(delta);
        var refAngle = player.Rotation.Radians();
        var relative = (targetAngle - refAngle).Normalized();

        // Map the relative angle to the nearest discrete direction.
        // ±45° = Forward, ±135° = Backward, sides = strafe.
        Direction = relative.Deg switch {
            > -45 and <= 45 => MovementDirection.Forward,
            > 45 and <= 135 => MovementDirection.StrafeRight,
            > -135 and <= -45 => MovementDirection.StrafeLeft,
            _ => MovementDirection.Backward,
        };
    }

    /// <summary>Stops all injected movement.</summary>
    public void Stop() => Direction = MovementDirection.None;
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
