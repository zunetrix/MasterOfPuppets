using System;
using System.Collections.Generic;
using System.Linq;

using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;

namespace MasterOfPuppets;

public static class GearSetHelper {
    public unsafe static List<ExecutableAction> GetAllowedItems() {
        var raptureGearSetModule = RaptureGearsetModule.Instance();
        var gearsetCount = InventoryManager.Instance()->GetPermittedGearsetCount();

        var GearSetList = new List<ExecutableAction>();

        for (var gearsetIndex = 0; gearsetIndex < gearsetCount; gearsetIndex++) {
            var gearset = raptureGearSetModule->GetGearset(gearsetIndex);
            var gearsetEntry = new ExecutableAction {
                ActionId = gearset->Id,
                ActionName = gearset->NameString,
                IconId = gearset->ClassJob + 62100u,
                TextCommand = $"/gs change {gearsetIndex + 1}",
                Category = $"{gearset->ClassJob}"
            };

            GearSetList.Add(gearsetEntry);
        }

        return GearSetList;
    }

    public static ExecutableAction? GetExecutableAction(string gearsetName) {
        var item = GetAllowedItems()
        .FirstOrDefault(gs => string.Equals(gs.ActionName, gearsetName, StringComparison.OrdinalIgnoreCase));

        return item ?? null;
    }

    private static ExecutableAction? GetExecutableAction(int gearsetIndex) {
        return GetAllowedItems().ElementAt(gearsetIndex);
    }

    private static unsafe bool HasGearsetForClassJobId(byte classJobId) {
        var raptureGearsetModule = RaptureGearsetModule.Instance();
        var gearsetCount = InventoryManager.Instance()->GetPermittedGearsetCount();

        for (var gearsetIndex = 0; gearsetIndex < gearsetCount; gearsetIndex++) {

            var gearset = raptureGearsetModule->GetGearset(gearsetIndex);
            if (!gearset->Flags.HasFlag(RaptureGearsetModule.GearsetFlag.Exists))
                continue;

            if (gearset->ClassJob == classJobId)
                return true;
        }

        return false;
    }
}

