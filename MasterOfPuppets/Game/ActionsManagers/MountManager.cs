using System.Collections.Generic;
using System.Linq;

using Lumina.Excel.Sheets;

namespace MasterOfPuppets;

public static class MountManager
{
    private static ExecutableAction GetExecutableAction(Mount mount)
    {
        return new ExecutableAction
        {
            ActionId = mount.RowId,
            ActionName = mount.Singular.ToString(),
            IconId = mount.Icon,
            TextCommand = $"/mount \"{mount.Singular}\"",
            // SortOrder = (mount.UIPriority << 8) + mount.Order
        };
    }

    public static List<ExecutableAction> GetAllowedItems()
    {
        return DalamudApi.DataManager.GetExcelSheet<Mount>()
            .Where(m => m.IsUnlocked())
            .Select(GetExecutableAction)
            .ToList();
    }

    private static Mount? GetMountById(uint id)
    {
        return DalamudApi.DataManager.Excel.GetSheet<Mount>().GetRowOrDefault(id);
    }

    public static ExecutableAction? GetExecutableActionById(uint actionId)
    {
        var action = GetMountById(actionId);
        return action == null ? null : GetExecutableAction(action.Value);
    }

    public static uint GetIconId(uint item)
    {
        uint undefinedIcon = 60042;
        return GetMountById(item)?.Icon ?? undefinedIcon;

    }

}
