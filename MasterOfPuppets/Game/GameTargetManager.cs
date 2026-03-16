using System;
using System.Linq;
using System.Numerics;

using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;
using ObjectKind = Dalamud.Game.ClientState.Objects.Enums.ObjectKind;

using FFXIVClientStructs.FFXIV.Client.Game.Control;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using BattleChara = FFXIVClientStructs.FFXIV.Client.Game.Character.BattleChara;
using System.Threading.Tasks;
// using GameObjectStruct = FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject;

namespace MasterOfPuppets;

public static class GameTargetManager {

    private static unsafe void TargetObjectInternal(Func<IGameObject, bool> match) {
        DalamudApi.Framework.RunOnFrameworkThread(() => {
            var player = DalamudApi.ObjectTable.LocalPlayer;
            if (player == null)
                return;

            IGameObject? closest = null;
            var closestDistanceSq = float.MaxValue;

            foreach (var actor in DalamudApi.ObjectTable) {
                if (actor == null)
                    continue;

                if (!match(actor))
                    continue;

                // unsafe
                try {
                    if (!((GameObject*)actor.Address)->GetIsTargetable())
                        continue;
                } catch {
                    continue;
                }

                var delta = actor.Position - player.Position;
                var distSq = delta.LengthSquared();

                if (distSq >= closestDistanceSq)
                    continue;

                closest = actor;
                closestDistanceSq = distSq;
            }

            if (closest != null)
                DalamudApi.TargetManager.Target = closest;
        });
    }

    public static void TargetObject(string objectName) {
        if (string.IsNullOrWhiteSpace(objectName)) {
            DalamudApi.PluginLog.Warning($"Invalid target: \"{objectName}\"");
            return;
        }

        TargetObjectInternal(actor =>
            actor.Name.TextValue.Contains(objectName, StringComparison.InvariantCultureIgnoreCase));
    }

    public static void TargetObject(ulong objectId) {
        TargetObjectInternal(actor => actor.GameObjectId == objectId);
    }

    public static unsafe void TargetOf(string assistName) {
        try {
            if (string.IsNullOrWhiteSpace(assistName)) {
                DalamudApi.PluginLog.Warning($"Invalid target of: \"{assistName}\"");
                return;
            }

            DalamudApi.Framework.RunOnFrameworkThread(delegate {
                // find target by name
                IGameObject closestMatch = null;
                var closestDistance = float.MaxValue;
                var player = DalamudApi.ObjectTable.LocalPlayer;
                if (player == null) return;

                // foreach (var assistActor in DalamudApi.ObjectTable.Where(o => o.ObjectKind == ObjectKind.Player))
                // var assistActor = DalamudApi.ObjectTable.AsEnumerable().FirstOrDefault(o => o.Name.TextValue.Equals(_objectName));
                foreach (var assistActor in DalamudApi.ObjectTable) {
                    if (assistActor == null) continue;

                    // if player concat world name to prevent same characters names conflict
                    var lookupName = assistActor.Name.TextValue;
                    if (assistActor.ObjectKind == ObjectKind.Player) {
                        var playerObject = assistActor as IPlayerCharacter;
                        var playerWorld = playerObject.HomeWorld.ValueNullable?.Name;
                        // var playerWorld = playerObject.HomeWorld.ValueNullable?.Name.ToDalamudString().TextValue;
                        if (playerWorld != null)
                            lookupName = $"{lookupName}@{playerWorld}";
                    }
                    // DalamudApi.PluginLog.Warning($"TargetOf: \"{lookupName}\"");

                    if (!lookupName.Contains(assistName, StringComparison.InvariantCultureIgnoreCase)
                        || !((GameObject*)assistActor.Address)->GetIsTargetable()) continue;

                    var distance = Vector3.Distance(player.Position, assistActor.Position);
                    if (closestMatch == null) {
                        closestMatch = assistActor;
                        closestDistance = distance;
                        continue;
                    }

                    if (!(closestDistance > distance)) continue;
                    closestMatch = assistActor;
                    closestDistance = distance;
                }

                if (closestMatch == null) return;
                if (closestMatch.TargetObject == null
                    || !((GameObject*)closestMatch.TargetObject.Address)->GetIsTargetable()
                    // || closestMatch.TargetObjectId == DalamudApi.ObjectTable.LocalPlayer.GameObjectId
                    ) {
                    return;
                }

                DalamudApi.PluginLog.Debug($"targeting: {closestMatch.TargetObject.Name.TextValue}");
                DalamudApi.TargetManager.Target = closestMatch.TargetObject;
            });
        } catch (Exception e) {
            DalamudApi.PluginLog.Error(e, $"Error target of \"{assistName}\"");
        }
    }

