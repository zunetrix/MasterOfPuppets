using System;
using System.Linq;
using System.Numerics;

using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;

using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Character;

using MasterOfPuppets.Util;

using GameObjectStruct = FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject;

namespace MasterOfPuppets;

public static class TargetManager {
    public static unsafe void TargetByName(string targetName) {
        try {
            if (string.IsNullOrWhiteSpace(targetName)) {
                DalamudApi.PluginLog.Warning($"Invalid target: \"{targetName}\"");
                return;
            }

            DalamudApi.Framework.RunOnFrameworkThread(delegate {
                // find target by name
                IGameObject closestMatch = null;
                var closestDistance = float.MaxValue;
                var player = DalamudApi.Objects.LocalPlayer;
                if (player == null) return;

                foreach (var actor in DalamudApi.Objects) {
                    if (actor == null) continue;
                    if (!actor.Name.TextValue.Contains(targetName, StringComparison.InvariantCultureIgnoreCase)
                        || !((GameObjectStruct*)actor.Address)->GetIsTargetable()) continue;

                    var distance = Vector3.Distance(player.Position, actor.Position);
                    if (closestMatch == null) {
                        closestMatch = actor;
                        closestDistance = distance;
                        continue;
                    }

                    if (!(closestDistance > distance)) continue;
                    closestMatch = actor;
                    closestDistance = distance;
                }

                if (closestMatch == null) return;

                // DalamudApi.PluginLog.Debug($"targeting: {closestMatch.Name.TextValue}");
                DalamudApi.Targets.Target = closestMatch;
            });
        } catch (Exception e) {
            DalamudApi.PluginLog.Error(e, $"Error while targeting \"{targetName}\"");
        }
    }

    public static unsafe void TargetByObjectId(ulong targetObjectId) {
        try {
            DalamudApi.Framework.RunOnFrameworkThread(delegate {
                // find target by object id
                IGameObject closestMatch = null;
                var closestDistance = float.MaxValue;
                var player = DalamudApi.Objects.LocalPlayer;
                if (player == null) return;

                foreach (var actor in DalamudApi.Objects) {
                    if (actor == null) continue;
                    if (actor.GameObjectId != targetObjectId
                        || !((GameObjectStruct*)actor.Address)->GetIsTargetable()) continue;

                    var distance = Vector3.Distance(player.Position, actor.Position);
                    if (closestMatch == null) {
                        closestMatch = actor;
                        closestDistance = distance;
                        continue;
                    }

                    if (!(closestDistance > distance)) continue;
                    closestMatch = actor;
                    closestDistance = distance;
                }

                if (closestMatch == null) return;
                DalamudApi.Targets.Target = closestMatch;
            });
        } catch (Exception e) {
            DalamudApi.PluginLog.Error(e, $"Error while targeting");
        }
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
                var player = DalamudApi.Objects.LocalPlayer;
                if (player == null) return;

                // foreach (var assistActor in DalamudApi.Objects.Where(o => o.ObjectKind == ObjectKind.Player))
                // var assistActor = DalamudApi.Objects.AsEnumerable().FirstOrDefault(o => o.Name.TextValue.Equals(_objectName));
                foreach (var assistActor in DalamudApi.Objects) {
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
                    // || closestMatch.TargetObjectId == DalamudApi.Objects.LocalPlayer.GameObjectId
                    ) {
                    return;
                }

                DalamudApi.PluginLog.Debug($"targeting: {closestMatch.TargetObject.Name.TextValue}");
                DalamudApi.Targets.Target = closestMatch.TargetObject;
            });
        } catch (Exception e) {
            DalamudApi.PluginLog.Error(e, $"Error target of \"{assistName}\"");
        }
    }

    public static void TargetClear() {
        try {
            DalamudApi.Framework.RunOnFrameworkThread(delegate {
                DalamudApi.Targets.Target = null;
            });
        } catch (Exception e) {
            DalamudApi.PluginLog.Error(e, $"Error while cleaning target");
        }
    }

    public static unsafe void TargetMyMinion() {
        try {
            DalamudApi.Framework.RunOnFrameworkThread(delegate {
                var localPlayer = DalamudApi.Objects.LocalPlayer;
                if (localPlayer == null) return;

                var c = (BattleChara*)localPlayer.Address;
                if (c == null) return;

                var minion = c->CompanionData.CompanionObject;
                if (minion == null) return;

                if (minion->Character.BaseId == 0) return;

                var minionObj = DalamudApi.Objects
                    .FirstOrDefault(o => o.Address == (nint)minion);

                if (minionObj == null) return;

                DalamudApi.Targets.Target = minionObj;
            });
        } catch (Exception e) {
            DalamudApi.PluginLog.Error(e, $"Error while targeting my minion");
        }
    }
}
