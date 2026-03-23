using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;

using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.Types;

using MasterOfPuppets.Movement;
using MasterOfPuppets.Util;

namespace MasterOfPuppets;

internal class EasterHatchingTide : SeasonalEventRunner {

    // Cluster action - AoE around player (Pruning Pirouette)
    private const uint ClusterActionId = 45127;
    // Line action - fires a beam facing the last object (Roaring Eggscapade)
    private const uint LineActionId = 42039;

    private const float GroupRadiusSq = 12f * 12f; // objects ≤ 12u apart belong to the same group
    private const float ArrivalRadiusSq = 3.5f * 3.5f;
    private const float LineMaxDeviation = 1.5f;     // max XZ perpendicular deviation for collinearity
    private const int LineMinCount = 4;
    private const int PostActionDelayMs = 2500;
    private const int ArrivalTimeoutMs = 15_000;
    private const int FaceDelayMs = 200;
    private const int AreaTransitionTimeoutMs = 30_000;
    private const int TimeLimitCheckMs = 160_000; // start checking EasterMowingResult after 2:40
    private const float ExitDistance = 43f;

    protected override HashSet<ushort> ValidTerritories { get; } = [1336];

    private Vector3 _spawnPosition;
    private float _spawnRotation;

    protected override void OnStart() {
        DalamudApi.ClientState.MapIdChanged += OnMapIdChanged;
    }

    protected override void OnStop() {
        DalamudApi.ClientState.MapIdChanged -= OnMapIdChanged;
    }

    protected override void AddJoinSteps() {
        AddStepWaitNotBetweenAreas();           // ensure fully loaded
        AddStepInteractWithNpc(1056067);        // interact with Hatching-tide registrator (polls until targetable)
        AddStepSelectString(0);                 // select first option to enter the event area
        AddStepWaitInValidTerritory();          // wait until territory 1336 is loaded
    }

    protected override void AddLeaveSteps() {
        AddStepWaitNotBetweenAreas();
    }

