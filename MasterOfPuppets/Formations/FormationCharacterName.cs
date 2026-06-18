using System;

namespace MasterOfPuppets.Formations;

public static class FormationCharacterName {
    private const char CrossWorldPrefix = '\uE0B1';
    private const char CrossWorldSeparator = '\uE0B2';

    public static string NormalizeWorldSeparator(string name) {
        if (string.IsNullOrWhiteSpace(name))
            return string.Empty;

        var normalized = name.Trim()
            .Replace(CrossWorldPrefix, '@')
            .Replace(CrossWorldSeparator, '@')
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
        configName = NormalizeWorldSeparator(configName);
        actorName = NormalizeWorldSeparator(actorName);

        if (string.Equals(configName, actorName, StringComparison.OrdinalIgnoreCase))
            return int.MaxValue;

        var configBaseName = GetBaseCharacterName(configName);
        var actorBaseName = GetBaseCharacterName(actorName);

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
}
