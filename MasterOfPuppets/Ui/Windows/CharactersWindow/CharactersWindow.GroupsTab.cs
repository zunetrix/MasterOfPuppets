using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.ImGuiNotification;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;

using MasterOfPuppets.Resources;
using MasterOfPuppets.Util.ImGuiExt;

namespace MasterOfPuppets;

public partial class CharactersWindow {
    private void DrawCidsGroupsTab() {
        using var tabItem = ImRaii.TabItem($"Groups###CidsGroupsTab");
        if (!tabItem) return;
        DrawCidsGroupsSplitView();
    }

    private void DrawCidsGroupsSplitView() {
        var groups = Plugin.Config.CidsGroups;

        float splitterW = 6f * ImGuiHelpers.GlobalScale;
        float minLeftW = 250f * ImGuiHelpers.GlobalScale;

        if (_groupLeftPanelWidth <= 0f) _groupLeftPanelWidth = 180f * ImGuiHelpers.GlobalScale;

        var avail = ImGui.GetContentRegionAvail();
        float spacing = ImGui.GetStyle().ItemSpacing.X;
        float maxLeftW = MathF.Max(avail.X - splitterW - 200f * ImGuiHelpers.GlobalScale - spacing * 2, minLeftW);
        _groupLeftPanelWidth = Math.Clamp(_groupLeftPanelWidth, minLeftW, maxLeftW);
        float h = avail.Y;

        // Left panel
        ImGui.BeginChild("##GroupsList", new Vector2(_groupLeftPanelWidth, h), true);
        DrawGroupsLeftPanel(groups);
        ImGui.EndChild();
        ImGui.SameLine();

        // Splitter
        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, Vector2.Zero);
        ImGui.InvisibleButton("##grpsplit", new Vector2(splitterW, h));
        if (ImGui.IsItemHovered()) ImGui.SetMouseCursor(ImGuiMouseCursor.ResizeEw);
        if (ImGui.IsItemActive() && ImGui.IsMouseDragging(ImGuiMouseButton.Left)) {
            _groupLeftPanelWidth += ImGui.GetIO().MouseDelta.X;
            _groupLeftPanelWidth = Math.Clamp(_groupLeftPanelWidth, minLeftW, maxLeftW);
        }
        ImGui.PopStyleVar();
        ImGui.SameLine();

