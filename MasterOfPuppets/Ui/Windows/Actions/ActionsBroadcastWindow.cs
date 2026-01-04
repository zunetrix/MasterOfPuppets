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

    private readonly FragmentContext? _fragmentContext;

    private readonly FragmentManager _fragmentManager = new();

    public ActionsBroadcastWindow(Plugin plugin) : base($"{Plugin.Name}###ActionsBroadcastWindow") {
        Plugin = plugin;

        _fragmentContext = new FragmentContext(plugin);

        Size = ImGuiHelpers.ScaledVector2(550, 450);
        SizeCondition = ImGuiCond.FirstUseEver;
        // SizeCondition = ImGuiCond.Always;
        // Flags = ImGuiWindowFlags.NoResize;

        _fragmentManager.Add(() => new EmotesFragment(_fragmentContext));
        _fragmentManager.Add(() => new FashionAccessoriesFragment(_fragmentContext));
        _fragmentManager.Add(() => new FacewearFragment(_fragmentContext));
        _fragmentManager.Add(() => new MountsFragment(_fragmentContext));
        _fragmentManager.Add(() => new MinionsFragment(_fragmentContext));
        _fragmentManager.Add(() => new ItemsFragment(_fragmentContext));
        _fragmentManager.Add(() => new GearSetFragment(_fragmentContext));

        _fragmentManager.Show(0);
    }

    public override void Draw() {
        ImGui.BeginGroup();
        DrawHeader();
        ImGui.EndGroup();

        ImGui.BeginChild("##ActionsBroadcastScrollableContent", new Vector2(-1, 0), false, ImGuiWindowFlags.HorizontalScrollbar);
        DrawDebugTypeList();

        ImGui.SameLine();
        DrawTypeContent(SelectedItemIndex);
        ImGui.EndChild();
    }

    private void DrawDebugTypeList() {
        var isFiltered = !string.IsNullOrEmpty(_searchString);

        var indices = isFiltered ? ListSearchedIndexes : Enumerable.Range(0, _fragmentManager.Fragments.Count).ToList();

        // left pane
        ImGui.BeginChild("##ActionsTypeList", ImGuiHelpers.ScaledVector2(200, 0), true);
        for (int i = 0; i < indices.Count; i++) {
            int realIndex = indices[i];
            var fragment = _fragmentManager.Fragments[realIndex];
            bool isSelected = SelectedItemIndex == realIndex;

            if (isSelected) {
                ImGui.PushStyleColor(ImGuiCol.Header, Style.Components.ButtonBlueHovered);
                ImGui.PushStyleColor(ImGuiCol.HeaderHovered, Style.Components.ButtonBlueHovered);
                ImGui.PushStyleColor(ImGuiCol.HeaderActive, Style.Components.ButtonBlueHovered);
            }

            ImGuiUtil.IconButton(fragment.Instance.Icon, $"##fragment_{realIndex}");
            ImGui.SameLine();
            if (ImGui.Selectable(fragment.Instance.Title, isSelected)) {
                SelectedItemIndex = realIndex;
                _fragmentManager.Show(realIndex);
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
        if (_fragmentManager.Fragments.Count == 0) return;
        var fragment = _fragmentManager.Fragments[itemIndex];

        ImGui.BeginGroup();
        ImGui.BeginChild("##ActionsBroadcastContent", new Vector2(0, -ImGui.GetFrameHeightWithSpacing()));
        ImGuiUtil.DrawColoredBanner($"{fragment.Instance.Title}", Style.Components.ButtonBlueHovered);

        ImGui.Spacing();

        _fragmentManager.Draw();

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
            _fragmentManager.Fragments
            .Select((fragment, index) => new { fragment, index })
            .Where(x => x.fragment.Instance.Title.Contains(_searchString, StringComparison.OrdinalIgnoreCase))
            .Select(x => x.index)
            .ToList()
        );
    }
}
