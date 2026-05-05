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
    private string _charSearchFilter = string.Empty;
    private string _addCharSelected = string.Empty;
    private readonly ImGuiComboSearch _addCharCombo = new();
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

    private void DrawCharactersTab() {
        using var tabItem = ImRaii.TabItem($"Characters List###CharactersTab");
        if (!tabItem) return;
        using (ImRaii.Group()) {
            DrawCharactersHeader();
        }

        using var scroll = ImRaii.Child("##CharactersScrollArea", new Vector2(-1, -1), false);
        if (scroll) DrawCharactersTable();
    }

    private void DrawCharactersHeader() {
        var availablePartyMembers = GetAvailablePartyMembers();

        // row 1: search filter + reset copied
        float resetBtnWidth = ImGui.GetFrameHeight() + ImGui.GetStyle().ItemSpacing.X;
        ImGui.SetNextItemWidth(-resetBtnWidth);
        ImGui.InputTextWithHint("##CharSearchFilter", "Search...", ref _charSearchFilter, 128);
        ImGui.SameLine();
        if (ImGuiUtil.IconButton(FontAwesomeIcon.Undo, "##ResetCopiedCidsBtn", "Reset Copied"))
            ResetCopiedCids();

        // row 2: add party member combo + help marker
        ImGui.Text("Add From Party:");
        ImGui.BeginDisabled(availablePartyMembers.Count == 0);
        float helpBtnWidth = ImGui.GetFrameHeight() + ImGui.GetStyle().ItemSpacing.X;
        ImGui.SetNextItemWidth(-helpBtnWidth);
        ImGui.PushStyleColor(ImGuiCol.Border, Style.Components.TooltipBorderColor);
        ImGui.PushStyleVar(ImGuiStyleVar.PopupBorderSize, 1);
        var partyNames = availablePartyMembers.Select(pm => pm.Name).ToList();
        if (_addCharCombo.Draw("##PartyMemberSelectList", partyNames, ref _addCharSelected)) {
            var found = availablePartyMembers.FirstOrDefault(pm => pm.Name == _addCharSelected);
            if (found != null) {
                Plugin.Config.AddCharacter(found);
                Plugin.IpcProvider.SyncConfiguration();
                _addCharSelected = string.Empty;
            }
        }
        ImGui.PopStyleVar();
        ImGui.PopStyleColor();
        ImGui.EndDisabled();

        // ImGui.SameLine();
        // if (ImGuiUtil.IconButton(FontAwesomeIcon.Crosshairs, $"##AddFromTargetBtn", "Add From Target")) {
        //     if (GameTargetManager.GetTargetPlayerInfo() is { } target) {
        //         Plugin.Config.AddCharacter(new Character { Cid = target.Cid, Name = target.FullName });
        //         Plugin.IpcProvider.SyncConfiguration();
        //     }
        // }

        ImGui.SameLine();
        ImGuiUtil.HelpMarker(
        """
        Added characters are used for assigning macro actions; once they're in the list, they don't need to be in the party to be used in macros
        """);

        ImGui.Separator();
        ImGuiHelpers.ScaledDummy(0, 5);
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

    private void DrawCharactersTable() {
        var allCharacters = Plugin.Config.Characters.ToList();
        var filteredIndices = Enumerable.Range(0, allCharacters.Count)
            .Where(i => string.IsNullOrEmpty(_charSearchFilter) ||
                        allCharacters[i].Name.Contains(_charSearchFilter, StringComparison.OrdinalIgnoreCase))
            .ToList();

        float actionsColWidth = ImGui.GetFrameHeight() * 2 + ImGui.GetStyle().ItemSpacing.X;
        float kbColWidth = ImGui.GetFrameHeight();

        if (!ImGui.BeginTable("##CharactersTable", 7,
            ImGuiTableFlags.RowBg | ImGuiTableFlags.PadOuterX |
            ImGuiTableFlags.NoSavedSettings | ImGuiTableFlags.BordersInnerV))
            return;

        ImGui.TableSetupColumn("#", ImGuiTableColumnFlags.WidthFixed, 28 * ImGuiHelpers.GlobalScale);
        ImGui.TableSetupColumn("Name", ImGuiTableColumnFlags.WidthStretch);
        ImGui.TableSetupColumn("Login", ImGuiTableColumnFlags.WidthFixed, kbColWidth);
        ImGui.TableSetupColumn("KB", ImGuiTableColumnFlags.WidthFixed, kbColWidth);
        ImGui.TableSetupColumn("Party", ImGuiTableColumnFlags.WidthFixed, kbColWidth);
        ImGui.TableSetupColumn("TP", ImGuiTableColumnFlags.WidthFixed, kbColWidth);
        ImGui.TableSetupColumn("Actions", ImGuiTableColumnFlags.WidthFixed, actionsColWidth);
        ImGui.TableHeadersRow();

        for (int fi = 0; fi < filteredIndices.Count; fi++) {
            int i = filteredIndices[fi];
            ImGui.PushID(i);
            ImGui.TableNextRow();

            // col 0: index
            ImGui.TableNextColumn();
            ImGui.Text($"{i + 1:00}");

            // col 1: name + drag-drop
            ImGui.TableNextColumn();
            ImGui.Selectable($"{allCharacters[i].Name}");
            ImGuiUtil.ToolTip("Drag to reorder");

            if (ImGui.BeginDragDropSource()) {
                unsafe {
                    ImGui.SetDragDropPayload("DND_CHARACTER_LIST", new ReadOnlySpan<byte>(&i, sizeof(int)), ImGuiCond.None);
                    ImGui.Button($"({i + 1}) {allCharacters[i].Name}");
                }
                ImGui.EndDragDropSource();
            }

            using (ImRaii.PushColor(ImGuiCol.DragDropTarget, Style.Components.DragDropTarget)) {
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
            }

            // col 2: auto-login toggle
            ImGui.TableNextColumn();
            bool loginEnabled = allCharacters[i].AutoLoginEnabled;
            if (ImGui.Checkbox($"##Login_{allCharacters[i].Cid}", ref loginEnabled)) {
                allCharacters[i].AutoLoginEnabled = loginEnabled;
                Plugin.Config.Save();
                Plugin.IpcProvider.SyncConfiguration();
                if (loginEnabled && !DalamudApi.ClientState.IsLoggedIn)
                    Plugin.AutoLoginManager.Start();
                else if (!AutoLoginPlanner.HasEnabledCandidates(Plugin.Config.Characters))
                    Plugin.AutoLoginManager.Stop();
            }
            ImGuiUtil.ToolTip("Use this character for title-screen auto-login.");

            // col 3: keyboard broadcast toggle
            ImGui.TableNextColumn();
            bool kbEnabled = allCharacters[i].KeyboardBroadcastEnabled;
            if (ImGui.Checkbox($"##KB_{allCharacters[i].Cid}", ref kbEnabled)) {
                allCharacters[i].KeyboardBroadcastEnabled = kbEnabled;
                Plugin.Config.Save();
                Plugin.IpcProvider.SyncConfiguration();
            }
            ImGuiUtil.ToolTip("Allow this character to receive keyboard broadcast from the master client");

            // col 4: auto accept party invite toggle
            ImGui.TableNextColumn();
            bool partyEnabled = allCharacters[i].AutoAcceptPartyInvite;
            if (ImGui.Checkbox($"##Party_{allCharacters[i].Cid}", ref partyEnabled)) {
                allCharacters[i].AutoAcceptPartyInvite = partyEnabled;
                Plugin.Config.Save();
                Plugin.IpcProvider.SyncConfiguration();
            }
            ImGuiUtil.ToolTip("Allow this character to auto-accept party invites (requires global toggle in Settings)");

            // col 5: auto accept teleport toggle
            ImGui.TableNextColumn();
            bool tpEnabled = allCharacters[i].AutoAcceptTeleport;
            if (ImGui.Checkbox($"##TP_{allCharacters[i].Cid}", ref tpEnabled)) {
                allCharacters[i].AutoAcceptTeleport = tpEnabled;
                Plugin.Config.Save();
                Plugin.IpcProvider.SyncConfiguration();
            }
            ImGuiUtil.ToolTip("Allow this character to auto-accept teleport requests (requires global toggle in Settings)");

            // col 6: action buttons (right-aligned by column sizing)
            ImGui.TableNextColumn();
            bool alreadyCopied = _copiedCids.Contains(allCharacters[i].Cid);
            var copyColor = alreadyCopied ? Style.Colors.Green : Style.Colors.White;
            if (ImGuiUtil.IconButton(FontAwesomeIcon.Copy, $"##CopyCharacterName_{allCharacters[i].Cid}", "Copy Name", copyColor)) {
                ImGui.SetClipboardText(allCharacters[i].Name);
                _copiedCids.Add(allCharacters[i].Cid);
                DalamudApi.ShowNotification(Language.ClipboardCopyMessage, NotificationType.Info, 5000);
            }
            ImGui.SameLine();
            if (ImGuiUtil.IconButton(FontAwesomeIcon.Trash, $"##RemoveCharacter_{allCharacters[i].Cid}", Language.DeleteInstructionTooltip)) {
                if (ImGui.GetIO().KeyCtrl) {
                    Plugin.Config.RemoveCharacter(allCharacters[i].Cid);
                    Plugin.IpcProvider.SyncConfiguration();
                }
            }

            ImGui.PopID();
        }

        ImGui.EndTable();
        ImGui.Spacing();
        ImGui.Spacing();
    }

    private void DrawCidsGroupsTab() {
        using var tabItem = ImRaii.TabItem($"Groups###CidsGroupsTab");
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

        //  left panel: scrollable group list
        using (var left = ImRaii.Child("##GroupsList", new Vector2(leftWidth, -1), true)) {
            if (left) {
                for (int gi = 0; gi < groups.Count; gi++) {
                    if (ImGui.Selectable($"{groups[gi].Name}##grp_{gi}", gi == _selectedCidGroupIndex))
                        _selectedCidGroupIndex = gi;
                }
            }
        }

        ImGui.SameLine();

        //  right panel: selected group content
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

        float renameBtnWidth = ImGui.GetFrameHeight();
        float deleteBtnWidth = ImGui.CalcTextSize("Delete Group").X + ImGui.GetStyle().FramePadding.X * 2;
        float spacing = ImGui.GetStyle().ItemSpacing.X;

        ImGui.SetNextItemWidth(-renameBtnWidth - deleteBtnWidth - spacing * 2);
        ImGui.InputText("##GroupRenameInput", ref _editGroupName, 255);

        ImGui.SameLine();
        if (ImGuiUtil.PrimaryIconButton(FontAwesomeIcon.Check, "##RenameGroupBtn", "Rename group")) {
            ApplyGroupRename(cidGroup, _editGroupName);
        }

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
