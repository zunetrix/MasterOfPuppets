using System;
using System.Collections.Generic;
using System.Numerics;

using Dalamud.Game.ClientState.Conditions;
using Dalamud.Plugin.Services;

using FFXIVClientStructs.FFXIV.Client.Game;

namespace MasterOfPuppets.Movement;

public class FollowPath : IDisposable {
    private Plugin Plugin { get; }

    // --- Movement config ---
    public bool MovementAllowed = true;
    public bool IgnoreDeltaY = false;

    /// <summary>Distance threshold to advance past a waypoint.</summary>
    public float Tolerance = 0.25f;

    /// <summary>
    /// When > 0, clears all remaining waypoints once the player is within this distance of the final destination.
    /// Useful for "close enough" stops without following every intermediate waypoint exactly.
    /// </summary>
    public float DestinationTolerance = 0;

    // --- Precision mode ---
    /// <summary>
    /// When enabled, the last waypoint is approached in small steps for accurate stopping.
    /// Activated via <see cref="Move"/> with <c>precisionMode: true</c>.
    /// </summary>
    public bool PrecisionMode = false;

    /// <summary>Stop distance for the final waypoint when <see cref="PrecisionMode"/> is on.</summary>
    public float FinalTolerance = 0.02f;

    /// <summary>Maximum movement step per frame when approaching the final waypoint in precision mode.</summary>
    public float FinalStepSize = 0.05f;

    // --- Facing after arrival ---
    /// <summary>
    /// When set, the character rotates to face this direction (radians) after reaching the destination.
    /// Cleared automatically once the player's rotation is within <see cref="FacingTolerance"/>.
    /// </summary>
    public Angle? DesiredFacing;

    /// <summary>Angular tolerance (radians) to consider facing complete. Default ~3 degrees.</summary>
    public float FacingTolerance = 0.05f;

    public List<Vector3> Waypoints = new();

    public event Action<Vector3, bool, float>? OnStuck;

    private readonly OverrideCamera _camera = new();
    private readonly OverrideMovement _movement = new();
    private DateTime _nextJump;
    private Vector3? _posPreviousFrame;
    private int _msWithNoSignificantMovement = 0;
    private Vector3? _facingTarget;

    public FollowPath(Plugin plugin) {
        Plugin = plugin;
    }

    public void Dispose() {
        _camera.Dispose();
        _movement.Dispose();
    }

    public void Update(IFramework fwk) {
        var player = DalamudApi.ObjectTable.LocalPlayer;
        if (player == null) return;

        // --- Advance past waypoints already traversed ---
        // Requires a previous frame position to have a valid movement vector.
        // Stops at Count == 1 so the final waypoint is handled by precision/facing logic below.
        while (Waypoints.Count > 1 && _posPreviousFrame.HasValue) {
            var a = Waypoints[0];
            var b = player.Position;
            var c = _posPreviousFrame.Value;

            if (DestinationTolerance > 0 && (b - Waypoints[^1]).Length() <= DestinationTolerance) {
                Waypoints.Clear();
                break;
            }

            if (IgnoreDeltaY) { a.Y = 0; b.Y = 0; c.Y = 0; }

            if (DistanceToLineSegment(a, b, c) > Tolerance)
                break;

            Waypoints.RemoveAt(0);
        }

        // --- Waypoints exhausted: rotate to face direction or stop fully ---
        if (Waypoints.Count == 0) {
            _posPreviousFrame = player.Position;
            if (DesiredFacing.HasValue)
                ApplyFacing(player.Position, player.Rotation);
            else
                StopMovementAndCamera();
            return;
        }

        // --- Stuck detection ---
        if (Plugin.Config.StopOnStuck && _posPreviousFrame.HasValue) {
            float dt = Math.Max(fwk.UpdateDelta.Milliseconds / 1000f, 0.0001f);
            float speed = Vector3.Distance(player.Position, _posPreviousFrame.Value) / dt;

            if (speed <= Plugin.Config.StuckTolerance)
                _msWithNoSignificantMovement += fwk.UpdateDelta.Milliseconds;
            else
                _msWithNoSignificantMovement = 0;

            if (_msWithNoSignificantMovement >= Plugin.Config.StuckTimeoutMs) {
                var destination = Waypoints[^1];
                Stop();
                OnStuck?.Invoke(destination, !IgnoreDeltaY, DestinationTolerance);
                return;
            }
        }

        _posPreviousFrame = player.Position;

        // --- Cancel movement if player pressed a key ---
        if (Plugin.Config.CancelMoveOnUserInput && _movement.UserInput) {
            Stop();
            return;
        }

        // --- Compute next desired position ---
        var target = Waypoints[0];

        if (PrecisionMode && Waypoints.Count == 1) {
            // Final waypoint in precision mode: step toward it gradually
            var delta = target - player.Position;
            if (IgnoreDeltaY) delta.Y = 0;
            var dist = delta.Length();

            if (dist <= FinalTolerance) {
                Waypoints.Clear();
                if (DesiredFacing.HasValue)
                    ApplyFacing(player.Position, player.Rotation);
                else
                    StopMovementAndCamera();
                return;
            }

            var nextPos = player.Position + Vector3.Normalize(delta) * MathF.Min(dist, FinalStepSize);
            if (IgnoreDeltaY) nextPos.Y = player.Position.Y;
            _movement.DesiredPosition = nextPos;
        } else {
            if (IgnoreDeltaY) target.Y = player.Position.Y;
            _movement.DesiredPosition = target;
        }

        // --- Flying: spam jump to take off from mount if needed ---
        if (_movement.DesiredPosition.Y > player.Position.Y
            && !DalamudApi.Condition[ConditionFlag.InFlight]
            && !DalamudApi.Condition[ConditionFlag.Diving]
            && !IgnoreDeltaY) {
            if (DalamudApi.Condition[ConditionFlag.Mounted])
                ExecuteJump();
            else {
                _movement.Enabled = false;
                return;
            }
        }

        _movement.Enabled = MovementAllowed;

        // --- Align camera toward movement direction ---
        _camera.Enabled = Plugin.Config.AlignCameraToMovement;
        _camera.SpeedH = _camera.SpeedV = 360.Degrees();
        _camera.DesiredAzimuth = Angle.FromDirectionXZ(_movement.DesiredPosition - player.Position) + 180.Degrees();
        _camera.DesiredAltitude = Plugin.Config.AlignCameraHeight.Degrees();
    }

