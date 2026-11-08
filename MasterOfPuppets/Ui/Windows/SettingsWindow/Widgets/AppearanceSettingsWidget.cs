using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text.RegularExpressions;

using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.ImGuiNotification;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;

using MasterOfPuppets.Util.ImGuiExt;

namespace MasterOfPuppets;

/// <summary>
/// Theme selector + editor (based on MidiBard's appearance settings).
/// Built-in themes are read-only ("Copy to edit"). Custom themes store only overridden values:
/// colors without an override show the Dalamud style and the reset button removes the override.
/// </summary>
public class AppearanceSettingsWidget : Widget {
    public override string Title => "Appearance";
    public override FontAwesomeIcon Icon => FontAwesomeIcon.PaintBrush;

    private static readonly (string Title, ImGuiCol[] Colors)[] ColorGroups = [
        ("Texts & Highlights", [ImGuiCol.Text, ImGuiCol.TextDisabled, ImGuiCol.TextSelectedBg]),
        ("Windows & Panels", [
            ImGuiCol.WindowBg, ImGuiCol.ChildBg, ImGuiCol.PopupBg, ImGuiCol.MenuBarBg, ImGuiCol.ModalWindowDimBg,
            ImGuiCol.TitleBg, ImGuiCol.TitleBgActive, ImGuiCol.TitleBgCollapsed]),
        ("Borders & Separators", [
            ImGuiCol.Border, ImGuiCol.BorderShadow, ImGuiCol.Separator, ImGuiCol.SeparatorHovered, ImGuiCol.SeparatorActive,
            ImGuiCol.ResizeGrip, ImGuiCol.ResizeGripHovered, ImGuiCol.ResizeGripActive]),
        ("Inputs & Controls", [
            ImGuiCol.FrameBg, ImGuiCol.FrameBgHovered, ImGuiCol.FrameBgActive, ImGuiCol.CheckMark, ImGuiCol.SliderGrab,
            ImGuiCol.SliderGrabActive, ImGuiCol.ScrollbarBg, ImGuiCol.ScrollbarGrab, ImGuiCol.ScrollbarGrabHovered,
            ImGuiCol.ScrollbarGrabActive, ImGuiCol.DragDropTarget]),
        ("Buttons & Headers", [
            ImGuiCol.Button, ImGuiCol.ButtonHovered, ImGuiCol.ButtonActive,
            ImGuiCol.Header, ImGuiCol.HeaderHovered, ImGuiCol.HeaderActive]),
        ("Tabs & Tables", [
            ImGuiCol.Tab, ImGuiCol.TabHovered, ImGuiCol.TabActive, ImGuiCol.TabUnfocused, ImGuiCol.TabUnfocusedActive,
            ImGuiCol.TableHeaderBg, ImGuiCol.TableBorderStrong, ImGuiCol.TableBorderLight, ImGuiCol.TableRowBg,
            ImGuiCol.TableRowBgAlt, ImGuiCol.DockingPreview, ImGuiCol.DockingEmptyBg]),
        ("Navigation & Misc", [
            ImGuiCol.NavHighlight, ImGuiCol.NavWindowingHighlight, ImGuiCol.NavWindowingDimBg, ImGuiCol.PlotLines,
            ImGuiCol.PlotLinesHovered, ImGuiCol.PlotHistogram, ImGuiCol.PlotHistogramHovered]),
    ];

    // colors not listed above (e.g. added by newer ImGui versions)
    private static readonly ImGuiCol[] OtherColors = Enum.GetValues<ImGuiCol>()
        .Where(c => c is >= 0 and < ImGuiCol.Count && !ColorGroups.Any(g => g.Colors.Contains(c)))
        .Distinct()
        .ToArray();

    private static readonly Dictionary<ImGuiCol, string> ColorLabels = new();

    private static readonly FontAwesomeIcon[] CustomToolbarIcons = [
        FontAwesomeIcon.Clone, FontAwesomeIcon.FileExport, FontAwesomeIcon.FileImport, FontAwesomeIcon.Trash,
    ];

    private string _editingName = string.Empty;
    private string? _editingId;

    public AppearanceSettingsWidget(WidgetContext ctx) : base(ctx) { }

    private Plugin Plugin => Context.Plugin;

    public override void Draw() {
        using (ImGuiGroupPanel.BeginGroupPanel("Theme")) {
            DrawThemeCombo();
            ImGui.Spacing();

            var current = ThemeManager.Current;
            if (current.IsBuiltIn)
                DrawBuiltInToolbar(current);
            else
                DrawCustomToolbar(current);
        }

        var selected = ThemeManager.Current;
        if (selected.IsBuiltIn) {
            ImGui.Spacing();
            ImGui.TextDisabled("Built-in themes are read-only. Use \"Copy to edit\" to customise one.");
            return;
        }

        ImGui.Spacing();
        DrawCustomThemeEditor(selected);
    }