    protected override async Task RunActivityLoop(CancellationToken ct) {
        DalamudApi.PluginLog.Debug("[EasterHatchingTide] Plant clear loop started.");
        var sw = Stopwatch.StartNew();

        // Linked CTS so the dialog watcher can cancel the loop independently of Stop().
        using var timerCts = CancellationTokenSource.CreateLinkedTokenSource(ct);

        // Independent per-frame watcher: checks every framework tick once the time
        // window opens. Cancels timerCts the moment the dialog becomes visible -
        // regardless of what the main loop is currently waiting on.
        Coroutine.StartRunOnFramework(
            runFunction: () => { },
            stopWhen: () => {
                if (sw.ElapsedMilliseconds < TimeLimitCheckMs) return false;
                return GameDialogManager.IsAddonReady(GameDialogManager.AddonName.EasterMowingResult);
            },
            callback: () => {
                DalamudApi.PluginLog.Warning("[EasterHatchingTide] EasterMowingResult visible - clicking Leave.");
                GameDialogManager.ClickEasterMowingLeave();
                timerCts.Cancel();
            },
            cancellationToken: timerCts.Token);

        try {
            while (!timerCts.Token.IsCancellationRequested) {
                if (!ValidTerritories.Contains(DalamudApi.ClientState.TerritoryType)) {
                    DalamudApi.PluginLog.Debug("[EasterHatchingTide] Not in a valid territory - stopping.");
                    break;
                }

                List<IGameObject>? objects = null;
                var playerPos = Vector3.Zero;
                await DalamudApi.Framework.RunOnFrameworkThread(() => {
                    var player = DalamudApi.ObjectTable.LocalPlayer;
                    if (player == null) return;
                    playerPos = player.Position;
                    objects = ScanEventObjects();
                });

                if (objects == null) {
                    // Player still loading - retry shortly.
                    await Task.Delay(700, timerCts.Token);
                    continue;
                }

                if (objects.Count == 0) {
                    var exitPoint = ComputeExitPoint();
                    DalamudApi.PluginLog.Debug($"[EasterHatchingTide] Area cleared - moving to exit {exitPoint}.");
                    Plugin.MovementManager.MoveTo(exitPoint);
                    await WaitForNewObjects(timerCts.Token);
                    continue;
                }

                DalamudApi.PluginLog.Debug($"[EasterHatchingTide] {objects.Count} event objects found.");

                var groups = GroupByProximity(objects);

                // Classify each group and find its effective move target first,
                // then pick the group whose target is actually closest to the player.
                var best = groups
                    .Select(g => {
                        if (IsLine(g, playerPos, out var lFirst, out var lLast))
                            return (isLine: true, target: lFirst.Position, lineFirst: lFirst, lineLast: lLast, group: g);
                        var centroid = g.Aggregate(Vector3.Zero, (acc, o) => acc + o.Position) / g.Count;
                        var nearestObj = g.OrderBy(o => Vector3.DistanceSquared(o.Position, centroid)).First();
                        return (isLine: false, target: nearestObj.Position, lineFirst: nearestObj, lineLast: nearestObj, group: g);
                    })
                    .OrderBy(x => Vector3.DistanceSquared(x.target, playerPos))
                    .First();

                if (best.isLine) {
                    DalamudApi.PluginLog.Debug(
                        $"[EasterHatchingTide] Line ({best.group.Count} objs): move to {best.lineFirst.Position}, face {best.lineLast.Position}");

                    Plugin.MovementManager.MoveTo(best.lineFirst.Position);
                    await WaitUntilArrived(best.lineFirst.Position, timerCts.Token);

                    var faceDir = best.lineLast.Position - best.lineFirst.Position;
                    var faceAngle = new Angle(MathF.Atan2(faceDir.X, faceDir.Z));
                    Plugin.MovementManager.FaceDirection(faceAngle);
                    await Task.Delay(FaceDelayMs, timerCts.Token);

                    DalamudApi.PluginLog.Debug($"[EasterHatchingTide] Using line action ({LineActionId}).");
                    GameActionManager.UseAction(LineActionId);
                } else {
                    DalamudApi.PluginLog.Debug(
                        $"[EasterHatchingTide] Cluster ({best.group.Count} objs): move to {best.target}");

                    Plugin.MovementManager.MoveTo(best.target);
                    await WaitUntilArrived(best.target, timerCts.Token);

                    DalamudApi.PluginLog.Debug($"[EasterHatchingTide] Using cluster action ({ClusterActionId}).");
                    GameActionManager.UseAction(ClusterActionId);
                }

                await Task.Delay(PostActionDelayMs, timerCts.Token);
            }
        } catch (OperationCanceledException) {
            // normal stop (either Stop() or time-limit dialog)
        } catch (Exception ex) {
            DalamudApi.PluginLog.Error(ex, "[EasterHatchingTide] ClearLoop error.");
        } finally {
            Plugin.MovementManager.StopMove();
            DalamudApi.PluginLog.Debug("[EasterHatchingTide] Plant clear loop finished.");
        }
    }

    private void OnMapIdChanged(uint mapId) {
        var player = DalamudApi.ObjectTable.LocalPlayer;
        if (player == null) return;
        _spawnPosition = player.Position;
        _spawnRotation = player.Rotation;
        var exit = ComputeExitPoint();
        DalamudApi.PluginLog.Debug($"[EasterHatchingTide] Map changed to ({mapId}), spawn={_spawnPosition}, rot={_spawnRotation:F2}rad, exit={exit}");
    }

    // 43 units forward from spawn in the direction the player faced on map entry.
    // FFXIV rotation: 0=south(+Z), π=north(-Z); forward = (sin(r), 0, cos(r)).
    private Vector3 ComputeExitPoint() {
        var forward = new Vector3(MathF.Sin(_spawnRotation), 0f, MathF.Cos(_spawnRotation));
        return _spawnPosition + forward * ExitDistance;
    }

