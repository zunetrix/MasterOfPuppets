using System.Collections.Generic;

using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;

using MasterOfPuppets.Extensions.Dalamud;

namespace MasterOfPuppets;

public static class GearsetManager {

    public static void ChangeGearset(Plugin plugin, int gearsetIndex) {
        if (!EnqueueGearsetItemsToArmoury(plugin, gearsetIndex)) {
            EquipGearset(gearsetIndex);
            return;
        }

        plugin.ItemMover.ItemsMoved += () => EquipGearset(gearsetIndex);
    }

    public static unsafe void MoveGearsetsToArmoury(Plugin plugin, IReadOnlyList<int> gearsetIndices) {
        var rapture = RaptureGearsetModule.Instance();

        // Track source descriptors already scheduled so shared items are only moved once
        // (first gearset in the list wins the target slot).
        var scheduledSources = new HashSet<InventoryDescriptor>();

        for (int listIdx = 0; listIdx < gearsetIndices.Count; listIdx++) {
            var gearsetIndex = gearsetIndices[listIdx];
            if (!rapture->IsValidGearset(gearsetIndex)) continue;

            var gearset = rapture->GetGearset(gearsetIndex);
            int targetSlot = listIdx;

            foreach (var item in gearset->Items) {
                if (item.ItemId == 0) continue;

                var armouryType = item.GetInventoryType();
                if (armouryType == default) continue;

                var armouryContainer = InventoryManager.Instance()->GetInventoryContainer(armouryType);
                var inArmoury = InventoryHelper.FindGearsetItemInArmoury(item, armouryContainer);

                // Already at the correct slot – nothing to do
                if (inArmoury?.Slot == targetSlot) continue;

                // Find item: prefer inventory, fall back to armoury (wrong slot)
                var src = item.FindGearsetItemInInventory() ?? inArmoury;
                if (src == null) continue;

                // Skip if this physical item was already scheduled by an earlier gearset
                if (!scheduledSources.Add(src.Value)) continue;

                plugin.ItemMover.Enqueue(src.Value, armouryType, targetSlot);
            }
        }
    }

    private static unsafe bool EnqueueGearsetItemsToArmoury(Plugin plugin, int gearsetIndex) {
        var rapture = RaptureGearsetModule.Instance();
        if (!rapture->IsValidGearset(gearsetIndex))
            return false;

        var gearset = rapture->GetGearset(gearsetIndex);
        bool any = false;

        foreach (var item in gearset->Items) {
            if (item.ItemId == 0)
                continue;

            var inventoryItem = item.FindGearsetItemInInventory();
            if (inventoryItem == null)
                continue;

            plugin.ItemMover.Enqueue(inventoryItem.Value, item.GetInventoryType());
            any = true;
        }

        return any;
    }

    private static void EquipGearset(int gearsetIndex) {
        Chat.SendMessage($"/gs change {gearsetIndex + 1}");
    }

    private static unsafe void ChangeGearsetGlamour(int gearsetIndex, int glamoutIndex) {
        if (glamoutIndex <= 0 || glamoutIndex > 20) return;

        var rapture = RaptureGearsetModule.Instance();
        if (!rapture->IsValidGearset(gearsetIndex))
            return;

        DalamudApi.Framework.RunOnFrameworkThread(() => {
            rapture->EquipGearset(gearsetIndex, (byte)glamoutIndex);
        });
    }
}
