using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;

namespace MasterOfPuppets.Movement;

public class AsyncMoveRequest : IDisposable {
    private readonly FollowPath _follow;
    private Task<List<Vector3>>? _pendingTask;
    private bool _pendingFly;
    private float _pendingDestRange;

    public bool TaskInProgress => _pendingTask != null;

    public AsyncMoveRequest(FollowPath follow) {
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
        // DalamudApi.PluginLog.Debug($"Kicking off pathfind from {from} to {to}");

        var path = await Task.Run(() => {
            return new List<Vector3> { to };
        });

        // DalamudApi.PluginLog.Debug($"Pathfinding done: {path.Count} waypoints");
        return path;
    }

    public bool MoveTo(Vector3 dest, bool fly, float range = 0) {
        if (_pendingTask != null) {
            // DalamudApi.PluginLog.Error($"Pathfinding task is in progress...");
            return false;
        }

        // var toleranceStr = range > 0 ? $" within {range}y" : "";
        // _pendingTask = _manager.QueryPath(DalamudApi.Objects.LocalPlayer?.Position ?? default, dest, fly, range: range);
        _pendingTask = QueryPath(DalamudApi.Objects.LocalPlayer?.Position ?? default, dest, fly, range: range);

        _pendingFly = fly;
        _pendingDestRange = range;
        return true;
    }

    public void MoveToCommand(Vector3 dest, Vector3 origin = new(), bool relativeToPlayer = true, bool fly = false) {
        if (relativeToPlayer) {
            var originActor = relativeToPlayer ? DalamudApi.Objects.LocalPlayer : null;
            origin = originActor?.Position ?? new();
        }
        var offset = dest;

        MoveTo(origin + offset, fly);
    }
}
