using System;

namespace MasterOfPuppets.Formations;

public static class FormationCharacterName {
    private static readonly char[] CrossWorldSeparators = ['\uE05D', '\uE0B1', '\uE0B2'];

    public static string NormalizeWorldSeparator(string name) {
        if (string.IsNullOrWhiteSpace(name))
            return string.Empty;

        var normalized = name.Trim();
        foreach (var sep in CrossWorldSeparators)
            normalized = normalized.Replace(sep, '@');

        normalized = normalized
            .Replace(" @", "@", StringComparison.Ordinal)
            .Replace("@ ", "@", StringComparison.Ordinal);

        while (normalized.Contains("@@", StringComparison.Ordinal))
            normalized = normalized.Replace("@@", "@", StringComparison.Ordinal);

        return normalized;
    }

    public static string FormatPlayerNameWorld(string playerName, string? worldName, string? fallbackName = null) {
        playerName = NormalizeWorldSeparator(playerName);
        worldName = NormalizeWorldSeparator(worldName ?? string.Empty);
        fallbackName = NormalizeWorldSeparator(fallbackName ?? string.Empty);

        if (playerName.Length > 0 && worldName.Length > 0)
            return $"{playerName}@{worldName}";

        if (fallbackName.Length > 0)
            return fallbackName;

        return playerName;
    }

    public static int MatchScore(string configName, string actorName) {
        if (string.IsNullOrWhiteSpace(configName) || string.IsNullOrWhiteSpace(actorName))
            return -1;

        configName = NormalizeWorldSeparator(configName);
        actorName = NormalizeWorldSeparator(actorName);

        if (string.Equals(configName, actorName, StringComparison.OrdinalIgnoreCase))
            return int.MaxValue;

        var configBaseName = GetBaseCharacterName(configName);
        var actorBaseName = GetBaseCharacterName(actorName);

        if (string.IsNullOrWhiteSpace(configBaseName) || string.IsNullOrWhiteSpace(actorBaseName))
            return -1;

        if (string.Equals(configBaseName, actorBaseName, StringComparison.OrdinalIgnoreCase))
            return int.MaxValue - 1;

        if (actorBaseName.Contains(configBaseName, StringComparison.OrdinalIgnoreCase))
            return configBaseName.Length;

        if (configBaseName.Contains(actorBaseName, StringComparison.OrdinalIgnoreCase))
            return actorBaseName.Length;

        return -1;
    }

    public static string GetBaseCharacterName(string fullName) {
        fullName = NormalizeWorldSeparator(fullName);
        if (fullName.Length == 0)
            return string.Empty;

        var atIndex = fullName.LastIndexOf('@');
        return atIndex >= 0 ? fullName[..atIndex].Trim() : fullName.Trim();
    }

    public static bool Matches(string left, string right) {
        if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right))
            return false;

        left = NormalizeWorldSeparator(left);
        right = NormalizeWorldSeparator(right);

        if (string.Equals(left, right, StringComparison.OrdinalIgnoreCase))
            return true;

        var leftHasWorld = left.Contains('@');
        var rightHasWorld = right.Contains('@');

        if (leftHasWorld && rightHasWorld)
            return false;

        var leftBase = GetBaseCharacterName(left);
        var rightBase = GetBaseCharacterName(right);
        return string.Equals(leftBase, rightBase, StringComparison.OrdinalIgnoreCase);
    }
}