    // selector
    private void DrawThemeCombo() {
        var current = ThemeManager.Current;
        ImGui.SetNextItemWidth(ImGuiGroupPanel.AvailableWidth);
        using var combo = ImRaii.Combo("##ThemeCombo", current.Name);
        if (!combo) return;

        ImGui.TextDisabled(" Built-in ");
        foreach (var theme in ThemeManager.BuiltIn)
            DrawThemeSelectable(theme, current);

        if (Plugin.Config.CustomThemes.Count == 0) return;
        ImGui.Separator();
        ImGui.TextDisabled(" Custom ");
        foreach (var theme in Plugin.Config.CustomThemes)
            DrawThemeSelectable(theme, current);
    }

    private void DrawThemeSelectable(UiTheme theme, UiTheme current) {
        var isSelected = theme.Id == current.Id;
        if (ImGui.Selectable($"{theme.Name}##theme_{theme.Id}", isSelected))
            Select(theme);
        if (isSelected) ImGui.SetItemDefaultFocus();
    }

    private void Select(UiTheme theme) {
        Plugin.Config.SelectedThemeId = theme.Id;
        Plugin.IpcProvider.SyncConfiguration();
    }

    // toolbars
    private void DrawBuiltInToolbar(UiTheme theme) {
        var spacing = ImGui.GetStyle().ItemSpacing.X;
        var width = (ImGuiGroupPanel.AvailableWidth - spacing * 2) / 3f;

        if (ImGui.Button("Copy to edit##CopyBuiltInTheme", new Vector2(width, 0)))
            AddCustom(theme.Clone($"{theme.Name} (Copy)"));

        ImGui.SameLine();
        using (ImRaii.Disabled(theme.Id == ThemeManager.DefaultThemeId)) {
            if (ImGui.Button("Export##ExportBuiltInTheme", new Vector2(width, 0)))
                ExportToClipboard(theme);
        }

        ImGui.SameLine();
        if (ImGui.Button("Import##ImportBuiltInTheme", new Vector2(width, 0)))
            ImportFromClipboard();
    }

    private void DrawCustomToolbar(UiTheme theme) {
        if (_editingId != theme.Id) {
            _editingId = theme.Id;
            _editingName = theme.Name;
        }

        var spacing = ImGui.GetStyle().ItemSpacing.X;
        var buttonsWidth = CustomToolbarIcons.Sum(icon => ImGuiUtil.GetIconButtonWidth(icon) + spacing);
        ImGui.SetNextItemWidth(ImGuiGroupPanel.AvailableWidth - buttonsWidth);
        if (ImGui.InputText("##ThemeName", ref _editingName, 64) && !string.IsNullOrWhiteSpace(_editingName))
            theme.Name = _editingName.Trim();
        if (ImGui.IsItemDeactivatedAfterEdit())
            Plugin.IpcProvider.SyncConfiguration();

        ImGui.SameLine();
        if (ImGuiUtil.IconButton(FontAwesomeIcon.Clone, "##DuplicateTheme", "Duplicate")) {
            AddCustom(theme.Clone($"{theme.Name} (Copy)"));
            return;
        }

        ImGui.SameLine();
        if (ImGuiUtil.IconButton(FontAwesomeIcon.FileExport, "##ExportTheme", "Export to clipboard"))
            ExportToClipboard(theme);

        ImGui.SameLine();
        if (ImGuiUtil.IconButton(FontAwesomeIcon.FileImport, "##ImportTheme", "Import from clipboard")) {
            ImportFromClipboard();
            return;
        }

        ImGui.SameLine();
        if (ImGuiUtil.IconButton(FontAwesomeIcon.Trash, "##DeleteTheme", "Delete (Ctrl+Click)") && ImGui.GetIO().KeyCtrl) {
            Plugin.Config.CustomThemes.Remove(theme);
            Plugin.Config.SelectedThemeId = ThemeManager.DefaultThemeId;
            Plugin.IpcProvider.SyncConfiguration();
        }
    }

    private void AddCustom(UiTheme theme) {
        Plugin.Config.CustomThemes.Add(theme);
        Select(theme);
    }

    // editor
    private void DrawCustomThemeEditor(UiTheme theme) {
        ImGui.TextDisabled("Colors without an override follow the Dalamud style. Changes preview live.");
        ImGui.Spacing();

        var changed = false;
        foreach (var (title, colors) in ColorGroups)
            changed |= DrawColorGroup(theme, title, colors);
        if (OtherColors.Length > 0)
            changed |= DrawColorGroup(theme, "Other", OtherColors);
        changed |= DrawStyleGroup(theme);

        // values preview live every frame; persist/sync only once an edit is committed
        if (changed)
            Plugin.IpcProvider.SyncConfiguration();
    }

