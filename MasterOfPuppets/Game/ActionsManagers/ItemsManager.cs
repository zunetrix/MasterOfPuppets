using System;
using System.Collections.Generic;
using System.Linq;

using Lumina.Excel.Sheets;

namespace MasterOfPuppets;

public static class ItemsManager
{
    private static ExecutableAction GetExecutableAction(Item item)
    {
        return new ExecutableAction
        {
            ActionId = item.RowId,
            // ActionName = item.Name.ToString(),
            ActionName = item.Singular.ToString(),
            IconId = item.Icon,
            TextCommand = $"/item {item.RowId}",
            // TextCommandAlias = emote.TextCommand.Value.Alias.ToList(),
            // Category = null,
            // SortOrder = emote.Order
        };
    }

    public static List<ExecutableAction> GetAllowedItems()
    {
        return DalamudApi.DataManager.GetExcelSheet<Item>()
            .Where(i => i.Singular.ToString().Equals("Heavenscracker", StringComparison.OrdinalIgnoreCase))
            .Select(GetExecutableAction)
            .ToList();
    }

    public static Item? GetItemByName(string itemName)
    {
        return DalamudApi.DataManager.Excel.GetSheet<Item>()!
        .FirstOrDefault(i => i.Singular.ToString() == itemName);
    }

    public static ExecutableAction? GetExecutableActionByName(string itemName)
    {
        var item = GetItemByName(itemName);
        return item == null ? null : GetExecutableAction(item.Value);
    }

    private static Item? GetItemById(uint id)
    {
        return DalamudApi.DataManager.Excel.GetSheet<Item>().GetRowOrDefault(id);
    }

    public static ExecutableAction? GetExecutableActionById(uint slotId)
    {
        var item = GetItemById(slotId);
        return item == null ? null : GetExecutableAction(item.Value);
    }

    public static uint GetIconId(uint item)
    {
        uint undefinedIcon = 60042;
        return GetItemById(item)?.Icon ?? undefinedIcon;
    }

}
