using System;
using System.Runtime.CompilerServices;

using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;

namespace MasterOfPuppets.Extensions.Dalamud;

public static class GearsetItemExtensions {
    public static unsafe bool IsInArmoury(
        this in RaptureGearsetModule.GearsetItem gearsetItem
    ) {
        var gearsetItemId = gearsetItem.ItemId;
        if (gearsetItemId == 0)
            return false;

        var inventoryType = GetInventoryType(gearsetItemId);
        if (inventoryType == default)
            return false;

        var inventoryContainer = InventoryManager.Instance()->GetInventoryContainer(inventoryType);
        if (inventoryContainer == null)
            return false;

        var gearsetGlamourId = gearsetItem.GlamourId;

        int containerSlot = FindContainerSlot(
            inventoryContainer,
            gearsetItem,
            gearsetItemId,
            gearsetGlamourId);

        if (containerSlot >= 0) {
            return true;
        }

        return false;
    }

    public static unsafe bool IsInInventory(
        this in RaptureGearsetModule.GearsetItem gearsetItem
    ) {
        var gearsetItemId = gearsetItem.ItemId;
        if (gearsetItemId == 0)
            return false;

        ReadOnlySpan<InventoryType> inventories = [
            InventoryType.Inventory1,
            InventoryType.Inventory2,
            InventoryType.Inventory3,
            InventoryType.Inventory4
        ];

        var gearsetGlamourId = gearsetItem.GlamourId;

        foreach (var inventory in inventories) {
            var inventoryContainer = InventoryManager.Instance()->GetInventoryContainer(inventory);
            if (inventoryContainer == null)
                continue;

            int containerSlot = FindContainerSlot(
                inventoryContainer,
                gearsetItem,
                gearsetItemId,
                gearsetGlamourId);

            if (containerSlot >= 0) {
                return true;
            }
        }

        return false;
    }

