using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.ImGuiNotification;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;

using MasterOfPuppets.Extensions.Dalamud;
using MasterOfPuppets.Resources;
using MasterOfPuppets.Util.ImGuiExt;

namespace MasterOfPuppets.Actions;

public class GearSetWidget : Widget {
    public override string Title => "Gear Set";
    public override FontAwesomeIcon Icon => FontAwesomeIcon.Briefcase;
    private readonly List<ExecutableAction> UnlockedActions = new();
    private string _searchString = string.Empty;
    private readonly List<int> ListSearchedIndexes = new();
    private bool _showEmptyGearsets = false;
    private int _selectedGersetId = -1;
    private string _gersetName = string.Empty;
    private bool _openRenamePopup = false;

    public GearSetWidget(WidgetContext ctx) : base(ctx) {
    }

    public override void OnShow() {
        ReloadData();
        base.OnShow();
    }

    public override void OnHide() {
        ListSearchedIndexes.Clear();
        _searchString = string.Empty;
        base.OnHide();
    }

    public override void Draw() {
        using (ImRaii.Group()) {
            DrawHeader();
        }

        ImGui.BeginChild("##GearSetListScrollableContent", new Vector2(-1, 0), false, ImGuiWindowFlags.AlwaysVerticalScrollbar);
        DrawGearSetTable();
        ImGui.EndChild();

        // cant be inside child / table contexct
        if (_openRenamePopup) {
            ImGui.OpenPopup("RenameGearsetPopup");
            _openRenamePopup = false;
        }

        DrawEditGearsetPopup();
    }

    private void ReloadData() {
        UnlockedActions.Clear();
        UnlockedActions.AddRange(GearsetHelper.GetAllowedItems());
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
        if (ImGuiUtil.IconButton(FontAwesomeIcon.Sync, $"##GearSetReloadDataBtn", "Reload")) {
            ReloadData();
        }
        ImGui.SameLine();
        if (ImGui.InputTextWithHint("##GearSetSearchInput", Language.SearchInputLabel, ref _searchString, 255, ImGuiInputTextFlags.AutoSelectAll)) {
            Search();
        }

        ImGui.SameLine();
        using (ImRaii.PushColor(ImGuiCol.Button, Style.Components.ButtonBlueNormal, _showEmptyGearsets)
        .Push(ImGuiCol.ButtonHovered, Style.Components.ButtonBlueHovered, _showEmptyGearsets)
        .Push(ImGuiCol.ButtonActive, Style.Components.ButtonBlueActive, _showEmptyGearsets)) {
            if (ImGuiUtil.IconButton(FontAwesomeIcon.BoxOpen, $"##ShowEmptyGearSetsBtn", "Show Empty Gear Sets")) {
                _showEmptyGearsets = !_showEmptyGearsets;
                Search();
            }
        }

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();
    }