    public static void TargetClear() {
        DalamudApi.Framework.RunOnFrameworkThread(delegate {
            DalamudApi.TargetManager.Target = null;
        });
    }

    public static unsafe void TargetMyMinion() {
        DalamudApi.Framework.RunOnFrameworkThread(delegate {
            var localPlayer = DalamudApi.ObjectTable.LocalPlayer;
            if (localPlayer == null) return;

            var c = (BattleChara*)localPlayer.Address;
            if (c == null) return;

            var minion = c->CompanionData.CompanionObject;
            if (minion == null) return;

            if (minion->Character.BaseId == 0) return;

            var minionObj = DalamudApi.ObjectTable
                .FirstOrDefault(o => o.Address == (nint)minion);

            // var ownerObject = DalamudApi.ObjectTable.PlayerObjects.FirstOrDefault(obj => obj.EntityId == targetId);

            if (minionObj == null) return;

            DalamudApi.TargetManager.Target = minionObj;
        });
    }

    public static Vector3? GetTargetPosition() {
        return DalamudApi.ObjectTable.LocalPlayer?.TargetObject?.Position;
    }

    public static Vector3 GetTargetOffsetFromMe() {
        var target = DalamudApi.ObjectTable.LocalPlayer?.TargetObject?.Position;
        var origin = DalamudApi.ObjectTable.LocalPlayer?.Position;
        if (!target.HasValue || !origin.HasValue)
            return default;

        var offset = target.Value - origin.Value;

        return offset;
    }

    public static ulong? GetTargetObjectId() {
        return DalamudApi.ObjectTable.LocalPlayer?.TargetObject?.GameObjectId;
    }

    public static string GetTargetName() {
        return DalamudApi.ObjectTable.LocalPlayer?.TargetObject?.Name?.TextValue ?? string.Empty;
    }

    public static unsafe void InteractWithTarget() {
        DalamudApi.Framework.RunOnTick(() => {
            var target = DalamudApi.ObjectTable.LocalPlayer?.TargetObject.Address;
            if (target == null) return;
            TargetSystem.Instance()->InteractWithObject((GameObject*)target.Value, false);
        });
    }

    public async static void InteractWithMyTarget(ulong objectId) {
        TargetObject(objectId);
        await Task.Delay(500);

        _ = DalamudApi.Framework.RunOnTick(() => {
            var target = DalamudApi.ObjectTable.LocalPlayer?.TargetObject.Address;
            if (target == null) return;
            unsafe {
                TargetSystem.Instance()->InteractWithObject((GameObject*)target.Value, false);
            }
        });
    }

    internal static IGameObject GetNearestEntrance(out float Distance) {
        var currentDistance = float.MaxValue;
        IGameObject currentObject = null;

        foreach (var x in DalamudApi.ObjectTable) {
            if (x.IsTargetable && Langstrings.Entrance.Any(r => r.IsMatch(x.Name.TextValue))) {
                var distance = Vector3.Distance(DalamudApi.ObjectTable.LocalPlayer.Position, x.Position);
                if (distance < currentDistance) {
                    currentDistance = distance;
                    currentObject = x;

                    Distance = currentDistance;
                    return currentObject;
                }
            }
        }
        Distance = currentDistance;
        return currentObject;
    }

    // public static (ulong Cid, string Name, string HomeWorld, string FullName)? GetTargetPlayerInfo() {
    //     var target = DalamudApi.TargetManager.Target;
    //     if (target == null || target.ObjectKind != ObjectKind.Player)
    //         return null;

    //     var player = target as IPlayerCharacter;
    //     if (player == null) return null;

    //     var world = player.HomeWorld.ValueNullable?.Name;
    //     ulong targetCid = player.????;
    //     string targetName = player.Name.TextValue;
    //     string targetHomeWorld = world != null ? $"{world}" : "";
    //     string targetFullName = $"{player.Name.TextValue}@{world}";

    //     return (targetCid, targetName, targetHomeWorld, targetFullName);
    // }
}
