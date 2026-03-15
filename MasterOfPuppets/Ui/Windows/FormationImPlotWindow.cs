using System;
using System.Linq;
using System.Numerics;

using Dalamud.Bindings.ImGui;
using Dalamud.Bindings.ImPlot;
using Dalamud.Interface;
using Dalamud.Interface.Windowing;

using MasterOfPuppets.Formations;
using MasterOfPuppets.Movement;
using MasterOfPuppets.Util.ImGuiExt;

namespace MasterOfPuppets;

public class FormationImPlotWindow : Window, IDisposable {
    private Plugin Plugin { get; }
    private ImPlotContextPtr _plotCtx;

    //  Left panel
    private string _searchFilter = string.Empty;
    private int _selFormation = -1;
    private string _newFmName = string.Empty;

    //  Plot
    private int _selPoint = -1;

    //  Shape generator
    private static readonly string[] ShapeNames = ["Polygon", "Square"];
    private static readonly string[] FaceNames = ["Outward", "Inward", "North"];
    private int _shapeType;
    private int _shapeN = 8;
    private float _shapeRadius = 5f;
    private float _shapeAngleOff;
    private int _faceMode;
    private bool _appendMode;

    //  World overlay
    private bool _worldOverlay;
    private float _markerSizePlot = 36f;
    private float _markerSizeWorld = 12f;

    //  Right panel
    private float _rightPanelWidth = 300f;
    private readonly ImGuiComboSearch _charCombo = new();
    private readonly ImGuiComboSearch _groupCombo = new();
    private string _charSelected = string.Empty;
    private string _groupSelected = string.Empty;

    private Formation? SelectedFormation =>
        _selFormation >= 0 && _selFormation < Plugin.Config.Formations.Count
            ? Plugin.Config.Formations[_selFormation] : null;

    public FormationImPlotWindow(Plugin plugin) : base("Formations [ImPlot]###FormationsImPlotWindow") {
        Plugin = plugin;
        Size = new Vector2(960, 620);
        SizeCondition = ImGuiCond.FirstUseEver;

        ImPlot.SetImGuiContext(ImGui.GetCurrentContext());
        _plotCtx = ImPlot.CreateContext();
        ImPlot.SetCurrentContext(_plotCtx);
    }

    public void Dispose() {
        ImPlot.DestroyContext(_plotCtx);
    }

    public override void Draw() {
        ImPlot.SetCurrentContext(_plotCtx);

        const float leftW = 220f;
        const float splitterW = 6f;
        const float minRightW = 150f;
        var avail = ImGui.GetContentRegionAvail();
        float spacing = ImGui.GetStyle().ItemSpacing.X;
        _rightPanelWidth = MathF.Max(minRightW, MathF.Min(_rightPanelWidth, avail.X - leftW - splitterW - 80f));
        float midW = MathF.Max(avail.X - leftW - splitterW - _rightPanelWidth - spacing * 3, 100f);
        float h = avail.Y;

        ImGui.BeginChild("##fwil", new Vector2(leftW, h), true);
        DrawLeftPanel();
        ImGui.EndChild();

        ImGui.SameLine();

        ImGui.BeginChild("##fwim", new Vector2(midW, h), true);
        DrawMiddlePanel();
        ImGui.EndChild();

        ImGui.SameLine();

        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(0, 0));
        ImGui.InvisibleButton("##fwisplit", new Vector2(splitterW, h));
        if (ImGui.IsItemHovered()) ImGui.SetMouseCursor(ImGuiMouseCursor.ResizeEw);
        if (ImGui.IsItemActive() && ImGui.IsMouseDragging(ImGuiMouseButton.Left)) {
            _rightPanelWidth -= ImGui.GetIO().MouseDelta.X;
            _rightPanelWidth = MathF.Max(minRightW, MathF.Min(_rightPanelWidth, avail.X - leftW - splitterW - 80f));
        }
        ImGui.PopStyleVar();

        ImGui.SameLine();

