using System.Collections.Generic;
using System.Linq;

using Lumina.Excel.Sheets;

namespace MasterOfPuppets;

public static class TextCommandHelper {
    private static ExecutableAction GetExecutableAction(TextCommand textCommand) {
        uint undefinedIcon = 60042;

        return new ExecutableAction {
            ActionId = textCommand.RowId,
            ActionName = textCommand.Command.ToString(),
            IconId = undefinedIcon,
            TextCommand = $"{textCommand.ShortCommand}",
            // Category =
            // SortOrder = action.Order
        };
    }

    public static List<ExecutableAction> GetAllowedTextCommands() {
        return DalamudApi.DataManager.GetExcelSheet<TextCommand>()
            // .Where(a => a.IsUnlocked())
            .Select(GetExecutableAction)
            .ToList();
    }

    public static TextCommand? GetTextCommand(string textCommandName) {
        // returns RowId = 0 for invalid names
        var textcommand = DalamudApi.DataManager.GetExcelSheet<TextCommand>()
        .FirstOrDefault(a => string.Equals(a.Command.ToString(), textCommandName, System.StringComparison.OrdinalIgnoreCase));

        var isTextCommandFound = textcommand.RowId != 0;
        return isTextCommandFound ? textcommand : null;
    }

    public static ExecutableAction? GetExecutableAction(string textCommandName) {
        var textCommand = GetTextCommand(textCommandName);
        return textCommand == null ? null : GetExecutableAction(textCommand.Value);
    }
}
