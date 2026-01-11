using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata.Ecma335;

using Dalamud.Memory;
using Dalamud.Utility;

using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using FFXIVClientStructs.Interop;

namespace MasterOfPuppets;

public static class InventoryHelper {

    public static readonly EquipSlotCategoryEnum[] EquipSlots = Enum.GetValues<EquipSlotCategoryEnum>().Where(x => (int)x <= 13).ToArray();
    /*
        MainHand,
        OffHand,
        Head,
        Body,
        Hands,
        Belt,
        Legs,
        Feet,
        Ears,
        Neck,
        Wrists,
        RingRight,
        RingLeft,
         m7
    */
    public static readonly EquipSlotCategoryEnum[][] GearsetSlotMap = [
        [EquipSlotCategoryEnum.WeaponMainHand, EquipSlotCategoryEnum.WeaponTwoHand],
        [EquipSlotCategoryEnum.OffHand],
        [EquipSlotCategoryEnum.Head],
        [EquipSlotCategoryEnum.Body],
        [EquipSlotCategoryEnum.Gloves],
        [EquipSlotCategoryEnum.Waist],
        [EquipSlotCategoryEnum.Legs],
        [EquipSlotCategoryEnum.Feet],
        [EquipSlotCategoryEnum.Ears],
        [EquipSlotCategoryEnum.Neck],
        [EquipSlotCategoryEnum.Wrists],
        [EquipSlotCategoryEnum.Ring],
        [EquipSlotCategoryEnum.Ring],
        ];

    public static unsafe InventoryDescriptor? FindGearsetItemInArmoury(RaptureGearsetModule.GearsetItem gearsetItem, InventoryContainer* inventoryContainer) {
        for (int i = 0; i < inventoryContainer->Size; i++) {
            var item = inventoryContainer->GetInventorySlot(i);
            if (
                item->GetItemId() == gearsetItem.ItemId
                && item->Stains.SequenceEqual([gearsetItem.Stain0Id, gearsetItem.Stain1Id])
                && item->Materia.SequenceEqual(gearsetItem.Materia)
                && item->MateriaGrades.SequenceEqual(gearsetItem.MateriaGrades)
                && gearsetItem.GlamourId == item->GlamourId
                ) {
                return new(inventoryContainer->Type, i);
            }
        }
        return null;
    }

    // var raptureGearSetModule = RaptureGearsetModule.Instance();
    // var gearsetCount = InventoryManager.Instance()->GetPermittedGearsetCount();
    // for (var gearsetIndex = 0; gearsetIndex < gearsetCount; gearsetIndex++) {
    //     var gearset = raptureGearSetModule->GetGearset(gearsetIndex);
    //     var items = gearset->Items;

    //     foreach (var item in items) {
    //         if (item.ItemId == 0) continue;

    //         var itemInfo = ItemHelper.GetItem(item.ItemId % 1_000_000u);
    //         var gearsetItemInventory = (EquipSlotCategoryEnum)itemInfo?.EquipSlotCategory.RowId switch {
    //             EquipSlotCategoryEnum.WeaponTwoHand => InventoryType.ArmoryMainHand,
    //             EquipSlotCategoryEnum.WeaponMainHand => InventoryType.ArmoryMainHand,
    //             EquipSlotCategoryEnum.OffHand => InventoryType.ArmoryOffHand,
    //             EquipSlotCategoryEnum.Head => InventoryType.ArmoryHead,
    //             EquipSlotCategoryEnum.Body => InventoryType.ArmoryBody,
    //             EquipSlotCategoryEnum.Gloves => InventoryType.ArmoryHands,
    //             EquipSlotCategoryEnum.Legs => InventoryType.ArmoryLegs,
    //             EquipSlotCategoryEnum.Feet => InventoryType.ArmoryFeets,
    //             EquipSlotCategoryEnum.Ears => InventoryType.ArmoryEar,
    //             EquipSlotCategoryEnum.Neck => InventoryType.ArmoryNeck,
    //             EquipSlotCategoryEnum.Wrists => InventoryType.ArmoryWrist,
    //             EquipSlotCategoryEnum.Ring => InventoryType.ArmoryRings,
    //             _ => default,
    //         };

    //         var armouryContainer = InventoryManager.Instance()->GetInventoryContainer(gearsetItemInventory);
    //         var armouryItem = InventoryHelper.FindGearsetItemInArmoury(item, armouryContainer);
    //         bool isInArmoury = armouryItem != null;
    //     }
    // }
    // var inventoryItem = InventoryHelper.FindGearsetItemInInventoryBag(item);
    // bool isInInvenotry = inventoryItem != null;


    public static unsafe InventoryDescriptor? FindGearsetItemInInventory(RaptureGearsetModule.GearsetItem gearsetItem) {
        ReadOnlySpan<InventoryType> inventories = [
            InventoryType.Inventory1,
            InventoryType.Inventory2,
            InventoryType.Inventory3,
            InventoryType.Inventory4
        ];

        foreach (var inventory in inventories) {
            var inventoryContainer = InventoryManager.Instance()->GetInventoryContainer(inventory);
            for (int i = 0; i < inventoryContainer->Size; i++) {
                var item = inventoryContainer->GetInventorySlot(i);
                if (
                    item->GetItemId() == gearsetItem.ItemId
                    && item->Stains.SequenceEqual([gearsetItem.Stain0Id, gearsetItem.Stain1Id])
                    && item->Materia.SequenceEqual(gearsetItem.Materia)
                    && item->MateriaGrades.SequenceEqual(gearsetItem.MateriaGrades)
                    && gearsetItem.GlamourId == item->GlamourId
                    ) {
                    return new(inventoryContainer->Type, i);
                }
            }
        }
        return null;
    }

