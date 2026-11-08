using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;

namespace MasterOfPuppets;

/// <summary>
/// Resolves the active <see cref="UiTheme"/> from the configuration and pushes it around window draws
/// (see <see cref="ThemedWindow"/>). The theme is resolved every frame by id, so selection changes,
/// in-place edits and configuration syncs from other clients apply without explicit refresh calls.
/// </summary>
internal static class ThemeManager {
    /// <summary>"No override": windows keep the Dalamud style.</summary>
    public const string DefaultThemeId = UiTheme.BuiltInPrefix + "dalamud";

    public static readonly UiTheme DefaultTheme = new() { Id = DefaultThemeId, Name = "Dalamud (Default)", IsBuiltIn = true };

    private static Configuration? _config;

    // Dalamud style colors captured before any theme push (shown by the editor for non-overridden colors)
    private static readonly Vector4[] BaseColors = new Vector4[(int)ImGuiCol.Count];
    private static int _baseColorsFrame = -1;

    public static void Initialize(Configuration config) => _config = config;

    public static IEnumerable<UiTheme> BuiltIn => BuiltInThemes.All.Prepend(DefaultTheme);

    public static UiTheme Current => _config == null ? DefaultTheme : Find(_config.SelectedThemeId) ?? DefaultTheme;

    public static UiTheme? Find(string? id) {
        if (string.IsNullOrEmpty(id)) return null;
        if (id == DefaultThemeId) return DefaultTheme;
        if (id.StartsWith(UiTheme.BuiltInPrefix, StringComparison.Ordinal))
            return BuiltInThemes.All.FirstOrDefault(t => t.Id == id);
        return _config?.CustomThemes.FirstOrDefault(t => t.Id == id);
    }

    public static Vector4 GetBaseColor(ImGuiCol col) => BaseColors[(int)col];

    /// <summary>Keeps fixed-color icons visible on the theme's buttons (the Dalamud style is left as is).</summary>
    public static Vector4 ReadableIconColor(Vector4 color) =>
        Current.Colors.TryGetValue(ImGuiCol.Button, out var button)
            ? BuiltInThemes.Readable(color, button, 3f, Resolve(ImGuiCol.Text, Vector4.One))
            : color;

    /// <summary>The active theme's color when it overrides <paramref name="col"/>, otherwise the fixed plugin color.</summary>
    public static Vector4 Resolve(ImGuiCol col, Vector4 fallback) =>
        Current.Colors.TryGetValue(col, out var color) ? color : fallback;

    /// <summary>
    /// Highlight for list selectables: the plugin accent on the Dalamud style; themes that set the header colors keep them.
    /// </summary>
    /// <param name="selected">Whether the accent applies to this item.</param>
    /// <param name="accentHover">Also use the accent when hovering an unselected item.</param>
    /// <param name="accent">Accent color (default blue).</param>
    public static ImRaii.ColorDisposable PushSelectionColors(bool selected = true, bool accentHover = false, Vector4? accent = null) {
        var color = accent ?? Style.Components.ButtonBlueHovered;
        var theme = Current.Colors;
        var hover = selected || accentHover;
        return ImRaii.PushColor(ImGuiCol.Header, color, selected && !theme.ContainsKey(ImGuiCol.Header))
            .Push(ImGuiCol.HeaderHovered, color, hover && !theme.ContainsKey(ImGuiCol.HeaderHovered))
            .Push(ImGuiCol.HeaderActive, color, hover && !theme.ContainsKey(ImGuiCol.HeaderActive));
    }

    /// <summary>Pushes <see cref="Current"/>; dispose to pop. Also snapshots the base style once per frame.</summary>
    public static IDisposable PushCurrent() {
        CaptureBaseColors();
        return new ThemeScope(Current);
    }

    private static void CaptureBaseColors() {
        var frame = ImGui.GetFrameCount();
        if (_baseColorsFrame == frame) return;
        _baseColorsFrame = frame;
        var colors = ImGui.GetStyle().Colors;
        for (var i = 0; i < BaseColors.Length; i++)
            BaseColors[i] = colors[i];
    }

    //  style vars

    /// <summary>Float style vars editable in the theme editor (label, min, max).</summary>
    public static readonly IReadOnlyList<(ImGuiStyleVar Var, string Label, float Min, float Max)> EditableStyleVars = [
        (ImGuiStyleVar.WindowRounding, "Window Rounding", 0f, 14f),
        (ImGuiStyleVar.ChildRounding, "Child Rounding", 0f, 14f),
        (ImGuiStyleVar.PopupRounding, "Popup Rounding", 0f, 14f),
        (ImGuiStyleVar.FrameRounding, "Frame Rounding", 0f, 12f),
        (ImGuiStyleVar.GrabRounding, "Grab Rounding", 0f, 12f),
        (ImGuiStyleVar.ScrollbarRounding, "Scrollbar Rounding", 0f, 12f),
        (ImGuiStyleVar.TabRounding, "Tab Rounding", 0f, 12f),
        (ImGuiStyleVar.WindowBorderSize, "Window Border Size", 0f, 3f),
        (ImGuiStyleVar.ChildBorderSize, "Child Border Size", 0f, 3f),
        (ImGuiStyleVar.PopupBorderSize, "Popup Border Size", 0f, 3f),
        (ImGuiStyleVar.FrameBorderSize, "Frame Border Size", 0f, 3f),
    ];

    /// <summary>Only float style vars can be pushed with a float (Vector2 vars would assert in ImGui).</summary>
    public static bool IsSupportedStyleVar(ImGuiStyleVar var) => EditableStyleVars.Any(v => v.Var == var);

    public static float GetBaseStyleVar(ImGuiStyleVar var) {
        var style = ImGui.GetStyle();
        return var switch {
            ImGuiStyleVar.WindowRounding => style.WindowRounding,
            ImGuiStyleVar.ChildRounding => style.ChildRounding,
            ImGuiStyleVar.PopupRounding => style.PopupRounding,
            ImGuiStyleVar.FrameRounding => style.FrameRounding,
            ImGuiStyleVar.GrabRounding => style.GrabRounding,
            ImGuiStyleVar.ScrollbarRounding => style.ScrollbarRounding,
            ImGuiStyleVar.TabRounding => style.TabRounding,
            ImGuiStyleVar.WindowBorderSize => style.WindowBorderSize,
            ImGuiStyleVar.ChildBorderSize => style.ChildBorderSize,
            ImGuiStyleVar.PopupBorderSize => style.PopupBorderSize,
            ImGuiStyleVar.FrameBorderSize => style.FrameBorderSize,
            _ => 0f,
        };
    }

    //  scope

    private sealed class ThemeScope : IDisposable {
        private readonly ImRaii.ColorDisposable _colors = new();
        private readonly ImRaii.StyleDisposable _styles = new();

        public ThemeScope(UiTheme theme) {
            foreach (var (col, value) in theme.Colors)
                if (col is >= 0 and < ImGuiCol.Count)
                    _colors.Push(col, value);

            foreach (var (var, value) in theme.StyleVars)
                if (IsSupportedStyleVar(var))
                    _styles.Push(var, value);
        }

        public void Dispose() {
            _styles.Dispose();
            _colors.Dispose();
        }
    }
}
