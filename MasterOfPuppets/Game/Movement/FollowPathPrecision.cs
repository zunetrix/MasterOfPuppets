using System;
using System.Collections.Generic;
using System.Numerics;

using Dalamud.Plugin.Services;

using FFXIVClientStructs.FFXIV.Client.Game;

namespace MasterOfPuppets.Movement;

public class FollowPathPrecision : IDisposable {
    private Plugin Plugin { get; }
    public bool MovementAllowed = true;
    public bool IgnoreDeltaY = false;
    public float PathTolerance = 0.25f;
    public float FinalTolerance = 0.02f;
    public float FinalStepSize = 0.05f;
    public float MinFinalDistance = 0.01f;

    public float DestinationTolerance = 0;
    public List<Vector3> Waypoints = new();

    private readonly OverrideCamera _camera = new();
    private readonly OverrideMovement _movement = new();
    private DateTime _nextJump;

    private Vector3? posPreviousFrame;

    private int _millisecondsWithNoSignificantMovement = 0;

    public event Action<Vector3, bool, float>? OnStuck;

    public FollowPathPrecision(Plugin plugin) {
        Plugin = plugin;
    }

    public void Dispose() {
        _camera.Dispose();
        _movement.Dispose();
    }

    public void Update(IFramework fwk) {
        var player = DalamudApi.ObjectTable.LocalPlayer;
        if (player == null)
            return;

        while (Waypoints.Count > 1 && posPreviousFrame.HasValue) {
            var a = Waypoints[0];
            var b = player.Position;
            var c = posPreviousFrame.Value;

            if (IgnoreDeltaY) {
                a.Y = 0;
                b.Y = 0;
                c.Y = 0;
            }

            if (DistanceToLineSegment(a, b, c) > PathTolerance)
                break;

            Waypoints.RemoveAt(0);
        }

        if (Waypoints.Count == 0) {
            posPreviousFrame = player.Position;
            _movement.Enabled = _camera.Enabled = false;
            _camera.SpeedH = _camera.SpeedV = default;
            _movement.DesiredPosition = player.Position;
        } else {
            if (Plugin.Config.StopOnStuck && posPreviousFrame.HasValue) {
                float delta = fwk.UpdateDelta.Milliseconds / 1000f;
                float distanceMoved = Vector3.Distance(player.Position, posPreviousFrame.Value) / Math.Max(delta, 0.0001f);

                if (distanceMoved <= Plugin.Config.StuckTolerance)
                    _millisecondsWithNoSignificantMovement += fwk.UpdateDelta.Milliseconds;
                else
                    _millisecondsWithNoSignificantMovement = 0;

                if (_millisecondsWithNoSignificantMovement >= Plugin.Config.StuckTimeoutMs) {
                    var destination = Waypoints[^1];
                    Stop();
                    OnStuck?.Invoke(destination, !IgnoreDeltaY, DestinationTolerance);
                    return;
                }
            }

            posPreviousFrame = player.Position;

            if (Plugin.Config.CancelMoveOnUserInput && _movement.UserInput) {
                Stop();
                return;
            }

            var target = Waypoints[0];
            var deltaVec = target - player.Position;
            var distance = deltaVec.Length();

            // -------------------------------
            // LAST WAYPOINT PRECISION
            // -------------------------------
            if (Waypoints.Count == 1) {
                var targetFinal = Waypoints[0];
                var delta = targetFinal - player.Position;

                if (IgnoreDeltaY)
                    delta.Y = 0;

                var dist = delta.Length();

                if (dist <= FinalTolerance) {
                    Stop();
                    return;
                }

                var dir = Vector3.Normalize(delta);

                float step = MathF.Min(dist, FinalStepSize);

                var nextPos = player.Position + dir * step;

                if (IgnoreDeltaY)
                    nextPos.Y = player.Position.Y;

                _movement.Enabled = MovementAllowed;
                _movement.DesiredPosition = nextPos;
            } else {
                // -------------------------------
                // WAYPOINT NORMAL
                // -------------------------------
                _movement.Enabled = MovementAllowed;

                if (IgnoreDeltaY)
                    target.Y = player.Position.Y;

                _movement.DesiredPosition = target;
            }

            // -------------------------------
            // FLY CHECK
            // -------------------------------
            if (_movement.DesiredPosition.Y > player.Position.Y &&
                !DalamudApi.Condition[Dalamud.Game.ClientState.Conditions.ConditionFlag.InFlight] &&
                !DalamudApi.Condition[Dalamud.Game.ClientState.Conditions.ConditionFlag.Diving] &&
                !IgnoreDeltaY) {

                if (DalamudApi.Condition[Dalamud.Game.ClientState.Conditions.ConditionFlag.Mounted])
                    ExecuteJump();
                else {
                    _movement.Enabled = false;
                    return;
                }
            }

            // -------------------------------
            // CAMERA
            // -------------------------------
            _camera.Enabled = Plugin.Config.AlignCameraToMovement;
            _camera.SpeedH = _camera.SpeedV = 360.Degrees();
            _camera.DesiredAzimuth =
                Angle.FromDirectionXZ(_movement.DesiredPosition - player.Position) + 180.Degrees();
            _camera.DesiredAltitude = Plugin.Config.AlignCameraHeight.Degrees();
        }
    }

    private static float DistanceToLineSegment(Vector3 v, Vector3 a, Vector3 b) {
        var ab = b - a;
        var av = v - a;

        if (ab.Length() == 0 || Vector3.Dot(av, ab) <= 0)
            return av.Length();

        var bv = v - b;
        if (Vector3.Dot(bv, ab) >= 0)
            return bv.Length();

        return Vector3.Cross(ab, av).Length() / ab.Length();
    }

    public void Stop() {
        _millisecondsWithNoSignificantMovement = 0;
        Waypoints.Clear();
    }

    private unsafe void ExecuteJump() {
        // Unable to jump while diving, prevents spamming error messages.
        if (DalamudApi.Condition[Dalamud.Game.ClientState.Conditions.ConditionFlag.Diving])
            return;

        if (DateTime.Now >= _nextJump) {
            ActionManager.Instance()->UseAction(ActionType.GeneralAction, 2);
            _nextJump = DateTime.Now.AddMilliseconds(100);
        }
    }

    public void Move(List<Vector3> waypoints, bool ignoreDeltaY, float destTolerance = 0.02f) {
        Waypoints = waypoints;
        IgnoreDeltaY = ignoreDeltaY;
        DestinationTolerance = destTolerance;
    }
}
