using System;
using System.Collections.Generic;
using System.Linq;

using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;

using MasterOfPuppets.Extensions.Dalamud;

namespace MasterOfPuppets;

public static class GearSetHelper {
    public static unsafe List<ExecutableAction> GetAllowedItems() {
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

    public static unsafe void ChangeGearset(Plugin plugin, int gearsetIndex) {
        var rapture = RaptureGearsetModule.Instance();
        if (!rapture->IsValidGearset(gearsetIndex))
            return;

        var gearset = rapture->GetGearset(gearsetIndex);

        bool needsMove = false;

        foreach (var item in gearset->Items) {
            if (item.ItemId == 0)
                continue;

            var inventoryItem = item.FindGearsetItemInInventory();
            if (inventoryItem == null)
                continue;

            var targetInventory = item.GetInventoryType();

            plugin.ItemMover.Enqueue(
                inventoryItem.Value,
                targetInventory
            );

            needsMove = true;
        }

        if (!needsMove) {
            EquipGearset(gearsetIndex);
            return;
        }

        plugin.ItemMover.OnAllItemsMoved = () => {
            EquipGearset(gearsetIndex);
        };
    }

    private static void EquipGearset(int gearsetIndex) {
        Chat.SendMessage($"/gs change {gearsetIndex + 1}");
        // DalamudApi.Framework.RunOnFrameworkThread(() => {
        //     RaptureGearsetModule.Instance()->EquipGearset(gearsetIndex);
        // });
    }
}

