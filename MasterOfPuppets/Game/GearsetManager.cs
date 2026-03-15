using System.Collections.Generic;

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

    public static void MoveGearsetsToArmoury(Plugin plugin, IReadOnlyList<int> gearsetIndices) {
        foreach (var idx in gearsetIndices)
            EnqueueGearsetItemsToArmoury(plugin, idx);
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
