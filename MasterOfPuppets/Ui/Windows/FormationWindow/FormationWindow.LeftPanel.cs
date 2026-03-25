using System;
using System.Linq;
using System.Numerics;

using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;

using MasterOfPuppets.Resources;
using MasterOfPuppets.Util.ImGuiExt;

namespace MasterOfPuppets;

public partial class FormationWindow {

    // =========================================================================
    // Left panel - formation list
    // =========================================================================

    private void DrawLeftPanel() {
        using (ImRaii.Group()) {
            if (ImGuiUtil.PrimaryIconButton(FontAwesomeIcon.Plus, "##fiadd_s", "New formation"))
                ImGui.OpenPopup("##finew");

            using (ImRaii.PushColor(ImGuiCol.Border, Style.Components.TooltipBorderColor)) {
                using (ImRaii.PushStyle(ImGuiStyleVar.PopupBorderSize, 1)) {
                    if (ImGui.BeginPopup("##finew")) {
                        ImGui.Text("Name:");
                        ImGui.SameLine();
                        ImGui.SetNextItemWidth(180);
                        bool enter = ImGui.InputText("##finewname", ref _newFmName, 64, ImGuiInputTextFlags.EnterReturnsTrue);
                        bool duplicate = !string.IsNullOrWhiteSpace(_newFmName) &&
                            Plugin.Config.Formations.Any(f =>
                                f.Name.Equals(_newFmName.Trim(), StringComparison.OrdinalIgnoreCase));
                        if (duplicate) {
                            ImGui.SameLine();
                            ImGui.TextColored(Style.Colors.Yellow, "already exists");
                        }
                        if ((enter || ImGui.Button("Create")) && !string.IsNullOrWhiteSpace(_newFmName) && !duplicate) {
                            AddFormation();
                            ImGui.CloseCurrentPopup();
                        }
                        ImGui.EndPopup();
                    }
                }
            }

            ImGui.SameLine();
            ImGui.SetNextItemWidth(-1);
            ImGui.InputTextWithHint("##filsrch", Language.SearchInputLabel, ref _searchFilter, 64);
            ImGui.Separator();
        }

        var formations = Plugin.Config.Formations;
        float btnW = ImGui.GetFrameHeight();
        float spc = ImGui.GetStyle().ItemSpacing.X + 3 * ImGuiHelpers.GlobalScale;
        float actColW = btnW * 3 + spc * 2;
        float listH = ImGui.GetContentRegionAvail().Y;

        if (!ImGui.BeginTable("##filtbl", 3,
            ImGuiTableFlags.RowBg | ImGuiTableFlags.NoSavedSettings |
            ImGuiTableFlags.ScrollY | ImGuiTableFlags.BordersInnerV,
            new Vector2(-1, listH)))
            return;

        ImGui.TableSetupScrollFreeze(0, 1);
        ImGui.TableSetupColumn("#", ImGuiTableColumnFlags.WidthFixed, 28f);
        ImGui.TableSetupColumn("Formation", ImGuiTableColumnFlags.WidthStretch);
        ImGui.TableSetupColumn("##acts", ImGuiTableColumnFlags.WidthFixed, actColW);
        ImGui.TableHeadersRow();

        int moveFrom = -1, moveTo = -1;

        for (int i = 0; i < formations.Count; i++) {
            var f = formations[i];
            if (!string.IsNullOrEmpty(_searchFilter) &&
                !f.Name.Contains(_searchFilter, StringComparison.OrdinalIgnoreCase))
                continue;

            ImGui.PushID(i);
            ImGui.TableNextRow();

            // Col 0: row number
            ImGui.TableNextColumn();
            ImGui.TextDisabled($"{i + 1}");

            // Col 1: name - rename InputText or Selectable with D&D + context menu
            ImGui.TableNextColumn();
            if (_renamingIdx == i) {
                if (_renamingFocusPending) {
                    ImGui.SetKeyboardFocusHere();
                    _renamingFocusPending = false;
                }
                ImGui.SetNextItemWidth(-1);
                if (ImGui.InputText("##firenin", ref _renameBuffer, 64,
                    ImGuiInputTextFlags.EnterReturnsTrue | ImGuiInputTextFlags.AutoSelectAll))
                    CommitRename(i);
                if (ImGui.IsItemDeactivated() && _renamingIdx == i)
                    CommitRename(i);
            } else {
                bool clicked = ImGui.Selectable(
                    f.Name.Length > 0 ? f.Name : "(unnamed)",
                    i == _selFormation);
                if (clicked && _selFormation != i) {
                    _selFormation = i;
                    _selPoint = -1;
                    _needsAxisReset = true;
                }

                // Drag source
                if (ImGui.BeginDragDropSource()) {
                    unsafe {
                        ImGui.SetDragDropPayload("DND_FORMATION",
                            new ReadOnlySpan<byte>(&i, sizeof(int)), ImGuiCond.None);
                    }
                    ImGui.Text(f.Name.Length > 0 ? f.Name : "(unnamed)");
                    ImGui.EndDragDropSource();
                }

                // Drop target
                using (ImRaii.PushColor(ImGuiCol.DragDropTarget, Style.Components.DragDropTarget)) {
                    if (ImGui.BeginDragDropTarget()) {
                        var payload = ImGui.AcceptDragDropPayload("DND_FORMATION");
                        bool dropping = false;
                        unsafe { dropping = !payload.IsNull; }
                        if (dropping && payload.IsDelivery()) {
                            unsafe { moveFrom = *(int*)payload.Data; moveTo = i; }
                        }
                        ImGui.EndDragDropTarget();
                    }
                }

                using (ImRaii.PushColor(ImGuiCol.Border, Style.Components.TooltipBorderColor)) {
                    using (ImRaii.PushStyle(ImGuiStyleVar.PopupBorderSize, 1)) {
                        // Context menu (right-click)
                        if (ImGui.BeginPopupContextItem($"##ficm{i}")) {
                            if (ImGui.MenuItem("Clone")) {
                                var clone = f.Clone();
                                clone.Name = MakeUniqueName(clone.Name);
                                Plugin.Config.Formations.Insert(i + 1, clone);
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
            if (ImGuiUtil.DangerIconButton(FontAwesomeIcon.Trash, $"##fidel{i}", Language.DeleteInstructionTooltip)
                && ImGui.GetIO().KeyCtrl) {
                Plugin.Config.Formations.RemoveAt(i);
                if (_selFormation == i)
                    _selFormation = formations.Count > 0 ? Math.Min(i, formations.Count - 1) : -1;
                else if (_selFormation > i)
                    _selFormation--;
                if (_renamingIdx == i) _renamingIdx = -1;
                else if (_renamingIdx > i) _renamingIdx--;
                _selPoint = -1;
                Plugin.Config.Save();
                Plugin.IpcProvider.SyncConfiguration();
                ImGui.PopID();
                break;
            }

            ImGui.SameLine();
            if (ImGuiUtil.IconButton(FontAwesomeIcon.Pen, $"##firnm{i}", "Rename")) {
                _renamingIdx = i;
                _renameBuffer = f.Name;
                _renamingFocusPending = true;
            }

            ImGui.SameLine();
            if (ImGuiUtil.SuccessIconButton(FontAwesomeIcon.Play, $"##fiexec{i}", "Execute formation"))
                Plugin.IpcProvider.ExecuteFormation(f.Name);

            ImGui.PopID();
        }

        // Apply reorder after full render pass
        if (moveFrom >= 0 && moveTo >= 0 && moveFrom != moveTo) {
            var moved = formations[moveFrom];
            formations.RemoveAt(moveFrom);
            formations.Insert(moveTo, moved);
            if (_selFormation == moveFrom) _selFormation = moveTo;
            else if (_selFormation > moveFrom && _selFormation <= moveTo) _selFormation--;
            else if (_selFormation < moveFrom && _selFormation >= moveTo) _selFormation++;

            Plugin.Config.Save();
            Plugin.IpcProvider.SyncConfiguration();
        }

        ImGui.EndTable();
    }

    private void CommitRename(int i) {
        if (_renamingIdx != i) return;
        var trimmed = _renameBuffer.Trim();
        if (!string.IsNullOrEmpty(trimmed))
            Plugin.Config.Formations[i].Name = trimmed;
        _renamingIdx = -1;

        Plugin.Config.Save();
        Plugin.IpcProvider.SyncConfiguration();
    }

    private string MakeUniqueName(string baseName) {
        if (!Plugin.Config.Formations.Any(f => f.Name == baseName)) return baseName;
        int n = 2;
        while (Plugin.Config.Formations.Any(f => f.Name == $"{baseName} ({n})")) n++;
        return $"{baseName} ({n})";
    }
}
