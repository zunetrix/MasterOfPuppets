using System;
using System.Collections.Generic;

namespace MasterOfPuppets;

public sealed class MacroRuntimeVariables {
    public string Me { get; init; } = string.Empty;
    public string Target { get; init; } = string.Empty;

    public Dictionary<string, string> ToDictionary() => new() {
        ["me"] = Me,
        ["target"] = Target,
    };

    public Dictionary<string, string> ResolveInlinePlaceholders(Dictionary<string, string>? inlineVars) {
        var result = new Dictionary<string, string>();
        if (inlineVars == null)
            return result;

        foreach (var (key, value) in inlineVars)
            result[key] = ResolvePlaceholder(value);

        return result;
    }

    private string ResolvePlaceholder(string value) {
        if (value.Equals("<me>", StringComparison.OrdinalIgnoreCase))
            return Me;
        if (value.Equals("<t>", StringComparison.OrdinalIgnoreCase))
            return Target;
        return value;
    }

    public static MacroRuntimeVariables Empty { get; } = new();

    public static MacroRuntimeVariables FromCurrentGameState() {
        var me = string.Empty;
        try {
            if (!string.IsNullOrWhiteSpace(DalamudApi.PlayerState.CharacterName)) {
                var world = DalamudApi.PlayerState.HomeWorld.Value.Name.ToString();
                me = string.IsNullOrWhiteSpace(world)
                    ? DalamudApi.PlayerState.CharacterName
                    : $"{DalamudApi.PlayerState.CharacterName}@{world}";
            }
        } catch {
            me = string.Empty;
        }

        return new MacroRuntimeVariables {
            Me = me,
            Target = GameTargetManager.GetTargetName(),
        };
    }
}