    public static unsafe InventoryDescriptor? FindFirstEmptyInventorySlot() {
        ReadOnlySpan<InventoryType> inventories = [
            InventoryType.Inventory1,
            InventoryType.Inventory2,
            InventoryType.Inventory3,
            InventoryType.Inventory4
        ];

        foreach (var inventory in inventories) {
            var inventoryContainer = InventoryManager.Instance()->GetInventoryContainer(inventory);
            for (int i = 0; i < inventoryContainer->GetSize(); i++) {
                if (inventoryContainer->GetInventorySlot(i)->ItemId == 0) {
                    return new(inventoryContainer->Type, i);
                }
            }
        }
        return null;
    }


    // public static unsafe void UpdateGearsetIfNeeded(int index, bool includeInventory = true, bool? shouldEquip = null) {
    //     var gearsetModule = RaptureGearsetModule.Instance();
    //     var isCurrent = gearsetModule->CurrentGearsetIndex == index;

    //     List<RaptureGearsetModule.GearsetItem> itemsToUnmove = [];

    //     if (index < gearsetModule->NumGearsets && gearsetModule->IsValidGearset(index)) {
    //         var entry = gearsetModule->Entries.GetPointer(index);
    //         InventoryDescriptor? ring = null;

    //         for (int q = 0; q < entry->Items.Length && q < InventoryHelper.GearsetSlotMap.Length; q++) {
    //             var gsItem = entry->GetItem((RaptureGearsetModule.GearsetItemIndex)q);
    //             var candidate = InventoryHelper.GetBestItemForJob((Job)entry->ClassJob, InventoryHelper.GearsetSlotMap[q], true, q == 12 ? [ring] : null, includeInventory);

    //             if (q == 11) ring = candidate;
    //             if (candidate != null) {
    //                 if (candidate.Value.GetSlot().GetItemId() == gsItem.ItemId) {
    //                     DalamudApi.PluginLog.Debug($"Skipping existing item for slot {q}");
    //                 } else {
    //                     var t = entry->Items.GetPointer(q);
    //                     t->ItemId = candidate.Value.GetSlot().GetItemId();
    //                     t->GlamourId = candidate.Value.GetSlot().GetGlamourId();
    //                     t->Flags = 0;
    //                     t->Stain0Id = candidate.Value.GetSlot().GetStain(0);
    //                     t->Stain1Id = candidate.Value.GetSlot().GetStain(1);
    //                     MemoryHelper.WriteRaw((nint)t->Materia.GetPointer(0), MemoryHelper.ReadRaw((nint)candidate.Value.GetSlot().Materia.GetPointer(0), sizeof(ushort) * 5));
    //                     MemoryHelper.WriteRaw((nint)t->MateriaGrades.GetPointer(0), MemoryHelper.ReadRaw((nint)candidate.Value.GetSlot().MateriaGrades.GetPointer(0), sizeof(byte) * 5));
    //                     if (candidate.Value.Type.EqualsAny(InventoryType.Inventory1, InventoryType.Inventory2, InventoryType.Inventory3, InventoryType.Inventory4)) {
    //                         S.ItemMover.ItemsToMove.Add(candidate.Value);
    //                         itemsToUnmove.Add(gsItem);
    //                     }
    //                     DalamudApi.PluginLog.Debug($"Setting item for slot {q}");

    //                     bool reequip = true;
    //                     shouldEquip ??= isCurrent && reequip;
    //                 }
    //             }
    //         }
    //         var mainHand = ExcelItemHelper.Get(entry->GetItem(RaptureGearsetModule.GearsetItemIndex.MainHand).ItemId % 1000000);
    //         if (mainHand != null && mainHand.Value.EquipSlotCategory.RowId == (uint)EquipSlotCategoryEnum.WeaponTwoHand) {
    //             var items = entry->Items;
    //             items.GetPointer((int)RaptureGearsetModule.GearsetItemIndex.OffHand)->ItemId = 0;
    //         }
    //         var ilvl = ItemLevelCalculator.Calculate(*entry);
    //         if (ilvl != null) {
    //             entry->ItemLevel = (short)ilvl.Value;
    //         }
    //     }

    //     if (shouldEquip == true) {
    //         DalamudApi.PluginLog.Information($"Re-equipping gearset {index} once all items moved to armory chest");

    //         P.TaskManager.Enqueue(() => {
    //             if (S.ItemMover.ItemsToMove.Count > 0) return false;
    //             if (!Player.Interactable) return false;
    //             if (!Svc.ClientState.IsLoggedIn) return null;
    //             RaptureGearsetModule.Instance()->EquipGearset(index);
    //             return true;
    //         }, new(showDebug: true));

    //         P.TaskManager.Enqueue(() => {
    //             return !Utils.CheckForUpdateNeeded(index, includeInventory);
    //         }, new(showDebug: true));
    //         if (C.UnmoveItems) {
    //             P.TaskManager.Enqueue(() => S.ItemMover.ItemsToUnmove = itemsToUnmove);
    //             P.TaskManager.Enqueue(() => S.ItemMover.ItemsToUnmove.Count == 0, new(showDebug: true));
    //         }
    //     }
    // }
}
