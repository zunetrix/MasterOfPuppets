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

    public bool TaskInProgress => _pendingTask != null;

    public MovementManager(FollowPath follow) {
        _follow = follow;

        _follow.OnStuck += (dest, fly, range) => {
            var RetryOnStuck = true;
            // !Plugin.Config.RetryOnStuck
            if (!RetryOnStuck)
                return;

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

    public void Update() {
        if (_pendingTask != null && _pendingTask.IsCompleted) {
            try {
                _follow.Move(_pendingTask.Result, !_pendingFly, _pendingDestRange);
            } catch (Exception ex) {
                DalamudApi.PluginLog.Error(ex, $"Update->Move");
            }
            _pendingTask.Dispose();
            _pendingTask = null;
        }
    }

    public async Task<List<Vector3>> QueryPath(Vector3 from, Vector3 to, bool flying, float range = 0) {
        var path = await Task.Run(() => {
            return new List<Vector3> { to };
        });
        return path;
    }

    public async Task<List<Vector3>> QueryPath(List<Vector3> pathPoints) {
        var path = await Task.Run(() => {
            return pathPoints.ToList();
        });
        return path;
    }

    public bool MoveTo(Vector3 dest, bool fly, float range = 0) {
        if (_pendingTask != null) {
            return false;
        }

        // _pendingTask = _manager.QueryPath(DalamudApi.ObjectTable.LocalPlayer?.Position ?? default, dest, fly, range: range);
        _pendingTask = QueryPath(default, dest, fly, range: range);

        _pendingFly = fly;
        _pendingDestRange = range;
        return true;
    }

    public bool MoveToPath(List<Vector3> path, bool fly = false, float range = 0) {
        if (_pendingTask != null) {
            return false;
        }

        _pendingTask = QueryPath(path);

        _pendingFly = fly;
        _pendingDestRange = range;
        return true;
    }

    public void MoveByOffsetPathCommand(List<Vector3> path, Vector3? origin = null, bool fly = false) {
        var baseOrigin =
            origin ??
            DalamudApi.ObjectTable.LocalPlayer?.Position ??
            Vector3.Zero;

        var resolvedPath = path
            .Select(offset => baseOrigin + offset)
            .ToList();

        MoveToPath(resolvedPath);
    }

    private void MoveByOffsetCommand(Vector3 offset, Vector3? origin = null, bool fly = false) {
        var baseOrigin =
            origin ??
            DalamudApi.ObjectTable.LocalPlayer?.Position ??
            Vector3.Zero;

        var destination = baseOrigin + offset;

        // DalamudApi.PluginLog.Warning($"MoveByOffset -> X:{destination.X}, Y:{destination.Y}, Z:{destination.Z}");

        MoveTo(destination, fly);
    }

    public static unsafe Vector3? GetObjectPosition(string objectName) {
        if (string.IsNullOrWhiteSpace(objectName)) {
            DalamudApi.PluginLog.Warning($"Invalid objectName: \"{objectName}\"");
            return null;
        }

        foreach (var actor in DalamudApi.ObjectTable) {
            if (actor == null)
                continue;

            // base name
            var lookupName = actor.Name.TextValue;
            if (lookupName.Length == 0)
                continue;

            // Player: Name@World
            if (actor.ObjectKind == ObjectKind.Player &&
                actor is IPlayerCharacter player &&
                player.HomeWorld.ValueNullable is { } world) {
                lookupName = $"{lookupName}@{world.Name}";
            }

            if (!lookupName.Contains(objectName, StringComparison.InvariantCultureIgnoreCase))
                continue;

            // can target
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

    public static Vector3? GetObjectPosition(ulong objectId) {
        foreach (var actor in DalamudApi.ObjectTable) {
            if (actor != null && actor.GameObjectId == objectId)
                return actor.Position;
        }
        return null;
    }

    public void MoveToObject(ulong objectId) {
        DalamudApi.Framework.RunOnFrameworkThread(delegate {
            var objectPosition = GetObjectPosition(objectId);
            if (objectPosition == null) {
                DalamudApi.PluginLog.Debug($"MoveToObject: Could not find object {objectId}");
                return;
            }
            // DalamudApi.PluginLog.Warning($"objectPosition X:{objectPosition.Value.X}, Y:{objectPosition.Value.Y}, Z:{objectPosition.Value.Z}");

            MoveByOffsetCommand(objectPosition.Value, Vector3.Zero);
        });
    }

    public void MoveToObject(string objectName) {
        DalamudApi.Framework.RunOnFrameworkThread(delegate {
            var objectPosition = GetObjectPosition(objectName);
            if (objectPosition == null) {
                DalamudApi.PluginLog.Debug($"MoveToObject: Could not find object {objectName}");
                return;
            }

            MoveByOffsetCommand(objectPosition.Value, Vector3.Zero);
        });
    }

    public void MoveToPosition(Vector3 position) {
        DalamudApi.Framework.RunOnFrameworkThread(delegate {
            MoveByOffsetCommand(position);
        });
    }

    public void MoveToCoord(Vector3 position) {
        DalamudApi.Framework.RunOnFrameworkThread(delegate {
            MoveByOffsetCommand(position, Vector3.Zero);
        });
    }

    public void MoveToPositionRelative(Vector3 position, string objectName) {
        DalamudApi.Framework.RunOnFrameworkThread(delegate {
            var originPosition = GetObjectPosition(objectName);
            if (originPosition == null) {
                DalamudApi.PluginLog.Debug($"MoveToObject: Could not find object {objectName}");
                return;
            }

            MoveByOffsetCommand(position, originPosition);
        });
    }

    public void MoveToTargetPosition() {
        DalamudApi.Framework.RunOnFrameworkThread(delegate {
            var targetObjectId = GameTargetManager.GetTargetObjectId();
            if (targetObjectId == null) return;

            MoveToObject(targetObjectId.Value);
        });
    }

    // TODO: Find a better way to stop moving when the destination is an unreachable point
    public void StopMove() {
        DalamudApi.Framework.RunOnFrameworkThread(delegate {
            _follow.Stop();
            // var position = new Vector3(0, 0, 0);
            // MoveByOffsetCommand(position);
        });
    }

    public unsafe void SetWalking(bool isWalking) {
        DalamudApi.Framework.RunOnFrameworkThread(delegate {
            var control = Control.Instance();
            if (control == null) return;
            control->IsWalking = isWalking;
        });
    }

    public unsafe void ToggleWalking() {
        DalamudApi.Framework.RunOnFrameworkThread(delegate {
            var control = Control.Instance();
            if (control == null) return;
            control->IsWalking = !control->IsWalking;
        });
    }

    // is walking have a different flag for while autorun is enabled
    public unsafe void SetWalkingAutoRun(bool isWalking) {
        DalamudApi.Framework.RunOnFrameworkThread(delegate {
            var control = Control.Instance();
            if (control == null) return;

            // DalamudApi.PluginLog.Debug($"Control address: {(nint)control:X16}");
            // DalamudApi.PluginLog.Debug($"control: {control->IsWalking} {Marshal.ReadByte((nint)control + 29976)}");
            // doesn't make player walk again during auto-run
            control->IsWalking = true;
            // makes player walk again during both auto-run and manual movement
            int IsWalkingWhileAutoRunOffset = 29976;
            Marshal.WriteByte((nint)control, IsWalkingWhileAutoRunOffset, 0x1);
        });
    }

    public void Rotate(int angle) {
        _follow.Rotate(angle);
    }
}