    /// <summary>
    /// Maintains a tiny movement target in <see cref="DesiredFacing"/> direction so the
    /// character rotates in place without actually walking anywhere.
    /// Clears <see cref="DesiredFacing"/> and stops once within <see cref="FacingTolerance"/>.
    /// </summary>
    private void ApplyFacing(Vector3 playerPos, float playerRotation) {
        var desiredDir = DesiredFacing!.Value.ToDirectionXZ();

        // Set a fixed target once per activation. Using a small offset (0.1 m) keeps
        // character movement negligible while still sending a movement input so the game
        // engine rotates the character. The target is NOT updated each frame so the
        // Precision guard in OverrideMovement can actually terminate the walk.
        _facingTarget ??= playerPos + desiredDir * 0.1f;
        _movement.DesiredPosition = _facingTarget.Value;
        _movement.Enabled = true;

        _camera.Enabled = Plugin.Config.AlignCameraToMovement;
        _camera.SpeedH = _camera.SpeedV = 360.Degrees();
        _camera.DesiredAzimuth = DesiredFacing.Value + 180.Degrees();

        var diff = (playerRotation.Radians() - DesiredFacing.Value).Normalized().Abs().Rad;
        if (diff <= FacingTolerance) {
            DesiredFacing = null;
            _facingTarget = null;
            StopMovementAndCamera();
        }
    }

    /// <summary>Disables movement and camera overrides.</summary>
    private void StopMovementAndCamera() {
        _movement.Enabled = _camera.Enabled = false;
        _camera.SpeedH = _camera.SpeedV = default;
        _movement.DesiredPosition = DalamudApi.ObjectTable.LocalPlayer?.Position ?? Vector3.Zero;
    }

    /// <summary>
    /// Starts following the given waypoint list.
    /// </summary>
    /// <param name="waypoints">World-space positions to visit in order.</param>
    /// <param name="ignoreDeltaY">If true, Y axis is ignored (ground/2D movement).</param>
    /// <param name="destTolerance">Stop early when within this distance of the final waypoint (0 = exact).</param>
    /// <param name="facing">Optional direction to face after reaching the destination.</param>
    public void Move(List<Vector3> waypoints, bool ignoreDeltaY, float destTolerance = 0, Angle? facing = null) {
        Waypoints = waypoints;
        IgnoreDeltaY = ignoreDeltaY;
        DestinationTolerance = destTolerance;
        DesiredFacing = facing;
        _facingTarget = null;
    }

    /// <summary>
    /// Rotates the character to face the given angle without moving.
    /// Only takes effect when no waypoints are active (or after they finish).
    /// </summary>
    public void FaceDirection(Angle angle) {
        DesiredFacing = angle;
        _facingTarget = null; // reset so ApplyFacing picks a fresh target
    }

    /// <summary>Stops all movement, clears waypoints and any pending facing rotation.</summary>
    public void Stop() {
        _msWithNoSignificantMovement = 0;
        Waypoints.Clear();
        DesiredFacing = null;
        _facingTarget = null;
        StopMovementAndCamera();
    }

    private unsafe void ExecuteJump() {
        if (DalamudApi.Condition[ConditionFlag.Diving]) return;
        if (DateTime.Now >= _nextJump) {
            ActionManager.Instance()->UseAction(ActionType.GeneralAction, 2);
            _nextJump = DateTime.Now.AddMilliseconds(100);
        }
    }

    /// <summary>
    /// Returns the shortest distance from point <paramref name="v"/> to the line segment
    /// from <paramref name="a"/> (previous frame) to <paramref name="b"/> (current frame).
    /// Used to decide whether the player has passed a waypoint.
    /// </summary>
    private static float DistanceToLineSegment(Vector3 v, Vector3 a, Vector3 b) {
        var ab = b - a;
        var av = v - a;
        if (ab.Length() == 0 || Vector3.Dot(av, ab) <= 0) return av.Length();
        var bv = v - b;
        if (Vector3.Dot(bv, ab) >= 0) return bv.Length();
        return Vector3.Cross(ab, av).Length() / ab.Length();
    }
}
