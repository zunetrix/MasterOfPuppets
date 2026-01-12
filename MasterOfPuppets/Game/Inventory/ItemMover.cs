using System;
using System.Collections.Generic;

using Dalamud.Plugin.Services;

using FFXIVClientStructs.FFXIV.Client.Game;

namespace MasterOfPuppets;

public unsafe class ItemMover : IDisposable {
    private Plugin Plugin { get; }

    public Queue<(InventoryDescriptor from, InventoryType to)> ItemsToMove = new();
    public Action? OnAllItemsMoved;

    private bool isWorking;
    private uint expectedItemId;

    public ItemMover(Plugin plugin) {
        Plugin = plugin;
        DalamudApi.Framework.Update += Framework_Update;
    }

    public void Dispose() {
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

        // check if item moved
        if (expectedItemId != 0 && slot->GetItemId() != expectedItemId) {
            expectedItemId = 0;
            ItemsToMove.Dequeue();
            return;
        }

        if (expectedItemId == 0 && EzThrottler.Throttle("ItemMover", 500)) {
            expectedItemId = slot->GetItemId();
            InventoryManager.Instance()->MoveItemSlot(fromInventory.Type, (ushort)fromInventory.Slot, toInventory, 0, true);
        }
    }

    private void Finish() {
        isWorking = false;
        expectedItemId = 0;

        OnAllItemsMoved?.Invoke();
        OnAllItemsMoved = null;
    }

    private void Reset() {
        ItemsToMove.Clear();
        isWorking = false;
        expectedItemId = 0;
    }
}
