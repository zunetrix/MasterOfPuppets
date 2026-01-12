using FFXIVClientStructs.FFXIV.Client.UI.Misc;

using MasterOfPuppets.Extensions.Dalamud;

namespace MasterOfPuppets;

public static class GearsetManager {

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

