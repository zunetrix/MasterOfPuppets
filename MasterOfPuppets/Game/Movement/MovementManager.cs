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
                DalamudApi.PluginLog.Warning($"[MovementManager] Stuck after {MaxStuckRetries} retries, giving up.");
                _stuckRetryCount = 0;
                return;
            }
            _stuckRetryCount++;
            DalamudApi.PluginLog.Warning($"[MovementManager] Stuck, retrying ({_stuckRetryCount}/{MaxStuckRetries})...");
            EnqueueMove(dest, fly, range);
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

    //  Frame update

    /// <summary>Called every frame. Applies a completed path query to FollowPath.</summary>
    public void Update() {
        if (_pendingTask != null && _pendingTask.IsCompleted) {
            try {
                _follow.Move(_pendingTask.Result, !_pendingFly, _pendingDestRange);
            } catch (Exception ex) {
                DalamudApi.PluginLog.Error(ex, "MovementManager.Update");
            }
            _pendingTask.Dispose();
            _pendingTask = null;
        }
    }

    //  Pathfinding (stub)

    /// <summary>
    /// Placeholder for future pathfinding (e.g. VNavMesh).
    /// Currently returns a straight-line single-waypoint path.
    /// </summary>
    public async Task<List<Vector3>> QueryPath(Vector3 from, Vector3 to, bool flying, float range = 0) =>
        await Task.Run(() => new List<Vector3> { to });

    public async Task<List<Vector3>> QueryPath(List<Vector3> pathPoints) =>
        await Task.Run(() => pathPoints.ToList());

    //  Internal movement entry points

    private bool EnqueueMove(Vector3 dest, bool fly = false, float range = 0) {
        if (_pendingTask != null) return false;
        _pendingTask = QueryPath(default, dest, fly, range);
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

    /// <summary>Moves along a path of offset vectors relative to <paramref name="origin"/> (defaults to player position).</summary>

    public void MoveByOffsetPath(List<Vector3> offsets, Vector3? origin = null, bool fly = false) {
        var baseOrigin = origin ?? DalamudApi.ObjectTable.LocalPlayer?.Position ?? Vector3.Zero;
        MoveToPath(offsets.Select(o => baseOrigin + o).ToList(), fly);
    }

    //  Public movement API

    /// <summary>Moves to an absolute world coordinate.</summary>
    public void MoveTo(Vector3 destination, Angle? facing = null) {
        DalamudApi.Framework.RunOnFrameworkThread(() => {
            _follow.DesiredFacing = facing;
            _stuckRetryCount = 0;
            EnqueueMove(destination);
        });
    }

    /// <summary>Moves to <paramref name="origin"/> + <paramref name="offset"/>.</summary>
    public void MoveTo(Vector3 offset, Vector3 origin, Angle? facing = null) {
        DalamudApi.Framework.RunOnFrameworkThread(() => {
            _follow.DesiredFacing = facing;
            _stuckRetryCount = 0;
            EnqueueMove(origin + offset);
        });
    }

    /// <summary>Moves to an offset relative to a named object's position.</summary>
    public void MoveTo(Vector3 offset, string objectName, Angle? facing = null) {
        DalamudApi.Framework.RunOnFrameworkThread(() => {
            var origin = GetObjectPosition(objectName);
            if (origin == null) {
                DalamudApi.PluginLog.Debug($"MoveTo: object not found \"{objectName}\"");
                return;
            }
            _follow.DesiredFacing = facing;
            _stuckRetryCount = 0;
            EnqueueMove(origin.Value + offset);
        });
    }

    /// <summary>Moves to the world position of the object with the given ID.</summary>
    public void MoveTo(ulong objectId) {
        DalamudApi.Framework.RunOnFrameworkThread(() => {
            var pos = GetObjectPosition(objectId);
            if (pos == null) {
                DalamudApi.PluginLog.Debug($"MoveTo: object not found id={objectId}");
                return;
            }
            _stuckRetryCount = 0;
            EnqueueMove(pos.Value);
        });
    }

    /// <summary>Moves to the world position of the first matching object by name.</summary>
    public void MoveTo(string objectName) {
        DalamudApi.Framework.RunOnFrameworkThread(() => {
            var pos = GetObjectPosition(objectName);
            if (pos == null) {
                DalamudApi.PluginLog.Debug($"MoveTo: object not found \"{objectName}\"");
                return;
            }
            _stuckRetryCount = 0;
            EnqueueMove(pos.Value);
        });
    }

    /// <summary>Moves to the current target's world position.</summary>
    public void MoveToTarget() {
        DalamudApi.Framework.RunOnFrameworkThread(() => {
            var targetId = GameTargetManager.GetTargetObjectId();
            if (targetId == null) return;
            MoveTo(targetId.Value);
        });
    }

    /// <summary>
    /// Stops all movement: clears waypoints, deactivates native follow,
    /// and injects zero RMI input for one frame to override any external movement source.
    /// </summary>
    public void StopMove() {
        DalamudApi.Framework.RunOnFrameworkThread(() => {
            _stuckRetryCount = 0;
            GameFunctions.FollowStop();
            _follow.ForceStop();
        });
    }

    /// <summary>Rotates the character to face the given angle without moving.</summary>
    public void FaceDirection(Angle angle) {
        // DalamudApi.Framework.RunOnFrameworkThread(() => GameFunctions.SetFacing(DalamudApi.ObjectTable.LocalPlayer, angle));
        DalamudApi.Framework.RunOnFrameworkThread(() => _follow.FaceDirection(angle));
    }

    //  Native follow

    /// <summary>Activates the game's native follow mode to follow the given entity.</summary>
    public void FollowNative(uint entityId) => GameFunctions.FollowStart(entityId);

    /// <summary>Stops the game's native follow mode.</summary>
    public void StopFollowNative() => GameFunctions.FollowStop();

    //  Walking control

    /// <summary>Enables or disables walk mode.</summary>
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

    /// <summary>Forces walk mode during auto-run as well as manual movement.</summary>
    public unsafe void SetWalkingAutoRun(bool isWalking) {
        DalamudApi.Framework.RunOnFrameworkThread(() => {
            var control = Control.Instance();
            if (control == null) return;
            control->IsWalking = true;
            const int isWalkingWhileAutoRunOffset = 29976;
            Marshal.WriteByte((nint)control, isWalkingWhileAutoRunOffset, 0x1);
        });
    }

    //  Object lookup

    public static unsafe Vector3? GetObjectPosition(string objectName) {
        if (string.IsNullOrWhiteSpace(objectName)) return null;

        foreach (var actor in DalamudApi.ObjectTable) {
            if (actor == null) continue;

            var name = actor.Name.TextValue;
            if (name.Length == 0) continue;

            if (actor.ObjectKind == ObjectKind.Player &&
                actor is IPlayerCharacter player &&
                player.HomeWorld.ValueNullable is { } world)
                name = $"{name}@{world.Name}";

            if (!name.Contains(objectName, StringComparison.InvariantCultureIgnoreCase)) continue;

            try {
                if (!((GameObjectStruct*)actor.Address)->GetIsTargetable()) continue;
            } catch {
                continue;
            }

            return actor.Position;
        }

        return null;
    }

    public static Vector3? GetObjectPosition(ulong objectId) {
        foreach (var actor in DalamudApi.ObjectTable)
            if (actor != null && actor.GameObjectId == objectId)
                return actor.Position;
        return null;
    }
}