    public static unsafe InventoryDescriptor? FindGearsetItemInInventory(
        this in RaptureGearsetModule.GearsetItem gearsetItem
    ) {
        var gearsetItemId = gearsetItem.ItemId;
        if (gearsetItemId == 0)
            return null;

        var gearsetGlamourId = gearsetItem.GlamourId;

        ReadOnlySpan<InventoryType> inventories = [
            InventoryType.Inventory1,
            InventoryType.Inventory2,
            InventoryType.Inventory3,
            InventoryType.Inventory4
        ];

        foreach (var inventory in inventories) {
            var inventoryContainer = InventoryManager.Instance()->GetInventoryContainer(inventory);
            if (inventoryContainer == null)
                continue;

            int containerSlot = FindContainerSlot(
                inventoryContainer,
                gearsetItem,
                gearsetItemId,
                gearsetGlamourId);

            if (containerSlot >= 0) {
                return new(inventoryContainer->Type, containerSlot);
            }
        }

        return null;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static InventoryType GetInventoryType(this in RaptureGearsetModule.GearsetItem gearsetItem) {
        var itemInfo = ItemHelper.GetItem(gearsetItem.ItemId % 1_000_000u);
        if (itemInfo == null)
            return default;

        return (EquipSlotCategoryEnum)itemInfo.Value.EquipSlotCategory.RowId switch {
            EquipSlotCategoryEnum.WeaponTwoHand => InventoryType.ArmoryMainHand,
            EquipSlotCategoryEnum.WeaponMainHand => InventoryType.ArmoryMainHand,
            EquipSlotCategoryEnum.OffHand => InventoryType.ArmoryOffHand,
            EquipSlotCategoryEnum.Head => InventoryType.ArmoryHead,
            EquipSlotCategoryEnum.Body => InventoryType.ArmoryBody,
            EquipSlotCategoryEnum.Gloves => InventoryType.ArmoryHands,
            EquipSlotCategoryEnum.Legs => InventoryType.ArmoryLegs,
            EquipSlotCategoryEnum.Feet => InventoryType.ArmoryFeets,
            EquipSlotCategoryEnum.Ears => InventoryType.ArmoryEar,
            EquipSlotCategoryEnum.Neck => InventoryType.ArmoryNeck,
            EquipSlotCategoryEnum.Wrists => InventoryType.ArmoryWrist,
            EquipSlotCategoryEnum.Ring => InventoryType.ArmoryRings,
            _ => default,
        };
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint GetInventoryTypeIcon(this in RaptureGearsetModule.GearsetItem gearsetItem) {
        var itemInfo = ItemHelper.GetItem(gearsetItem.ItemId % 1_000_000u);
        if (itemInfo == null)
            return 0;

        return (EquipSlotCategoryEnum)itemInfo.Value.EquipSlotCategory.RowId switch {
            EquipSlotCategoryEnum.WeaponTwoHand => 60011,
            EquipSlotCategoryEnum.WeaponMainHand => 60011,
            EquipSlotCategoryEnum.OffHand => 60110,
            EquipSlotCategoryEnum.Head => 60124,
            EquipSlotCategoryEnum.Body => 60126,
            EquipSlotCategoryEnum.Gloves => 60129,
            EquipSlotCategoryEnum.Legs => 60128,
            EquipSlotCategoryEnum.Feet => 60130,
            EquipSlotCategoryEnum.Ears => 60133,
            EquipSlotCategoryEnum.Neck => 60132,
            EquipSlotCategoryEnum.Wrists => 60134,
            EquipSlotCategoryEnum.Ring => 60135,
            _ => 0,
        };
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static InventoryType GetInventoryType(uint itemId) {
        var itemInfo = ItemHelper.GetItem(itemId % 1_000_000u);
        if (itemInfo == null)
            return default;

        return (EquipSlotCategoryEnum)itemInfo.Value.EquipSlotCategory.RowId switch {
            EquipSlotCategoryEnum.WeaponTwoHand => InventoryType.ArmoryMainHand,
            EquipSlotCategoryEnum.WeaponMainHand => InventoryType.ArmoryMainHand,
            EquipSlotCategoryEnum.OffHand => InventoryType.ArmoryOffHand,
            EquipSlotCategoryEnum.Head => InventoryType.ArmoryHead,
            EquipSlotCategoryEnum.Body => InventoryType.ArmoryBody,
            EquipSlotCategoryEnum.Gloves => InventoryType.ArmoryHands,
            EquipSlotCategoryEnum.Legs => InventoryType.ArmoryLegs,
            EquipSlotCategoryEnum.Feet => InventoryType.ArmoryFeets,
            EquipSlotCategoryEnum.Ears => InventoryType.ArmoryEar,
            EquipSlotCategoryEnum.Neck => InventoryType.ArmoryNeck,
            EquipSlotCategoryEnum.Wrists => InventoryType.ArmoryWrist,
            EquipSlotCategoryEnum.Ring => InventoryType.ArmoryRings,
            _ => default,
        };
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static unsafe int FindContainerSlot(
        InventoryContainer* container,
        in RaptureGearsetModule.GearsetItem gear,
        uint itemId,
        uint glamourId
    ) {
        for (int i = 0; i < container->Size; i++) {
            var slot = container->GetInventorySlot(i);
            if (slot == null)
                continue;

            if (slot->GetItemId() != itemId)
                continue;

            if (slot->GlamourId != glamourId)
                continue;

            if (MatchStains(slot, gear) && MatchMateria(slot, gear))
                return i;
        }
        return -1;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static unsafe bool MatchStains(
        InventoryItem* slot,
        in RaptureGearsetModule.GearsetItem gearsetItem
    ) =>
    slot->Stains[0] == gearsetItem.Stain0Id &&
    slot->Stains[1] == gearsetItem.Stain1Id;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static unsafe bool MatchMateria(
        InventoryItem* slot,
        in RaptureGearsetModule.GearsetItem gearsetItem
    ) {
        for (int i = 0; i < 5; i++) {
            if (slot->Materia[i] != gearsetItem.Materia[i]) return false;
            if (slot->MateriaGrades[i] != gearsetItem.MateriaGrades[i]) return false;
        }
        return true;
    }
}
