using System;
using System.Numerics;
using System.Linq;
using System.Collections.Generic;

using Dalamud.Interface;
using Dalamud.Interface.Windowing;
using Dalamud.Interface.Utility;
using Dalamud.Interface.ImGuiNotification;
using Dalamud.Bindings.ImGui;

using MasterOfPuppets.Resources;

namespace MasterOfPuppets;

public class MacroHelpWindow : Window
{
    private Plugin Plugin { get; }

    private int SelectedItemIndex = 0;

    private string _searchString = string.Empty;
    private readonly List<int> ListSearchedIndexes = new();

    public MacroHelpWindow(Plugin plugin) : base($"{Plugin.Name} Help###MacroHelpWindow")
    {
        Plugin = plugin;

        Size = ImGuiHelpers.ScaledVector2(550, 450);
        SizeCondition = ImGuiCond.FirstUseEver;
        // SizeCondition = ImGuiCond.Always;
        // Flags = ImGuiWindowFlags.NoResize;
    }

    public override void Draw()
    {
        ImGui.BeginChild("##MacroHelpHeaderFixedHeight", new Vector2(-1, 45 * ImGuiHelpers.GlobalScale), false, ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse);
        DrawHeader();
        ImGui.EndChild();

        ImGui.BeginChild("##MacroHelpListScrollableContent", new Vector2(-1, 0), false, ImGuiWindowFlags.HorizontalScrollbar);
        DrawMacroHelpList();
        ImGui.SameLine();
        DrawMacroHelpContent(SelectedItemIndex);
        ImGui.EndChild();
    }

    private void DrawMacroHelpList()
    {
        var isFiltered = !string.IsNullOrEmpty(_searchString);
        // var itemCount = isFiltered ? ListSearchedIndexes.Count : MopMacroActionsHelper.Actions.Count;

        var helpData = isFiltered
            ? ListSearchedIndexes.Select(i => MopMacroActionsHelper.Actions[i])
            : MopMacroActionsHelper.Actions;

        var macroActionGroups = helpData
        .GroupBy(a => a.Category);
        // .OrderBy(g => g.Key);

        // left pane
        ImGui.BeginChild("##MacroHelpCommandList", ImGuiHelpers.ScaledVector2(250, 0), true);
        foreach (var macroActionGroup in macroActionGroups)
        {
            if (ImGui.CollapsingHeader($"{macroActionGroup.Key}##MacroHelpCategory{macroActionGroup.Key}"))
            {
                foreach (var action in macroActionGroup)
                {
                    int realIndex = MopMacroActionsHelper.Actions.IndexOf(action);
                    bool isSelected = SelectedItemIndex == realIndex;

                    if (isSelected)
                    {
                        ImGui.PushStyleColor(ImGuiCol.Header, Style.Components.ButtonBlueHovered);
                        ImGui.PushStyleColor(ImGuiCol.HeaderHovered, Style.Components.ButtonBlueHovered);
                        ImGui.PushStyleColor(ImGuiCol.HeaderActive, Style.Components.ButtonBlueHovered);
                    }

                    if (ImGui.Selectable(action.SuggestionCommand, isSelected))
                    {
                        SelectedItemIndex = realIndex;
                    }

                    if (isSelected)
                        ImGui.PopStyleColor(3);
                }
            }
            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();
        }
        ImGui.EndChild();
    }

    private void DrawMacroHelpContent(int itemIndex)
    {
        var MacroHelpData = MopMacroActionsHelper.Actions[itemIndex];

        ImGui.BeginGroup();
        ImGui.BeginChild("##MacroHelpContent", new Vector2(0, -ImGui.GetFrameHeightWithSpacing()));
        ImGuiUtil.DrawColoredBanner($"{MacroHelpData.SuggestionCommand}", Style.Components.ButtonBlueHovered);
        ImGui.Spacing();
        ImGui.Indent();

        ImGui.TextUnformatted("Usage:");
        if (ImGuiUtil.IconButton(FontAwesomeIcon.Copy, $"##CopyMopActionTextCommand", "Copy Text Command"))
        {
            ImGui.SetClipboardText($"{MacroHelpData.SuggestionCommand}");
            DalamudApi.ShowNotification(Language.ClipboardCopyMessage, NotificationType.Info, 5000);
        }
        ImGui.SameLine();
        ImGui.TextUnformatted(MacroHelpData.TextCommand);

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        ImGui.TextUnformatted("Example:");
        ImGui.TextWrapped(MacroHelpData.Example);
        if (ImGui.IsItemClicked())
        {
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

    private void DrawHeader()
    {
        ImGui.Spacing();

        if (ImGui.InputTextWithHint("##MacroHelpSearchInput", Language.SearchInputLabel, ref _searchString, 255, ImGuiInputTextFlags.AutoSelectAll))
        {
            Search();
        }

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();
    }

    private void Search()
    {
        ListSearchedIndexes.Clear();

        ListSearchedIndexes.AddRange(
            MopMacroActionsHelper.Actions
            .Select((item, index) => new { item, index })
            .Where(x => x.item.TextCommand.Contains(_searchString, StringComparison.OrdinalIgnoreCase))
            .Select(x => x.index)
            .ToList()
        );
    }
}
