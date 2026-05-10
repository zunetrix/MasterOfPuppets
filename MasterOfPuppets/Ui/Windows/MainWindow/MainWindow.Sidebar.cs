using System;
using System.Numerics;

using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;

using MasterOfPuppets.Util.ImGuiExt;

namespace MasterOfPuppets;

public partial class MainWindow : Window {

    private void DrawSidebar() {
        const float collapsedWidth = 38f;
        float expandedWidth = _sidebarWidth * ImGuiHelpers.GlobalScale;
        float width = _sidebarCollapsed ? collapsedWidth * ImGuiHelpers.GlobalScale : expandedWidth;

        var extraActions = new (FontAwesomeIcon Icon, string Label, Action Action)[] {
            (FontAwesomeIcon.Users, "Characters", () => Ui.CharactersWindow.Toggle()),
            (FontAwesomeIcon.ArrowsDownToPeople, "Formation", () => Ui.FormationWindow.Toggle()),
            (FontAwesomeIcon.Display, "Windows Layout", () => Ui.WindowLayoutWindow.Toggle()),
            (FontAwesomeIcon.UsersViewfinder, "PeerMonitor", () => Ui.PeerMonitorWindow.Toggle()),
            (FontAwesomeIcon.Cog, "Settings", () => Ui.SettingsWindow.Toggle()),
            (FontAwesomeIcon.QuestionCircle, "Help", () => Ui.HelpWindow.Toggle()),
        };

        var childFlags = _sidebarCollapsed ? ImGuiWindowFlags.NoScrollbar : ImGuiWindowFlags.None;
        using (ImRaii.Child("##MopNavSidebar", new Vector2(width, -1), true, childFlags)) {

            //  Toggle collapse button
            if (ImGuiUtil.IconButton(FontAwesomeIcon.Bars, "##SidebarToggleBtn", _sidebarCollapsed ? "Expand sidebar" : "Collapse sidebar")) {
                _sidebarCollapsed = !_sidebarCollapsed;
                // hide widget when collapsing to avoid invisible draw
            }

            if (!_sidebarCollapsed) {
                //  Search bar
                ImGui.SameLine();
                ImGui.SetNextItemWidth(-1);
                ImGui.InputTextWithHint("##SidebarSearchInput", "Search...", ref _sidebarSearch, 64);

                ImGui.Spacing();
                ImGui.Separator();
                ImGui.Spacing();

                //  Macros shortcut (opens separate window)
                if (string.IsNullOrEmpty(_sidebarSearch) || "Macros".Contains(_sidebarSearch, StringComparison.OrdinalIgnoreCase)) {
                    DrawNavAction(FontAwesomeIcon.Scroll, "Macros", () => Ui.MacroWindow.Toggle(), false);

                    ImGui.Separator();
                    ImGui.Spacing();
                }

                //  Sections
                bool anyNavDrawn = false;
                foreach (var (section, icon, label, hasSeparatorBefore) in NavItems) {
                    if (!string.IsNullOrEmpty(_sidebarSearch) &&
                        !label.Contains(_sidebarSearch, StringComparison.OrdinalIgnoreCase))
                        continue;

                    if (hasSeparatorBefore && anyNavDrawn) {
                        ImGui.Separator();
                        ImGui.Spacing();
                    }

                    DrawNavItem(section, icon, label);
                    anyNavDrawn = true;
                }

                bool extraSeparatorDrawn = false;
                foreach (var (icon, label, action) in extraActions) {
                    if (!string.IsNullOrEmpty(_sidebarSearch) &&
                        !label.Contains(_sidebarSearch, StringComparison.OrdinalIgnoreCase))
                        continue;

                    if (!extraSeparatorDrawn && anyNavDrawn) {
                        ImGui.Separator();
                        ImGui.Spacing();
                        extraSeparatorDrawn = true;
                    }

                    DrawNavAction(icon, label, action, false);
                }
            } else {
                //  Collapsed: icons only
                ImGui.Spacing();

                DrawNavActionIconOnly(FontAwesomeIcon.Scroll, "Macros", () => Ui.MacroWindow.Toggle());

                ImGui.Spacing();
                ImGui.Separator();
                ImGui.Spacing();

                foreach (var (section, icon, label, hasSeparatorBefore) in NavItems) {
                    if (hasSeparatorBefore) {
                        ImGui.Spacing();
                        ImGui.Separator();
                        ImGui.Spacing();
                    }
                    DrawNavItemIconOnly(section, icon, label);
                }

                ImGui.Spacing();
                ImGui.Separator();
                ImGui.Spacing();

                foreach (var (icon, label, action) in extraActions) {
                    DrawNavActionIconOnly(icon, label, action);
                }
            }
        }
    }

