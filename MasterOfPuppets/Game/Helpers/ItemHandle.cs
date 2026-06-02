using Dalamud.Utility;

using FFXIVClientStructs.FFXIV.Client.Game;

using Lumina.Excel;
using Lumina.Excel.Sheets;

namespace MasterOfPuppets;

// https://github.com/Haselnussbomber/HaselTweaks/

public readonly record struct ItemHandle {
    public ItemHandle(uint itemId) {
        ItemId = itemId;
    }

    public ItemHandle(ItemLocation itemLocation) {
        ItemLocation = itemLocation;
        unsafe {
            var inventoryItem = itemLocation.GetInventoryItem();
            ItemId = inventoryItem != null ? inventoryItem->GetItemId() : 0;
        }
    }

    public uint ItemId { get; }

    public ItemLocation? ItemLocation { get; }

    public bool IsEmpty => ItemId == 0;

    public uint BaseItemId => ItemUtil.GetBaseId(ItemId).ItemId;

    public ItemKind ItemKind => ItemUtil.GetBaseId(ItemId).Kind;

    public bool IsNormalItem => ItemUtil.IsNormalItem(ItemId);

    public bool IsCollectible => ItemUtil.IsCollectible(ItemId);

    public bool IsHighQuality => ItemUtil.IsHighQuality(ItemId);

    public bool IsEventItem => ItemUtil.IsEventItem(ItemId);

    public override string ToString() {
        return $"{nameof(ItemHandle)}#{(IsEmpty ? "Empty" : ItemId)}";
    }

    public static unsafe implicit operator ItemHandle(InventoryItem* item) => new(item);

    public static implicit operator ItemHandle(Item item) => new(item.RowId);

    public static implicit operator ItemHandle(RowRef<Item> rowRef) => new(rowRef.RowId);

    public static implicit operator ItemHandle(EventItem eventItem) => new(eventItem.RowId);

    public static implicit operator ItemHandle(RowRef<EventItem> rowRef) => new(rowRef.RowId);

    public static implicit operator ItemHandle(ItemLocation itemLocation) => new(itemLocation);

    public static implicit operator ItemHandle(uint itemId) => new(itemId);

    public static implicit operator uint(ItemHandle itemInfo) => itemInfo.ItemId;
}
