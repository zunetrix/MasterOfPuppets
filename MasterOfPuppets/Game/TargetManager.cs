using System;
using System.Numerics;

using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;

using GameObjectStruct = FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject;

namespace MasterOfPuppets;

public static class TargetManager
{
    public static unsafe void TargetByName(string targetName)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(targetName))
            {
                DalamudApi.PluginLog.Warning($"Invalid target: \"{targetName}\"");
                return;
            }

            DalamudApi.Framework.RunOnFrameworkThread(delegate
            {
                // find target by name
                IGameObject closestMatch = null;
                var closestDistance = float.MaxValue;
                var player = DalamudApi.ClientState.LocalPlayer;
                if (player == null) return;

                foreach (var actor in DalamudApi.Objects)
                {
                    if (actor == null) continue;
                    if (!actor.Name.TextValue.Contains(targetName, StringComparison.InvariantCultureIgnoreCase)
                        || !((GameObjectStruct*)actor.Address)->GetIsTargetable()) continue;

                    var distance = Vector3.Distance(player.Position, actor.Position);
                    if (closestMatch == null)
                    {
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
        }
        catch (Exception e)
        {
            DalamudApi.PluginLog.Error(e, $"Error while targeting \"{targetName}\"");
        }
    }

    public static unsafe void TargetByObjectId(ulong targetObjectId)
    {
        try
        {
            DalamudApi.Framework.RunOnFrameworkThread(delegate
            {
                // find target by object id
                IGameObject closestMatch = null;
                var closestDistance = float.MaxValue;
                var player = DalamudApi.ClientState.LocalPlayer;
                if (player == null) return;

                foreach (var actor in DalamudApi.Objects)
                {
                    if (actor == null) continue;
                    if (actor.GameObjectId != targetObjectId
                        || !((GameObjectStruct*)actor.Address)->GetIsTargetable()) continue;

                    var distance = Vector3.Distance(player.Position, actor.Position);
                    if (closestMatch == null)
                    {
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
        }
        catch (Exception e)
        {
            DalamudApi.PluginLog.Error(e, $"Error while targeting");
        }
    }

    public static unsafe void TargetOf(string assistName)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(assistName))
            {
                DalamudApi.PluginLog.Warning($"Invalid target of: \"{assistName}\"");
                return;
            }

            DalamudApi.Framework.RunOnFrameworkThread(delegate
            {
                // find target by name
                IGameObject closestMatch = null;
                var closestDistance = float.MaxValue;
                var player = DalamudApi.ClientState.LocalPlayer;
                if (player == null) return;

                // foreach (var assistActor in DalamudApi.Objects.Where(o => o.ObjectKind == ObjectKind.Player))
                // var assistActor = DalamudApi.Objects.AsEnumerable().FirstOrDefault(o => o.Name.TextValue.Equals(_objectName));
                foreach (var assistActor in DalamudApi.Objects)
                {
                    if (assistActor == null) continue;

                    // if player concat world name to prevent same characters names conflict
                    var lookupName = assistActor.Name.TextValue;
                    if (assistActor.ObjectKind == ObjectKind.Player)
                    {
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
                    if (closestMatch == null)
                    {
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
                    // || closestMatch.TargetObjectId == DalamudApi.ClientState.LocalPlayer.GameObjectId
                    )
                {
                    return;
                }

                DalamudApi.Targets.Target = closestMatch.TargetObject;
            });
        }
        catch (Exception e)
        {
            DalamudApi.PluginLog.Error(e, $"Error target of \"{assistName}\"");
        }
    }

    public static unsafe void TargetClear()
    {
        try
        {
            DalamudApi.Framework.RunOnFrameworkThread(delegate
            {
                DalamudApi.Targets.Target = null;
            });
        }
        catch (Exception e)
        {
            DalamudApi.PluginLog.Error(e, $"Error while cleaning target");
        }
    }

}
