using System;
using System.Linq;
using System.Numerics;

using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;

using MasterOfPuppets.Resources;
using MasterOfPuppets.Util.ImGuiExt;

namespace MasterOfPuppets;

public partial class WindowLayoutWindow {
    private void DrawRightPanel() {
        var layout = SelectedLayout;
        if (layout == null) { ImGui.TextDisabled("No layout selected"); return; }

        var slot = SelectedSlot;

        //  Slots list
        ImGui.SetNextItemOpen(true, ImGuiCond.Appearing);
        if (ImGui.CollapsingHeader($"Slots ({layout.Slots.Count})##wlslots")) {
            if (layout.Slots.Count == 0) {
                ImGui.TextDisabled("Click [+] in the preview to add a slot");
            } else {
                float btnW = ImGui.GetFrameHeight();
                float rowH = ImGui.GetFrameHeightWithSpacing();
                float hdrH = ImGui.GetFrameHeightWithSpacing();
                float maxH = 6 * rowH + hdrH;
                float tblH = Math.Min(layout.Slots.Count * rowH + hdrH, maxH);

                if (ImGui.BeginTable("##wlslttbl", 6,
                    ImGuiTableFlags.RowBg | ImGuiTableFlags.NoSavedSettings |
                    ImGuiTableFlags.BordersInnerV | ImGuiTableFlags.ScrollY,
                    new Vector2(-1, tblH))) {

                    ImGui.TableSetupScrollFreeze(0, 1);
                    ImGui.TableSetupColumn("##sel", ImGuiTableColumnFlags.WidthFixed, 25f);
                    ImGui.TableSetupColumn("X", ImGuiTableColumnFlags.WidthStretch);
                    ImGui.TableSetupColumn("Y", ImGuiTableColumnFlags.WidthStretch);
                    ImGui.TableSetupColumn("W", ImGuiTableColumnFlags.WidthStretch);
                    ImGui.TableSetupColumn("H", ImGuiTableColumnFlags.WidthStretch);
                    ImGui.TableSetupColumn("##del", ImGuiTableColumnFlags.WidthFixed, btnW);
                    ImGui.TableHeadersRow();

                    int delIdx = -1;
                    for (int i = 0; i < layout.Slots.Count; i++) {
                        var s = layout.Slots[i];
                        ImGui.PushID(i);
                        ImGui.TableNextRow();

                        // Col 0: row selector
                        ImGui.TableNextColumn();
                        using (ImRaii.PushColor(ImGuiCol.Header, Style.Components.ButtonBlueHovered)
                                     .Push(ImGuiCol.HeaderHovered, Style.Components.ButtonBlueHovered)
                                     .Push(ImGuiCol.HeaderActive, Style.Components.ButtonBlueHovered)) {
                            if (ImGui.Selectable($"{i + 1:00}##wlsrow{i}", i == _selSlot))
                                _selSlot = i;
                        }
                        ImGuiUtil.ToolTip("Drag to reorder");

                        if (ImGui.BeginDragDropSource()) {
                            unsafe {
                                ImGui.SetDragDropPayload("DND_LAYOUT_WINDOW", new ReadOnlySpan<byte>(&i, sizeof(int)), ImGuiCond.None);
                                ImGui.Text($"#{i + 1}");
                            }
                            ImGui.EndDragDropSource();
                        }

                        using (ImRaii.PushColor(ImGuiCol.DragDropTarget, Style.Components.DragDropTarget)) {
                            if (ImGui.BeginDragDropTarget()) {
                                var payload = ImGui.AcceptDragDropPayload("DND_LAYOUT_WINDOW");
                                bool isDropping = false;
                                unsafe { isDropping = !payload.IsNull; }
                                if (isDropping && payload.IsDelivery()) {
                                    unsafe {
                                        int from = *(int*)payload.Data;
                                        if (from != i) {
                                            var layoutSlot = layout.Slots[from];
                                            layout.Slots.RemoveAt(from);
                                            layout.Slots.Insert(i, layoutSlot);
                                            if (_selSlot == from) _selSlot = i;
                                            Plugin.IpcProvider.SyncConfiguration();
                                        }
                                    }
                                }
                                ImGui.EndDragDropTarget();
                            }
                        }
                        // Col 1: X
                        ImGui.TableNextColumn();
                        ImGui.SetNextItemWidth(-1);
                        var x = s.X;
                        if (ImGui.DragInt($"##sx{i}", ref x, 1, -9999, 9999)) {
                            s.X = x;
                        }
                        if (ImGui.IsItemClicked(ImGuiMouseButton.Right)) {
                            s.X = 0;
                            Plugin.Config.Save();
                            Plugin.IpcProvider.SyncConfiguration();
                        }
                        if (ImGui.IsItemActivated()) _selSlot = i;
                        if (ImGui.IsItemDeactivatedAfterEdit()) { Plugin.Config.Save(); Plugin.IpcProvider.SyncConfiguration(); }

                        // Col 2: Y
                        ImGui.TableNextColumn();
                        ImGui.SetNextItemWidth(-1);
                        var y = s.Y;
                        if (ImGui.DragInt($"##sy{i}", ref y, 1, -9999, 9999)) {
                            s.Y = y;
                        }
                        if (ImGui.IsItemClicked(ImGuiMouseButton.Right)) {
                            s.Y = 0;
                            Plugin.Config.Save();
                            Plugin.IpcProvider.SyncConfiguration();
                        }
                        if (ImGui.IsItemActivated()) _selSlot = i;
                        if (ImGui.IsItemDeactivatedAfterEdit()) { Plugin.Config.Save(); Plugin.IpcProvider.SyncConfiguration(); }

                        // Col 3: W
                        ImGui.TableNextColumn();
                        ImGui.SetNextItemWidth(-1);
                        var width = s.Width;
                        if (ImGui.DragInt($"##sw{i}", ref width, 1, 100, 9999)) {
                            s.Width = width;
                        }
                        if (ImGui.IsItemClicked(ImGuiMouseButton.Right)) {
                            s.Width = 130;
                            Plugin.Config.Save();
                            Plugin.IpcProvider.SyncConfiguration();
                        }
                        if (ImGui.IsItemActivated()) _selSlot = i;
                        if (ImGui.IsItemDeactivatedAfterEdit()) { Plugin.Config.Save(); Plugin.IpcProvider.SyncConfiguration(); }

                        // Col 4: H
                        ImGui.TableNextColumn();
                        ImGui.SetNextItemWidth(-1);
                        var height = s.Height;
                        if (ImGui.DragInt($"##sh{i}", ref height, 1, 60, 9999)) {
                            s.Height = height;
                        }
                        if (ImGui.IsItemClicked(ImGuiMouseButton.Right)) {
                            s.Height = 100;
                            Plugin.Config.Save();
                            Plugin.IpcProvider.SyncConfiguration();
                        }
                        if (ImGui.IsItemActivated()) _selSlot = i;
                        if (ImGui.IsItemDeactivatedAfterEdit()) { Plugin.Config.Save(); Plugin.IpcProvider.SyncConfiguration(); }

                        // Col 5: Delete
                        ImGui.TableNextColumn();
                        if (ImGuiUtil.DangerIconButton(FontAwesomeIcon.Trash, $"##wldslt{i}", Language.DeleteInstructionTooltip)
                            && ImGui.GetIO().KeyCtrl)
                            delIdx = i;

                        ImGui.PopID();
                    }
                    ImGui.EndTable();

                    if (delIdx >= 0) {
                        layout.Slots.RemoveAt(delIdx);
                        if (_selSlot >= layout.Slots.Count)
                            _selSlot = layout.Slots.Count - 1;
                        Plugin.Config.Save();
                        Plugin.IpcProvider.SyncConfiguration();
                    }
                }
            }

            ImGui.Spacing();
            if (ImGuiUtil.DangerButton("Clear All##wlclearslots", new Vector2(-1, 0)) && ImGui.GetIO().KeyCtrl) {
                layout.Slots.Clear();
                _selSlot = -1;
                Plugin.Config.Save();
                Plugin.IpcProvider.SyncConfiguration();
            }
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("Ctrl+Click to clear all slots");
        }

        //  Selected slot detail
        if (slot == null) {
            ImGui.Spacing();
            ImGui.TextDisabled("Select a slot to edit associations");
            return;
        }

        ImGui.Spacing();

        // Characters
        if (ImGui.CollapsingHeader("Characters##wlchars")) {
            int delIdx = -1;
            float delW = ImGui.GetFrameHeight();

            var assignedCids = layout.Slots.SelectMany(s => s.Cids).ToHashSet();
            var availChars = Plugin.Config.Characters
                .Where(c => !assignedCids.Contains(c.Cid) || slot.Cids.Contains(c.Cid))
                .Where(c => !slot.Cids.Contains(c.Cid))
                .Select(c => c.Name)
                .ToList();

            ImGui.BeginDisabled(availChars.Count == 0);
            ImGui.SetNextItemWidth(-1);
            if (_charCombo.Draw("##wlcharcombo", availChars, ref _charSelected)) {
                var found = Plugin.Config.Characters.FirstOrDefault(c => c.Name == _charSelected);
                if (found != null) {
                    slot.Cids.Add(found.Cid);
                    Plugin.Config.Save();
                    Plugin.IpcProvider.SyncConfiguration();
                }
                _charSelected = string.Empty;
            }
            ImGui.EndDisabled();

            if (ImGui.BeginTable("##wlchartbl", 3,
                    ImGuiTableFlags.RowBg | ImGuiTableFlags.ScrollY | ImGuiTableFlags.NoSavedSettings,
                    new Vector2(-1, 80f))) {
                ImGui.TableSetupColumn("#", ImGuiTableColumnFlags.WidthFixed, 22f);
                ImGui.TableSetupColumn("Name", ImGuiTableColumnFlags.WidthStretch);
                ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthFixed, delW);
                for (int i = 0; i < slot.Cids.Count; i++) {
                    var cn = Plugin.Config.Characters.FirstOrDefault(c => c.Cid == slot.Cids[i])?.Name
                             ?? slot.Cids[i].ToString("X16");
                    ImGui.TableNextRow();
                    ImGui.TableNextColumn(); ImGui.Text($"{i + 1}");
                    ImGui.TableNextColumn(); ImGui.Text(cn);
                    ImGui.TableNextColumn();
                    if (ImGuiUtil.DangerIconButton(FontAwesomeIcon.Trash, $"##wldc{i}", Language.DeleteInstructionTooltip)
                        && ImGui.GetIO().KeyCtrl)
                        delIdx = i;
                }
                ImGui.EndTable();
            }
            if (delIdx >= 0) {
                slot.Cids.RemoveAt(delIdx);
                Plugin.Config.Save();
                Plugin.IpcProvider.SyncConfiguration();
            }
        }

