using System;
using System.Linq;

using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;

namespace MasterOfPuppets;

public static class InventoryHelper {

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
                var inventorySlot = inventoryContainer->GetInventorySlot(i);

                if (
                    inventorySlot->GetItemId() == gearsetItem.ItemId
                    && inventorySlot->Stains.SequenceEqual([gearsetItem.Stain0Id, gearsetItem.Stain1Id])
                    && inventorySlot->Materia.SequenceEqual(gearsetItem.Materia)
                    && inventorySlot->MateriaGrades.SequenceEqual(gearsetItem.MateriaGrades)
                    && gearsetItem.GlamourId == inventorySlot->GlamourId
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
}
