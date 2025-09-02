using System.Collections.Generic;
using System.Linq;

using Lumina.Excel.Sheets;

namespace MasterOfPuppets;

public static class FashionAccessoriesHelper
{
    private static ExecutableAction GetExecutableAction(Ornament ornament)
    {
        return new ExecutableAction
        {
            ActionId = ornament.RowId,
            ActionName = ornament.Singular.ToString(),
            IconId = ornament.Icon,
            TextCommand = $"/fashion \"{ornament.Singular}\"",
            // SortOrder = ornament.Order
        };
    }

    public static List<ExecutableAction> GetAllowedItems()
    {
        var excludedIds = new uint[] { 32 };
        return DalamudApi.DataManager.GetExcelSheet<Ornament>()!
            .Where(o => o.IsUnlocked() && !excludedIds.Contains(o.RowId))
            .Select(GetExecutableAction)
            .ToList();
    }

    private static Ornament? GetOrnamentById(uint id)
    {
        return DalamudApi.DataManager.Excel.GetSheet<Ornament>().GetRowOrDefault(id);
    }

    public static ExecutableAction? GetExecutableActionById(uint actionId)
    {
        var action = GetOrnamentById(actionId);
        return action == null ? null : GetExecutableAction(action.Value);
    }

    public static uint GetIconId(uint item)
    {
        uint undefinedIcon = 60042;
        return GetOrnamentById(item)?.Icon ?? undefinedIcon;
    }
}
