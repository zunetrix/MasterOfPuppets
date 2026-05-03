using System;
using System.Linq;
using System.Numerics;

using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;

using MasterOfPuppets.Formations;
using MasterOfPuppets.Resources;
using MasterOfPuppets.Util.ImGuiExt;

namespace MasterOfPuppets;

public partial class FormationWindow {
    private void DrawRightPanel() {
        var formation = SelectedFormation;
        if (formation == null) { ImGui.TextDisabled("No formation selected"); return; }

        var pt2 = _selPoint >= 0 && _selPoint < formation.Points.Count
            ? formation.Points[_selPoint] : null;

        var precision = Plugin.Config.FormationMovePrecision;
        ImGui.SetNextItemWidth(-1);
        if (ImGui.DragFloat("Precision##fiprecision", ref precision, 0.01f, 0.01f, 5f, "%.2f")) {
            Plugin.Config.FormationMovePrecision = precision;
            Plugin.Config.Save();
            Plugin.IpcProvider.SyncConfiguration();
        }

        ImGui.Separator();

        //  Points list
        ImGui.SetNextItemOpen(true, ImGuiCond.Appearing);
        if (ImGui.CollapsingHeader($"Points ({formation.Points.Count})##fipts")) {
            if (formation.Points.Count == 0) {
                ImGui.TextDisabled("Shift+Click plot to add");
            } else {
                float btnW = ImGui.GetFrameHeight();
                float rowH = ImGui.GetFrameHeightWithSpacing();
                float headerH = ImGui.GetFrameHeightWithSpacing();
                float maxH = 8 * rowH + headerH;
                float tableH = Math.Min(formation.Points.Count * rowH + headerH, maxH);
                if (ImGui.BeginTable("##fipttbl", 6,
                    ImGuiTableFlags.RowBg | ImGuiTableFlags.NoSavedSettings | ImGuiTableFlags.BordersInnerV |
                    ImGuiTableFlags.ScrollY,
                    new Vector2(-1, tableH))) {
                    ImGui.TableSetupScrollFreeze(0, 1);
                    ImGui.TableSetupColumn("##drag", ImGuiTableColumnFlags.WidthFixed, 25f);
                    ImGui.TableSetupColumn("X", ImGuiTableColumnFlags.WidthStretch);
                    ImGui.TableSetupColumn("Z", ImGuiTableColumnFlags.WidthStretch);
                    ImGui.TableSetupColumn("A°", ImGuiTableColumnFlags.WidthFixed, 60f);
                    ImGui.TableSetupColumn("##sel", ImGuiTableColumnFlags.WidthFixed, btnW);
                    ImGui.TableSetupColumn("##del", ImGuiTableColumnFlags.WidthFixed, btnW);
                    ImGui.TableHeadersRow();
                    int delIdx = -1;
                    for (int i = 0; i < formation.Points.Count; i++) {
                        var p = formation.Points[i];
                        ImGui.PushID(i);
                        ImGui.TableNextRow();

                        // Column 0: drag handle + selection Selectable
                        ImGui.TableNextColumn();
                        using (ImRaii.PushColor(ImGuiCol.Header, Style.Components.ButtonBlueHovered)
                            .Push(ImGuiCol.HeaderHovered, Style.Components.ButtonBlueHovered)
                            .Push(ImGuiCol.HeaderActive, Style.Components.ButtonBlueHovered)) {
                            if (ImGui.Selectable($"{i + 1:00}##firow", i == _selPoint))
                                _selPoint = i;
                        }
                        ImGuiUtil.ToolTip("Drag to reorder");

                        if (ImGui.BeginDragDropSource()) {
                            unsafe {
                                ImGui.SetDragDropPayload("DND_FORMATION_POINT", new ReadOnlySpan<byte>(&i, sizeof(int)), ImGuiCond.None);
                                ImGui.Text($"#{i + 1}");
                            }
                            ImGui.EndDragDropSource();
                        }

                        using (ImRaii.PushColor(ImGuiCol.DragDropTarget, Style.Components.DragDropTarget)) {
                            if (ImGui.BeginDragDropTarget()) {
                                var payload = ImGui.AcceptDragDropPayload("DND_FORMATION_POINT");
                                bool isDropping = false;
                                unsafe { isDropping = !payload.IsNull; }
                                if (isDropping && payload.IsDelivery()) {
                                    unsafe {
                                        int from = *(int*)payload.Data;
                                        if (from != i) {
                                            var pt = formation.Points[from];
                                            formation.Points.RemoveAt(from);
                                            formation.Points.Insert(i, pt);
                                            if (_selPoint == from) _selPoint = i;
                                            Plugin.Config.Save();
                                            Plugin.IpcProvider.SyncConfiguration();
                                        }
                                    }
                                }
                                ImGui.EndDragDropTarget();
                            }
                        }

                        // Column 1: saved formation X (also plot X)
                        ImGui.TableNextColumn();
                        ImGui.SetNextItemWidth(-1);
                        var plotX = p.Offset.X;
                        if (ImGui.DragFloat("##x", ref plotX, 0.001f, -500f, 500f, "%.3f")) {
                            p.Offset.X = plotX;
                        }
                        if (ImGui.IsItemActivated()) {
                            _selPoint = i;
                        }
                        if (ImGui.IsItemDeactivatedAfterEdit()) {
                            Plugin.Config.Save();
                            Plugin.IpcProvider.SyncConfiguration();
                        }
                        if (ImGui.IsItemClicked(ImGuiMouseButton.Right)) {
                            p.Offset.X = 0;
                            Plugin.Config.Save();
                            Plugin.IpcProvider.SyncConfiguration();
                        }

                        // Column 2: saved formation Z
                        ImGui.TableNextColumn();
                        ImGui.SetNextItemWidth(-1);
                        ImGui.DragFloat("##z", ref p.Offset.Z, 0.001f, -500f, 500f, "%.3f");
                        if (ImGui.IsItemActivated()) {
                            _selPoint = i;
                        }
                        if (ImGui.IsItemDeactivatedAfterEdit()) {
                            Plugin.Config.Save();
                            Plugin.IpcProvider.SyncConfiguration();
                        }
                        if (ImGui.IsItemClicked(ImGuiMouseButton.Right)) {
                            p.Offset.Z = 0;
                            Plugin.Config.Save();
                            Plugin.IpcProvider.SyncConfiguration();
                        }

                        // Column 3: A°
                        ImGui.TableNextColumn();
                        ImGui.SetNextItemWidth(-1);
                        ImGui.DragFloat("##a", ref p.Angle, 1f, -180f, 180f, "%.0f\u00b0");
                        if (ImGui.IsItemActivated()) {
                            _selPoint = i;
                        }
                        if (ImGui.IsItemDeactivatedAfterEdit()) {
                            Plugin.Config.Save();
                            Plugin.IpcProvider.SyncConfiguration();
                        }
                        if (ImGui.IsItemClicked(ImGuiMouseButton.Right)) {
                            p.Angle = 0;
                            Plugin.Config.Save();
                            Plugin.IpcProvider.SyncConfiguration();
                        }

                        // Column 4: select
                        ImGui.TableNextColumn();
                        if (ImGuiUtil.IconButton(FontAwesomeIcon.Crosshairs, $"##fisel{i}", "Select point"))
                            _selPoint = i;

                        // Column 5: delete
                        ImGui.TableNextColumn();
                        if (ImGuiUtil.DangerIconButton(FontAwesomeIcon.Trash, $"##fidp{i}", Language.DeleteInstructionTooltip) && ImGui.GetIO().KeyCtrl)
                            delIdx = i;

                        ImGui.PopID();
                    }
                    ImGui.EndTable();
                    if (delIdx >= 0) {
                        formation.Points.RemoveAt(delIdx);
                        _selPoint = formation.Points.Count > 0
                            ? Math.Clamp(_selPoint, 0, formation.Points.Count - 1) : -1;
                        Plugin.Config.Save();
                        Plugin.IpcProvider.SyncConfiguration();
                    }
                }
            }
            ImGui.Spacing();
            if (ImGuiUtil.DangerButton("Clear All##ficlear", new Vector2(-1, 0)) && ImGui.GetIO().KeyCtrl) {
                formation.Points.Clear();
                _selPoint = -1;
                Plugin.Config.Save();
                Plugin.IpcProvider.SyncConfiguration();
            }
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("Ctrl+Click to clear all points");
        }

        //  Characters
        if (ImGui.CollapsingHeader("Characters##fichars")) {
            if (pt2 == null) {
                ImGui.TextDisabled("Select a point");
            } else {
                int delIdx = -1;
                float delW = ImGui.GetFrameHeight();

                // Exclude characters already assigned to ANY point in the formation
                var assignedCids = formation.Points.SelectMany(p => p.Cids).ToHashSet();
                var availChars = Plugin.Config.Characters
                    .Where(c => !assignedCids.Contains(c.Cid))
                    .Select(c => c.Name)
                    .ToList();

                ImGui.BeginDisabled(availChars.Count == 0);
                ImGui.SetNextItemWidth(-1);
                if (_charCombo.Draw("##ficharcombo", availChars, ref _charSelected)) {
                    var found = Plugin.Config.Characters.FirstOrDefault(c => c.Name == _charSelected);
                    if (found != null) {
                        pt2.Cids.Add(found.Cid);
                        Plugin.Config.Save();
                        Plugin.IpcProvider.SyncConfiguration();
                    }
                    _charSelected = string.Empty;
                }
                ImGui.EndDisabled();

                ImGui.Spacing();
                ImGui.Separator();
                ImGui.Spacing();

                if (ImGui.BeginTable("##fichartbl", 3,
                        ImGuiTableFlags.RowBg | ImGuiTableFlags.ScrollY | ImGuiTableFlags.NoSavedSettings,
                        new Vector2(-1, 80f))) {
                    ImGui.TableSetupColumn("#", ImGuiTableColumnFlags.WidthFixed, 22f);
                    ImGui.TableSetupColumn("Name", ImGuiTableColumnFlags.WidthStretch);
                    ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthFixed, delW);
                    for (int i = 0; i < pt2.Cids.Count; i++) {
                        var cn = Plugin.Config.Characters.FirstOrDefault(c => c.Cid == pt2.Cids[i])?.Name
                                 ?? pt2.Cids[i].ToString("X16");
                        ImGui.TableNextRow();
                        ImGui.TableNextColumn(); ImGui.Text($"{i + 1}");
                        ImGui.TableNextColumn(); ImGui.Text(cn);
                        ImGui.TableNextColumn();
                        if (ImGuiUtil.DangerIconButton(FontAwesomeIcon.Trash, $"##fidc{i}", Language.DeleteInstructionTooltip) && ImGui.GetIO().KeyCtrl)
                            delIdx = i;
                    }
                    ImGui.EndTable();
                }
                if (delIdx >= 0) {
                    pt2.Cids.RemoveAt(delIdx);
                    Plugin.Config.Save();
                    Plugin.IpcProvider.SyncConfiguration();
                }
            }
        }

