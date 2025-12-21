using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.ImGuiNotification;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Windowing;

using MasterOfPuppets.Extensions.Dalamud;
using MasterOfPuppets.Resources;
using MasterOfPuppets.Util.ImGuiExt;

namespace MasterOfPuppets;

public class GearSetWindow : Window {
    private Plugin Plugin { get; }
    private readonly List<ExecutableAction> UnlockedActions = new();

    private string _searchString = string.Empty;
    private readonly List<int> ListSearchedIndexes = new();
    private bool _showEmptyGearsets = false;

    public GearSetWindow(Plugin plugin) : base($"{Language.GearSetTitle}###GearSetWindow") {
        Plugin = plugin;

        Size = ImGuiHelpers.ScaledVector2(500, 450);
        SizeCondition = ImGuiCond.FirstUseEver;
        // SizeCondition = ImGuiCond.Always;
        // Flags = ImGuiWindowFlags.NoResize;
    }

    public override void OnOpen() {
        UnlockedActions.Clear();
        UnlockedActions.AddRange(GearSetHelper.GetAllowedItems());
        base.OnOpen();
    }

    public override void OnClose() {
        ListSearchedIndexes.Clear();
        _searchString = string.Empty;
        base.OnClose();
    }

    public override void Draw() {
        ImGui.BeginGroup();
        DrawHeader();
        ImGui.EndGroup();

        ImGui.BeginChild("##GearSetListScrollableContent", new Vector2(-1, 0), false, ImGuiWindowFlags.NoScrollbar);
        DrawGearSetTable();
        ImGui.EndChild();
    }

    private void Search() {
        ListSearchedIndexes.Clear();

        ListSearchedIndexes.AddRange(
            UnlockedActions
                .Select((item, index) => new { item, index })
                .Where(x => {
                    bool isEmpty = x.item.Category == "0";

                    if (!_showEmptyGearsets && isEmpty)
                        return false;

                    if (!string.IsNullOrWhiteSpace(_searchString))
                        return x.item.ActionName.Contains(_searchString, StringComparison.OrdinalIgnoreCase);

                    return true;
                })
                .Select(x => x.index)
        );
    }

    private void DrawHeader() {
        ImGui.Text($"{Language.GearSetTitle}");
        ImGui.SameLine();
        ImGuiUtil.HelpMarker("""
        Click on icon to execute (broadcast)
        Right click to copy command
        """);

        ImGui.Spacing();

        if (ImGui.InputTextWithHint("##GearSetSearchInput", Language.SearchInputLabel, ref _searchString, 255, ImGuiInputTextFlags.AutoSelectAll)) {
            Search();
        }

        ImGui.SameLine();
        var showingEmptyGearSets = false;
        if (_showEmptyGearsets) {
            ImGui.PushStyleColor(ImGuiCol.Button, Style.Components.ButtonBlueNormal);
            ImGui.PushStyleColor(ImGuiCol.ButtonHovered, Style.Components.ButtonBlueHovered);
            ImGui.PushStyleColor(ImGuiCol.ButtonActive, Style.Components.ButtonBlueActive);
            showingEmptyGearSets = true;
        }
        if (ImGuiUtil.IconButton(FontAwesomeIcon.BoxOpen, $"##ShowEmptyGearSetsBtn", "Show Empty Gear Sets")) {
            _showEmptyGearsets = !_showEmptyGearsets;
            Search();
        }
        if (showingEmptyGearSets)
            ImGui.PopStyleColor(3);

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();
    }

    private void DrawGearSetEntry(int actionIndex, ExecutableAction gearset) {
        ImGui.PushID($"##gearset_{actionIndex}");
        ImGui.TableNextRow();
        ImGui.TableNextColumn();
        ImGui.Text($"{actionIndex + 1:00}");

        ImGui.TableNextColumn();
        DalamudApi.TextureProvider.DrawIcon(gearset.IconId, ImGuiHelpers.ScaledVector2(30, 30));
        ImGuiUtil.ToolTip($"""
            {gearset.ActionName}

            Command:
            {gearset.TextCommand}
            """);
        if (ImGui.IsItemClicked()) {
            Plugin.IpcProvider.ExecuteTextCommand($"/mopbr {gearset.TextCommand}");
        }
        ImGuiUtil.ToolTip(Language.ClickToExecute);

        ImGui.TableNextColumn();
        ImGui.Text($"{gearset.ActionName}");
        if (ImGui.IsItemClicked()) {
            ImGui.SetClipboardText(gearset.TextCommand);
            DalamudApi.ShowNotification(Language.ClipboardCopyMessage, NotificationType.Info, 5000);
        }
        ImGuiUtil.ToolTip(Language.ClickToCopy);

        // ImGui.TableNextColumn();
        // ImGui.Text($"{gearset.Category}");

        ImGui.PopID();
    }

    private void DrawGearSetTable(float iconSize = 30) {
        var lineHeight = iconSize + ImGui.GetStyle().ItemSpacing.Y;

        if (ListSearchedIndexes.Count == 0)
            Search();

        var itemsToDraw = ListSearchedIndexes
        .Select(i => UnlockedActions[i])
        .ToList();

        var tableFlags = ImGuiTableFlags.RowBg | ImGuiTableFlags.PadOuterX |
               ImGuiTableFlags.NoSavedSettings | ImGuiTableFlags.BordersInnerV;
        var tableColumnCount = 3;

        if (ImGui.BeginTable("##ItemTable", tableColumnCount, tableFlags)) {
            ImGui.TableSetupColumn("  ", ImGuiTableColumnFlags.WidthFixed);
            ImGui.TableSetupColumn("Icon", ImGuiTableColumnFlags.WidthFixed);
            ImGui.TableSetupColumn("Name", ImGuiTableColumnFlags.WidthStretch, 1.0f);
            // ImGui.TableSetupColumn("Job", ImGuiTableColumnFlags.WidthStretch);

            var clipper = new ImGuiListClipper();
            clipper.Begin(itemsToDraw.Count, lineHeight);

            while (clipper.Step()) {
                for (int i = clipper.DisplayStart; i < clipper.DisplayEnd; i++) {
                    DrawGearSetEntry(i, itemsToDraw[i]);
                }
            }

            clipper.End();
            ImGui.EndTable();
        }
    }
}
