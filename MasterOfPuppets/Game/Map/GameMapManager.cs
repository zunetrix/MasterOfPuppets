using System.Numerics;

using FFXIVClientStructs.FFXIV.Client.UI.Agent;

namespace MasterOfPuppets;

public static unsafe class GameMapManager {

    public static Vector2? GetFlagPosition() {
        var map = AgentMap.Instance();
        if (map == null || map->FlagMarkerCount == 0)
            return null;

        var marker = map->FlagMapMarkers[0];
        return new(marker.XFloat, marker.YFloat);
    }

    public static void SetFlagMapMarker(uint territoryId, uint mapId, Vector2 position) {
        var agentMap = AgentMap.Instance();
        // remove flag
        agentMap->FlagMarkerCount = 0;
        var worldPosition = new Vector3(position.X, 0, position.Y);
        agentMap->SetFlagMapMarker(territoryId, mapId, worldPosition);

        // agentMap->OpenMap(mapId, territoryId);
        // agentMap->SetFlagMapMarker(TerritoryId, MapId, worldPos);
        // agentMap->SetFlagMapMarker(agentMap->CurrentTerritoryId, agentMap->CurrentMapId, worldPos);

        // DalamudApi.Framework.RunOnTick(() => {
        //     AgentChatLog.Instance()->InsertTextCommandParam(1048, false);
        // });
        // }
    }
}
