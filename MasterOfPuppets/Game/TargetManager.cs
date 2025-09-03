using System;
using System.Numerics;

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
}
