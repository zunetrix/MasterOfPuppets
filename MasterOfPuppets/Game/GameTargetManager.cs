using System;
using System.Linq;
using System.Numerics;

using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;

using FFXIVClientStructs.FFXIV.Client.Game.Character;

using GameObjectStruct = FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject;

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
                    if (!((GameObjectStruct*)actor.Address)->GetIsTargetable())
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
                        || !((GameObjectStruct*)assistActor.Address)->GetIsTargetable()) continue;

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
                    || !((GameObjectStruct*)closestMatch.TargetObject.Address)->GetIsTargetable()
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
}
