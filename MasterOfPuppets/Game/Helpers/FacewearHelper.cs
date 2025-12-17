using System.Collections.Generic;
using System.Linq;

using Lumina.Excel.Sheets;

namespace MasterOfPuppets;

public static class FacewearHelper {
    private static ExecutableAction GetExecutableAction(Glasses facewear) {
        return new ExecutableAction {
            ActionId = facewear.RowId,
            ActionName = facewear.Name.ToString(),
            IconId = (uint)facewear.Icon,
            TextCommand = $"/facewear \"{facewear.Name}\"",
            // Category = facewear.Style.ValueNullable?.Name.ToString() ?? "Unknown",
            // SortOrder = (int)(((facewear.Style.ValueNullable?.Order ?? 0) << sizeof(ushort)) + facewear.RowId),
        };
    }

    public static List<ExecutableAction> GetAllowedItems() {
        return DalamudApi.DataManager.GetExcelSheet<Glasses>()
            .Where(f => f.IsUnlocked())
            .Select(GetExecutableAction)
            .ToList();
    }

    private static Glasses? GetFacewear(uint id) {
        return DalamudApi.DataManager.Excel.GetSheet<Glasses>().GetRowOrDefault(id);
    }

    public static ExecutableAction? GetExecutableAction(uint actionId) {
        var action = GetFacewear(actionId);
        return action == null ? null : GetExecutableAction(action.Value);
    }

    public static uint GetIconId(uint item) {
        uint undefinedIcon = 60042;
        return (uint?)GetFacewear(item)?.Icon ?? undefinedIcon;
    }
}
