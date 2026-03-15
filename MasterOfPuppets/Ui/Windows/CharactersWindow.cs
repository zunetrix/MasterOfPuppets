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
using Dalamud.Utility;

using MasterOfPuppets.Extensions;
using MasterOfPuppets.Extensions.Dalamud;
using MasterOfPuppets.Resources;
using MasterOfPuppets.Util.ImGuiExt;

namespace MasterOfPuppets;

public class CharactersWindow : Window {
    private Plugin Plugin { get; }

    private string _tmpGroupName = string.Empty;
    private string _editGroupName = string.Empty;
    private int _editGroupNameLastIndex = -1;
    private int _selectedCidGroupIndex { get; set; } = 0;
    public HashSet<ulong> _copiedCids = new();

    public CharactersWindow(Plugin plugin) : base($"{Plugin.Name} Characters###CharactersWindow") {
        Plugin = plugin;

        Size = ImGuiHelpers.ScaledVector2(450, 400);
        SizeCondition = ImGuiCond.FirstUseEver;
    }

    public override void PreDraw() {
        base.PreDraw();
    }

    private bool IsValidGroup() {
        return Plugin.Config.CidsGroups.IndexExists(_selectedCidGroupIndex) && Plugin.Config.CidsGroups.Count > 0;
    }

    public override void Draw() {
        using var tabBar = ImRaii.TabBar($"{Language.SettingsGeneralTab}###CharactersManagerTabs");
        if (!tabBar) return;

        DrawCharactersTab();
        DrawCidsGroupsTab();
    }

    // ─────────────────────────────────────────────────────────────
    // Tab 1: Characters List
    // ─────────────────────────────────────────────────────────────

    private void DrawCharactersTab() {
        using var tabItem = ImRaii.TabItem($"Characters List###CharactersTab");
        if (!tabItem) return;
        DrawPartyMemberSelector();
        DrawCharactersTable();
    }

    private List<Character> GetAvailablePartyMembers() {
        var usedCids = Plugin.Config.Characters
            .Select(c => c.Cid)
            .ToHashSet();

        return DalamudApi.PartyList
            .Select(pm => pm.GetPartyMemberData())
            .Where(pm => !usedCids.Contains(pm.Cid))
            .Select(pm => new Character { Cid = pm.Cid, Name = $"{pm.Name}@{pm.World}" })
            .ToList();
    }

    private void DrawPartyMemberSelector() {
        var availablePartyMembers = GetAvailablePartyMembers();

        ImGui.BeginDisabled(availablePartyMembers.Count == 0);
        ImGui.Text(Language.CharactersLabel);

        ImGui.PushStyleColor(ImGuiCol.Border, Style.Components.TooltipBorderColor);
        ImGui.PushStyleVar(ImGuiStyleVar.PopupBorderSize, 1);
        if (ImGui.BeginCombo("##PartyMemberSelectList", "Select a party character to add")) {
            foreach (var partyMember in availablePartyMembers) {
                if (ImGui.Selectable($"{partyMember.Name}##{partyMember.Cid}", false)) {
                    Plugin.Config.AddCharacter(partyMember);
                    Plugin.IpcProvider.SyncConfiguration();
                }
            }
            ImGui.EndCombo();
        }
        ImGui.PopStyleVar();
        ImGui.PopStyleColor();

        ImGui.EndDisabled();
        ImGuiUtil.HelpMarker("""
        Added characters are used to assign macro actions, once in the list they dont need be in the party to be assigned in macros

        Drag to reorder
        """);

        ImGui.SameLine();
        if (ImGuiUtil.IconButton(FontAwesomeIcon.Undo, "##ResetCopiedCidsBtn", "Reset Copied")) {
            ResetCopiedCids();
        }

        ImGui.Spacing();
        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();
        ImGui.Spacing();
        ImGui.Spacing();
    }

