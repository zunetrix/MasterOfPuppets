using System;

namespace MasterOfPuppets;

public static class AutoLoginDialogMatcher {
    public static bool IsMissingLastLoggedOutCharacterSelectOk(string text) {
        if (string.IsNullOrWhiteSpace(text))
            return false;

        var normalized = Normalize(text);
        return normalized.Contains("last logged out", StringComparison.OrdinalIgnoreCase) &&
               normalized.Contains("could not be found", StringComparison.OrdinalIgnoreCase) &&
               normalized.Contains("current data center", StringComparison.OrdinalIgnoreCase);
    }

    private static string Normalize(string text) =>
        string.Join(' ', text.Split([' ', '\t', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries));
}