    // Waits until the player reaches the target position; condition evaluated on the framework thread.
    private static Task WaitUntilArrived(Vector3 target, CancellationToken ct) {
        var tcs = new TaskCompletionSource();
        var arrived = false;
        Coroutine.StartRunOnFramework(
            runFunction: () => { },
            stopWhen: () => {
                var p = DalamudApi.ObjectTable.LocalPlayer;
                if (p != null && Vector3.DistanceSquared(p.Position, target) <= ArrivalRadiusSq) {
                    arrived = true;
                    return true;
                }
                return p == null;
            },
            callback: () => {
                if (!arrived && !ct.IsCancellationRequested)
                    DalamudApi.PluginLog.Debug($"[EasterHatchingTide] Timeout waiting to reach {target}.");
                tcs.TrySetResult();
            },
            timeoutMs: ArrivalTimeoutMs,
            cancellationToken: ct
        );
        return tcs.Task;
    }

    // Waits on the framework thread until new event objects appear (signals teleport to next sub-area).
    private static Task WaitForNewObjects(CancellationToken ct) {
        var tcs = new TaskCompletionSource();
        Coroutine.StartRunOnFramework(
            runFunction: () => { },
            stopWhen: () => ScanEventObjects().Count > 0,
            callback: () => {
                DalamudApi.PluginLog.Debug("[EasterHatchingTide] New objects detected - next sub-area loaded.");
                tcs.TrySetResult();
            },
            timeoutMs: AreaTransitionTimeoutMs,
            cancellationToken: ct
        );
        return tcs.Task;
    }

    private static List<IGameObject> ScanEventObjects() {
        var result = new List<IGameObject>();
        foreach (var actor in DalamudApi.ObjectTable) {
            if (actor?.ObjectKind == ObjectKind.CardStand)
                result.Add(actor);
        }
        return result;
    }

    private static List<List<IGameObject>> GroupByProximity(List<IGameObject> objects) {
        var groups = new List<List<IGameObject>>();
        var assigned = new bool[objects.Count];

        for (var i = 0; i < objects.Count; i++) {
            if (assigned[i]) continue;

            var group = new List<IGameObject> { objects[i] };
            assigned[i] = true;

            var queue = new Queue<int>();
            queue.Enqueue(i);

            while (queue.Count > 0) {
                var curr = queue.Dequeue();
                for (var j = 0; j < objects.Count; j++) {
                    if (assigned[j]) continue;
                    if (Vector3.DistanceSquared(objects[curr].Position, objects[j].Position) <= GroupRadiusSq) {
                        assigned[j] = true;
                        group.Add(objects[j]);
                        queue.Enqueue(j);
                    }
                }
            }

            groups.Add(group);
        }

        return groups;
    }

    private static bool IsLine(
        List<IGameObject> group,
        Vector3 playerPos,
        out IGameObject first,
        out IGameObject last) {

        first = group[0];
        last = group[0];

        if (group.Count < LineMinCount) return false;

        var pts = group.Select(o => new Vector2(o.Position.X, o.Position.Z)).ToList();

        var (p1, p2) = GetFurthestPair(pts);
        var dir = Vector2.Normalize(p2 - p1);
        var perp = new Vector2(-dir.Y, dir.X);

        foreach (var p in pts) {
            if (MathF.Abs(Vector2.Dot(p - p1, perp)) > LineMaxDeviation) return false;
        }

        var sorted = group
            .OrderBy(o => Vector2.Dot(new Vector2(o.Position.X, o.Position.Z) - p1, dir))
            .ToList();

        var playerXZ = new Vector2(playerPos.X, playerPos.Z);
        var endA = new Vector2(sorted[0].Position.X, sorted[0].Position.Z);
        var endB = new Vector2(sorted[^1].Position.X, sorted[^1].Position.Z);

        if (Vector2.DistanceSquared(playerXZ, endA) <= Vector2.DistanceSquared(playerXZ, endB)) {
            first = sorted[0];
            last = sorted[^1];
        } else {
            first = sorted[^1];
            last = sorted[0];
        }

        return true;
    }

    private static (Vector2, Vector2) GetFurthestPair(List<Vector2> pts) {
        var (p1, p2) = (pts[0], pts[0]);
        var maxDist = 0f;

        for (var i = 0; i < pts.Count; i++) {
            for (var j = i + 1; j < pts.Count; j++) {
                var d = Vector2.DistanceSquared(pts[i], pts[j]);
                if (d > maxDist) { maxDist = d; p1 = pts[i]; p2 = pts[j]; }
            }
        }

        return (p1, p2);
    }
}