    private void DrawCharactersTable() {
        var characters = Plugin.Config.Characters;

        float actionsColWidth = ImGui.GetFrameHeight() * 2 + ImGui.GetStyle().ItemSpacing.X;

        if (!ImGui.BeginTable("##CharactersTable", 3,
            ImGuiTableFlags.RowBg | ImGuiTableFlags.PadOuterX |
            ImGuiTableFlags.NoSavedSettings | ImGuiTableFlags.BordersInnerV))
            return;

        ImGui.TableSetupColumn("#", ImGuiTableColumnFlags.WidthFixed, 28 * ImGuiHelpers.GlobalScale);
        ImGui.TableSetupColumn("Name", ImGuiTableColumnFlags.WidthStretch);
        ImGui.TableSetupColumn("Actions", ImGuiTableColumnFlags.WidthFixed, actionsColWidth);
        ImGui.TableHeadersRow();

        for (int i = 0; i < characters.Count; i++) {
            ImGui.PushID(i);
            ImGui.TableNextRow();

            // col 0: index
            ImGui.TableNextColumn();
            ImGui.Text($"{i + 1:00}");

            // col 1: name + drag-drop
            ImGui.TableNextColumn();
            ImGui.Selectable($"{characters[i].Name}");
            ImGuiUtil.ToolTip("Drag to reorder");

            if (ImGui.BeginDragDropSource()) {
                unsafe {
                    ImGui.SetDragDropPayload("DND_CHARACTER_LIST", new ReadOnlySpan<byte>(&i, sizeof(int)), ImGuiCond.None);
                    ImGui.Button($"({i + 1}) {characters[i].Name}");
                }
                ImGui.EndDragDropSource();
            }

            ImGui.PushStyleColor(ImGuiCol.DragDropTarget, Style.Components.DragDropTarget);
            if (ImGui.BeginDragDropTarget()) {
                var dragDropPayload = ImGui.AcceptDragDropPayload("DND_CHARACTER_LIST");
                bool isDropping = false;
                unsafe { isDropping = !dragDropPayload.IsNull; }
                if (isDropping && dragDropPayload.IsDelivery()) {
                    unsafe {
                        int originalIndex = *(int*)dragDropPayload.Data;
                        int offset = i - originalIndex;
                        if (offset != 0 && originalIndex + offset >= 0) {
                            Plugin.Config.MoveCharacterToIndex(originalIndex, originalIndex + offset);
                            Plugin.IpcProvider.SyncConfiguration();
                        }
                    }
                }
                ImGui.EndDragDropTarget();
            }
            ImGui.PopStyleColor();

            // col 2: action buttons (right-aligned by column sizing)
            ImGui.TableNextColumn();
            bool alreadyCopied = _copiedCids.Contains(characters[i].Cid);
            var copyColor = alreadyCopied ? Style.Colors.Green : Style.Colors.White;
            if (ImGuiUtil.IconButton(FontAwesomeIcon.Copy, $"##CopyCharacterName_{characters[i].Cid}", "Copy Name", copyColor)) {
                ImGui.SetClipboardText(characters[i].Name);
                _copiedCids.Add(characters[i].Cid);
                DalamudApi.ShowNotification(Language.ClipboardCopyMessage, NotificationType.Info, 5000);
            }
            ImGui.SameLine();
            if (ImGuiUtil.IconButton(FontAwesomeIcon.Trash, $"##RemoveCharacter_{characters[i].Cid}", Language.DeleteInstructionTooltip)) {
                if (ImGui.GetIO().KeyCtrl) {
                    Plugin.Config.RemoveCharacter(characters[i].Cid);
                    Plugin.IpcProvider.SyncConfiguration();
                }
            }

            ImGui.PopID();
        }

        ImGui.EndTable();
        ImGui.Spacing();
        ImGui.Spacing();
    }

    // ─────────────────────────────────────────────────────────────
    // Tab 2: Characters Groups
    // ─────────────────────────────────────────────────────────────

    private void DrawCidsGroupsTab() {
        using var tabItem = ImRaii.TabItem($"Characters Groups###CidsGroupsTab");
        if (!tabItem) return;
        DrawCidsGroupsHeader();
        DrawCidsGroupsSplitView();
    }

    private void DrawCidsGroupsHeader() {
        float addBtnWidth = ImGui.CalcTextSize("Add Group").X
            + ImGui.GetStyle().FramePadding.X * 2
            + ImGui.GetStyle().ItemSpacing.X;
        ImGui.SetNextItemWidth(-addBtnWidth);
        ImGui.InputTextWithHint("##GroupNameInput", "Group name", ref _tmpGroupName, 255, ImGuiInputTextFlags.AutoSelectAll);
        ImGui.SameLine();
        if (ImGui.Button("Add Group##AddNewGroupBtn")) {
            if (!_tmpGroupName.IsNullOrEmpty()) {
                var trimmedName = _tmpGroupName.Trim();
                var nameExists = Plugin.Config.CidsGroups.Any(g => g.Name.Equals(trimmedName, StringComparison.OrdinalIgnoreCase));

                if (nameExists) {
                    DalamudApi.ShowNotification($"A group named \"{trimmedName}\" already exists", NotificationType.Warning, 3000);
                } else {
                    Plugin.Config.CidsGroups.Add(new CidGroup { Name = trimmedName, Cids = new() });
                    _selectedCidGroupIndex = Plugin.Config.CidsGroups.Count - 1;
                    _tmpGroupName = string.Empty;
                    Plugin.Config.Save();
                    Plugin.IpcProvider.SyncConfiguration();
                }
            }
        }
        ImGui.Separator();
        ImGui.Spacing();
    }

