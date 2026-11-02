using System.Collections.Generic;
using System.Linq;

using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;

using MasterOfPuppets.Extensions.Dalamud;

namespace MasterOfPuppets;

public static class GearsetManager {
    // try move gearset items to armoury before equip
    public static void ChangeGearset(Plugin plugin, int gearsetIndex) {
        if (!MoveGearsetItemsToArmoury(plugin, gearsetIndex)) {
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

    public static bool ShouldSkipSlotForMove(
        int slotIndex,
        bool ignoreWeapons,
        bool ignoreAccessories,
        bool ignoreSoulCrystal) {
        var slot = (RaptureGearsetModule.GearsetItemIndex)slotIndex;
        if (slot == RaptureGearsetModule.GearsetItemIndex.Belt)
            return true;

        if (ignoreWeapons && (slot == RaptureGearsetModule.GearsetItemIndex.MainHand || slot == RaptureGearsetModule.GearsetItemIndex.OffHand))
            return true;

        if (ignoreAccessories && (slot == RaptureGearsetModule.GearsetItemIndex.Ears
                               || slot == RaptureGearsetModule.GearsetItemIndex.Neck
                               || slot == RaptureGearsetModule.GearsetItemIndex.Wrists
                               || slot == RaptureGearsetModule.GearsetItemIndex.RingRight
                               || slot == RaptureGearsetModule.GearsetItemIndex.RingLeft))
            return true;

        if (ignoreSoulCrystal && slot == RaptureGearsetModule.GearsetItemIndex.SoulStone)
            return true;

        return false;
    }

    public static unsafe bool MoveGearsetItemsToArmoury(
        Plugin plugin,
        int gearsetIndex,
        bool ignoreWeapons = false,
        bool ignoreAccessories = false,
        bool ignoreSoulCrystal = false) {
        var rapture = RaptureGearsetModule.Instance();
        if (!rapture->IsValidGearset(gearsetIndex))
            return false;

        var gearset = rapture->GetGearset(gearsetIndex);
        bool any = false;

        for (int i = 0; i < gearset->Items.Length; i++) {
            if (ShouldSkipSlotForMove(i, ignoreWeapons, ignoreAccessories, ignoreSoulCrystal))
                continue;

            var item = gearset->Items[i];
            if (item.ItemId == 0)
                continue;

            if (item.IsInArmoury())
                continue;

            var inventoryItem = item.FindGearsetItemInInventory();
            if (inventoryItem == null)
                continue;

            var armouryType = item.GetInventoryType();
            if (armouryType == default)
                continue;

            plugin.ItemMover.Enqueue(inventoryItem.Value, armouryType);
            any = true;
        }

        return any;
    }

    public static unsafe bool MoveGearsetItemsToInventory(
        Plugin plugin,
        int gearsetIndex,
        bool ignoreWeapons = false,
        bool ignoreAccessories = false,
        bool ignoreSoulCrystal = false) {
        var rapture = RaptureGearsetModule.Instance();
        if (!rapture->IsValidGearset(gearsetIndex))
            return false;

        int freeSlots = InventoryHelper.GetEmptyInventorySlotCount();
        if (freeSlots <= 0) {
            DalamudApi.ChatGui.PrintError("[MasterOfPuppets] Cannot move items: Inventory is full.");
            return false;
        }

        var gearset = rapture->GetGearset(gearsetIndex);
        int enqueuedCount = 0;

        for (int i = 0; i < gearset->Items.Length; i++) {
            if (ShouldSkipSlotForMove(i, ignoreWeapons, ignoreAccessories, ignoreSoulCrystal))
                continue;

            var item = gearset->Items[i];
            if (item.ItemId == 0)
                continue;

            if (!item.IsInArmoury())
                continue;

            var armouryType = item.GetInventoryType();
            if (armouryType == default)
                continue;

            var container = InventoryManager.Instance()->GetInventoryContainer(armouryType);
            var armourySrc = InventoryHelper.FindGearsetItemInArmoury(item, container);
            if (armourySrc == null)
                continue;

            if (enqueuedCount >= freeSlots) {
                DalamudApi.ChatGui.Print("[MasterOfPuppets] Some items were not moved because the inventory became full.");
                break;
            }

            plugin.ItemMover.Enqueue(armourySrc.Value, InventoryType.Inventory1);
            enqueuedCount++;
        }

        return enqueuedCount > 0;
    }

    // EquippedItems container slots: 0 MainHand, 1 OffHand, 2 Head, 3 Body, 4 Hands, 5 Waist,
    // 6 Legs, 7 Feet, 8 Ears, 9 Neck, 10 Wrists, 11 RingRight, 12 RingLeft, 13 SoulCrystal
    private const int EquippedMainHandSlot = 0;
    private const int EquippedOffHandSlot = 1;
    private const int EquippedSoulCrystalSlot = 13;

    private static readonly InventoryType[] EquipSourceInventories = [
        InventoryType.ArmoryMainHand,
        InventoryType.ArmoryOffHand,
        InventoryType.ArmoryHead,
        InventoryType.ArmoryBody,
        InventoryType.ArmoryHands,
        InventoryType.ArmoryLegs,
        InventoryType.ArmoryFeets,
        InventoryType.ArmoryEar,
        InventoryType.ArmoryNeck,
        InventoryType.ArmoryWrist,
        InventoryType.ArmoryRings,
        InventoryType.ArmorySoulCrystal,
        InventoryType.Inventory1,
        InventoryType.Inventory2,
        InventoryType.Inventory3,
        InventoryType.Inventory4,
    ];

    /// <summary>Equips a single item by name or item id, searching the armoury chest then inventory bags.</summary>
    public static unsafe bool EquipItem(Plugin plugin, string itemNameOrId) {
        var item = uint.TryParse(itemNameOrId, out var itemId)
            ? ItemHelper.GetItem(itemId % 1_000_000u)
            : ItemHelper.GetItem(itemNameOrId);

        if (item == null || item.Value.RowId == 0) {
            DalamudApi.ChatGui.PrintError($"[MasterOfPuppets] Item \"{itemNameOrId}\" not found.");
            return false;
        }

        var itemRow = item.Value;
        var itemName = itemRow.Name.ToString();
        var equipSlots = GetEquipSlots(itemRow.EquipSlotCategory.RowId);
        if (equipSlots == null) {
            DalamudApi.ChatGui.PrintError($"[MasterOfPuppets] \"{itemName}\" is not a piece of equipment.");
            return false;
        }

        var invManager = InventoryManager.Instance();
        var equipped = invManager->GetInventoryContainer(InventoryType.EquippedItems);
        if (equipped == null) return false;

        foreach (var slot in equipSlots) {
            var equippedSlot = equipped->GetInventorySlot(slot);
            if (equippedSlot != null && equippedSlot->ItemId == itemRow.RowId) {
                DalamudApi.PluginLog.Debug($"[EquipItem] \"{itemName}\" already equipped - skip");
                return false;
            }
        }

        var src = FindItemForEquip(itemRow.RowId);
        if (src == null) {
            DalamudApi.ChatGui.PrintError($"[MasterOfPuppets] \"{itemName}\" not found in Armoury Chest or Inventory.");
            return false;
        }

        // Prefer an empty target slot (rings), otherwise replace the first one
        int targetSlot = equipSlots[0];
        foreach (var slot in equipSlots) {
            var equippedSlot = equipped->GetInventorySlot(slot);
            if (equippedSlot == null || equippedSlot->ItemId == 0) {
                targetSlot = slot;
                break;
            }
        }

        plugin.ItemMover.Enqueue(src.Value, InventoryType.EquippedItems, targetSlot);
        return true;
    }

    /// <summary>
    /// Equips the exact gearset item copy (matching materia/dyes/glamour) from armoury or inventory
    /// into its gearset slot. GearsetItemIndex values match the EquippedItems container slots.
    /// </summary>
    public static unsafe bool EquipGearsetItem(Plugin plugin, RaptureGearsetModule.GearsetItem item, RaptureGearsetModule.GearsetItemIndex slotIndex) {
        if (item.ItemId == 0 || slotIndex == RaptureGearsetModule.GearsetItemIndex.Belt)
            return false;

        var armouryType = item.GetInventoryType();
        if (armouryType == default)
            return false;

        var armouryContainer = InventoryManager.Instance()->GetInventoryContainer(armouryType);
        var src = InventoryHelper.FindGearsetItemInArmoury(item, armouryContainer) ?? item.FindGearsetItemInInventory();
        if (src == null) {
            DalamudApi.ChatGui.PrintError("[MasterOfPuppets] Item not found in Armoury Chest or Inventory.");
            return false;
        }

        plugin.ItemMover.Enqueue(src.Value, InventoryType.EquippedItems, (int)slotIndex);
        return true;
    }

    /// <summary>
    /// Moves every equipped item to the armoury chest except Main Hand, Off Hand and Soul Crystal.
    /// Moves are queued in ItemMover and executed one at a time (each waits for the previous to be confirmed).
    /// </summary>
    public static unsafe bool UnequipAll(Plugin plugin) {
        var equipped = InventoryManager.Instance()->GetInventoryContainer(InventoryType.EquippedItems);
        if (equipped == null) return false;

        bool any = false;
        for (int i = 0; i < equipped->Size; i++) {
            if (i is EquippedMainHandSlot or EquippedOffHandSlot or EquippedSoulCrystalSlot)
                continue;

            var slot = equipped->GetInventorySlot(i);
            if (slot == null || slot->ItemId == 0)
                continue;

            var armouryType = GetArmouryType(slot->ItemId);
            if (armouryType == default)
                continue;

            var src = new InventoryDescriptor(InventoryType.EquippedItems, i);
            if (plugin.ItemMover.ItemsToMove.Any(m => m.from.Type == src.Type && m.from.Slot == src.Slot))
                continue;

            plugin.ItemMover.Enqueue(src, armouryType);
            any = true;
        }

        return any;
    }

    private static unsafe InventoryDescriptor? FindItemForEquip(uint baseItemId) {
        var invManager = InventoryManager.Instance();
        foreach (var type in EquipSourceInventories) {
            var container = invManager->GetInventoryContainer(type);
            if (container == null) continue;

            for (int i = 0; i < container->Size; i++) {
                var slot = container->GetInventorySlot(i);
                if (slot != null && slot->ItemId == baseItemId)
                    return new(type, i);
            }
        }
        return null;
    }

    private static int[]? GetEquipSlots(uint equipSlotCategory) => (EquipSlotCategoryEnum)equipSlotCategory switch {
        EquipSlotCategoryEnum.WeaponMainHand or EquipSlotCategoryEnum.WeaponTwoHand => [0],
        EquipSlotCategoryEnum.OffHand => [1],
        EquipSlotCategoryEnum.Head => [2],
        EquipSlotCategoryEnum.Body
            or EquipSlotCategoryEnum.BodyHead
            or EquipSlotCategoryEnum.BodyGlovesLegsFeet
            or EquipSlotCategoryEnum.BodyHeadGlovesLegsFeet
            or EquipSlotCategoryEnum.BodyGlovesLegs
            or EquipSlotCategoryEnum.BodyLegsFeet
            or EquipSlotCategoryEnum.BodyGloves => [3],
        EquipSlotCategoryEnum.Gloves => [4],
        EquipSlotCategoryEnum.Legs or EquipSlotCategoryEnum.LegsFeet => [6],
        EquipSlotCategoryEnum.Feet => [7],
        EquipSlotCategoryEnum.Ears => [8],
        EquipSlotCategoryEnum.Neck => [9],
        EquipSlotCategoryEnum.Wrists => [10],
        EquipSlotCategoryEnum.Ring => [11, 12],
        EquipSlotCategoryEnum.SoulCrystal => [13],
        _ => null,
    };

    private static InventoryType GetArmouryType(uint itemId) {
        var itemInfo = ItemHelper.GetItem(itemId % 1_000_000u);
        if (itemInfo == null)
            return default;

        return (EquipSlotCategoryEnum)itemInfo.Value.EquipSlotCategory.RowId switch {
            EquipSlotCategoryEnum.WeaponTwoHand or EquipSlotCategoryEnum.WeaponMainHand => InventoryType.ArmoryMainHand,
            EquipSlotCategoryEnum.OffHand => InventoryType.ArmoryOffHand,
            EquipSlotCategoryEnum.Head => InventoryType.ArmoryHead,
            EquipSlotCategoryEnum.Body
                or EquipSlotCategoryEnum.BodyHead
                or EquipSlotCategoryEnum.BodyGlovesLegsFeet
                or EquipSlotCategoryEnum.BodyHeadGlovesLegsFeet
                or EquipSlotCategoryEnum.BodyGlovesLegs
                or EquipSlotCategoryEnum.BodyLegsFeet
                or EquipSlotCategoryEnum.BodyGloves => InventoryType.ArmoryBody,
            EquipSlotCategoryEnum.Gloves => InventoryType.ArmoryHands,
            EquipSlotCategoryEnum.Legs or EquipSlotCategoryEnum.LegsFeet => InventoryType.ArmoryLegs,
            EquipSlotCategoryEnum.Feet => InventoryType.ArmoryFeets,
            EquipSlotCategoryEnum.Ears => InventoryType.ArmoryEar,
            EquipSlotCategoryEnum.Neck => InventoryType.ArmoryNeck,
            EquipSlotCategoryEnum.Wrists => InventoryType.ArmoryWrist,
            EquipSlotCategoryEnum.Ring => InventoryType.ArmoryRings,
            EquipSlotCategoryEnum.SoulCrystal => InventoryType.ArmorySoulCrystal,
            _ => default,
        };
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