    private void DrawGearSetEntry(int actionIndex, ExecutableAction gearset) {
        int gearsetId = (int)gearset.ActionId;
        ImGui.PushID($"##gearset_{actionIndex}");
        ImGui.TableNextRow();

        ImGui.TableNextColumn();
        ImGui.Text($"{gearsetId + 1:00}");

        ImGui.TableNextColumn();
        DalamudApi.TextureProvider.DrawIcon(gearset.IconId, ImGuiHelpers.ScaledVector2(30, 30));
        ImGuiUtil.ToolTip($"""
        {gearset.ActionName}

        Command:
        {gearset.TextCommand}
        """);
        if (ImGui.IsItemClicked()) {
            ImGui.SetClipboardText(gearset.TextCommand);
            DalamudApi.ShowNotification(Language.ClipboardCopyMessage, NotificationType.Info, 5000);
        }
        ImGuiUtil.ToolTip(Language.ClickToCopy);

        ImGui.TableNextColumn();
        ImGui.Selectable($"{gearset.ActionName}");
        ImGuiUtil.ToolTip("Drag to reorder");

        if (ImGui.BeginDragDropSource()) {
            unsafe {
                ImGui.SetDragDropPayload("DND_GEARSET", new ReadOnlySpan<byte>(&gearsetId, sizeof(int)), ImGuiCond.None);
                ImGui.Button($"({gearsetId + 1}) {gearset.ActionName}");
            }
            ImGui.EndDragDropSource();
        }

        using (ImRaii.PushColor(ImGuiCol.DragDropTarget, Style.Components.DragDropTarget)) {
            if (ImGui.BeginDragDropTarget()) {
                var dragDropPayload = ImGui.AcceptDragDropPayload("DND_GEARSET");
                bool isDropping = false;
                unsafe { isDropping = !dragDropPayload.IsNull; }
                if (isDropping && dragDropPayload.IsDelivery()) {
                    unsafe {
                        int originalIndex = *(int*)dragDropPayload.Data;
                        int offset = gearsetId - originalIndex;
                        if (offset != 0 && originalIndex + offset >= 0) {
                            if (originalIndex < 0 || gearsetId < 0) return;
                            Context.Plugin.IpcProvider.ReorderGearset(originalIndex, gearsetId);
                        }
                    }
                }
                ImGui.EndDragDropTarget();
            }
        }

        ImGui.TableNextColumn();
        if (ImGuiUtil.IconButton(FontAwesomeIcon.Edit, $"##RenameGearset_{gearsetId}", "Rename")) {
            _selectedGersetId = gearsetId;
            _gersetName = gearset.ActionName ?? string.Empty;
            _openRenamePopup = true;
        }
        ImGuiUtil.ToolTip("Broadcast rename gearset");

        ImGui.SameLine();
        if (ImGuiUtil.IconButton(FontAwesomeIcon.Briefcase, $"##EquipGearsetInventory_{gearsetId}", "Equip From Inventory")) {
            Context.Plugin.IpcProvider.ChangeGearset(gearsetId);
        }
        ImGuiUtil.ToolTip("""
        Try equip gearset from inventory
        Move the items from inventory to armoury (first slot) then equip gearset
        """);

        ImGui.SameLine();
        if (ImGuiUtil.IconButton(FontAwesomeIcon.Play, $"##EquipGearset_{gearsetId}", "Equip Gearset")) {
            Context.Plugin.IpcProvider.ExecuteTextCommand($"/mopbr {gearset.TextCommand}");
        }

        ImGui.PopID();
    }

    private void DrawEditGearsetPopup() {
        using var borderColor = ImRaii.PushColor(ImGuiCol.Border, Style.Components.TooltipBorderColor);
        using var popupBorder = ImRaii.PushStyle(ImGuiStyleVar.PopupBorderSize, 1);
        using var popup = ImRaii.Popup("RenameGearsetPopup");
        if (!popup) return;

        ImGui.Text("Rename Gearset");
        ImGui.InputTextWithHint("##RenameGearsetInput", "Gearset Name", ref _gersetName, 15);

        if (ImGuiUtil.SuccessButton("Save##SaveGearsetName")) {
            if (_selectedGersetId < 0) return;
            RenameGearset(_selectedGersetId, _gersetName);
            ImGui.CloseCurrentPopup();
        }

        ImGui.SameLine();
        if (ImGuiUtil.DangerButton("Cancel##CancelRenameGearset"))
            ImGui.CloseCurrentPopup();
    }

    private void RenameGearset(int gearsetId, string gersetNewName) {
        Context.Plugin.IpcProvider.RenameGearset(gearsetId, gersetNewName);
    }

    private void DrawGearSetTable(float iconSize = 30) {
        // var lineHeight = iconSize + ImGui.GetStyle().ItemSpacing.Y;
        var lineHeight = iconSize + 10;

        if (ListSearchedIndexes.Count == 0)
            Search();

        var itemsToDraw = ListSearchedIndexes
        .Select(i => UnlockedActions[i])
        .ToList();

        var tableFlags = ImGuiTableFlags.RowBg | ImGuiTableFlags.PadOuterX |
               ImGuiTableFlags.NoSavedSettings | ImGuiTableFlags.BordersInnerV;
        var tableColumnCount = 4;

        if (ImGui.BeginTable("##ItemTable", tableColumnCount, tableFlags)) {
            ImGui.TableSetupColumn("GS#", ImGuiTableColumnFlags.WidthFixed);
            ImGui.TableSetupColumn("Icon", ImGuiTableColumnFlags.WidthFixed);
            ImGui.TableSetupColumn("Name", ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableSetupColumn("Actions", ImGuiTableColumnFlags.WidthFixed);

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
