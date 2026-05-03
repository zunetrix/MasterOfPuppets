using System;
using System.Linq;
using System.Numerics;

using Dalamud.Game.ClientState.Objects.Types;

using FFXIVClientStructs.FFXIV.Client.Game.Object;

using MasterOfPuppets.Extensions.Dalamud;
using MasterOfPuppets.Util;

using BattleChara = FFXIVClientStructs.FFXIV.Client.Game.Character.BattleChara;
using ObjectKind = Dalamud.Game.ClientState.Objects.Enums.ObjectKind;

namespace MasterOfPuppets;

public static class GameTargetManager {

    internal static unsafe void TargetNearestObjectInternal(Func<IGameObject, bool> match) {
        DalamudApi.Framework.RunOnFrameworkThread(() => {
            var player = DalamudApi.ObjectTable.LocalPlayer;
            if (player == null) return;

            IGameObject? closest = null;
            var closestDistSq = float.MaxValue;

            foreach (var actor in DalamudApi.ObjectTable) {
                if (actor == null) continue;
                if (!match(actor)) continue;

                try {
                    if (!((GameObject*)actor.Address)->GetIsTargetable()) continue;
                } catch {
                    continue;
                }

                var distSq = actor.DistanceSquaredTo(player);
                if (distSq >= closestDistSq) continue;

                closest = actor;
                closestDistSq = distSq;
            }

            if (closest != null)
                DalamudApi.TargetManager.Target = closest;
        });
    }

    public static void TargetNearestAetheryte() {
        TargetNearestObjectInternal(actor => actor.ObjectKind == ObjectKind.Aetheryte);
    }

    public static void TargetObject(string objectName) {
        if (string.IsNullOrWhiteSpace(objectName)) {
            DalamudApi.PluginLog.Warning($"Invalid target: \"{objectName}\"");
            return;
        }

        TargetNearestObjectInternal(actor =>
            actor.Name.TextValue.Contains(objectName, StringComparison.InvariantCultureIgnoreCase));
    }

    public static void TargetObject(ulong objectId) {
        TargetNearestObjectInternal(actor => actor.GameObjectId == objectId);
    }

    public static unsafe void TargetOf(string assistName) {
        if (string.IsNullOrWhiteSpace(assistName)) {
            DalamudApi.PluginLog.Warning($"Invalid target of: \"{assistName}\"");
            return;
        }

        DalamudApi.Framework.RunOnFrameworkThread(() => {
            var player = DalamudApi.ObjectTable.LocalPlayer;
            if (player == null) return;

            IGameObject? closestMatch = null;
            var closestDistSq = float.MaxValue;

            foreach (var actor in DalamudApi.ObjectTable) {
                if (actor == null) continue;

                var lookupName = actor.Name.TextValue;
                if (actor.ObjectKind == ObjectKind.Pc) {
                    lookupName = actor.GetPlayerNameWorld();
                }

                if (!lookupName.Contains(assistName, StringComparison.InvariantCultureIgnoreCase)) continue;

                try {
                    if (!((GameObject*)actor.Address)->GetIsTargetable()) continue;
                } catch {
                    continue;
                }

                var distSq = actor.DistanceSquaredTo(player);
                if (distSq >= closestDistSq) continue;

                closestMatch = actor;
                closestDistSq = distSq;
            }

            if (closestMatch?.TargetObject == null) return;

            try {
                if (!((GameObject*)closestMatch.TargetObject.Address)->GetIsTargetable()) return;
            } catch {
                return;
            }

            DalamudApi.PluginLog.Debug($"targeting: {closestMatch.TargetObject.Name.TextValue}");
            DalamudApi.TargetManager.Target = closestMatch.TargetObject;
        });
    }

    public static void TargetClear() {
        DalamudApi.Framework.RunOnFrameworkThread(() => {
            DalamudApi.TargetManager.Target = null;
        });
    }

    public static unsafe void TargetMyMinion() {
        DalamudApi.Framework.RunOnFrameworkThread(() => {
            var localPlayer = DalamudApi.ObjectTable.LocalPlayer;
            if (localPlayer == null) return;

            var minion = ((BattleChara*)localPlayer.Address)->CompanionData.CompanionObject;
            if (minion == null || minion->Character.BaseId == 0) return;

            var minionObj = DalamudApi.ObjectTable.FirstOrDefault(o => o.Address == (nint)minion);
            if (minionObj == null) return;

            DalamudApi.TargetManager.Target = minionObj;
        });
    }

    public static Vector3? GetTargetPosition() =>
        DalamudApi.ObjectTable.LocalPlayer?.TargetObject?.Position;

    public static Vector3 GetTargetOffsetFromMe() {
        var target = DalamudApi.ObjectTable.LocalPlayer?.TargetObject?.Position;
        var origin = DalamudApi.ObjectTable.LocalPlayer?.Position;
        if (!target.HasValue || !origin.HasValue) return default;
        return target.Value - origin.Value;
    }

    public static ulong? GetTargetObjectId() =>
        DalamudApi.ObjectTable.LocalPlayer?.TargetObject?.GameObjectId;

    public static string GetTargetName() {
        var target = DalamudApi.ObjectTable.LocalPlayer?.TargetObject;
        if (target == null)
            return string.Empty;

        return target.GetPlayerNameWorld() ?? target.Name.TextValue;
    }

    public static void InteractWithTarget() {
        DalamudApi.Framework.RunOnTick(() => {
            DalamudApi.ObjectTable.LocalPlayer?.TargetObject?.Interact();
        });
    }

    public static void InteractWithMyTarget(ulong objectId) {
        TargetThenInteract(() => TargetObject(objectId));
    }

    internal static void TargetThenInteract(Action setTarget, Action? afterInteract = null) {
        setTarget();
        var ok = false;
        Coroutine.StartRunOnFramework(
            runFunction: () => { },
            stopWhen: () => ok = DalamudApi.ObjectTable.LocalPlayer?.TargetObject != null,
            callback: () => {
                if (!ok) return;
                var target = DalamudApi.ObjectTable.LocalPlayer?.TargetObject;
                if (target == null) return;
                target.Interact();
                if (afterInteract == null) return;
                Coroutine.StartRunOnFramework(
                    runFunction: () => { },
                    timeoutMs: 500,
                    callback: () => DalamudApi.Framework.RunOnTick(afterInteract));
            },
            timeoutMs: 2000);
    }
}
