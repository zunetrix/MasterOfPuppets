using System.Collections.Generic;
using System.Linq;

using Lumina.Excel.Sheets;

namespace MasterOfPuppets;

public static class ActionHelper {
    private static ExecutableAction GetExecutableAction(Action action) {
        return new ExecutableAction {
            ActionId = action.RowId,
            ActionName = action.Name.ToString(),
            IconId = action.Icon,
            TextCommand = $"/action {action.Name}",
            // Category =
            // SortOrder = action.Order
        };
    }

    public static List<ExecutableAction> GetAllowedItems() {
        return DalamudApi.DataManager.GetExcelSheet<Action>()
            .Where(a => a.IsUnlocked())
            .Select(GetExecutableAction)
            .ToList();
    }

    public static Action? GetAction(string actionName) {
        // returns RowId = 0 for invalid names
        var action = DalamudApi.DataManager.GetExcelSheet<Action>()
        .FirstOrDefault(a => string.Equals(a.Name.ToString(), actionName, System.StringComparison.OrdinalIgnoreCase));

        var isActionFound = action.RowId != 0;
        return isActionFound ? action : null;
    }

    public static ExecutableAction? GetExecutableAction(string actionName) {
        var action = GetAction(actionName);
        return action == null ? null : GetExecutableAction(action.Value);
    }

    private static Action? GetAction(uint id) {
        return DalamudApi.DataManager.GetExcelSheet<Action>().GetRowOrDefault(id);
    }

    public static ExecutableAction? GetExecutableAction(uint slotId) {
        var action = GetAction(slotId);
        return action == null ? null : GetExecutableAction(action.Value);
    }

    public static uint GetIconId(uint item) {
        uint undefinedIcon = 60042;
        return GetAction(item)?.Icon ?? undefinedIcon;
    }
}
