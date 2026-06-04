using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.ImGuiNotification;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;

using MasterOfPuppets.Resources;
using MasterOfPuppets.Util.ImGuiExt;

namespace MasterOfPuppets;

public class HelpWindow : Window {
    private Plugin Plugin { get; }

    private int SelectedItemIndex = 0;

    private string _searchString = string.Empty;
    private readonly List<int> ListSearchedIndexes = new();

    public HelpWindow(Plugin plugin) : base($"{Plugin.Name} Help###HelpWindow") {
        Plugin = plugin;

        Size = ImGuiHelpers.ScaledVector2(550, 450);
        SizeCondition = ImGuiCond.FirstUseEver;
        // SizeCondition = ImGuiCond.Always;
        // Flags = ImGuiWindowFlags.NoResize;
    }

    public override void Draw() {
        using (ImRaii.Group()) {
            DrawHeader();
        }

        using var child = ImRaii.Child("##MacroHelpListScrollableContent", new Vector2(-1, 0), false, ImGuiWindowFlags.HorizontalScrollbar);
        if (!child) return;
        DrawMacroHelpList();
        ImGui.SameLine();
        DrawMacroHelpContent(SelectedItemIndex);
    }

    private void DrawMacroHelpList() {
        var isFiltered = !string.IsNullOrEmpty(_searchString);

        var helpData = isFiltered
            ? ListSearchedIndexes.Select(i => MopCommandsHelper.Actions[i])
            : MopCommandsHelper.Actions;

        var macroActionGroups = helpData.GroupBy(a => a.Category);

        // left pane
        using var child = ImRaii.Child("##MacroHelpCommandList", ImGuiHelpers.ScaledVector2(250, 0), true);
        if (!child) return;

        foreach (var catGroup in macroActionGroups) {
            if (ImGui.CollapsingHeader($"{catGroup.Key}##MacroHelpCategory{catGroup.Key}")) {

                if (isFiltered) {
                    // In search mode: flatten — render all commands directly without sub-grouping
                    foreach (var action in catGroup) {
                        DrawActionSelectable(action);
                    }
                } else {
                    // Normal mode: group by SubCategory and render two-level tree
                    var subGroups = catGroup.GroupBy(a => a.SubCategory);

                    foreach (var subGroup in subGroups) {
                        if (subGroup.Key == MopActionSubCategory.None) {
                            // No subcategory — render items directly under the category
                            foreach (var action in subGroup) {
                                DrawActionSelectable(action);
                            }
                        } else {
                            // Render a collapsible TreeNode for the subcategory
                            using var treeNode = ImRaii.TreeNode($"  {subGroup.Key}##MacroHelpSub{catGroup.Key}{subGroup.Key}");
                            if (treeNode.Success) {
                                foreach (var action in subGroup) {
                                    DrawActionSelectable(action);
                                }
                            }
                        }
                    }
                }
            }

            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();
        }
    }

    private void DrawActionSelectable(MopAction action) {
        int realIndex = MopCommandsHelper.Actions.IndexOf(action);
        bool isSelected = SelectedItemIndex == realIndex;

        using (ImRaii.PushColor(ImGuiCol.Header, Style.Components.ButtonBlueHovered, isSelected)
        .Push(ImGuiCol.HeaderHovered, Style.Components.ButtonBlueHovered, isSelected)
        .Push(ImGuiCol.HeaderActive, Style.Components.ButtonBlueHovered, isSelected)) {
            if (ImGui.Selectable(action.SuggestionCommand, isSelected)) {
                SelectedItemIndex = realIndex;
            }
        }
    }

    private void DrawMacroHelpContent(int itemIndex) {
        var MacroHelpData = MopCommandsHelper.Actions[itemIndex];

        ImGui.BeginGroup();
        ImGui.BeginChild("##MacroHelpContent", new Vector2(0, -ImGui.GetFrameHeightWithSpacing()));
        ImGuiUtil.DrawColoredBanner($"{MacroHelpData.SuggestionCommand}", Style.Components.ButtonBlueHovered);
        ImGui.Spacing();
        ImGui.Indent();

        ImGui.Text("Usage:");
        if (ImGuiUtil.IconButton(FontAwesomeIcon.Copy, $"##CopyMopActionTextCommand", "Copy Text Command")) {
            ImGui.SetClipboardText($"{MacroHelpData.SuggestionCommand}");
            DalamudApi.ShowNotification(Language.ClipboardCopyMessage, NotificationType.Info, 5000);
        }
        ImGui.SameLine();
        ImGui.Text(MacroHelpData.TextCommand);

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        ImGui.Text("Example:");
        ImGui.TextWrapped(MacroHelpData.Example);
        if (ImGui.IsItemClicked()) {
            ImGui.SetClipboardText(MacroHelpData.Example);
            DalamudApi.ShowNotification(Language.ClipboardCopyMessage, NotificationType.Info, 5000);
        }
        ImGuiUtil.ToolTip(Language.ClickToCopy);

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        ImGui.TextWrapped(MacroHelpData.Notes);

        ImGui.Unindent();
        ImGui.EndChild();
        ImGui.EndGroup();
    }

    private void DrawHeader() {
        ImGui.Spacing();

        if (ImGui.InputTextWithHint("##MacroHelpSearchInput", Language.SearchInputLabel, ref _searchString, 255, ImGuiInputTextFlags.AutoSelectAll)) {
            Search();
        }

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();
    }

    private void Search() {
        ListSearchedIndexes.Clear();

        ListSearchedIndexes.AddRange(
            MopCommandsHelper.Actions
            .Select((item, index) => new { item, index })
            .Where(x => x.item.TextCommand.Contains(_searchString, StringComparison.OrdinalIgnoreCase))
            .Select(x => x.index)
            .ToList()
        );
    }
}
