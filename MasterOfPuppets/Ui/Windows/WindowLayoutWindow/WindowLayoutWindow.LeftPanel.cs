using System;
using System.Linq;
using System.Numerics;

using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;

using MasterOfPuppets.Resources;
using MasterOfPuppets.Util.ImGuiExt;
using MasterOfPuppets.WindowLayouts;

namespace MasterOfPuppets;

public partial class WindowLayoutWindow {
    private void DrawLeftPanel() {
        //  Header row: [+] button + search
        using (ImRaii.Group()) {
            if (ImGuiUtil.PrimaryIconButton(FontAwesomeIcon.Plus, "##wladd", "New layout"))
                ImGui.OpenPopup("##wlnew");

            using (ImRaii.PushColor(ImGuiCol.Border, Style.Components.TooltipBorderColor)) {
                using (ImRaii.PushStyle(ImGuiStyleVar.PopupBorderSize, 1)) {
                    if (ImGui.BeginPopup("##wlnew")) {
                        ImGui.Text("Name:");
                        ImGui.SameLine();
                        ImGui.SetNextItemWidth(180);
                        bool enter = ImGui.InputText("##wlnewname", ref _newLayoutName, 64, ImGuiInputTextFlags.EnterReturnsTrue);
                        bool dupName = !string.IsNullOrWhiteSpace(_newLayoutName) &&
                            Plugin.Config.WindowLayouts.Any(l =>
                                l.Name.Equals(_newLayoutName.Trim(), StringComparison.OrdinalIgnoreCase));
                        if (dupName) {
                            ImGui.SameLine();
                            ImGui.TextColored(Style.Colors.Yellow, "already exists");
                        }
                        if ((enter || ImGui.Button("Create")) && !string.IsNullOrWhiteSpace(_newLayoutName) && !dupName) {
                            AddLayout();
                            ImGui.CloseCurrentPopup();
                        }
                        ImGui.EndPopup();
                    }
                }
            }

            ImGui.SameLine();
            ImGui.SetNextItemWidth(-1);
            ImGui.InputTextWithHint("##wlsrch", Language.SearchInputLabel, ref _searchFilter, 64);
            ImGui.Separator();
        }

        //  Layout list
        var layouts = Plugin.Config.WindowLayouts;
        float btnW = ImGui.GetFrameHeight();
        float spc = ImGui.GetStyle().ItemSpacing.X + 3 * ImGuiHelpers.GlobalScale;
        float actColW = btnW * 3 + spc * 2;
        float listH = ImGui.GetContentRegionAvail().Y;

        if (!ImGui.BeginTable("##wlltbl", 3,
            ImGuiTableFlags.RowBg | ImGuiTableFlags.NoSavedSettings |
            ImGuiTableFlags.ScrollY | ImGuiTableFlags.BordersInnerV,
            new Vector2(-1, listH)))
            return;

        ImGui.TableSetupScrollFreeze(0, 1);
        ImGui.TableSetupColumn("#", ImGuiTableColumnFlags.WidthFixed, 28f);
        ImGui.TableSetupColumn("Layout", ImGuiTableColumnFlags.WidthStretch);
        ImGui.TableSetupColumn("##wlacts", ImGuiTableColumnFlags.WidthFixed, actColW);
        ImGui.TableHeadersRow();

        int moveFrom = -1, moveTo = -1;

        for (int i = 0; i < layouts.Count; i++) {
            var layout = layouts[i];
            if (!string.IsNullOrEmpty(_searchFilter) &&
                !layout.Name.Contains(_searchFilter, StringComparison.OrdinalIgnoreCase))
                continue;

            ImGui.PushID(i);
            ImGui.TableNextRow();

            // Col 0: row number
            ImGui.TableNextColumn();
            ImGui.TextDisabled($"{i + 1}");

            // Col 1: name / rename input
            ImGui.TableNextColumn();
            if (_renamingIdx == i) {
                if (_renamingFocusPending) {
                    ImGui.SetKeyboardFocusHere();
                    _renamingFocusPending = false;
                }
                ImGui.SetNextItemWidth(-1);
                if (ImGui.InputText("##wlrenin", ref _renameBuffer, 64,
                    ImGuiInputTextFlags.EnterReturnsTrue | ImGuiInputTextFlags.AutoSelectAll))
                    CommitRename(i);
                if (ImGui.IsItemDeactivated() && _renamingIdx == i)
                    CommitRename(i);
            } else {
                using (ImRaii.PushColor(ImGuiCol.Header, Style.Components.ButtonBlueHovered, i == _selLayout)
                            .Push(ImGuiCol.HeaderHovered, Style.Components.ButtonBlueHovered, i == _selLayout)
                            .Push(ImGuiCol.HeaderActive, Style.Components.ButtonBlueHovered, i == _selLayout)) {
                    bool clicked = ImGui.Selectable(
                        layout.Name.Length > 0 ? layout.Name : "(unnamed)",
                        i == _selLayout);
                    if (clicked && _selLayout != i) {
                        _selLayout = i;
                        _selSlot = -1;
                    }
                }

                // Drag source
                if (ImGui.BeginDragDropSource()) {
                    unsafe {
                        ImGui.SetDragDropPayload("DND_WLAYOUT",
                            new ReadOnlySpan<byte>(&i, sizeof(int)), ImGuiCond.None);
                    }
                    ImGui.Text(layout.Name.Length > 0 ? layout.Name : "(unnamed)");
                    ImGui.EndDragDropSource();
                }

                // Drop target
                using (ImRaii.PushColor(ImGuiCol.DragDropTarget, Style.Components.DragDropTarget)) {
                    if (ImGui.BeginDragDropTarget()) {
                        var payload = ImGui.AcceptDragDropPayload("DND_WLAYOUT");
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
                        if (ImGui.BeginPopupContextItem($"##wlcm{i}")) {
                            if (ImGui.MenuItem("Clone")) {
                                var clone = layout.Clone();
                                clone.Name = MakeUniqueName(clone.Name);
                                Plugin.Config.WindowLayouts.Insert(i + 1, clone);
                                Plugin.Config.Save();
                                Plugin.IpcProvider.SyncConfiguration();
                            }
                            ImGui.EndPopup();
                        }
                    }
                }
            }

            // Col 2: actions - Delete | Rename | Apply
            ImGui.TableNextColumn();
            if (ImGuiUtil.DangerIconButton(FontAwesomeIcon.Trash, $"##wldel{i}", Language.DeleteInstructionTooltip)
                && ImGui.GetIO().KeyCtrl) {
                Plugin.Config.WindowLayouts.RemoveAt(i);
                if (_selLayout == i)
                    _selLayout = layouts.Count > 0 ? Math.Min(i, layouts.Count - 1) : -1;
                else if (_selLayout > i)
                    _selLayout--;
                if (_renamingIdx == i) _renamingIdx = -1;
                else if (_renamingIdx > i) _renamingIdx--;
                _selSlot = -1;
                Plugin.Config.Save();
                Plugin.IpcProvider.SyncConfiguration();
                ImGui.PopID();
                break;
            }

            ImGui.SameLine();
            if (ImGuiUtil.IconButton(FontAwesomeIcon.Pen, $"##wlrnm{i}", "Rename")) {
                _renamingIdx = i;
                _renameBuffer = layout.Name;
                _renamingFocusPending = true;
            }

            ImGui.SameLine();
            if (ImGuiUtil.SuccessIconButton(FontAwesomeIcon.Play, $"##wlexec{i}", "Apply layout to all clients"))
                Plugin.IpcProvider.ApplyWindowLayout(layout.Name);

            ImGui.PopID();
        }

        // Apply reorder after full render
        if (moveFrom >= 0 && moveTo >= 0 && moveFrom != moveTo) {
            var moved = layouts[moveFrom];
            layouts.RemoveAt(moveFrom);
            layouts.Insert(moveTo, moved);
            if (_selLayout == moveFrom) _selLayout = moveTo;
            else if (_selLayout > moveFrom && _selLayout <= moveTo) _selLayout--;
            else if (_selLayout < moveFrom && _selLayout >= moveTo) _selLayout++;
            Plugin.Config.Save();
            Plugin.IpcProvider.SyncConfiguration();
        }

        ImGui.EndTable();
    }

    private void CommitRename(int i) {
        if (_renamingIdx != i) return;
        var trimmed = _renameBuffer.Trim();
        if (!string.IsNullOrEmpty(trimmed))
            Plugin.Config.WindowLayouts[i].Name = trimmed;
        _renamingIdx = -1;
        Plugin.Config.Save();
        Plugin.IpcProvider.SyncConfiguration();
    }

    private string MakeUniqueName(string baseName) {
        if (!Plugin.Config.WindowLayouts.Any(l => l.Name == baseName)) return baseName;
        int n = 2;
        while (Plugin.Config.WindowLayouts.Any(l => l.Name == $"{baseName} ({n})")) n++;
        return $"{baseName} ({n})";
    }
}
