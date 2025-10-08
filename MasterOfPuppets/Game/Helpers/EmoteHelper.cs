using System.Collections.Generic;
using System.Linq;

using Lumina.Excel.Sheets;

using MasterOfPuppets.Util;

namespace MasterOfPuppets;

public static class EmoteHelper {
    private static ExecutableAction GetExecutableAction(Emote emote) {
        return new ExecutableAction {
            ActionId = emote.RowId,
            ActionName = emote.Name.ToString(),
            IconId = emote.Icon,
            TextCommand = $"{emote.TextCommand.Value.Command}",
            // TextCommandAlias = emote.TextCommand.Value.Alias.ToList(),
            Category = emote.EmoteCategory.ValueNullable?.Name.ToString() ?? null,
            // SortOrder = emote.Order
        };
    }

    public static List<ExecutableAction> GetAllowedItems() {
        return DalamudApi.DataManager.GetExcelSheet<Emote>()
            .Where(e => e.IsUnlocked() && e.EmoteCategory.ValueNullable?.Name.ToString() != "Expressions")
            .Select(GetExecutableAction)
            .ToList();
    }

    private static Emote? GetEmoteById(uint id) {
        return DalamudApi.DataManager.Excel.GetSheet<Emote>().GetRowOrDefault(id);
    }

    public static ExecutableAction? GetExecutableActionById(uint slotId) {
        var emote = GetEmoteById(slotId);
        return emote == null ? null : GetExecutableAction(emote.Value);
    }

    public static uint GetIconId(uint item) {
        uint undefinedIcon = 60042;
        return GetEmoteById(item)?.Icon ?? undefinedIcon;
    }

}
