using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;

using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Utility.Signatures;
using Dalamud.Game.ClientState.Objects.SubKinds;

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
            // DalamudApi.PluginLog.Information($"Pathfinding complete");
            try {
                _follow.Move(_pendingTask.Result, !_pendingFly, _pendingDestRange);
            } catch (Exception ex) {
                DalamudApi.PluginLog.Error(ex, $"Pathfinding complete");
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

    public bool MoveTo(Vector3 dest, bool fly, float range = 0) {
        if (_pendingTask != null) {
            // DalamudApi.PluginLog.Error($"Pathfinding task is in progress...");
            return false;
        }

        // _pendingTask = _manager.QueryPath(DalamudApi.Objects.LocalPlayer?.Position ?? default, dest, fly, range: range);
        _pendingTask = QueryPath(default, dest, fly, range: range);

        _pendingFly = fly;
        _pendingDestRange = range;
        return true;
    }

    // private static class Signatures {
    //     internal const string ToggleWalk = "38 1D ?? ?? ?? ?? 75 2D";
    // }

    // // set byte 1 to walk
    // private delegate void ToggleWalkDelegate(uint arg1);

    // private static ToggleWalkDelegate? _toggleWalk { get; }

    // static MovementManager() {
    //     if (DalamudApi.SigScanner.TryScanText(Signatures.ToggleWalk, out var _toggleWalkPtr)) {
    //         _toggleWalk = Marshal.GetDelegateForFunctionPointer<ToggleWalkDelegate>(_toggleWalkPtr);
    //     }
    // }

    // TODO: fix signature
    [Signature("38 1D ?? ?? ?? ?? 75 2D", ScanType = ScanType.StaticAddress)]
    private static readonly IntPtr walkingBoolPtr = IntPtr.Zero;
    internal static unsafe bool IsWalking {
        get => walkingBoolPtr != IntPtr.Zero && *(bool*)walkingBoolPtr;
        set {
            if (walkingBoolPtr != IntPtr.Zero) {
                if (value == *(bool*)walkingBoolPtr) return;
                DalamudApi.PluginLog.Warning($"setting walk {value}, current {*(bool*)walkingBoolPtr}");
                *(bool*)walkingBoolPtr = value;
            }
        }
    }

    public static void EnableWalk() {
        IsWalking = true;
    }

    public static void DisableWalk() {
        IsWalking = false;
    }

    public void MoveToCommand(Vector3 offset, Vector3? origin = null, bool fly = false) {
        var baseOrigin =
            origin ??
            DalamudApi.Objects.LocalPlayer?.Position ??
            Vector3.Zero;

        var destination = baseOrigin + offset;
        MoveTo(destination, fly);
    }

    public static unsafe Vector3? GetObjectPosition(string objectName) {
        if (string.IsNullOrWhiteSpace(objectName)) {
            DalamudApi.PluginLog.Warning($"Invalid objectName: \"{objectName}\"");
            return null;
        }

        foreach (var actor in DalamudApi.Objects) {
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
        foreach (var actor in DalamudApi.Objects) {
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

            MoveToCommand(objectPosition.Value, Vector3.Zero);
        });
    }

    public void MoveToObject(string objectName) {
        DalamudApi.Framework.RunOnFrameworkThread(delegate {
            var objectPosition = GetObjectPosition(objectName);
            if (objectPosition == null) {
                DalamudApi.PluginLog.Debug($"MoveToObject: Could not find object {objectName}");
                return;
            }

            MoveToCommand(objectPosition.Value, Vector3.Zero);
        });
    }

    public void MoveToPosition(Vector3 position) {
        DalamudApi.Framework.RunOnFrameworkThread(delegate {
            MoveToCommand(position);
        });
    }

    public void MoveToPositionRelative(Vector3 position, string objectName) {
        DalamudApi.Framework.RunOnFrameworkThread(delegate {
            var originPosition = GetObjectPosition(objectName);
            if (originPosition == null) {
                DalamudApi.PluginLog.Debug($"MoveToObject: Could not find object {objectName}");
                return;
            }

            MoveToCommand(position, originPosition);
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
            var position = new Vector3(0, 0, 0);
            MoveToCommand(position);
        });
    }
}
