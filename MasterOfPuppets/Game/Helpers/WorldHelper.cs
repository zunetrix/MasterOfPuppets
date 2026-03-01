using System;
using System.Linq;

using Lumina.Excel.Sheets;

namespace MasterOfPuppets;

public static class WorldHelper {
    public static World? GetWorld(string worldName) {
        var world = DalamudApi.DataManager.GetExcelSheet<World>(DalamudApi.ClientState.ClientLanguage)
        .FirstOrDefault(i => string.Equals(i.Name.ToString(), worldName, StringComparison.OrdinalIgnoreCase));

        var isWorldFound = world.RowId > 0;
        return isWorldFound ? world : null;
    }

    public static World? GetWorld(uint worldId) {
        return DalamudApi.DataManager.GetExcelSheet<World>(DalamudApi.ClientState.ClientLanguage).GetRowOrDefault(worldId);
    }

    public static uint? GetWorldId(string worldName) {
        var world = GetWorld(worldName);
        return world == null ? null : world.Value.RowId;
    }
}
