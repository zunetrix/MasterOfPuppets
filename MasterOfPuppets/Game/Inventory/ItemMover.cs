using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

using Dalamud.Plugin.Services;

using FFXIVClientStructs.FFXIV.Client.Game;

namespace MasterOfPuppets;

public unsafe class ItemMover : IDisposable {
    private Plugin Plugin { get; }

    public Queue<(InventoryDescriptor from, InventoryType to)> ItemsToMove = new();
    public Action? OnAllItemsMoved;

    private bool isWorking;
    private ItemSignature? expectedItem;

    public ItemMover(Plugin plugin) {
        Plugin = plugin;
        DalamudApi.Framework.Update += Framework_Update;
    }

    public void Dispose() {
        Reset();
        DalamudApi.Framework.Update -= Framework_Update;
    }

    public void Enqueue(InventoryDescriptor from, InventoryType to) {
        ItemsToMove.Enqueue((from, to));
        isWorking = true;
    }

    private void Framework_Update(IFramework framework) {
        if (!isWorking)
            return;

        if (DalamudApi.ObjectTable.LocalPlayer == null) {
            Reset();
            return;
        }

        if (ItemsToMove.Count == 0) {
            Finish();
            return;
        }

        var (fromInventory, toInventory) = ItemsToMove.Peek();
        var container = InventoryManager.Instance()->GetInventoryContainer(fromInventory.Type);
        if (container == null) {
            ItemsToMove.Dequeue();
            return;
        }

        var slot = container->GetInventorySlot(fromInventory.Slot);
        if (slot == null) {
            ItemsToMove.Dequeue();
            return;
        }

        // check if item was moved
        if (expectedItem.HasValue && !MatchesItemSignature(slot, expectedItem.Value)) {
            expectedItem = null;
            ItemsToMove.Dequeue();
            return;
        }

        if (!expectedItem.HasValue && EzThrottler.Throttle("ItemMover", 500)) {
            expectedItem = CaptureItemSignature(slot);

            var targetSlot = InventoryHelper.FindFirstEmptyArmourySlot(toInventory)?.Slot ?? 0;

            InventoryManager.Instance()->MoveItemSlot(
                fromInventory.Type,
                (ushort)fromInventory.Slot,
                toInventory,
                (ushort)targetSlot,
                true
            );
        }
    }

    private static ItemSignature CaptureItemSignature(InventoryItem* slot) {
        var sig = new ItemSignature {
            ItemId = slot->GetItemId(),
            GlamourId = slot->GlamourId,
        };

        for (int i = 0; i < 5; i++) {
            sig.Materia[i] = slot->Materia[i];
            sig.MateriaGrades[i] = slot->MateriaGrades[i];
        }

        sig.Stains[0] = slot->Stains[0];
        sig.Stains[1] = slot->Stains[1];

        return sig;
    }

    private void Finish() {
        isWorking = false;
        expectedItem = null;

        OnAllItemsMoved?.Invoke();
        OnAllItemsMoved = null;
    }

    private void Reset() {
        ItemsToMove.Clear();
        isWorking = false;
        expectedItem = null;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool MatchesItemSignature(InventoryItem* slot, in ItemSignature sig) {
        if (slot->GetItemId() != sig.ItemId) return false;
        if (slot->GlamourId != sig.GlamourId) return false;

        if (slot->Stains[0] != sig.Stains[0]) return false;
        if (slot->Stains[1] != sig.Stains[1]) return false;

        for (int i = 0; i < 5; i++) {
            if (slot->Materia[i] != sig.Materia[i]) return false;
            if (slot->MateriaGrades[i] != sig.MateriaGrades[i]) return false;
        }

        return true;
    }

    private struct ItemSignature {
        public uint ItemId;
        public uint GlamourId;
        public fixed ushort Materia[5];
        public fixed byte MateriaGrades[5];
        public fixed byte Stains[2];
    }
}
