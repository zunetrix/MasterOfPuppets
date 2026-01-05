using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Windowing;

using MasterOfPuppets.Resources;
using MasterOfPuppets.Util.ImGuiExt;

namespace MasterOfPuppets.Debug;

public class DebugWindow : Window {
    private Plugin Plugin { get; }

    private int SelectedItemIndex = 0;

    private string _searchString = string.Empty;
    private readonly List<int> ListSearchedIndexes = new();

    private readonly WidgetContext? _widgetContext;
    private readonly WidgetManager _widgetManager = new();

    public DebugWindow(Plugin plugin) : base($"{Plugin.Name} Debug###DebugWindow") {
        Plugin = plugin;

        _widgetContext = new WidgetContext(plugin);

        Size = ImGuiHelpers.ScaledVector2(550, 450);
        SizeCondition = ImGuiCond.FirstUseEver;
        // SizeCondition = ImGuiCond.Always;
        // Flags = ImGuiWindowFlags.NoResize;

        _widgetManager.Add(() => new ActionsDebugWidget(_widgetContext));
        _widgetManager.Add(() => new ConflictingPluginDebugWidget(_widgetContext));
        _widgetManager.Add(() => new FontAwesomeDebugWidget(_widgetContext));
        _widgetManager.Add(() => new GeneralDebugWidget(_widgetContext));
        _widgetManager.Add(() => new HotbarDebugWidget(_widgetContext));
        _widgetManager.Add(() => new HotbarPetDebugWidget(_widgetContext));
        _widgetManager.Add(() => new InventoryDebugWidget(_widgetContext));
        _widgetManager.Add(() => new MovementDebugWidget(_widgetContext));
        _widgetManager.Add(() => new TargetDebugWidget(_widgetContext));
        _widgetManager.Add(() => new UiElementDebugWidget(_widgetContext));

        _widgetManager.Show(0);
    }

    public override void Draw() {
        ImGui.BeginGroup();
        DrawHeader();
        ImGui.EndGroup();

        ImGui.BeginChild("##DebugScrollableContent", new Vector2(-1, 0), false, ImGuiWindowFlags.HorizontalScrollbar);
        DrawDebugTypeList();

        ImGui.SameLine();
        DrawTypeContent(SelectedItemIndex);
        ImGui.EndChild();
    }

    private void DrawDebugTypeList() {
        var isFiltered = !string.IsNullOrEmpty(_searchString);
        var indices = isFiltered ? ListSearchedIndexes : Enumerable.Range(0, _widgetManager.Widgets.Count).ToList();

        // left pane
        ImGui.BeginChild("##DebugTypeList", ImGuiHelpers.ScaledVector2(200, 0), true);
        for (int i = 0; i < indices.Count; i++) {
            int realIndex = indices[i];
            var widget = _widgetManager.Widgets[realIndex];
            bool isSelected = SelectedItemIndex == realIndex;

            if (isSelected) {
                ImGui.PushStyleColor(ImGuiCol.Header, Style.Components.ButtonBlueHovered);
                ImGui.PushStyleColor(ImGuiCol.HeaderHovered, Style.Components.ButtonBlueHovered);
                ImGui.PushStyleColor(ImGuiCol.HeaderActive, Style.Components.ButtonBlueHovered);
            }

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
        ImGui.BeginChild("##DebugContent", new Vector2(0, -ImGui.GetFrameHeightWithSpacing()));
        ImGuiUtil.DrawColoredBanner($"{widget.Instance.Title}", Style.Components.ButtonBlueHovered);

        ImGui.Spacing();

        _widgetManager.Draw();

        ImGui.EndChild();
        ImGui.EndGroup();
    }

    private void DrawHeader() {
        ImGui.Spacing();

        if (ImGui.InputTextWithHint("##DebugSearchInput", Language.SearchInputLabel, ref _searchString, 255, ImGuiInputTextFlags.AutoSelectAll)) {
            Search();
        }

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
