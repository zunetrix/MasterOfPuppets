using System;
using System.Numerics;

using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;

using MasterOfPuppets.Resources;
using MasterOfPuppets.Util.ImGuiExt;

namespace MasterOfPuppets;

public partial class XivLauncherWindow {
    private void DrawLeftPanel() {
        var config = Plugin.Config;

        using (ImRaii.Group()) {
            if (!XivLauncherManager.IsLaunching) {
                if (ImGuiUtil.IconButtonStyled(FontAwesomeIcon.Play, ImGuiUtil.IconButtonStyle.Primary, "##xlplayselected", "Launch selected accounts")) {
                    var selected = config.XivLaunchEntries.FindAll(e => _selectedForLaunch.Contains(e));
                    if (selected.Count > 0)
                        XivLauncherManager.StartQueue(config, selected);
                }
                ImGui.SameLine();
                if (ImGuiUtil.IconButtonStyled(FontAwesomeIcon.Forward, ImGuiUtil.IconButtonStyle.Success, "##xlplayall", "Launch all enabled accounts")) {
                    XivLauncherManager.StartQueue(config, config.XivLaunchEntries.FindAll(e => e.Enabled));
                }
            } else {
                if (ImGuiUtil.IconButtonStyled(FontAwesomeIcon.Stop, ImGuiUtil.IconButtonStyle.Danger, "##xlstop", "Cancel launch queue")) {
                    XivLauncherManager.Cancel();
                }
                ImGui.SameLine();
                ImGui.TextColored(Style.Colors.Yellow, $"{XivLauncherManager.CurrentIndex}/{XivLauncherManager.TotalCount}");

                ImGui.SameLine();
                ImGuiUtil.HelpMarker(XivLauncherManager.Status);
            }

            ImGui.Spacing();

            if (ImGuiUtil.IconButtonStyled(FontAwesomeIcon.Plus, ImGuiUtil.IconButtonStyle.Primary, "##xladd", "Add Account")) {
                config.XivLaunchEntries.Add(new XivLaunchEntry {
                    Name = $"Account {config.XivLaunchEntries.Count + 1}",
                    AutoLogin = true,
                    Enabled = true,
                });
                _selEntry = config.XivLaunchEntries.Count - 1;
                Plugin.Config.Save();
                Plugin.IpcProvider.SyncConfiguration();
            }

            ImGui.SameLine();
            ImGui.SetNextItemWidth(-1);
            ImGui.InputTextWithHint("##xlsrch", Language.SearchInputLabel, ref _searchFilter, 64);
            ImGui.Separator();
        }

        // List
        var entries = config.XivLaunchEntries;
        float btnW = ImGui.GetFrameHeight();
        float spc = ImGui.GetStyle().ItemSpacing.X;
        float cellPadding = ImGui.GetStyle().CellPadding.X * 2;
        float actColW = btnW * 3 + spc * 2 + cellPadding;
        float listH = ImGui.GetContentRegionAvail().Y;

        if (!ImGui.BeginTable("##xlltbl", 3,
            ImGuiTableFlags.RowBg | ImGuiTableFlags.NoSavedSettings |
            ImGuiTableFlags.ScrollY | ImGuiTableFlags.BordersInnerV,
            new Vector2(-1, listH)))
            return;

        ImGui.TableSetupScrollFreeze(0, 1);
        ImGui.TableSetupColumn("#", ImGuiTableColumnFlags.WidthFixed, 28f);
        ImGui.TableSetupColumn("Account", ImGuiTableColumnFlags.WidthStretch);
        ImGui.TableSetupColumn("##xlacts", ImGuiTableColumnFlags.WidthFixed, actColW);
        ImGui.TableHeadersRow();

        int moveFrom = -1, moveTo = -1;

        for (int i = 0; i < entries.Count; i++) {
            var entry = entries[i];

            bool matchSearch = string.IsNullOrEmpty(_searchFilter) ||
                (entry.Name?.Contains(_searchFilter, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (entry.UserName?.Contains(_searchFilter, StringComparison.OrdinalIgnoreCase) ?? false);

            if (!matchSearch) continue;

            ImGui.PushID(i);
            ImGui.TableNextRow();

            // Col 0: Index / Checkbox
            ImGui.TableNextColumn();
            var isSelected = _selectedForLaunch.Contains(entry);
            if (ImGui.Checkbox("##enbl", ref isSelected)) {
                if (isSelected) _selectedForLaunch.Add(entry);
                else _selectedForLaunch.Remove(entry);
            }

            // Col 1: Name
            ImGui.TableNextColumn();
            if (_renamingIdx == i) {
                if (_renamingFocusPending) {
                    ImGui.SetKeyboardFocusHere();
                    _renamingFocusPending = false;
                }
                ImGui.SetNextItemWidth(-1);
                if (ImGui.InputText("##xlrenin", ref _renameBuffer, 64,
                    ImGuiInputTextFlags.EnterReturnsTrue | ImGuiInputTextFlags.AutoSelectAll))
                    CommitRename(i);
                if (ImGui.IsItemDeactivated() && _renamingIdx == i)
                    CommitRename(i);
            } else {
                using (ImRaii.PushColor(ImGuiCol.Header, Style.Components.ButtonBlueHovered, i == _selEntry)
                            .Push(ImGuiCol.HeaderHovered, Style.Components.ButtonBlueHovered, i == _selEntry)
                            .Push(ImGuiCol.HeaderActive, Style.Components.ButtonBlueHovered, i == _selEntry)) {

                    var displayName = !string.IsNullOrWhiteSpace(entry.Name) ? entry.Name :
                                      !string.IsNullOrWhiteSpace(entry.UserName) ? entry.UserName : "(unnamed)";

                    bool clicked = ImGui.Selectable(displayName, i == _selEntry);
                    if (clicked && _selEntry != i) {
                        _selEntry = i;
                    }
                }

                // Drag and drop reordering
                if (ImGui.BeginDragDropSource()) {
                    unsafe {
                        ImGui.SetDragDropPayload("DND_XIVLAUNCH",
                            new ReadOnlySpan<byte>(&i, sizeof(int)), ImGuiCond.None);
                    }
                    ImGui.Text(entry.Name ?? string.Empty);
                    ImGui.EndDragDropSource();
                }

                using (ImRaii.PushColor(ImGuiCol.DragDropTarget, Style.Components.DragDropTarget)) {
                    if (ImGui.BeginDragDropTarget()) {
                        var payload = ImGui.AcceptDragDropPayload("DND_XIVLAUNCH");
                        bool dropping = false;
                        unsafe { dropping = !payload.IsNull; }
                        if (dropping && payload.IsDelivery()) {
                            unsafe { moveFrom = *(int*)payload.Data; moveTo = i; }
                        }
                        ImGui.EndDragDropTarget();
                    }
                }
            }

            // Col 2: Actions
            ImGui.TableNextColumn();

            if (ImGuiUtil.IconButtonStyled(FontAwesomeIcon.Trash, ImGuiUtil.IconButtonStyle.Danger, $"##xldel{i}", Language.DeleteInstructionTooltip)
                && ImGui.GetIO().KeyCtrl) {
                config.XivLaunchEntries.RemoveAt(i);
                if (_selEntry == i) _selEntry = -1;
                else if (_selEntry > i) _selEntry--;
                Plugin.Config.Save();
                Plugin.IpcProvider.SyncConfiguration();
                ImGui.PopID();
                break;
            }

            ImGui.SameLine();

            if (ImGuiUtil.IconButton(FontAwesomeIcon.Pen, $"##xlrnm{i}", "Rename")) {
                _renamingIdx = i;
                _renameBuffer = entry.Name ?? string.Empty;
                _renamingFocusPending = true;
            }
            ImGui.SameLine();

            using (ImRaii.Disabled(XivLauncherManager.IsLaunching)) {
                if (ImGuiUtil.IconButtonStyled(FontAwesomeIcon.Play, ImGuiUtil.IconButtonStyle.Success, $"##xllaunch_{i}", "Launch account")) {
                    XivLauncherManager.StartQueue(config, new[] { entry });
                }
            }
            ImGui.PopID();
        }

        if (moveFrom >= 0 && moveTo >= 0 && moveFrom != moveTo) {
            var moved = entries[moveFrom];
            entries.RemoveAt(moveFrom);
            entries.Insert(moveTo, moved);
            if (_selEntry == moveFrom) _selEntry = moveTo;
            else if (_selEntry > moveFrom && _selEntry <= moveTo) _selEntry--;
            else if (_selEntry < moveFrom && _selEntry >= moveTo) _selEntry++;
            Plugin.Config.Save();
            Plugin.IpcProvider.SyncConfiguration();
        }

        ImGui.EndTable();
    }

    private void CommitRename(int i) {
        if (_renamingIdx != i) return;
        var trimmed = _renameBuffer.Trim();
        Plugin.Config.XivLaunchEntries[i].Name = trimmed;
        _renamingIdx = -1;
        Plugin.Config.Save();
        Plugin.IpcProvider.SyncConfiguration();
    }
}