        // Groups
        if (ImGui.CollapsingHeader("Groups##wlgrps")) {
            int delIdx = -1;
            float delW = ImGui.GetFrameHeight();

            var assignedGroups = layout.Slots.SelectMany(s => s.GroupIds).ToHashSet();
            var availGroups = Plugin.Config.CidsGroups
                .Where(g => !assignedGroups.Contains(g.Name) || slot.GroupIds.Contains(g.Name))
                .Where(g => !slot.GroupIds.Contains(g.Name))
                .Select(g => g.Name)
                .ToList();

            ImGui.BeginDisabled(availGroups.Count == 0);
            ImGui.SetNextItemWidth(-1);
            if (_groupCombo.Draw("##wlgrpcombo", availGroups, ref _groupSelected)) {
                if (!string.IsNullOrEmpty(_groupSelected)) {
                    slot.GroupIds.Add(_groupSelected);
                    Plugin.Config.Save();
                    Plugin.IpcProvider.SyncConfiguration();
                }
                _groupSelected = string.Empty;
            }
            ImGui.EndDisabled();

            if (ImGui.BeginTable("##wlgrptbl", 3,
                    ImGuiTableFlags.RowBg | ImGuiTableFlags.ScrollY | ImGuiTableFlags.NoSavedSettings,
                    new Vector2(-1, 80f))) {
                ImGui.TableSetupColumn("#", ImGuiTableColumnFlags.WidthFixed, 22f);
                ImGui.TableSetupColumn("Name", ImGuiTableColumnFlags.WidthStretch);
                ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthFixed, delW);
                for (int i = 0; i < slot.GroupIds.Count; i++) {
                    var gn = slot.GroupIds[i];
                    ImGui.TableNextRow();
                    ImGui.TableNextColumn(); ImGui.Text($"{i + 1}");
                    ImGui.TableNextColumn(); ImGui.Text(gn);
                    ImGui.TableNextColumn();
                    if (ImGuiUtil.DangerIconButton(FontAwesomeIcon.Trash, $"##wldg{i}", Language.DeleteInstructionTooltip)
                        && ImGui.GetIO().KeyCtrl)
                        delIdx = i;
                }
                ImGui.EndTable();
            }
            if (delIdx >= 0) {
                slot.GroupIds.RemoveAt(delIdx);
                Plugin.Config.Save();
                Plugin.IpcProvider.SyncConfiguration();
            }
        }

        //  Quick apply selected slot
        // ImGui.Spacing();
        // ImGui.Separator();
        // ImGui.Spacing();
        // if (ImGuiUtil.SuccessButton($"Apply \"{layout.Name}\"##wlapply", new Vector2(-1, 0)))
        //     Plugin.IpcProvider.ApplyWindowLayout(layout.Name);
        // ImGuiUtil.ToolTip("Broadcast layout to all clients. Each client moves its own window to the assigned slot.");
    }
}