    private static bool DrawColorGroup(UiTheme theme, string title, ImGuiCol[] colors) {
        var overrides = colors.Count(theme.Colors.ContainsKey);
        if (!ImGui.CollapsingHeader($"{title} ({overrides}/{colors.Length})###grp_{title}")) return false;

        using var table = ImRaii.Table($"##tbl_{title}", 3, ImGuiTableFlags.SizingStretchProp | ImGuiTableFlags.PadOuterX);
        if (!table) return false;

        ImGui.TableSetupColumn("Property", ImGuiTableColumnFlags.WidthStretch);
        ImGui.TableSetupColumn("Color", ImGuiTableColumnFlags.WidthFixed, 220f * ImGuiHelpers.GlobalScale);
        ImGui.TableSetupColumn("##reset", ImGuiTableColumnFlags.WidthFixed, ImGuiUtil.GetIconButtonWidth(FontAwesomeIcon.Undo));

        var committed = false;
        foreach (var col in colors)
            committed |= DrawColorRow(theme, col);
        return committed;
    }

    private static bool DrawColorRow(UiTheme theme, ImGuiCol col) {
        var label = GetLabel(col);
        var isOverridden = theme.Colors.TryGetValue(col, out var value);
        if (!isOverridden) value = ThemeManager.GetBaseColor(col);

        ImGui.TableNextRow();
        ImGui.TableNextColumn();
        ImGui.AlignTextToFramePadding();
        if (isOverridden) ImGui.TextUnformatted(label);
        else ImGui.TextDisabled(label);

        ImGui.TableNextColumn();
        ImGui.SetNextItemWidth(-1);
        if (ImGui.ColorEdit4($"##col_{col}", ref value, ImGuiColorEditFlags.AlphaPreviewHalf | ImGuiColorEditFlags.AlphaBar))
            theme.Colors[col] = value;
        var committed = ImGui.IsItemDeactivatedAfterEdit();

        ImGui.TableNextColumn();
        using (ImRaii.Disabled(!isOverridden)) {
            if (ImGuiUtil.IconButton(FontAwesomeIcon.Undo, $"##rst_{col}", "Reset to Dalamud style")) {
                theme.Colors.Remove(col);
                committed = true;
            }
        }
        return committed;
    }

    private static bool DrawStyleGroup(UiTheme theme) {
        var vars = ThemeManager.EditableStyleVars;
        var overrides = vars.Count(v => theme.StyleVars.ContainsKey(v.Var));
        if (!ImGui.CollapsingHeader($"Rounding & Borders ({overrides}/{vars.Count})###grp_style")) return false;

        var committed = false;
        foreach (var (var, label, min, max) in vars) {
            var enabled = theme.StyleVars.TryGetValue(var, out var value);
            if (!enabled) value = ThemeManager.GetBaseStyleVar(var);

            if (ImGui.Checkbox($"##en_{var}", ref enabled)) {
                if (enabled) theme.StyleVars[var] = value;
                else theme.StyleVars.Remove(var);
                committed = true;
            }
            ImGuiUtil.ToolTip($"Override {label.ToLowerInvariant()}");

            ImGui.SameLine();
            using (ImRaii.Disabled(!enabled)) {
                ImGui.SetNextItemWidth(220f * ImGuiHelpers.GlobalScale);
                if (ImGui.SliderFloat($"{label}##sty_{var}", ref value, min, max, "%.1f"))
                    theme.StyleVars[var] = value;
                committed |= ImGui.IsItemDeactivatedAfterEdit();
            }
        }
        return committed;
    }

    private static string GetLabel(ImGuiCol col) {
        if (ColorLabels.TryGetValue(col, out var label)) return label;
        return ColorLabels[col] = Regex.Replace(col.ToString(), "(?<=[a-z])(?=[A-Z])", " ");
    }

    // import / export
    private static void ExportToClipboard(UiTheme theme) {
        try {
            ImGui.SetClipboardText(theme.ExportToString());
            DalamudApi.ShowNotification($"Theme '{theme.Name}' exported to clipboard", NotificationType.Success);
        } catch (Exception ex) {
            DalamudApi.PluginLog.Error(ex, "Failed to export theme");
            DalamudApi.ShowNotification("Failed to export theme", NotificationType.Error);
        }
    }

    private void ImportFromClipboard() {
        try {
            var imported = UiTheme.ImportFromString(ImGui.GetClipboardText());
            AddCustom(imported);
            DalamudApi.ShowNotification($"Theme '{imported.Name}' imported", NotificationType.Success);
        } catch (Exception ex) {
            DalamudApi.PluginLog.Error(ex, "Failed to import theme");
            DalamudApi.ShowNotification("Clipboard does not contain a valid theme", NotificationType.Error);
        }
    }
}
