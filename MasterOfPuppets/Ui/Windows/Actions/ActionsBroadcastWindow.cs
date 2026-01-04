using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

using Dalamud.Bindings.ImGui;
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

    private readonly List<IFragment> Fragments = new();
    private readonly FragmentContext? _FragmentContext;

    public ActionsBroadcastWindow(Plugin plugin) : base($"{Plugin.Name}###ActionsBroadcastWindow") {
        Plugin = plugin;

        _FragmentContext = new FragmentContext(plugin);

        Size = ImGuiHelpers.ScaledVector2(550, 450);
        SizeCondition = ImGuiCond.FirstUseEver;
        // SizeCondition = ImGuiCond.Always;
        // Flags = ImGuiWindowFlags.NoResize;

        Fragments.Add(new EmotesFragment());
        Fragments.Add(new FacewearFragment());
        Fragments.Add(new FashionAccessoriesFragment());
        Fragments.Add(new GearSetFragment());
        Fragments.Add(new ItemsFragment());
        Fragments.Add(new MinionsFragment());
        Fragments.Add(new MountsFragment());
    }

    public override void Draw() {
        ImGui.BeginGroup();
        DrawHeader();
        ImGui.EndGroup();

        ImGui.BeginChild("##ActionsBroadcastScrollableContent", new Vector2(-1, 0), false, ImGuiWindowFlags.HorizontalScrollbar);
        DrawDebugTypeList();

        ImGui.SameLine();
        DrawDebugTypeContent(SelectedItemIndex);
        ImGui.EndChild();
    }

    private void DrawDebugTypeList() {
        var isFiltered = !string.IsNullOrEmpty(_searchString);

        var indices = isFiltered ? ListSearchedIndexes : Enumerable.Range(0, Fragments.Count).ToList();

        // left pane
        ImGui.BeginChild("##ActionsTypeList", ImGuiHelpers.ScaledVector2(250, 0), true);
        for (int i = 0; i < indices.Count; i++) {
            int realIndex = indices[i];
            var fragment = Fragments[realIndex];
            bool isSelected = SelectedItemIndex == realIndex;

            if (isSelected) {
                ImGui.PushStyleColor(ImGuiCol.Header, Style.Components.ButtonBlueHovered);
                ImGui.PushStyleColor(ImGuiCol.HeaderHovered, Style.Components.ButtonBlueHovered);
                ImGui.PushStyleColor(ImGuiCol.HeaderActive, Style.Components.ButtonBlueHovered);
            }

            if (ImGui.Selectable(fragment.Title, isSelected)) {
                SelectedItemIndex = realIndex;
            }

            if (isSelected)
                ImGui.PopStyleColor(3);
        }

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        ImGui.EndChild();
    }

    private void DrawDebugTypeContent(int itemIndex) {
        if (Fragments.Count == 0) return;
        var fragment = Fragments[itemIndex];

        ImGui.BeginGroup();
        ImGui.BeginChild("##ActionsBroadcastContent", new Vector2(0, -ImGui.GetFrameHeightWithSpacing()));
        ImGuiUtil.DrawColoredBanner($"{fragment.Title}", Style.Components.ButtonBlueHovered);

        ImGui.Spacing();
        ImGui.Indent();

        // render selected debug tab
        fragment.Render(_FragmentContext);

        ImGui.Unindent();
        ImGui.EndChild();
        ImGui.EndGroup();
    }

    private void DrawHeader() {
        ImGui.Spacing();

        if (ImGui.InputTextWithHint("##ActionsBroadcastSearchInput", Language.SearchInputLabel, ref _searchString, 255, ImGuiInputTextFlags.AutoSelectAll)) {
            Search();
        }

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();
    }

    private void Search() {
        ListSearchedIndexes.Clear();

        ListSearchedIndexes.AddRange(
            Fragments
            .Select((tab, index) => new { tab, index })
            .Where(x => x.tab.Title.Contains(_searchString, StringComparison.OrdinalIgnoreCase))
            .Select(x => x.index)
            .ToList()
        );
    }
}
