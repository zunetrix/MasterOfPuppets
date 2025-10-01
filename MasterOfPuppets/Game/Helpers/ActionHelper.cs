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

    public static Action? GetActionByName(string actionName) {
        // returns RowId = 0 for invalid names
        var action = DalamudApi.DataManager.GetExcelSheet<Action>()!
        .FirstOrDefault(a => string.Equals(a.Name.ToString(), actionName, System.StringComparison.OrdinalIgnoreCase));

        var isActionFound = action.RowId != 0;
        return isActionFound ? action : null;
    }

    public static ExecutableAction? GetExecutableActionByName(string actionName) {
        var action = GetActionByName(actionName);
        return action == null ? null : GetExecutableAction(action.Value);
    }

    private static Action? GetActionById(uint id) {
        return DalamudApi.DataManager.GetExcelSheet<Action>()!.GetRowOrDefault(id);
    }

    public static ExecutableAction? GetExecutableActionById(uint slotId) {
        var action = GetActionById(slotId);
        return action == null ? null : GetExecutableAction(action.Value);
    }

    public static uint GetIconId(uint item) {
        uint undefinedIcon = 60042;
        return GetActionById(item)?.Icon ?? undefinedIcon;
    }
}
