using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text.RegularExpressions;

using Dalamud.Bindings.ImGui;

using MasterOfPuppets.Extensions;

using Newtonsoft.Json;

namespace MasterOfPuppets;

/// <summary>
/// A UI theme: a sparse set of ImGui color / style overrides.
/// Anything not present falls back to the Dalamud style, so partial themes are valid.
/// Keys are serialized by enum name, which keeps saved themes stable across ImGui enum reorders.
/// </summary>
public class UiTheme {
    public const string BuiltInPrefix = "builtin:";

    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = "Custom Theme";
    public Dictionary<ImGuiCol, Vector4> Colors { get; set; } = new();
    public Dictionary<ImGuiStyleVar, float> StyleVars { get; set; } = new();

    [JsonIgnore]
    public bool IsBuiltIn { get; init; }

    public UiTheme Clone(string name) => new() {
        Name = name,
        Colors = new Dictionary<ImGuiCol, Vector4>(Colors),
        StyleVars = new Dictionary<ImGuiStyleVar, float>(StyleVars),
    };

    public string ExportToString() => this.JsonSerialize().Compress();

    /// <summary>Accepts the compressed export string or raw JSON (optionally wrapped in a ``` code block).</summary>
    public static UiTheme ImportFromString(string input) {
        if (string.IsNullOrWhiteSpace(input))
            throw new ArgumentException("Theme import string is empty.");

        input = Regex.Replace(input.Trim(), @"^```[a-zA-Z]*\r?\n?|```$", string.Empty, RegexOptions.Multiline).Trim();
        var json = input.StartsWith('{') && input.EndsWith('}') ? input : input.Decompress();

        var theme = json.JsonDeserialize<UiTheme>() ?? throw new ArgumentException("Invalid theme data.");
        theme.Id = Guid.NewGuid().ToString();
        if (string.IsNullOrWhiteSpace(theme.Name))
            theme.Name = "Imported Theme";
        theme.Colors ??= new();
        theme.StyleVars = (theme.StyleVars ?? new())
            .Where(kv => ThemeManager.IsSupportedStyleVar(kv.Key))
            .ToDictionary(kv => kv.Key, kv => kv.Value);
        return theme;
    }
}
