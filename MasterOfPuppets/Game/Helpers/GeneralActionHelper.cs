using System.Collections.Generic;
using System.Linq;

using FFXIVClientStructs.FFXIV.Client.Game.UI;

using Lumina.Excel.Sheets;

namespace MasterOfPuppets;

public static class GeneralActionHelper {
    private static ExecutableAction GetExecutableAction(GeneralAction action) {
        return new ExecutableAction {
            ActionId = action.RowId,
            ActionName = action.Name.ToString(),
            IconId = (uint)action.Icon,
            // HotbarSlotType = HotbarSlotType.GeneralAction,
            TextCommand = $"/gaction \"{action.Name}\"",
            // SortOrder = action.UIPriority,
        };
    }

    public static unsafe List<ExecutableAction> GetAllowedItems() {
        return DalamudApi.DataManager.GetExcelSheet<GeneralAction>()
            .Where(action => action.UnlockLink == 0 || UIState.Instance()->IsUnlockLinkUnlocked(action.UnlockLink))
            .Select(GetExecutableAction).ToList();
    }

    public static GeneralAction? GetGeneralAction(string actionName) {
        // returns RowId = 0 for invalid names
        var action = DalamudApi.DataManager.GetExcelSheet<GeneralAction>(DalamudApi.ClientState.ClientLanguage)
        .FirstOrDefault(a => string.Equals(a.Name.ToString(), actionName, System.StringComparison.OrdinalIgnoreCase));

        var isActionFound = action.RowId != 0;
        return isActionFound ? action : null;
    }

    public static ExecutableAction? GetExecutableAction(string actionName) {
        var action = GetGeneralAction(actionName);
        return action == null ? null : GetExecutableAction(action.Value);
    }

    private static GeneralAction? GetGeneralAction(uint id) {
        // action.Icon is int
        return DalamudApi.DataManager.GetExcelSheet<GeneralAction>().GetRowOrDefault(id);
    }

    public static ExecutableAction? GetExecutableAction(uint slotId) {
        var action = GetGeneralAction(slotId);
        return action == null ? null : GetExecutableAction(action.Value);
    }

    public static uint GetIconId(uint item) {
        uint undefinedIcon = 60042;
        return (uint?)GetGeneralAction(item)?.Icon ?? undefinedIcon;
    }
}
