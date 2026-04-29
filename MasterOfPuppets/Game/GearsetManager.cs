using System.Collections.Generic;

using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;

using MasterOfPuppets.Extensions.Dalamud;

namespace MasterOfPuppets;

public static class GearsetManager {
    // try move gearset items to armoury before equip
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

                // Already at the correct slot - nothing to do
                if (inArmoury?.Slot == targetSlot) {
                    var info = ItemHelper.GetItem(item.ItemId % 1_000_000u);
                    var itemName = info != null ? info.Value.Name.ToString() : item.ItemId.ToString();
                    DalamudApi.PluginLog.Debug($"[ItemMover] GS{gearsetIndex + 1} \"{itemName}\" already at {armouryType}[{targetSlot}] - skip");
                    continue;
                }

                // Find item: prefer inventory, fall back to armoury (wrong slot)
                var src = item.FindGearsetItemInInventory() ?? inArmoury;
                if (src == null) continue;

                // Skip if this physical item was already scheduled by an earlier gearset
                if (!scheduledSources.Add(src.Value)) continue;

                plugin.ItemMover.Enqueue(src.Value, armouryType, targetSlot);
            }
        }
    }

    // swap gearset items between inventory -> amoury
    public static unsafe void SwapGearsets(Plugin plugin, int inventoryGearsetIndex, int armouryGearsetIndex) {
        var rapture = RaptureGearsetModule.Instance();
        if (!rapture->IsValidGearset(inventoryGearsetIndex) ||
            !rapture->IsValidGearset(armouryGearsetIndex))
            return;

        var invGearset = rapture->GetGearset(inventoryGearsetIndex);
        var armGearset = rapture->GetGearset(armouryGearsetIndex);
        var invManager = InventoryManager.Instance();

        for (int i = 0; i < invGearset->Items.Length; i++) {
            var invItem = invGearset->Items[i];
            var armItem = armGearset->Items[i];

            if (invItem.ItemId == 0) continue;

            var armouryType = invItem.GetInventoryType();
            if (armouryType == default) continue;

            var armouryContainer = invManager->GetInventoryContainer(armouryType);

            // skip it already in armoury
            if (InventoryHelper.FindGearsetItemInArmoury(invItem, armouryContainer) != null) continue;

            var invSrc = invItem.FindGearsetItemInInventory();
            if (invSrc == null) continue;

            // try move to 2nd gearset of empty slot
            var armItemInArmoury = armItem.ItemId != 0
                ? InventoryHelper.FindGearsetItemInArmoury(armItem, armouryContainer)
                : null;
            var targetSlot = armItemInArmoury?.Slot ?? InventoryHelper.FindFirstEmptyArmourySlot(armouryType)?.Slot;
            if (targetSlot == null) continue;

            plugin.ItemMover.Enqueue(invSrc.Value, armouryType, targetSlot.Value);
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

    public static void EquipGearset(int gearsetIndex) {
        Chat.SendMessage($"/gs change {gearsetIndex + 1}");
    }

    public static unsafe void RenameGearset(int gearsetIndex, string gearsetName) {
        if (gearsetIndex < 0 || gearsetIndex > 99) return;
        if (string.IsNullOrEmpty(gearsetName) || gearsetName.Length > 15) return;

        var rapture = RaptureGearsetModule.Instance();
        if (!rapture->IsValidGearset(gearsetIndex))
            return;

        var agentGearSet = AgentGearSet.Instance();
        if (agentGearSet == null) return;

        DalamudApi.Framework.RunOnFrameworkThread(() => {
            agentGearSet->RenameGearset(gearsetIndex, gearsetName);
        });
    }

    public static unsafe void ReorderGearset(int gearsetIndex, int newGearsetIndex) {
        if (newGearsetIndex < 0 || newGearsetIndex > 99) return;
        if (gearsetIndex < 0 || gearsetIndex > 99) return;

        var rapture = RaptureGearsetModule.Instance();
        if (!rapture->IsValidGearset(gearsetIndex))
            return;

        var agentGearSet = AgentGearSet.Instance();
        if (agentGearSet == null) return;

        DalamudApi.Framework.RunOnFrameworkThread(() => {
            agentGearSet->ReassignGearsetId(gearsetIndex, newGearsetIndex);
            // agentGearSet->ReassignGearSetNumber(newGearsetIndex);
            // rapture->ReassignGearsetId(newGearsetIndex, gearsetIndex);
        });
    }

    // private static unsafe void ChangeGearsetGlamour(int gearsetIndex, int glamoutIndex) {
    //     if (glamoutIndex <= 0 || glamoutIndex > 20) return;

    //     var rapture = RaptureGearsetModule.Instance();
    //     if (!rapture->IsValidGearset(gearsetIndex))
    //         return;

    //     DalamudApi.Framework.RunOnFrameworkThread(() => {
    //         rapture->EquipGearset(gearsetIndex, (byte)glamoutIndex);
    //     });
    // }
}
