using System.Collections.Generic;
using System.Linq;

using Lumina.Excel.Sheets;

namespace MasterOfPuppets;

public static class MinionHelper {
    private static ExecutableAction GetExecutableAction(Companion minion) {
        return new ExecutableAction {
            ActionId = minion.RowId,
            ActionName = minion.Singular.ToString(),
            IconId = minion.Icon,
            TextCommand = $"/minion \"{minion.Singular}\"",
            // SortOrder = minion.Order
        };
    }

    public static List<ExecutableAction> GetAllowedItems() {
        return DalamudApi.DataManager.GetExcelSheet<Companion>()
            .Where(m => m.IsUnlocked())
            .Select(GetExecutableAction)
            .ToList();
    }

    private static Companion? GetMinionById(uint id) {
        return DalamudApi.DataManager.Excel.GetSheet<Companion>().GetRowOrDefault(id);
    }

    public static ExecutableAction? GetExecutableActionById(uint actionId) {
        var action = GetMinionById(actionId);
        return action == null ? null : GetExecutableAction(action.Value);
    }

    public static uint GetIconId(uint item) {
        uint undefinedIcon = 60042;
        return GetMinionById(item)?.Icon ?? undefinedIcon;
    }
}
