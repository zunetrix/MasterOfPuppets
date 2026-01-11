using System;
using System.Collections.Generic;

using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;

namespace MasterOfPuppets;

public unsafe class ItemMover : IDisposable {
    private Plugin Plugin { get; }

    public List<InventoryDescriptor> ItemsToMove = [];
    public List<RaptureGearsetModule.GearsetItem> ItemsToUnmove = [];
    public int Attempts = 0;

    public ItemMover(Plugin plugin) {
        Plugin = plugin;

        DalamudApi.Framework.Update += Framework_Update;
    }

    public void Dispose() {
        DalamudApi.Framework.Update -= Framework_Update;
    }

    private void Framework_Update(Dalamud.Plugin.Services.IFramework framework) {
        if (ItemsToMove.Count == 0 && ItemsToUnmove.Count == 0) return;

        if (!(DalamudApi.ObjectTable.LocalPlayer != null)) {
            ItemsToMove.Clear();
            ItemsToUnmove.Clear();
            return;
        }

        if (ItemsToMove.Count > 0) {
            var next = ItemsToMove[0];
            var item = InventoryManager.Instance()->GetInventoryContainer(next.Type)->GetInventorySlot(next.Slot);
            if (item->GetItemId() == 0) {
                ItemsToMove.RemoveAt(0);
                Attempts = 0;
                return;
            }

            if (Attempts > 10) {
                DalamudApi.PluginLog.Warning($"Too many move attempts, skipping move of {next.Type}/{next.Slot}");
                ItemsToMove.RemoveAt(0);
                Attempts = 0;
                return;
            }

            if (item->GetItemId() != next.Data.RowId + (next.IsHQ ? 1000000 : 0)) {
                DalamudApi.PluginLog.Warning($"Requested item mismatch, skipping {next.Type}/{next.Slot}");
                ItemsToMove.RemoveAt(0);
                Attempts = 0;
                return;
            }

            var targetInventory = (EquipSlotCategoryEnum)next.Data.Value.EquipSlotCategory.RowId switch {
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

            if (targetInventory == default) {
                DalamudApi.PluginLog.Warning($"Can't find suitable inventory, skipping {next.Type}/{next.Slot}");
                ItemsToMove.RemoveAt(0);
                Attempts = 0;
                return;
            }

            if (targetInventory == next.Type) {
                DalamudApi.PluginLog.Warning($"Can't move to the same inventory, skipping {next.Type}/{next.Slot}");
                ItemsToMove.RemoveAt(0);
                Attempts = 0;
                return;
            }

            var targetSlot = -1;
            var cont = InventoryManager.Instance()->GetInventoryContainer(targetInventory);
            for (int i = 0; i < cont->GetSize(); i++) {
                if (cont->GetInventorySlot(i)->ItemId == 0) {
                    targetSlot = i;
                    break;
                }
            }

            if (targetSlot == -1) {
                DalamudApi.PluginLog.Warning($"Can't find free slot in {targetInventory}, skipping {next.Type}/{next.Slot}");
                ItemsToMove.RemoveAt(0);
                Attempts = 0;
                return;
            }
            if (EzThrottler.Throttle("MoveItem")) {
                DalamudApi.PluginLog.Information($"Move item from {next.Type}/{next.Slot} to {targetInventory}/{targetSlot}");
                InventoryManager.Instance()->MoveItemSlot(next.Type, (ushort)next.Slot, targetInventory, (ushort)targetSlot, true);
                Attempts++;
            }
        } else if (ItemsToUnmove.Count > 0) {
            var next = ItemsToUnmove[0];

            if (next.ItemId % 1000000 == 0) {
                ItemsToUnmove.RemoveAt(0);
                Attempts = 0;
                return;
            }

            var itemInfo = ItemHelper.GetItem(next.ItemId % 1_000_000u);
            var targetInventory = (EquipSlotCategoryEnum)itemInfo?.EquipSlotCategory.RowId switch {
                EquipSlotCategoryEnum.WeaponTwoHand => InventoryType.ArmoryMainHand, //60011
                EquipSlotCategoryEnum.WeaponMainHand => InventoryType.ArmoryMainHand, //60011
                EquipSlotCategoryEnum.OffHand => InventoryType.ArmoryOffHand, //60110
                EquipSlotCategoryEnum.Head => InventoryType.ArmoryHead, // 60124
                EquipSlotCategoryEnum.Body => InventoryType.ArmoryBody, // 60126
                EquipSlotCategoryEnum.Gloves => InventoryType.ArmoryHands, //60129
                EquipSlotCategoryEnum.Legs => InventoryType.ArmoryLegs, //60128
                EquipSlotCategoryEnum.Feet => InventoryType.ArmoryFeets, //60130
                EquipSlotCategoryEnum.Ears => InventoryType.ArmoryEar, //60133
                EquipSlotCategoryEnum.Neck => InventoryType.ArmoryNeck, //60132
                EquipSlotCategoryEnum.Wrists => InventoryType.ArmoryWrist, //60134
                EquipSlotCategoryEnum.Ring => InventoryType.ArmoryRings, //60135
                _ => default,
            };

            if (targetInventory == default) {
                DalamudApi.PluginLog.Warning($"Can't find source inventory, skipping {itemInfo?.Name}");
                ItemsToUnmove.RemoveAt(0);
                Attempts = 0;
                return;
            }

            var srcCont = InventoryManager.Instance()->GetInventoryContainer(targetInventory);
            var item = InventoryHelper.FindGearsetItemInArmoury(next, srcCont);
            if (item == null) {
                DalamudApi.PluginLog.Debug($"Could not find item {ItemHelper.GetItem(next.ItemId % 1000000)?.Name}");
                ItemsToUnmove.RemoveAt(0);
                Attempts = 0;
                return;
            }

            if (Attempts > 10) {
                DalamudApi.PluginLog.Warning($"Can't find source inventory, skipping  {ItemHelper.GetItem(next.ItemId % 1000000)?.Name}");
                ItemsToUnmove.RemoveAt(0);
                Attempts = 0;
                return;
            }

            var targetSlot = -1;
            InventoryContainer* targetCont = null;

            foreach (var inv in (InventoryType[])[InventoryType.Inventory1, InventoryType.Inventory2, InventoryType.Inventory3, InventoryType.Inventory4]) {
                var cont = InventoryManager.Instance()->GetInventoryContainer(inv);
                for (int i = 0; i < cont->GetSize(); i++) {
                    if (cont->GetInventorySlot(i)->ItemId == 0) {
                        targetSlot = i;
                        targetCont = cont;
                        break;
                    }
                }
            }

            if (targetSlot == -1 || targetCont == null) {
                DalamudApi.PluginLog.Warning($"Can't find free slot in inventory, skipping {ItemHelper.GetItem(next.ItemId % 1000000)?.Name}");
                ItemsToUnmove.RemoveAt(0);
                Attempts = 0;
                return;
            }
            if (EzThrottler.Throttle("MoveItem")) {
                DalamudApi.PluginLog.Information($"Move item from {item} to {targetCont->Type}/{targetSlot}");
                InventoryManager.Instance()->MoveItemSlot(item.Value.Type, (ushort)item.Value.Slot, targetCont->Type, (ushort)targetSlot, true);
                Attempts++;
            }
        }
    }
}
