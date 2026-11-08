using System;
using System.Numerics;

using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;

using MasterOfPuppets.Util.ImGuiExt;

namespace MasterOfPuppets;

public partial class MainWindow : ThemedWindow {

    private void DrawSidebar() {
        const float collapsedWidth = 38f;
        float expandedWidth = _sidebarWidth * ImGuiHelpers.GlobalScale;
        float width = _sidebarCollapsed ? collapsedWidth * ImGuiHelpers.GlobalScale : expandedWidth;

        var childFlags = _sidebarCollapsed ? ImGuiWindowFlags.NoScrollbar : ImGuiWindowFlags.None;
        using (ImRaii.Child("##MopNavSidebar", new Vector2(width, -1), true, childFlags)) {

            // Toggle collapse button
            if (ImGuiUtil.IconButton(FontAwesomeIcon.Bars, "##SidebarToggleBtn", _sidebarCollapsed ? "Expand sidebar" : "Collapse sidebar")) {
                _sidebarCollapsed = !_sidebarCollapsed;
            }

            var items = Plugin.Config.MainMenuItems;

            if (!_sidebarCollapsed) {
                // Search bar
                ImGui.SameLine();
                ImGui.SetNextItemWidth(-1);
                ImGui.InputTextWithHint("##SidebarSearchInput", "Search...", ref _sidebarSearch, 64);

                ImGui.Spacing();
                ImGui.Separator();
                ImGui.Spacing();

                foreach (var cfg in items) {
                    if (!cfg.Enabled) continue;
                    if (!MainMenuRegistry.TryGetDef(cfg.Id, out var def)) continue;

                    if (!string.IsNullOrEmpty(_sidebarSearch) &&
                        !def.Label.Contains(_sidebarSearch, StringComparison.OrdinalIgnoreCase))
                        continue;

                    if (def.Kind == MenuItemKind.View) {
                        DrawNavItem(def.Id, def.Icon, def.Label);
                    } else {
                        DrawNavAction(def.Icon, def.Label, () => def.OnClick?.Invoke(Ui), false);
                    }
                }
            } else {
                // Collapsed: icons only
                ImGui.Spacing();

                foreach (var cfg in items) {
                    if (!cfg.Enabled) continue;
                    if (!MainMenuRegistry.TryGetDef(cfg.Id, out var def)) continue;

                    if (def.Kind == MenuItemKind.View) {
                        DrawNavItemIconOnly(def.Id, def.Icon, def.Label);
                    } else {
                        DrawNavActionIconOnly(def.Icon, def.Label, () => def.OnClick?.Invoke(Ui));
                    }
                }
            }
        }
    }

    private void DrawNavItem(string sectionId, FontAwesomeIcon icon, string label) {
        bool isSelected = _selectedSection.Equals(sectionId, StringComparison.OrdinalIgnoreCase);

        using var headerSel = ThemeManager.PushSelectionColors(isSelected, accentHover: true);

        ImGuiUtil.IconButton(icon, $"##NavIcon_{sectionId}");
        ImGui.SameLine();
        if (ImGui.Selectable(label, isSelected)) {
            SelectSection(sectionId);
        }
    }

    private void DrawNavAction(FontAwesomeIcon icon, string label, Action action, bool isActive) {
        using var headerSel = ThemeManager.PushSelectionColors(isActive, accentHover: true);

        ImGuiUtil.IconButton(icon, $"##NavActionIcon_{label}");
        ImGui.SameLine();
        if (ImGui.Selectable(label, isActive)) {
            action();
        }
    }

    private void DrawNavItemIconOnly(string sectionId, FontAwesomeIcon icon, string tooltip) {
        bool isSelected = _selectedSection.Equals(sectionId, StringComparison.OrdinalIgnoreCase);

        using var col = ImRaii.PushColor(ImGuiCol.Button, ThemeManager.Resolve(ImGuiCol.Header, Style.Components.ButtonBlueNormal), isSelected)
                               .Push(ImGuiCol.ButtonHovered, ThemeManager.Resolve(ImGuiCol.HeaderHovered, Style.Components.ButtonBlueHovered))
                               .Push(ImGuiCol.ButtonActive, ThemeManager.Resolve(ImGuiCol.HeaderActive, Style.Components.ButtonBlueActive));

        if (ImGuiUtil.IconButton(icon, $"##NavIconOnly_{sectionId}", tooltip)) {
            SelectSection(sectionId);
        }
    }

    private void DrawNavActionIconOnly(FontAwesomeIcon icon, string tooltip, Action action) {
        if (ImGuiUtil.IconButton(icon, $"##NavActionIconOnly_{tooltip}", tooltip)) {
            action();
        }
    }

    private void SelectSection(string sectionId) {
        if (_selectedSection.Equals(sectionId, StringComparison.OrdinalIgnoreCase)) return;

        _selectedSection = sectionId;

        if (MainMenuRegistry.TryGetDef(sectionId, out var def) && def.WidgetIndex.HasValue) {
            _widgetManager.Show(def.WidgetIndex.Value);
        }
    }

    private void DrawSidebarSplitter() {
        const float minSidebarPx = 120f;
        const float maxSidebarPx = 400f;

        var splitterWidth = 6f * ImGuiHelpers.GlobalScale;
        using (ImRaii.PushStyle(ImGuiStyleVar.FramePadding, Vector2.Zero)) {
            ImGui.InvisibleButton("##SidebarSplitter", new Vector2(splitterWidth, -1));

            if (ImGui.IsItemHovered())
                ImGui.SetMouseCursor(ImGuiMouseCursor.ResizeEw);

            if (ImGui.IsItemActive() && ImGui.IsMouseDragging(ImGuiMouseButton.Left) && !_sidebarCollapsed) {
                _sidebarWidth += ImGui.GetIO().MouseDelta.X / ImGuiHelpers.GlobalScale;
                _sidebarWidth = MathF.Max(minSidebarPx, MathF.Min(_sidebarWidth, maxSidebarPx));
            }
        }
    }
}