        // Right panel
        using (var right = ImRaii.Child("##GroupContent", new Vector2(-1, -1), false)) {
            if (!right) return;
            if (!IsValidGroup()) {
                ImGui.TextDisabled("Select a group");
                return;
            }
            DrawGroupContentPanel(groups[_selectedCidGroupIndex]);
        }
    }

    private void DrawGroupsLeftPanel(List<CidGroup> groups) {
        using (ImRaii.Group()) {
            // + button
            if (ImGuiUtil.IconButtonStyled(FontAwesomeIcon.Plus, ImGuiUtil.IconButtonStyle.Primary, "##grpadd", "New group"))
                ImGui.OpenPopup("##grpnew");

            using (ImRaii.PushColor(ImGuiCol.Border, Style.Components.TooltipBorderColor)) {
                using (ImRaii.PushStyle(ImGuiStyleVar.PopupBorderSize, 1)) {
                    if (ImGui.BeginPopup("##grpnew")) {
                        ImGui.Text("Name:");
                        ImGui.SameLine();
                        ImGui.SetNextItemWidth(180);
                        bool enter = ImGui.InputText("##grpnewname", ref _newGroupName, 64, ImGuiInputTextFlags.EnterReturnsTrue);
                        bool duplicate = !string.IsNullOrWhiteSpace(_newGroupName) &&
                            groups.Any(g => g.Name.Equals(_newGroupName.Trim(), StringComparison.OrdinalIgnoreCase));
                        if (duplicate) {
                            ImGui.SameLine();
                            ImGui.TextColored(Style.Colors.Yellow, "already exists");
                        }
                        if ((enter || ImGui.Button("Create")) && !string.IsNullOrWhiteSpace(_newGroupName) && !duplicate) {
                            groups.Add(new CidGroup { Name = _newGroupName.Trim(), Cids = new() });
                            _selectedCidGroupIndex = groups.Count - 1;
                            _newGroupName = string.Empty;
                            Plugin.Config.Save();
                            Plugin.IpcProvider.SyncConfiguration();
                            ImGui.CloseCurrentPopup();
                        }
                        ImGui.EndPopup();
                    }
                }
            }

            ImGui.SameLine();
            ImGui.SetNextItemWidth(-1);
            ImGui.InputTextWithHint("##grpsrch", "Search...", ref _groupSearchFilter, 64);
            ImGui.Separator();
        }

        float btnW = ImGui.GetFrameHeight();
        float spc = ImGui.GetStyle().ItemSpacing.X + 3 * ImGuiHelpers.GlobalScale;
        float actColW = btnW * 2 + spc;
        float listH = ImGui.GetContentRegionAvail().Y;

        if (!ImGui.BeginTable("##grptbl", 3,
            ImGuiTableFlags.RowBg | ImGuiTableFlags.NoSavedSettings |
            ImGuiTableFlags.ScrollY | ImGuiTableFlags.BordersInnerV,
            new Vector2(-1, listH)))
            return;

        ImGui.TableSetupScrollFreeze(0, 1);
        ImGui.TableSetupColumn("#", ImGuiTableColumnFlags.WidthFixed, 24f * ImGuiHelpers.GlobalScale);
        ImGui.TableSetupColumn("Group", ImGuiTableColumnFlags.WidthStretch);
        ImGui.TableSetupColumn("##acts", ImGuiTableColumnFlags.WidthFixed, actColW);
        ImGui.TableHeadersRow();

        int moveFrom = -1, moveTo = -1;

        for (int i = 0; i < groups.Count; i++) {
            var g = groups[i];
            if (!string.IsNullOrEmpty(_groupSearchFilter) &&
                !g.Name.Contains(_groupSearchFilter, StringComparison.OrdinalIgnoreCase))
                continue;

            ImGui.PushID(i);
            ImGui.TableNextRow();

            // Col 0: row number
            ImGui.TableNextColumn();
            ImGui.TextDisabled($"{i + 1}");

            // Col 1: name (rename input or selectable with D&D + context menu)
            ImGui.TableNextColumn();
            if (_groupRenamingIdx == i) {
                if (_groupRenamingFocusPending) {
                    ImGui.SetKeyboardFocusHere();
                    _groupRenamingFocusPending = false;
                }
                ImGui.SetNextItemWidth(-1);
                if (ImGui.InputText("##grprenin", ref _groupRenameBuffer, 64,
                    ImGuiInputTextFlags.EnterReturnsTrue | ImGuiInputTextFlags.AutoSelectAll))
                    CommitGroupRename(i);
                if (ImGui.IsItemDeactivated() && _groupRenamingIdx == i)
                    CommitGroupRename(i);
            } else {
                using (ImRaii.PushColor(ImGuiCol.Header, Style.Components.ButtonBlueHovered, i == _selectedCidGroupIndex)
                            .Push(ImGuiCol.HeaderHovered, Style.Components.ButtonBlueHovered, i == _selectedCidGroupIndex)
                            .Push(ImGuiCol.HeaderActive, Style.Components.ButtonBlueHovered, i == _selectedCidGroupIndex)) {
                    bool clicked = ImGui.Selectable(
                        g.Name.Length > 0 ? g.Name : "(unnamed)",
                        i == _selectedCidGroupIndex);
                    if (clicked && _selectedCidGroupIndex != i)
                        _selectedCidGroupIndex = i;
                }

                // Drag source
                if (ImGui.BeginDragDropSource()) {
                    unsafe {
                        ImGui.SetDragDropPayload("DND_CIDGROUP",
                            new ReadOnlySpan<byte>(&i, sizeof(int)), ImGuiCond.None);
                    }
                    ImGui.Text(g.Name.Length > 0 ? g.Name : "(unnamed)");
                    ImGui.EndDragDropSource();
                }
                ImGuiUtil.ToolTip("Drag to reorder");

                // Drop target
                using (ImRaii.PushColor(ImGuiCol.DragDropTarget, Style.Components.DragDropTarget)) {
                    if (ImGui.BeginDragDropTarget()) {
                        var payload = ImGui.AcceptDragDropPayload("DND_CIDGROUP");
                        bool dropping = false;
                        unsafe { dropping = !payload.IsNull; }
                        if (dropping && payload.IsDelivery()) {
                            unsafe { moveFrom = *(int*)payload.Data; moveTo = i; }
                        }
                        ImGui.EndDragDropTarget();
                    }
                }

                // Context menu
                using (ImRaii.PushColor(ImGuiCol.Border, Style.Components.TooltipBorderColor)) {
                    using (ImRaii.PushStyle(ImGuiStyleVar.PopupBorderSize, 1)) {
                        if (ImGui.BeginPopupContextItem($"##grpcm{i}")) {
                            if (ImGui.MenuItem("Clone")) {
                                var clone = new CidGroup { Name = MakeUniqueGroupName(g.Name), Cids = new(g.Cids) };
                                groups.Insert(i + 1, clone);
                                Plugin.Config.Save();
                                Plugin.IpcProvider.SyncConfiguration();
                            }
                            ImGui.EndPopup();
                        }
                    }
                }
            }

            // Col 2: actions
            ImGui.TableNextColumn();
            if (ImGuiUtil.IconButtonStyled(FontAwesomeIcon.Trash, ImGuiUtil.IconButtonStyle.Danger, $"##grpdel{i}", Language.DeleteInstructionTooltip)
                && ImGui.GetIO().KeyCtrl) {
                DeleteGroupAt(i);
                ImGui.PopID();
                break;
            }

            ImGui.SameLine();
            if (ImGuiUtil.IconButton(FontAwesomeIcon.Pen, $"##grprnm{i}", "Rename")) {
                _groupRenamingIdx = i;
                _groupRenameBuffer = g.Name;
                _groupRenamingFocusPending = true;
            }

            ImGui.PopID();
        }

        // Apply reorder after full render pass
        if (moveFrom >= 0 && moveTo >= 0 && moveFrom != moveTo) {
            var moved = groups[moveFrom];
            groups.RemoveAt(moveFrom);
            groups.Insert(moveTo, moved);
            if (_selectedCidGroupIndex == moveFrom) _selectedCidGroupIndex = moveTo;
            else if (_selectedCidGroupIndex > moveFrom && _selectedCidGroupIndex <= moveTo) _selectedCidGroupIndex--;
            else if (_selectedCidGroupIndex < moveFrom && _selectedCidGroupIndex >= moveTo) _selectedCidGroupIndex++;
            Plugin.Config.Save();
            Plugin.IpcProvider.SyncConfiguration();
        }

        ImGui.EndTable();
    }

    private void DrawGroupContentPanel(CidGroup cidGroup) {
        // sync edit buffer when selection changes
        if (_editGroupNameLastIndex != _selectedCidGroupIndex) {
            _editGroupName = cidGroup.Name;
            _editGroupNameLastIndex = _selectedCidGroupIndex;
        }

        ImGui.Spacing();

        // add-character combo
        var availableCharacters = Plugin.Config.Characters
            .Where(c => !cidGroup.Cids.Contains(c.Cid))
            .ToList();

        using (ImRaii.Disabled(availableCharacters.Count == 0)) {
            ImGui.SetNextItemWidth(-1);
            using (ImRaii.PushColor(ImGuiCol.Border, Style.Components.TooltipBorderColor)) {
                using (ImRaii.PushStyle(ImGuiStyleVar.PopupBorderSize, 1)) {
                    if (ImGui.BeginCombo($"##AddCharToGroup_{_selectedCidGroupIndex}", "Add character to group")) {
                        foreach (var c in availableCharacters) {
                            if (ImGui.Selectable($"{c.Name}##{c.Cid}", false)) {
                                cidGroup.Cids.Add(c.Cid);
                                Plugin.Config.Save();
                                Plugin.IpcProvider.SyncConfiguration();
                            }
                        }
                        ImGui.EndCombo();
                    }
                }
            }
        }

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        DrawCidGroupCharactersTable(cidGroup);
    }

    private void DrawCidGroupCharactersTable(CidGroup cidGroup) {
        float deleteColWidth = ImGui.GetFrameHeight() + ImGui.GetStyle().FramePadding.X;

        if (!ImGui.BeginTable($"##CidGroupTable_{_selectedCidGroupIndex}", 3,
            ImGuiTableFlags.RowBg | ImGuiTableFlags.PadOuterX | ImGuiTableFlags.ScrollY |
            ImGuiTableFlags.NoSavedSettings | ImGuiTableFlags.BordersInnerV))
            return;

        ImGui.TableSetupScrollFreeze(0, 1);
        ImGui.TableSetupColumn("#", ImGuiTableColumnFlags.WidthFixed, 28 * ImGuiHelpers.GlobalScale);
        ImGui.TableSetupColumn("Name", ImGuiTableColumnFlags.WidthStretch);
        ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthFixed, deleteColWidth);
        ImGui.TableHeadersRow();

        int deleteIndex = -1;

        for (int i = 0; i < cidGroup.Cids.Count; i++) {
            var targetCid = cidGroup.Cids[i];
            var character = Plugin.Config.Characters.FirstOrDefault(c => c.Cid == targetCid)
                ?? new Character { Cid = targetCid, Name = $"Unknown ({targetCid})" };

            ImGui.PushID(i);
            ImGui.TableNextRow();

            // col 0: index
            ImGui.TableNextColumn();
            ImGui.Text($"{i + 1:00}");

            // col 1: name + drag-drop
            ImGui.TableNextColumn();
            ImGui.Selectable($"{character.Name}##cgsel");
            ImGuiUtil.ToolTip("Drag to reorder");

            if (ImGui.BeginDragDropSource()) {
                unsafe {
                    ImGui.SetDragDropPayload("DND_CIDGROUP_CHARS", new ReadOnlySpan<byte>(&i, sizeof(int)), ImGuiCond.None);
                    ImGui.Text($"({i + 1}) {character.Name}");
                }
                ImGui.EndDragDropSource();
            }

            using (ImRaii.PushColor(ImGuiCol.DragDropTarget, Style.Components.DragDropTarget)) {
                if (ImGui.BeginDragDropTarget()) {
                    var payload = ImGui.AcceptDragDropPayload("DND_CIDGROUP_CHARS");
                    bool isDropping = false;
                    unsafe { isDropping = !payload.IsNull; }
                    if (isDropping && payload.IsDelivery()) {
                        unsafe {
                            int from = *(int*)payload.Data;
                            if (from != i) {
                                var cid = cidGroup.Cids[from];
                                cidGroup.Cids.RemoveAt(from);
                                cidGroup.Cids.Insert(i, cid);
                                Plugin.Config.Save();
                                Plugin.IpcProvider.SyncConfiguration();
                            }
                        }
                    }
                    ImGui.EndDragDropTarget();
                }
            }

            // col 2: delete button
            ImGui.TableNextColumn();
            if (ImGuiUtil.IconButtonStyled(FontAwesomeIcon.Trash, ImGuiUtil.IconButtonStyle.Danger, $"##del_{i}", Language.DeleteInstructionTooltip))
                deleteIndex = i;

            ImGui.PopID();
        }

        ImGui.EndTable();

        // deferred delete to avoid modifying list mid-loop
        if (deleteIndex >= 0) {
            cidGroup.Cids.RemoveAt(deleteIndex);
            Plugin.Config.Save();
            Plugin.IpcProvider.SyncConfiguration();
        }
    }

    private void CommitGroupRename(int i) {
        if (_groupRenamingIdx != i) return;
        var groups = Plugin.Config.CidsGroups;
        var trimmed = _groupRenameBuffer.Trim();
        if (!string.IsNullOrEmpty(trimmed) && trimmed != groups[i].Name)
            ApplyGroupRename(groups[i], trimmed);
        _groupRenamingIdx = -1;
    }

    private void DeleteGroupAt(int i) {
        var groups = Plugin.Config.CidsGroups;
        var deletedName = groups[i].Name;

        groups.RemoveAt(i);

        // remove references from macros
        foreach (var macro in Plugin.Config.Macros)
            foreach (var command in macro.Commands)
                command.GroupIds.RemoveAll(n => n == deletedName);

        // remove references from formations
        foreach (var formation in Plugin.Config.Formations)
            foreach (var point in formation.Points)
                point.GroupIds.RemoveAll(n => n.Equals(deletedName, StringComparison.OrdinalIgnoreCase));

        // remove references from window layouts
        foreach (var windowLayout in Plugin.Config.WindowLayouts)
            foreach (var slot in windowLayout.Slots)
                slot.GroupIds.RemoveAll(n => n.Equals(deletedName, StringComparison.OrdinalIgnoreCase));

        // adjust selection index
        if (_selectedCidGroupIndex == i)
            _selectedCidGroupIndex = groups.Count > 0 ? Math.Min(i, groups.Count - 1) : 0;
        else if (_selectedCidGroupIndex > i)
            _selectedCidGroupIndex--;

        // adjust rename index
        if (_groupRenamingIdx == i) _groupRenamingIdx = -1;
        else if (_groupRenamingIdx > i) _groupRenamingIdx--;

        Plugin.Config.Save();
        Plugin.IpcProvider.SyncConfiguration();
    }

    private string MakeUniqueGroupName(string baseName) {
        var groups = Plugin.Config.CidsGroups;
        if (!groups.Any(g => g.Name == baseName)) return baseName;
        int n = 2;
        while (groups.Any(g => g.Name == $"{baseName} ({n})")) n++;
        return $"{baseName} ({n})";
    }

    private void ApplyGroupRename(CidGroup group, string newName) {
        newName = newName.Trim();

        if (string.IsNullOrEmpty(newName) || newName == group.Name) {
            _editGroupName = group.Name;
            return;
        }

        var nameExists = Plugin.Config.CidsGroups.Any(g => g != group && g.Name.Equals(newName, StringComparison.OrdinalIgnoreCase));
        if (nameExists) {
            DalamudApi.ShowNotification($"A group named \"{newName}\" already exists", NotificationType.Warning, 3000);
            _editGroupName = group.Name;
            return;
        }

        var oldName = group.Name;
        group.Name = newName;
        _editGroupName = newName;

        Plugin.MacroManager.RenameMacrosGroupIds(oldName, newName);
        RenameFromationsGroupIds(oldName, newName);
        RenameWindowsLayoutGroupIds(oldName, newName);

        Plugin.Config.Save();
        Plugin.IpcProvider.SyncConfiguration();
    }

    public void RenameFromationsGroupIds(string oldName, string newName) {
        foreach (var formation in Plugin.Config.Formations) {
            foreach (var formationPoint in formation.Points) {
                for (int i = 0; i < formationPoint.GroupIds.Count; i++)
                    if (formationPoint.GroupIds[i].Equals(oldName, StringComparison.OrdinalIgnoreCase))
                        formationPoint.GroupIds[i] = newName;
            }
        }
    }

    public void RenameWindowsLayoutGroupIds(string oldName, string newName) {
        foreach (var windowLayout in Plugin.Config.WindowLayouts) {
            foreach (var windowLayoutSlotslot in windowLayout.Slots) {
                for (int i = 0; i < windowLayoutSlotslot.GroupIds.Count; i++)
                    if (windowLayoutSlotslot.GroupIds[i].Equals(oldName, StringComparison.OrdinalIgnoreCase))
                        windowLayoutSlotslot.GroupIds[i] = newName;
            }
        }
    }
}
