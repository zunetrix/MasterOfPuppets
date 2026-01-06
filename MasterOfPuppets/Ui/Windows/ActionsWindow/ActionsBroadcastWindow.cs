using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Windowing;

using MasterOfPuppets.Actions;
using MasterOfPuppets.Resources;
using MasterOfPuppets.Util.ImGuiExt;

namespace MasterOfPuppets;

public class ActionsBroadcastWindow : Window {
    private Plugin Plugin { get; }

    private int SelectedItemIndex = 0;

    private string _searchString = string.Empty;
    private readonly List<int> ListSearchedIndexes = new();

    private readonly WidgetContext? _widgetContext;

    private readonly WidgetManager _widgetManager = new();

    public ActionsBroadcastWindow(Plugin plugin) : base($"{Plugin.Name}###ActionsBroadcastWindow") {
        Plugin = plugin;

        _widgetContext = new WidgetContext(plugin);

        Size = ImGuiHelpers.ScaledVector2(550, 450);
        SizeCondition = ImGuiCond.FirstUseEver;
        // SizeCondition = ImGuiCond.Always;
        // Flags = ImGuiWindowFlags.NoResize;

        _widgetManager.Add(() => new EmotesWidget(_widgetContext));
        _widgetManager.Add(() => new FashionAccessoriesWidget(_widgetContext));
        _widgetManager.Add(() => new FacewearWidget(_widgetContext));
        _widgetManager.Add(() => new MountsWidget(_widgetContext));
        _widgetManager.Add(() => new MinionsWidget(_widgetContext));
        _widgetManager.Add(() => new ItemsWidget(_widgetContext));
        _widgetManager.Add(() => new GearSetWidget(_widgetContext));

        _widgetManager.Show(0);
    }

    public override void Draw() {
        ImGui.BeginGroup();
        DrawHeader();
        ImGui.EndGroup();

        ImGui.BeginChild("##ActionsBroadcastScrollableContent", new Vector2(-1, 0), false, ImGuiWindowFlags.HorizontalScrollbar);
        if (Plugin.Config.ShowPanelActionsBroadcast) {
            DrawDebugTypeList();
        }

        ImGui.SameLine();
        DrawTypeContent(SelectedItemIndex);
        ImGui.EndChild();
    }

    private void DrawDebugTypeList() {
        var isFiltered = !string.IsNullOrEmpty(_searchString);

        var indices = isFiltered ? ListSearchedIndexes : Enumerable.Range(0, _widgetManager.Widgets.Count).ToList();

        // left pane
        ImGui.BeginChild("##ActionsTypeList", ImGuiHelpers.ScaledVector2(200, 0), true);
        for (int i = 0; i < indices.Count; i++) {
            int realIndex = indices[i];
            var widget = _widgetManager.Widgets[realIndex];
            bool isSelected = SelectedItemIndex == realIndex;

            if (isSelected) {
                ImGui.PushStyleColor(ImGuiCol.Header, Style.Components.ButtonBlueHovered);
                ImGui.PushStyleColor(ImGuiCol.HeaderHovered, Style.Components.ButtonBlueHovered);
                ImGui.PushStyleColor(ImGuiCol.HeaderActive, Style.Components.ButtonBlueHovered);
            }

            ImGuiUtil.IconButton(widget.Instance.Icon, $"##fragment_{realIndex}");
            ImGui.SameLine();
            if (ImGui.Selectable(widget.Instance.Title, isSelected)) {
                SelectedItemIndex = realIndex;
                _widgetManager.Show(realIndex);
            }

            if (isSelected)
                ImGui.PopStyleColor(3);
        }

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        ImGui.EndChild();
    }

    private void DrawTypeContent(int itemIndex) {
        if (_widgetManager.Widgets.Count == 0) return;
        var widget = _widgetManager.Widgets[itemIndex];

        ImGui.BeginGroup();
        ImGui.BeginChild("##ActionsBroadcastContent", new Vector2(0, -ImGui.GetFrameHeightWithSpacing()));
        ImGuiUtil.DrawColoredBanner($"{widget.Instance.Title}", Style.Components.ButtonBlueHovered);

        ImGui.Spacing();

        _widgetManager.Draw();

        ImGui.EndChild();
        ImGui.EndGroup();
    }

    private void DrawHeader() {
        ImGui.Spacing();

        if (ImGuiUtil.IconButton(FontAwesomeIcon.Bars, $"##ToggleActionsBroadcastPanelBtn", Language.TogglePanelBtn)) {
            Plugin.Config.ShowPanelActionsBroadcast = !Plugin.Config.ShowPanelActionsBroadcast;
            Plugin.IpcProvider.SyncConfiguration();
        }

        ImGui.SameLine();
        ImGui.SetNextItemWidth(400);
        if (ImGui.InputTextWithHint("##ActionsBroadcastSearchInput", Language.SearchInputLabel, ref _searchString, 255, ImGuiInputTextFlags.AutoSelectAll)) {
            Search();
        }

        ImGui.SameLine();
        ImGui.Spacing();
        ImGui.SameLine();

        ImGui.Text("Icon Size:");

        ImGui.SameLine();
        int actionsIconSize = (int)Plugin.Config.ActionIconSize;
        ImGui.SetNextItemWidth(100);
        ImGui.SameLine();
        if (ImGui.DragInt("##ActionIconSizeDrag", ref actionsIconSize, 1, 20, 150)) {
            actionsIconSize = Math.Clamp(actionsIconSize, 20, 150);
            Plugin.Config.ActionIconSize = actionsIconSize;
            Plugin.IpcProvider.SyncConfiguration();
        }
        ImGuiUtil.ToolTip("Drag or double-click to type");

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();
    }

    private void Search() {
        ListSearchedIndexes.Clear();

        ListSearchedIndexes.AddRange(
            _widgetManager.Widgets
            .Select((fragment, index) => new { fragment, index })
            .Where(x => x.fragment.Instance.Title.Contains(_searchString, StringComparison.OrdinalIgnoreCase))
            .Select(x => x.index)
            .ToList()
        );
    }
}