    private void DrawCidsGroupsSplitView() {
        var groups = Plugin.Config.CidsGroups;
        float leftWidth = 130 * ImGuiHelpers.GlobalScale;

        // ─── left panel: scrollable group list ───
        using (var left = ImRaii.Child("##GroupsList", new Vector2(leftWidth, -1), true)) {
            if (left) {
                for (int gi = 0; gi < groups.Count; gi++) {
                    if (ImGui.Selectable($"{groups[gi].Name}##grp_{gi}", gi == _selectedCidGroupIndex))
                        _selectedCidGroupIndex = gi;
                }
            }
        }

        ImGui.SameLine();

        // ─── right panel: selected group content ───
        using (var right = ImRaii.Child("##GroupContent", new Vector2(-1, -1), false)) {
            if (!right) return;
            if (!IsValidGroup()) {
                ImGui.TextDisabled("Select a group");
                return;
            }
            DrawGroupContentPanel(groups[_selectedCidGroupIndex]);
        }
    }

    private void DrawGroupContentPanel(CidGroup cidGroup) {
        // sync edit buffer when selection changes
        if (_editGroupNameLastIndex != _selectedCidGroupIndex) {
            _editGroupName = cidGroup.Name;
            _editGroupNameLastIndex = _selectedCidGroupIndex;
        }

        float deleteBtnWidth = ImGui.CalcTextSize("Delete Group").X + ImGui.GetStyle().FramePadding.X * 2;
        ImGui.SetNextItemWidth(-deleteBtnWidth - ImGui.GetStyle().ItemSpacing.X);
        if (ImGui.InputText("##GroupRenameInput", ref _editGroupName, 255, ImGuiInputTextFlags.EnterReturnsTrue)) {
            ApplyGroupRename(cidGroup, _editGroupName);
        }
        ImGuiUtil.ToolTip("Press Enter to rename");

        ImGui.SameLine();

        using (ImRaii.PushColor(ImGuiCol.Button, Style.Components.ButtonDangerNormal)
                     .Push(ImGuiCol.ButtonHovered, Style.Components.ButtonDangerHovered)
                     .Push(ImGuiCol.ButtonActive, Style.Components.ButtonDangerActive)) {
            if (ImGui.Button("Delete Group##DeleteGroupBtn")) {
                if (ImGui.GetIO().KeyCtrl) {
                    var deletedName = cidGroup.Name;
                    Plugin.Config.CidsGroups.RemoveAt(_selectedCidGroupIndex);

                    foreach (var macro in Plugin.Config.Macros)
                        foreach (var command in macro.Commands)
                            command.GroupIds.RemoveAll(n => n == deletedName);

                    _selectedCidGroupIndex = Math.Max(0, _selectedCidGroupIndex - 1);
                    Plugin.Config.Save();
                    Plugin.IpcProvider.SyncConfiguration();
                    return;
                }
            }
        }
        ImGuiUtil.ToolTip(Language.DeleteInstructionTooltip);

        ImGui.Spacing();

        // add-character combo
        var availableCharacters = Plugin.Config.Characters
            .Where(c => !cidGroup.Cids.Contains(c.Cid))
            .ToList();

        ImGui.BeginDisabled(availableCharacters.Count == 0);
        ImGui.SetNextItemWidth(-1);
        ImGui.PushStyleColor(ImGuiCol.Border, Style.Components.TooltipBorderColor);
        ImGui.PushStyleVar(ImGuiStyleVar.PopupBorderSize, 1);
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
        ImGui.PopStyleVar();
        ImGui.PopStyleColor();
        ImGui.EndDisabled();

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        DrawCidGroupCharactersTable(cidGroup);
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

        foreach (var macro in Plugin.Config.Macros)
            foreach (var command in macro.Commands)
                for (int i = 0; i < command.GroupIds.Count; i++)
                    if (command.GroupIds[i] == oldName)
                        command.GroupIds[i] = newName;

        Plugin.Config.Save();
        Plugin.IpcProvider.SyncConfiguration();
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

            ImGui.PushStyleColor(ImGuiCol.DragDropTarget, Style.Components.DragDropTarget);
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
            ImGui.PopStyleColor();

            // col 2: delete button
            ImGui.TableNextColumn();
            if (ImGuiUtil.DangerIconButton(FontAwesomeIcon.Trash, $"##del_{i}", Language.DeleteInstructionTooltip))
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

    private void ResetCopiedCids() {
        _copiedCids = new();
    }
}
