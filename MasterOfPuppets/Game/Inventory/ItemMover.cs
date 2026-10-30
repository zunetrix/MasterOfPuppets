using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

using Dalamud.Plugin.Services;

using FFXIVClientStructs.FFXIV.Client.Game;

namespace MasterOfPuppets;

public unsafe class ItemMover : IDisposable {
    private Plugin Plugin { get; }

    public Queue<(InventoryDescriptor from, InventoryType toType, int toSlot)> ItemsToMove = new();
    public event Action? ItemsMoved;

    private bool isWorking;
    private ItemSignature? expectedItem;

    public ItemMover(Plugin plugin) {
        Plugin = plugin;
    }

    public void Dispose() {
        Reset();
    }

    /// <summary>Enqueue a move to the first available empty slot in <paramref name="to"/>.</summary>
    public void Enqueue(InventoryDescriptor from, InventoryType to) {
        ItemsToMove.Enqueue((from, to, -1));
        StartIfNeeded();
    }

    /// <summary>Enqueue a move to a specific slot in <paramref name="toType"/>.</summary>
    public void Enqueue(InventoryDescriptor from, InventoryType toType, int toSlot) {
        ItemsToMove.Enqueue((from, toType, toSlot));
        StartIfNeeded();
    }

    private void StartIfNeeded() {
        if (isWorking) return;
        isWorking = true;
        DalamudApi.Framework.Update += Framework_Update;
    }

    private void Framework_Update(IFramework framework) {
        if (DalamudApi.ObjectTable.LocalPlayer == null) {
            Reset();
            return;
        }

        if (ItemsToMove.Count == 0) {
            Finish();
            return;
        }

        var (fromInventory, toInventory, toSlot) = ItemsToMove.Peek();
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
            var movedName = ItemHelper.GetItem(expectedItem.Value.ItemId % 1_000_000u)?.Name.ToString() ?? expectedItem.Value.ItemId.ToString();
            DalamudApi.PluginLog.Debug($"[ItemMover] Confirmed \"{movedName}\" moved. {ItemsToMove.Count - 1} remaining.");
            expectedItem = null;
            ItemsToMove.Dequeue();
            return;
        }

        if (!expectedItem.HasValue && EzThrottler.Throttle("ItemMover", 250)) {
            var targetInventory = toInventory;
            int targetSlot = toSlot;

            if (IsInventoryBag(toInventory)) {
                // If moving to general inventory bags:
                // Find the first empty slot across Inventory1..Inventory4 dynamically
                if (targetSlot < 0 || !IsSlotEmpty(targetInventory, targetSlot)) {
                    var empty = InventoryHelper.FindFirstEmptyInventorySlot();
                    if (empty == null) {
                        DalamudApi.PluginLog.Warning("[ItemMover] No empty inventory slots available. Stopping item move.");
                        ItemsToMove.Clear();
                        Finish();
                        return;
                    }
                    targetInventory = empty.Value.Type;
                    targetSlot = empty.Value.Slot;
                }
            } else if (targetSlot < 0) {
                // Moving to Armoury Chest dynamically
                var empty = InventoryHelper.FindFirstEmptyArmourySlot(toInventory);
                if (empty == null) {
                    DalamudApi.PluginLog.Warning($"[ItemMover] No empty armoury slots available in {toInventory}. Skipping item.");
                    ItemsToMove.Dequeue();
                    return;
                }
                targetSlot = empty.Value.Slot;
            }

            expectedItem = CaptureItemSignature(slot);

            var itemName = ItemHelper.GetItem(expectedItem.Value.ItemId % 1_000_000u)?.Name.ToString() ?? expectedItem.Value.ItemId.ToString();
            DalamudApi.PluginLog.Debug($"[ItemMover] Moving \"{itemName}\" (id:{expectedItem.Value.ItemId}) {fromInventory.Type}[{fromInventory.Slot}] → {targetInventory}[{targetSlot}]. {ItemsToMove.Count} queued.");

            InventoryManager.Instance()->MoveItemSlot(
                fromInventory.Type,
                (ushort)fromInventory.Slot,
                targetInventory,
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
        DalamudApi.Framework.Update -= Framework_Update;
        isWorking = false;
        expectedItem = null;

        DalamudApi.PluginLog.Debug("[ItemMover] Done - all items moved.");

        var handlers = ItemsMoved;
        ItemsMoved = null;
        handlers?.Invoke();
    }

    public Task WhenComplete() {
        if (!isWorking) return Task.CompletedTask;
        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        ItemsMoved += () => tcs.TrySetResult();
        return tcs.Task;
    }

    private void Reset() {
        DalamudApi.Framework.Update -= Framework_Update;
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

    public static bool IsInventoryBag(InventoryType type) => type is
        InventoryType.Inventory1 or
        InventoryType.Inventory2 or
        InventoryType.Inventory3 or
        InventoryType.Inventory4;

    public static unsafe bool IsSlotEmpty(InventoryType type, int slotIndex) {
        var container = InventoryManager.Instance()->GetInventoryContainer(type);
        if (container == null || slotIndex < 0 || slotIndex >= container->Size) return false;
        var slot = container->GetInventorySlot(slotIndex);
        return slot == null || slot->ItemId == 0;
    }

    private struct ItemSignature {
        public uint ItemId;
        public uint GlamourId;
        public fixed ushort Materia[5];
        public fixed byte MateriaGrades[5];
        public fixed byte Stains[2];
    }
}