        ImGui.BeginChild("##fwir", new Vector2(_rightPanelWidth, h), true);
        DrawRightPanel();
        ImGui.EndChild();
    }

    // =========================================================================
    // Left panel
    // =========================================================================

    private void DrawLeftPanel() {
        if (ImGuiUtil.IconButton(FontAwesomeIcon.Plus, "##fiadd_s", "New formation"))
            ImGui.OpenPopup("##finew");
        if (ImGui.BeginPopup("##finew")) {
            ImGui.Text("Name:");
            ImGui.SameLine();
            ImGui.SetNextItemWidth(150);
            bool enter = ImGui.InputText("##finewname", ref _newFmName, 64, ImGuiInputTextFlags.EnterReturnsTrue);
            if ((enter || ImGui.Button("Create")) && !string.IsNullOrWhiteSpace(_newFmName)) {
                AddFormation();
                ImGui.CloseCurrentPopup();
            }
            ImGui.EndPopup();
        }
        ImGui.SameLine();
        ImGui.SetNextItemWidth(-1);
        ImGui.InputText("##filsrch", ref _searchFilter, 64);

        var formations = Plugin.Config.Formations;
        ImGui.BeginChild("##fillist", new Vector2(-1, -1), false);
        for (int i = 0; i < formations.Count; i++) {
            var f = formations[i];
            if (!string.IsNullOrEmpty(_searchFilter) &&
                !f.Name.Contains(_searchFilter, StringComparison.OrdinalIgnoreCase))
                continue;

            if (ImGui.Selectable(f.Name.Length > 0 ? f.Name : "(unnamed)", i == _selFormation) &&
                _selFormation != i) {
                _selFormation = i;
                _selPoint = -1;
            }
        }
        ImGui.EndChild();
    }

    private void AddFormation() {
        if (string.IsNullOrWhiteSpace(_newFmName)) return;
        Plugin.Config.Formations.Add(new Formation { Name = _newFmName });
        _selFormation = Plugin.Config.Formations.Count - 1;
        _selPoint = -1;
        _newFmName = string.Empty;
        Plugin.Config.Save();
    }

    // =========================================================================
    // Middle panel – shape toolbar + ImPlot canvas
    // =========================================================================

    private void DrawMiddlePanel() {
        var formation = SelectedFormation;

        DrawShapeToolbar(formation);
        ImGui.SameLine();
        ImGui.Text("Pt:");
        ImGui.SameLine();
        ImGui.SetNextItemWidth(38);
        ImGui.DragFloat("##mszpi", ref _markerSizePlot, 0.5f, 5f, 80f, "%.0f");
        ImGui.SameLine();
        ImGui.Text("W:");
        ImGui.SameLine();
        ImGui.SetNextItemWidth(38);
        ImGui.DragFloat("##mszwi", ref _markerSizeWorld, 0.5f, 2f, 30f, "%.0f");
        ImGui.SameLine();
        if (ImGui.Button("From Party##fi") && formation != null)
            SnapshotParty(formation);
        ImGui.SameLine();
        ImGui.Checkbox("World##fiwov", ref _worldOverlay);

        if (formation == null) {
            ImGui.TextDisabled("Select a formation");
            return;
        }

        DrawPlot(formation);

        if (_worldOverlay) DrawWorldOverlay(formation);
    }

    private void DrawPlot(Formation formation) {
        // Set initial axis limits centered on origin with margin around the farthest point
        float limit = 8f;
        if (formation.Points.Count > 0) {
            float max = formation.Points.Max(p => MathF.Max(MathF.Abs(p.Offset.X), MathF.Abs(p.Offset.Z)));
            limit = MathF.Max(max + 3f, 8f);
        }

        ImPlot.SetNextAxisLimits(ImAxis.X1, -limit, limit, ImPlotCond.Once);
        ImPlot.SetNextAxisLimits(ImAxis.Y1, -limit, limit, ImPlotCond.Once);

        var plotSize = ImGui.GetContentRegionAvail();
        if (!ImPlot.BeginPlot("##fmipplot", plotSize,
                ImPlotFlags.Equal | ImPlotFlags.NoTitle | ImPlotFlags.NoLegend |
                ImPlotFlags.NoMenus | ImPlotFlags.NoBoxSelect))
            return;

        ImPlot.PushPlotClipRect();
        var dl = ImPlot.GetPlotDrawList();

        // Origin marker
        dl.AddCircle(ImPlot.PlotToPixels(0.0f, 0.0f), 4f, 0x88FFFFFF);

        bool anyDragged = false;
        int pointId = 0;

        for (int i = 0; i < formation.Points.Count; i++) {
            var pt = formation.Points[i];
            bool selected = i == _selPoint;
            uint color = selected ? 0xFFFFAA00u : 0xFF3377CCu;

            // Plot coords: X = game X (east), Y = −game Z (north = up)
            double x = pt.Offset.X;
            double y = -pt.Offset.Z;

            // Fixed-pixel oriented triangle
            var ptPx = ImPlot.PlotToPixels((float)x, (float)y);
            DrawTri(dl, ptPx, pt.Angle, color, _markerSizePlot);

            // Label centered on triangle in screen space, on foreground so DragPoint doesn't cover it
            var label = $"{i + 1}";
            var labelSz = ImGui.CalcTextSize(label);
            ImGui.GetForegroundDrawList().AddText(ptPx - labelSz * 0.5f, 0xFFFFFFFF, label);

            // Click detection before DragPoint so the click isn't consumed first
            if (ImPlot.IsPlotHovered() &&
                Vector2.Distance(ptPx, ImGui.GetMousePos()) < _markerSizePlot * 0.5f &&
                ImGui.IsMouseClicked(ImGuiMouseButton.Left)) {
                _selPoint = i;
                anyDragged = true; // prevent empty-area deselect this frame
            }

            // DragPoint for dragging; only commit offset when mouse actually moved
            if (ImPlot.DragPoint(pointId++, ref x, ref y, ImGui.ColorConvertU32ToFloat4(color))) {
                anyDragged = true;
                if (ImGui.GetMouseDragDelta(ImGuiMouseButton.Left).LengthSquared() > 16f) {
                    _selPoint = i;
                    pt.Offset.X = (float)x;
                    pt.Offset.Z = -(float)y;
                    if (ImGui.IsMouseReleased(ImGuiMouseButton.Left))
                        Plugin.Config.Save();
                }
            }
        }

        // Shift+Click on empty area → add point
        if (!anyDragged && ImPlot.IsPlotHovered() &&
                ImGui.IsMouseClicked(ImGuiMouseButton.Left) && ImGui.GetIO().KeyShift) {
            var mp = ImPlot.GetPlotMousePos();
            formation.Points.Add(new FormationPoint {
                Offset = new Vector3((float)mp.X, 0f, -(float)mp.Y),
            });
            _selPoint = formation.Points.Count - 1;
            Plugin.Config.Save();
        }

        // Click on empty area → deselect
        if (!anyDragged && ImPlot.IsPlotHovered() &&
                ImGui.IsMouseClicked(ImGuiMouseButton.Left) && !ImGui.GetIO().KeyShift) {
            _selPoint = -1;
        }

        ImPlot.PopPlotClipRect();
        ImPlot.EndPlot();
    }

    /// <summary>Fixed-pixel-size oriented triangle drawn at a screen-space center.</summary>
    private static void DrawTri(ImDrawListPtr dl, Vector2 center, float angleDeg, uint color, float r = 36f) {
        float rad = angleDeg * Angle.DegToRad;
        var fwd = new Vector2(MathF.Sin(rad), -MathF.Cos(rad));
        var right = new Vector2(MathF.Cos(rad), MathF.Sin(rad));

        var p0 = center + fwd * r;
        var p1 = center - fwd * 0.4f * r + right * 0.6f * r;
        var p2 = center - fwd * 0.4f * r - right * 0.6f * r;

        dl.AddTriangleFilled(p0, p1, p2, color);
        dl.AddTriangle(p0, p1, p2, 0xFFFFFFFF);
    }

    private void DrawShapeToolbar(Formation? formation) {
        ImGui.SetNextItemWidth(75);
        ImGui.Combo("##shtypei", ref _shapeType, ShapeNames, ShapeNames.Length);
        ImGui.SameLine();

        if (_shapeType == 0) {
            ImGui.Text("N:");
            ImGui.SameLine();
            ImGui.SetNextItemWidth(38);
            ImGui.DragInt("##shni", ref _shapeN, 0.1f, 2, 32);
            ImGui.SameLine();
        }

        ImGui.Text("R:");
        ImGui.SameLine();
        ImGui.SetNextItemWidth(46);
        ImGui.DragFloat("##shri", ref _shapeRadius, 0.1f, 0.5f, 50f, "%.1f");
        ImGui.SameLine();

        ImGui.Text("A\u00b0:");
        ImGui.SameLine();
        ImGui.SetNextItemWidth(46);
        ImGui.DragFloat("##shai", ref _shapeAngleOff, 1f, -180f, 180f, "%.0f");
        ImGui.SameLine();

        ImGui.SetNextItemWidth(68);
        ImGui.Combo("##shfacei", ref _faceMode, FaceNames, FaceNames.Length);
        ImGui.SameLine();

        ImGui.Checkbox("Ap##shappi", ref _appendMode);
        if (ImGui.IsItemHovered()) ImGui.SetTooltip("Append to existing points");
        ImGui.SameLine();

        if (ImGui.Button("Gen##i") && formation != null)
            GenerateShape(formation);
    }

    private void GenerateShape(Formation formation) {
        int n = _shapeType == 1 ? 4 : _shapeN;
        float baseR = _shapeAngleOff * Angle.DegToRad;
        float step = 2 * MathF.PI / n;

        if (!_appendMode) formation.Points.Clear();
        for (int i = 0; i < n; i++) {
            float a = baseR + i * step;
            float ang = _faceMode switch {
                1 => a * Angle.RadToDeg + 180f,
                2 => 0f,
                _ => a * Angle.RadToDeg,
            };
            formation.Points.Add(new FormationPoint {
                Offset = new Vector3(_shapeRadius * MathF.Sin(a), 0, _shapeRadius * MathF.Cos(a)),
                Angle = ang,
            });
        }
        _selPoint = -1;
        Plugin.Config.Save();
    }

    private void SnapshotParty(Formation formation) {
        var player = DalamudApi.ObjectTable.LocalPlayer;
        if (player == null) return;
        foreach (var member in DalamudApi.PartyList) {
            var obj = DalamudApi.ObjectTable.FirstOrDefault(o => o.EntityId == (uint)member.EntityId);
            if (obj == null) continue;
            var offset = obj.Position - player.Position;
            var pt = new FormationPoint { Offset = new Vector3(offset.X, 0, offset.Z) };
            if (member.ContentId != 0) pt.Cids.Add((ulong)member.ContentId);
            formation.Points.Add(pt);
        }
        _selPoint = -1;
        Plugin.Config.Save();
    }

    private void DrawWorldOverlay(Formation formation) {
        var player = DalamudApi.ObjectTable.LocalPlayer;
        if (player == null) return;

        var fgDl = ImGui.GetForegroundDrawList();
        var playerPos = player.Position;

        float cosR = MathF.Cos(player.Rotation);
        float sinR = MathF.Sin(player.Rotation);

        DalamudApi.GameGui.WorldToScreen(playerPos, out var originPx);
        var screenFwdFallback = DalamudApi.GameGui.WorldToScreen(
                new Vector3(playerPos.X + sinR, playerPos.Y, playerPos.Z + cosR), out var fwdOriginPx)
            ? Vector2.Normalize(fwdOriginPx - originPx)
            : new Vector2(0f, -1f);

        for (int i = 0; i < formation.Points.Count; i++) {
            var pt = formation.Points[i];

            var worldPos = playerPos + new Vector3(
                 pt.Offset.X * cosR + pt.Offset.Z * sinR,
                 pt.Offset.Y,
                -pt.Offset.X * sinR + pt.Offset.Z * cosR);
            if (!DalamudApi.GameGui.WorldToScreen(worldPos, out var screenPos)) continue;

            float A = pt.Angle * Angle.DegToRad;
            float sinA = MathF.Sin(A), cosA = MathF.Cos(A);
            var facingWorld = new Vector3(
                worldPos.X + sinA * cosR - cosA * sinR,
                worldPos.Y,
                worldPos.Z - sinA * sinR - cosA * cosR);
            var fwd = DalamudApi.GameGui.WorldToScreen(facingWorld, out var facingPx)
                ? Vector2.Normalize(screenPos - facingPx)
                : screenFwdFallback;
            var right = new Vector2(-fwd.Y, fwd.X);

            float r = _markerSizeWorld;
            uint c = i == _selPoint ? 0xFFFFAA00u : 0xFF3388FFu;
            var p0 = screenPos + fwd * r;
            var p1 = screenPos - fwd * 0.4f * r + right * 0.6f * r;
            var p2 = screenPos - fwd * 0.4f * r - right * 0.6f * r;
            fgDl.AddTriangleFilled(p0, p1, p2, c);
            fgDl.AddTriangle(p0, p1, p2, 0xFFFFFFFF);
            var label = (i + 1).ToString();
            fgDl.AddText(screenPos - ImGui.CalcTextSize(label) * 0.5f, 0xFFFFFFFF, label);
        }
    }

    // =========================================================================
    // Right panel
    // =========================================================================

    private void DrawRightPanel() {
        var formation = SelectedFormation;
        if (formation == null) { ImGui.TextDisabled("No formation selected"); return; }

        // ── Header: Execute | Delete | Name ──────────────────────────────
        if (ImGuiUtil.IconButton(FontAwesomeIcon.Play, "##execfmi", "Execute formation"))
            Plugin.IpcProvider.ExecuteFormation(formation.Name);
        ImGui.SameLine();
        if (ImGuiUtil.DangerIconButton(FontAwesomeIcon.Trash, "##fidelfm", "Delete formation (Ctrl+Click)") && ImGui.GetIO().KeyCtrl) {
            Plugin.Config.Formations.RemoveAt(_selFormation);
            _selFormation = Plugin.Config.Formations.Count > 0
                ? Math.Clamp(_selFormation, 0, Plugin.Config.Formations.Count - 1) : -1;
            _selPoint = -1;
            Plugin.Config.Save();
            return;
        }
        ImGui.SameLine();
        ImGui.SetNextItemWidth(-1);
        var name = formation.Name;
        if (ImGui.InputText("##finame", ref name, 64)) formation.Name = name;
        if (ImGui.IsItemDeactivatedAfterEdit()) Plugin.Config.Save();

        ImGui.Spacing();

        var pt2 = _selPoint >= 0 && _selPoint < formation.Points.Count
            ? formation.Points[_selPoint] : null;

        // ── Points list ──────────────────────────────────────────────────
        ImGui.SetNextItemOpen(true, ImGuiCond.Once);
        if (ImGui.CollapsingHeader($"Points ({formation.Points.Count})##fipts")) {
            if (formation.Points.Count == 0) {
                ImGui.TextDisabled("Shift+Click plot to add");
            } else {
                if (ImGui.BeginTable("##fipttbl", 4,
                        ImGuiTableFlags.RowBg | ImGuiTableFlags.ScrollY |
                        ImGuiTableFlags.NoSavedSettings | ImGuiTableFlags.BordersInnerV,
                        new Vector2(-1, 120f))) {
                    ImGui.TableSetupScrollFreeze(0, 1);
                    ImGui.TableSetupColumn("#", ImGuiTableColumnFlags.WidthFixed, 22f);
                    ImGui.TableSetupColumn("X", ImGuiTableColumnFlags.WidthStretch);
                    ImGui.TableSetupColumn("Z", ImGuiTableColumnFlags.WidthStretch);
                    ImGui.TableSetupColumn("A°", ImGuiTableColumnFlags.WidthFixed, 40f);
                    ImGui.TableHeadersRow();
                    for (int i = 0; i < formation.Points.Count; i++) {
                        var p = formation.Points[i];
                        ImGui.TableNextRow();
                        ImGui.TableNextColumn();
                        if (ImGui.Selectable($"##firow{i}", i == _selPoint, ImGuiSelectableFlags.SpanAllColumns))
                            _selPoint = i;
                        ImGui.SameLine(0, 4);
                        ImGui.Text($"{i + 1}");
                        ImGui.TableNextColumn(); ImGui.Text($"{p.Offset.X:F3}");
                        ImGui.TableNextColumn(); ImGui.Text($"{p.Offset.Z:F3}");
                        ImGui.TableNextColumn(); ImGui.Text($"{p.Angle:F0}°");
                    }
                    ImGui.EndTable();
                }
            }
            ImGui.Spacing();
            if (ImGui.Button("Clear All##ficlear", new Vector2(-1, 0)) && ImGui.GetIO().KeyCtrl) {
                formation.Points.Clear();
                _selPoint = -1;
                Plugin.Config.Save();
            }
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("Ctrl+Click to clear all points");
        }

        // ── Point Editor ─────────────────────────────────────────────────
        ImGui.SetNextItemOpen(true, ImGuiCond.Once);
        if (ImGui.CollapsingHeader("Point Editor##fiptedit")) {
            if (pt2 == null) {
                ImGui.TextDisabled("Select a point");
            } else {
                ImGui.Text($"#{_selPoint + 1} / {formation.Points.Count}");
                ImGui.SameLine();
                if (ImGuiUtil.DangerIconButton(FontAwesomeIcon.Trash, "##fidelpt", "Delete point (Ctrl+Click)") && ImGui.GetIO().KeyCtrl) {
                    formation.Points.RemoveAt(_selPoint);
                    _selPoint = formation.Points.Count > 0
                        ? Math.Clamp(_selPoint, 0, formation.Points.Count - 1) : -1;
                    Plugin.Config.Save();
                }
                ImGui.Text("X:"); ImGui.SameLine(); ImGui.SetNextItemWidth(-1);
                ImGui.DragFloat("##iptx", ref pt2.Offset.X, 0.001f, -500f, 500f, "%.3f");
                if (ImGui.IsItemDeactivatedAfterEdit()) Plugin.Config.Save();
                ImGui.Text("Z:"); ImGui.SameLine(); ImGui.SetNextItemWidth(-1);
                ImGui.DragFloat("##iptz", ref pt2.Offset.Z, 0.001f, -500f, 500f, "%.3f");
                if (ImGui.IsItemDeactivatedAfterEdit()) Plugin.Config.Save();
                ImGui.Text("A\u00b0:"); ImGui.SameLine(); ImGui.SetNextItemWidth(-1);
                ImGui.DragFloat("##ipta", ref pt2.Angle, 1f, -360f, 360f, "%.1f\u00b0");
                if (ImGui.IsItemDeactivatedAfterEdit()) Plugin.Config.Save();
            }
        }

        // ── Characters ───────────────────────────────────────────────────
        if (ImGui.CollapsingHeader("Characters##fichars")) {
            if (pt2 == null) {
                ImGui.TextDisabled("Select a point");
            } else {
                int delIdx = -1;
                float delW = ImGui.GetFrameHeight();
                var availChars = Plugin.Config.Characters
                    .Where(c => !pt2.Cids.Contains(c.Cid)).Select(c => c.Name).ToList();
                ImGui.SetNextItemWidth(-1);
                if (_charCombo.Draw("##ficharcombo", availChars, ref _charSelected)) {
                    var found = Plugin.Config.Characters.FirstOrDefault(c => c.Name == _charSelected);
                    if (found != null) { pt2.Cids.Add(found.Cid); Plugin.Config.Save(); }
                    _charSelected = string.Empty;
                }
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
                        ImGui.TableNextColumn(); ImGui.TextUnformatted(cn);
                        ImGui.TableNextColumn();
                        if (ImGuiUtil.DangerIconButton(FontAwesomeIcon.Trash, $"##fidc{i}", "Remove")) delIdx = i;
                    }
                    ImGui.EndTable();
                }
                if (delIdx >= 0) { pt2.Cids.RemoveAt(delIdx); Plugin.Config.Save(); }
            }
        }

        // ── Groups ───────────────────────────────────────────────────────
        if (ImGui.CollapsingHeader("Groups##figrps")) {
            if (pt2 == null) {
                ImGui.TextDisabled("Select a point");
            } else {
                int delIdx = -1;
                float delW = ImGui.GetFrameHeight();
                var availGroups = Plugin.Config.CidsGroups
                    .Where(g => !pt2.GroupIds.Contains(g.Name)).Select(g => g.Name).ToList();
                ImGui.SetNextItemWidth(-1);
                if (_groupCombo.Draw("##figrpcombo", availGroups, ref _groupSelected)) {
                    if (!string.IsNullOrEmpty(_groupSelected)) { pt2.GroupIds.Add(_groupSelected); Plugin.Config.Save(); }
                    _groupSelected = string.Empty;
                }
                if (ImGui.BeginTable("##figrptbl", 3,
                        ImGuiTableFlags.RowBg | ImGuiTableFlags.ScrollY | ImGuiTableFlags.NoSavedSettings,
                        new Vector2(-1, 80f))) {
                    ImGui.TableSetupColumn("#", ImGuiTableColumnFlags.WidthFixed, 22f);
                    ImGui.TableSetupColumn("Name", ImGuiTableColumnFlags.WidthStretch);
                    ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthFixed, delW);
                    for (int i = 0; i < pt2.GroupIds.Count; i++) {
                        ImGui.TableNextRow();
                        ImGui.TableNextColumn(); ImGui.Text($"{i + 1}");
                        ImGui.TableNextColumn(); ImGui.TextUnformatted(pt2.GroupIds[i]);
                        ImGui.TableNextColumn();
                        if (ImGuiUtil.DangerIconButton(FontAwesomeIcon.Trash, $"##fidg{i}", "Remove")) delIdx = i;
                    }
                    ImGui.EndTable();
                }
                if (delIdx >= 0) { pt2.GroupIds.RemoveAt(delIdx); Plugin.Config.Save(); }
            }
        }
    }
}
