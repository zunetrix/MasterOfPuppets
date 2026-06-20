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

namespace MasterOfPuppets;

public partial class CharactersWindow {
    private void DrawCharactersTab() {
        using var tabItem = ImRaii.TabItem($"Characters###CharactersTab");
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
        using (ImRaii.Disabled(availablePartyMembers.Count == 0)) {
            float helpBtnWidth = ImGui.GetFrameHeight() + ImGui.GetStyle().ItemSpacing.X;
            ImGui.SetNextItemWidth(-helpBtnWidth);
            using (ImRaii.PushColor(ImGuiCol.Border, Style.Components.TooltipBorderColor)) {
                using (ImRaii.PushStyle(ImGuiStyleVar.PopupBorderSize, 1)) {
                    var partyNames = availablePartyMembers.Select(pm => pm.Name).ToList();
                    if (_addCharCombo.Draw("##PartyMemberSelectList", partyNames, ref _addCharSelected)) {
                        var found = availablePartyMembers.FirstOrDefault(pm => pm.Name == _addCharSelected);
                        if (found != null) {
                            Plugin.Config.AddCharacter(found);
                            Plugin.IpcProvider.SyncConfiguration();
                            _addCharSelected = string.Empty;
                        }
                    }
                }
            }
        }

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

        ImGui.Spacing();
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

    private void ResetCopiedCids() {
        _copiedCids = new();
    }
}
