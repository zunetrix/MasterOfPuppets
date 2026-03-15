using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.SubKinds;

using FFXIVClientStructs.FFXIV.Client.Game.Control;

using GameObjectStruct = FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject;

namespace MasterOfPuppets.Movement;

public class MovementManager : IDisposable {
    private readonly FollowPath _follow;
    private Task<List<Vector3>>? _pendingTask;
    private bool _pendingFly;
    private float _pendingDestRange;

    private int _stuckRetryCount;
    private const int MaxStuckRetries = 3;

    public bool TaskInProgress => _pendingTask != null;

    public MovementManager(FollowPath follow) {
        _follow = follow;

        _follow.OnStuck += (dest, fly, range) => {
            if (_stuckRetryCount >= MaxStuckRetries) {
                DalamudApi.PluginLog.Warning($"[MovementManager] Stuck at destination after {MaxStuckRetries} retries, giving up.");
                _stuckRetryCount = 0;
                return;
            }
            _stuckRetryCount++;
            DalamudApi.PluginLog.Warning($"[MovementManager] Stuck, retrying ({_stuckRetryCount}/{MaxStuckRetries})...");
            MoveTo(dest, fly, range);
        };
    }

    public void Dispose() {
        if (_pendingTask != null) {
            if (!_pendingTask.IsCompleted)
                _pendingTask.Wait();
            _pendingTask.Dispose();
            _pendingTask = null;
        }
    }

    /// <summary>
    /// Called every frame. Applies a resolved path from a pending <see cref="QueryPath"/> task
    /// to <see cref="FollowPath"/> once it completes.
    /// </summary>
    public void Update() {
        if (_pendingTask != null && _pendingTask.IsCompleted) {
            try {
                _follow.Move(_pendingTask.Result, !_pendingFly, _pendingDestRange);
            } catch (Exception ex) {
                DalamudApi.PluginLog.Error(ex, "Update->Move");
            }
            _pendingTask.Dispose();
            _pendingTask = null;
        }
    }

    /// <summary>
    /// Placeholder for future pathfinding integration (e.g. VNavMesh).
    /// Currently returns a straight-line path of one waypoint.
    /// </summary>
    public async Task<List<Vector3>> QueryPath(Vector3 from, Vector3 to, bool flying, float range = 0) {
        return await Task.Run(() => new List<Vector3> { to });
    }

    /// <summary>
    /// Placeholder for future pathfinding integration.
    /// Currently returns the provided points unchanged.
    /// </summary>
    public async Task<List<Vector3>> QueryPath(List<Vector3> pathPoints) {
        return await Task.Run(() => pathPoints.ToList());
    }

    /// <summary>Enqueues a straight-line move to an absolute world position.</summary>
    public bool MoveTo(Vector3 dest, bool fly, float range = 0) {
        if (_pendingTask != null) return false;
        _pendingTask = QueryPath(default, dest, fly, range: range);
        _pendingFly = fly;
        _pendingDestRange = range;
        return true;
    }

    /// <summary>Enqueues movement along an explicit multi-waypoint path.</summary>
    public bool MoveToPath(List<Vector3> path, bool fly = false, float range = 0) {
        if (_pendingTask != null) return false;
        _stuckRetryCount = 0;
        _pendingTask = QueryPath(path);
        _pendingFly = fly;
        _pendingDestRange = range;
        return true;
    }

    /// <summary>
    /// Moves along a path of offset vectors, each resolved relative to <paramref name="origin"/>
    /// (defaults to the player's current position if null).
    /// </summary>
    public void MoveByOffsetPath(List<Vector3> offsets, Vector3? origin = null, bool fly = false) {
        var baseOrigin = origin ?? DalamudApi.ObjectTable.LocalPlayer?.Position ?? Vector3.Zero;
        MoveToPath(offsets.Select(o => baseOrigin + o).ToList(), fly);
    }

    // -----------------------------------------------------------------------
    // Internal helper: resolves destination = origin + offset, then calls MoveTo.
    // origin: null = player position (relative move), Vector3.Zero = world origin (absolute coord).
    // -----------------------------------------------------------------------
    private void MoveByOffset(Vector3 offset, Vector3? origin = null, bool fly = false) {
        var baseOrigin = origin ?? DalamudApi.ObjectTable.LocalPlayer?.Position ?? Vector3.Zero;
        _stuckRetryCount = 0;
        MoveTo(baseOrigin + offset, fly);
    }

    // -----------------------------------------------------------------------
    // Object / position lookup
    // -----------------------------------------------------------------------

    /// <summary>
    /// Finds the world position of the first targetable object whose name contains
    /// <paramref name="objectName"/> (case-insensitive). For players, matches "Name@World".
    /// Returns null if not found.
    /// </summary>
    public static unsafe Vector3? GetObjectPosition(string objectName) {
        if (string.IsNullOrWhiteSpace(objectName)) {
            DalamudApi.PluginLog.Warning($"Invalid objectName: \"{objectName}\"");
            return null;
        }

        foreach (var actor in DalamudApi.ObjectTable) {
            if (actor == null) continue;

            var lookupName = actor.Name.TextValue;
            if (lookupName.Length == 0) continue;

            if (actor.ObjectKind == ObjectKind.Player &&
                actor is IPlayerCharacter player &&
                player.HomeWorld.ValueNullable is { } world)
                lookupName = $"{lookupName}@{world.Name}";

            if (!lookupName.Contains(objectName, StringComparison.InvariantCultureIgnoreCase))
                continue;

            try {
                if (!((GameObjectStruct*)actor.Address)->GetIsTargetable())
                    continue;
            } catch {
                continue;
            }

            return actor.Position;
        }

        return null;
    }

