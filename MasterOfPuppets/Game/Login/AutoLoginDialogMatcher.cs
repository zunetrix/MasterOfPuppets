using System;
using System.Text.RegularExpressions;

using Lumina.Excel.Sheets;
using Lumina.Text.ReadOnly;

namespace MasterOfPuppets;

public static class AutoLoginDialogMatcher {

    // The character you last logged out with in this play environment could not be found on the current data center.<br>Please connect to another data center from the Data Center Selection screen.
    private static ReadOnlySeString TextLastLoggedOut =>
        DalamudApi.DataManager.GetExcelSheet<Lobby>(DalamudApi.ClientState.ClientLanguage)
            ?.GetRow(1237).Text
        ?? default;

    public static bool IsMissingLastLoggedOutCharacterSelectOk(string text) {
        if (string.IsNullOrWhiteSpace(text))
            return false;

        return Normalize(text).Contains(Normalize(TextLastLoggedOut.ToString()), StringComparison.OrdinalIgnoreCase);
    }

    private static string Normalize(string text) =>
        Regex.Replace(text, @"\s+", " ").Trim();
}