    private void DrawNavItem(NavSection section, FontAwesomeIcon icon, string label) {
        bool isSelected = _selectedSection == section;

        using var headerSel = ImRaii.PushColor(ImGuiCol.Header, Style.Components.ButtonBlueHovered, isSelected)
                                    .Push(ImGuiCol.HeaderHovered, Style.Components.ButtonBlueHovered)
                                    .Push(ImGuiCol.HeaderActive, Style.Components.ButtonBlueHovered);

        ImGuiUtil.IconButton(icon, $"##NavIcon_{section}");
        ImGui.SameLine();
        if (ImGui.Selectable(label, isSelected)) {
            SelectSection(section);
        }
    }

    private void DrawNavAction(FontAwesomeIcon icon, string label, Action action, bool isActive) {
        using var headerSel = ImRaii.PushColor(ImGuiCol.Header, Style.Components.ButtonBlueHovered, isActive)
                                    .Push(ImGuiCol.HeaderHovered, Style.Components.ButtonBlueHovered)
                                    .Push(ImGuiCol.HeaderActive, Style.Components.ButtonBlueHovered);

        ImGuiUtil.IconButton(icon, $"##NavActionIcon_{label}");
        ImGui.SameLine();
        if (ImGui.Selectable(label, isActive)) {
            action();
        }
    }

    private void DrawNavItemIconOnly(NavSection section, FontAwesomeIcon icon, string tooltip) {
        bool isSelected = _selectedSection == section;

        using var col = ImRaii.PushColor(ImGuiCol.Button, Style.Components.ButtonBlueNormal, isSelected)
                               .Push(ImGuiCol.ButtonHovered, Style.Components.ButtonBlueHovered)
                               .Push(ImGuiCol.ButtonActive, Style.Components.ButtonBlueActive);

        if (ImGuiUtil.IconButton(icon, $"##NavIconOnly_{section}", tooltip)) {
            SelectSection(section);
        }
    }

    private void DrawNavActionIconOnly(FontAwesomeIcon icon, string tooltip, Action action) {
        if (ImGuiUtil.IconButton(icon, $"##NavActionIconOnly_{tooltip}", tooltip)) {
            action();
        }
    }

    private void SelectSection(NavSection section) {
        if (_selectedSection == section) return;

        // Hide current widget if switching away from a widget section
        int prevIndex = GetWidgetIndex(_selectedSection);
        if (prevIndex >= 0) {
            // WidgetManager.Show on the new one will hide the old one
        }

        _selectedSection = section;

        // Immediately show/init widget for the new section
        int newIndex = GetWidgetIndex(section);
        if (newIndex >= 0) {
            _widgetManager.Show(newIndex);
        }
    }

    private void DrawSidebarSplitter() {
        const float minSidebarPx = 120f;
        const float maxSidebarPx = 400f;

        var splitterWidth = 6f * ImGuiHelpers.GlobalScale;
        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(0, 0));
        ImGui.InvisibleButton("##SidebarSplitter", new Vector2(splitterWidth, -1));

        if (ImGui.IsItemHovered())
            ImGui.SetMouseCursor(ImGuiMouseCursor.ResizeEw);

        if (ImGui.IsItemActive() && ImGui.IsMouseDragging(ImGuiMouseButton.Left) && !_sidebarCollapsed) {
            _sidebarWidth += ImGui.GetIO().MouseDelta.X / ImGuiHelpers.GlobalScale;
            _sidebarWidth = MathF.Max(minSidebarPx, MathF.Min(_sidebarWidth, maxSidebarPx));
        }
        ImGui.PopStyleVar();
    }
}
