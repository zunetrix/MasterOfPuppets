using System;
using System.Numerics;

using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;

using MasterOfPuppets.Resources;
using MasterOfPuppets.Util.ImGuiExt;

namespace MasterOfPuppets;

public class SettingsWindow : Window {
    private Plugin Plugin { get; }
    private readonly WidgetContext _widgetContext;
    private readonly WidgetManager _widgetManager = new();
    private int _selectedIndex;
    private float _sidebarWidth = 150f;

    public SettingsWindow(Plugin plugin) : base($"{Plugin.Name} {Language.SettingsTitle}###SettingsWindowMop") {
        Plugin = plugin;
        _widgetContext = new WidgetContext(plugin);

        Size = ImGuiHelpers.ScaledVector2(650, 500);
        SizeCondition = ImGuiCond.FirstUseEver;
        SizeConstraints = new WindowSizeConstraints { MinimumSize = ImGuiHelpers.ScaledVector2(500, 300) };

        _widgetManager.Add(() => new GeneralSettingsWidget(_widgetContext));
        _widgetManager.Add(() => new MultiboxSettingsWidget(_widgetContext));
        _widgetManager.Add(() => new BroadcastSettingsWidget(_widgetContext));
        _widgetManager.Add(() => new MacrosSettingsWidget(_widgetContext));
        _widgetManager.Add(() => new CommandsSettingsWidget(_widgetContext));
        _widgetManager.Add(() => new RemoteControlSettingsWidget(_widgetContext));
    }

    public override void PreDraw() {
        Flags = ImGuiWindowFlags.None;
        base.PreDraw();
    }

    public override void Draw() {
        DrawSidePanel();
        ImGui.SameLine();
        DrawSplitter();
        ImGui.SameLine();
        DrawContent();
    }

    private void DrawSidePanel() {
        var scaledWidth = _sidebarWidth * ImGuiHelpers.GlobalScale;
        using var listChild = ImRaii.Child("##List", new Vector2(scaledWidth, 0), true);
        if (!listChild) return;

        for (int i = 0; i < _widgetManager.Widgets.Count; i++) {
            var widget = _widgetManager.Widgets[i];

            bool isSelected = _selectedIndex == i;

            using (ImRaii.PushColor(ImGuiCol.Header, Style.Components.ButtonPurpleHovered, isSelected)
                         .Push(ImGuiCol.HeaderHovered, Style.Components.ButtonPurpleHovered, isSelected)
                         .Push(ImGuiCol.HeaderActive, Style.Components.ButtonPurpleHovered, isSelected)) {
                if (ImGui.Selectable(widget.Instance.Title, isSelected)) {
                    _selectedIndex = i;
                    _widgetManager.Show(i);
                }
            }
        }
    }

    private void DrawSplitter() {
        const float minWidthPx = 80f;
        const float maxWidthPx = 250f;

        using (ImRaii.PushStyle(ImGuiStyleVar.FramePadding, Vector2.Zero)) {
            ImGui.InvisibleButton("##SettingsSplitter", ImGuiHelpers.ScaledVector2(5, -1));
            if (ImGui.IsItemHovered())
                ImGui.SetMouseCursor(ImGuiMouseCursor.ResizeEw);

            if (ImGui.IsItemActive() && ImGui.IsMouseDragging(ImGuiMouseButton.Left)) {
                _sidebarWidth += ImGui.GetIO().MouseDelta.X / ImGuiHelpers.GlobalScale;
                _sidebarWidth = Math.Clamp(_sidebarWidth, minWidthPx, maxWidthPx);
            }
        }
    }

    private void DrawContent() {
        if (_widgetManager.Widgets.Count == 0) return;
        var widget = _widgetManager.Widgets[_selectedIndex];

        using var contentChild = ImRaii.Child("##Content", new Vector2(0, 0));
        if (!contentChild) return;

        ImGuiUtil.DrawColoredBanner(widget.Instance.Title, Style.Components.ButtonPurpleHovered);
        ImGui.Spacing();
        _widgetManager.Draw();
    }
}