        //  Groups
        if (ImGui.CollapsingHeader("Groups##figrps")) {
            if (pt2 == null) {
                ImGui.TextDisabled("Select a point");
            } else {
                int delIdx = -1;
                float delW = ImGui.GetFrameHeight();

                // Exclude groups already assigned to ANY point in the formation
                var assignedGroups = formation.Points.SelectMany(p => p.GroupIds).ToHashSet();
                var availGroups = Plugin.Config.CidsGroups
                    .Where(g => !assignedGroups.Contains(g.Name))
                    .Select(g => g.Name)
                    .ToList();

                ImGui.BeginDisabled(availGroups.Count == 0);
                float buttonWidth = ImGui.GetFrameHeight();
                float spacing = ImGui.GetStyle().ItemSpacing.X;
                ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X - buttonWidth - spacing);

                if (_groupCombo.Draw("##figrpcombo", availGroups, ref _groupSelected)) {
                    if (!string.IsNullOrEmpty(_groupSelected)) {
                        pt2.GroupIds.Add(_groupSelected);
                        Plugin.Config.Save();
                        Plugin.IpcProvider.SyncConfiguration();
                    }
                    _groupSelected = string.Empty;
                }
                ImGui.EndDisabled();

                ImGui.SameLine();
                if (ImGuiUtil.PrimaryIconButton(FontAwesomeIcon.Users, $"##CharactersMenu", "Characters/Groups")) {
                    Plugin.Ui.CharactersWindow.Toggle();
                }

                ImGui.Spacing();
                ImGui.Separator();
                ImGui.Spacing();

                if (ImGui.BeginTable("##figrptbl", 3,
                        ImGuiTableFlags.RowBg | ImGuiTableFlags.ScrollY | ImGuiTableFlags.NoSavedSettings,
                        new Vector2(-1, 80f))) {
                    ImGui.TableSetupColumn("#", ImGuiTableColumnFlags.WidthFixed, 22f);
                    ImGui.TableSetupColumn("Name", ImGuiTableColumnFlags.WidthStretch);
                    ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthFixed, delW);
                    for (int i = 0; i < pt2.GroupIds.Count; i++) {
                        var gn = Plugin.Config.CidsGroups.FirstOrDefault(g => g.Name == pt2.GroupIds[i])?.Name
                                 ?? $"Unknown ({pt2.GroupIds[i]})";
                        ImGui.TableNextRow();
                        ImGui.TableNextColumn(); ImGui.Text($"{i + 1}");
                        ImGui.TableNextColumn(); ImGui.Text(gn);
                        ImGui.TableNextColumn();
                        if (ImGuiUtil.DangerIconButton(FontAwesomeIcon.Trash, $"##fidg{i}", Language.DeleteInstructionTooltip) && ImGui.GetIO().KeyCtrl)
                            delIdx = i;
                    }
                    ImGui.EndTable();
                }
                if (delIdx >= 0) {
                    pt2.GroupIds.RemoveAt(delIdx);
                    Plugin.Config.Save();
                    Plugin.IpcProvider.SyncConfiguration();
                }
            }
        }
    }
}