    /// <summary>Returns the world position of the object with the given game object ID, or null.</summary>
    public static Vector3? GetObjectPosition(ulong objectId) {
        foreach (var actor in DalamudApi.ObjectTable)
            if (actor != null && actor.GameObjectId == objectId)
                return actor.Position;
        return null;
    }

    // -----------------------------------------------------------------------
    // Public movement API
    // -----------------------------------------------------------------------

    /// <summary>
    /// Moves to a position offset from the player's current location.
    /// X = left(+) / right(-), Y = up(+) / down(-) [inactive], Z = forward(+) / back(-).
    /// Optionally faces <paramref name="facing"/> direction (degrees) after arriving.
    /// </summary>
    public void MoveToPosition(Vector3 offset, Angle? facing = null) {
        DalamudApi.Framework.RunOnFrameworkThread(() => {
            _follow.DesiredFacing = facing;
            MoveByOffset(offset);
        });
    }

    /// <summary>
    /// Moves to an absolute world coordinate (origin = world zero).
    /// Optionally faces <paramref name="facing"/> direction (degrees) after arriving.
    /// </summary>
    public void MoveToCoord(Vector3 worldPosition, Angle? facing = null) {
        DalamudApi.Framework.RunOnFrameworkThread(() => {
            _follow.DesiredFacing = facing;
            MoveByOffset(worldPosition, Vector3.Zero);
        });
    }

    /// <summary>
    /// Moves to a position offset relative to <paramref name="objectName"/>'s current location.
    /// Optionally faces <paramref name="facing"/> direction (degrees) after arriving.
    /// </summary>
    public void MoveToPositionRelative(Vector3 offset, string objectName, Angle? facing = null) {
        DalamudApi.Framework.RunOnFrameworkThread(() => {
            var origin = GetObjectPosition(objectName);
            if (origin == null) {
                DalamudApi.PluginLog.Debug($"MoveToPositionRelative: object not found \"{objectName}\"");
                return;
            }
            _follow.DesiredFacing = facing;
            MoveByOffset(offset, origin);
        });
    }

    /// <summary>Moves to the world position of the object with the given ID.</summary>
    public void MoveToObject(ulong objectId) {
        DalamudApi.Framework.RunOnFrameworkThread(() => {
            var pos = GetObjectPosition(objectId);
            if (pos == null) {
                DalamudApi.PluginLog.Debug($"MoveToObject: object not found id={objectId}");
                return;
            }
            MoveByOffset(pos.Value, Vector3.Zero);
        });
    }

    /// <summary>Moves to the world position of the first matching object by name.</summary>
    public void MoveToObject(string objectName) {
        DalamudApi.Framework.RunOnFrameworkThread(() => {
            var pos = GetObjectPosition(objectName);
            if (pos == null) {
                DalamudApi.PluginLog.Debug($"MoveToObject: object not found \"{objectName}\"");
                return;
            }
            MoveByOffset(pos.Value, Vector3.Zero);
        });
    }

    /// <summary>Moves to the current target's world position.</summary>
    public void MoveToTargetPosition() {
        DalamudApi.Framework.RunOnFrameworkThread(() => {
            var targetId = GameTargetManager.GetTargetObjectId();
            if (targetId == null) return;
            MoveToObject(targetId.Value);
        });
    }

    /// <summary>Immediately stops all movement and clears any pending waypoints.</summary>
    public void StopMove() {
        DalamudApi.Framework.RunOnFrameworkThread(() => {
            _stuckRetryCount = 0;
            _follow.Stop();
        });
    }

    /// <summary>
    /// Rotates the character to face the given angle (in degrees) without moving.
    /// Takes effect immediately if not currently moving, or after the current move completes.
    /// </summary>
    public void FaceDirection(Angle angle) {
        DalamudApi.Framework.RunOnFrameworkThread(() => {
            _follow.FaceDirection(angle);
        });
    }

    // -----------------------------------------------------------------------
    // Walking control
    // -----------------------------------------------------------------------

    /// <summary>Enables or disables walk mode (character walks instead of runs).</summary>
    public unsafe void SetWalking(bool isWalking) {
        DalamudApi.Framework.RunOnFrameworkThread(() => {
            var control = Control.Instance();
            if (control == null) return;
            control->IsWalking = isWalking;
        });
    }

    /// <summary>Toggles walk/run mode.</summary>
    public unsafe void ToggleWalking() {
        DalamudApi.Framework.RunOnFrameworkThread(() => {
            var control = Control.Instance();
            if (control == null) return;
            control->IsWalking = !control->IsWalking;
        });
    }

    /// <summary>
    /// Forces walk mode to apply during auto-run as well as manual movement.
    /// Uses a secondary memory offset that controls walking while auto-run is active.
    /// </summary>
    public unsafe void SetWalkingAutoRun(bool isWalking) {
        DalamudApi.Framework.RunOnFrameworkThread(() => {
            var control = Control.Instance();
            if (control == null) return;
            control->IsWalking = true;
            const int IsWalkingWhileAutoRunOffset = 29976;
            Marshal.WriteByte((nint)control, IsWalkingWhileAutoRunOffset, 0x1);
        });
    }
}
