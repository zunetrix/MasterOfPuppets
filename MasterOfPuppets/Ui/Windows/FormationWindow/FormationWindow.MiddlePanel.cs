using System;
using System.Linq;
using System.Numerics;

using Dalamud.Bindings.ImGui;
using Dalamud.Bindings.ImPlot;
using Dalamud.Game.ClientState.Objects.Types;

using MasterOfPuppets.Extensions;
using MasterOfPuppets.Extensions.Dalamud;
using MasterOfPuppets.Formations;
using MasterOfPuppets.Movement;
using MasterOfPuppets.Util.ImGuiExt;

namespace MasterOfPuppets;

public partial class FormationWindow {
    private void DrawMiddlePanel() {
        var formation = SelectedFormation;

        DrawShapeToolbar(formation);
        ImGui.SameLine();
        ImGui.Text("Size:");
        ImGui.SameLine();
        ImGui.SetNextItemWidth(46);
        ImGui.DragFloat("##mszpi", ref _arrowSize, 0.01f, 0.1f, 3f, "%.2f");
        if (ImGui.IsItemHovered()) ImGui.SetTooltip("Arrow size in plot units (scales with zoom)");
        ImGui.SameLine();
        ImGui.Text("W:");
        ImGui.SameLine();
        ImGui.SetNextItemWidth(38);
        ImGui.DragFloat("##mszwi", ref _markerSizeWorld, 0.05f, 0.2f, 5f, "%.1f");
        if (ImGui.IsItemHovered()) ImGui.SetTooltip("World arrow size in meters");
        ImGui.SameLine();
        if (ImGui.Button("From Party##fi") && formation != null)
            SnapshotParty(formation);
        ImGui.SameLine();
        ImGui.Checkbox("World Preview##fiwov", ref _worldOverlay);

        if (formation == null) {
            ImGui.TextDisabled("Select a formation");
            return;
        }

        DrawPlot(formation);

        if (_worldOverlay) DrawWorldOverlay(formation);
    }

    private void DrawPlot(Formation formation) {
        float limit = 8f;
        if (formation.Points.Count > 0) {
            float max = formation.Points.Max(p => MathF.Max(MathF.Abs(p.Offset.X), MathF.Abs(p.Offset.Z)));
            if (float.IsFinite(max)) limit = MathF.Max(max + 3f, 8f);
        }

        // Always set axis limits - ImPlotCond.Once only applies once per plot lifetime,
        // so it respects user zoom after the first frame but resets when formation changes
        ImPlot.SetNextAxisLimits(ImAxis.X1, -limit, limit, ImPlotCond.Once);
        ImPlot.SetNextAxisLimits(ImAxis.Y1, -limit, limit, ImPlotCond.Once);

        var plotSize = ImGui.GetContentRegionAvail();
        if (!ImPlot.BeginPlot("##fmipplot", plotSize,
                ImPlotFlags.Equal | ImPlotFlags.NoTitle | ImPlotFlags.NoLegend |
                ImPlotFlags.NoMenus | ImPlotFlags.NoBoxSelect))
            return;

        ImPlot.PushPlotClipRect();
        var dl = ImPlot.GetPlotDrawList();

        dl.AddCircle(ImPlot.PlotToPixels(0.0f, 0.0f), 4f, 0x88FFFFFF);
        DrawNorthMarker(dl, limit);

        bool anyDragged = false;
        int pointId = 0;
        bool keyAlt = ImGui.GetIO().KeyAlt;
        bool keyCtrl = ImGui.GetIO().KeyCtrl;
        bool keyShift = ImGui.GetIO().KeyShift;

        for (int i = 0; i < formation.Points.Count; i++) {
            var pt = formation.Points[i];
            bool selected = i == _selPoint;
            uint color = selected ? 0xFFFFAA00u : 0xFF3377CCu;

            // Use OffsetToPlot for consistent coordinate mapping
            var plot = pt.Offset.OffsetToPlot();
            double x = plot.X;
            double y = plot.Y;

            DrawArrow(dl, (float)x, (float)y, pt.Angle, color, _arrowSize);

            var ptPx = ImPlot.PlotToPixels((float)x, (float)y);
            var label = $"{i + 1}";
            var labelSz = ImGui.CalcTextSize(label);
            ImGui.GetForegroundDrawList().AddText(ptPx - labelSz * 0.5f, 0xFFFFFFFF, label);

            var edgePx = ImPlot.PlotToPixels((float)x + _arrowSize * 0.5f, (float)y);
            float clickR = MathF.Max(Vector2.Distance(ptPx, edgePx), 8f);

            if (ImPlot.IsPlotHovered() &&
                Vector2.Distance(ptPx, ImGui.GetMousePos()) < clickR &&
                ImGui.IsMouseClicked(ImGuiMouseButton.Left)) {
                _selPoint = i;
                anyDragged = true;
            }

            double origX = x, origY = y;

            if (ImPlot.DragPoint(pointId++, ref x, ref y, ImGui.ColorConvertU32ToFloat4(color))) {
                anyDragged = true;
                _selPoint = i;

                if (keyAlt) {
                    x = origX;
                    y = origY;
                    pt.Angle = FormationMath.NormalizeDegrees(pt.Angle + ImGui.GetIO().MouseDelta.X);
                    if (ImGui.IsMouseReleased(ImGuiMouseButton.Left)) {
                        Plugin.Config.Save();
                        Plugin.IpcProvider.SyncConfiguration();
                    }
                } else {
                    if (keyCtrl) {
                        x = Math.Round(x, MidpointRounding.AwayFromZero);
                        y = Math.Round(y, MidpointRounding.AwayFromZero);
                    } else if (keyShift) {
                        x = Math.Round(x * 4, MidpointRounding.AwayFromZero) / 4;
                        y = Math.Round(y * 4, MidpointRounding.AwayFromZero) / 4;
                    }

                    // Always update offset while dragging so preview stays in sync
                    pt.Offset = new Vector2((float)x, (float)y).PlotToOffset();

                    if (ImGui.IsMouseReleased(ImGuiMouseButton.Left)) {
                        Plugin.Config.Save();
                        Plugin.IpcProvider.SyncConfiguration();
                    }
                }
            }
        }

        if (!anyDragged && ImPlot.IsPlotHovered() &&
                ImGui.IsMouseClicked(ImGuiMouseButton.Left) && keyShift) {
            var mp = ImPlot.GetPlotMousePos();
            formation.Points.Add(new FormationPoint {
                // Use PlotToOffset for new points added via click
                Offset = new Vector2((float)mp.X, (float)mp.Y).PlotToOffset(),
            });
            _selPoint = formation.Points.Count - 1;
            Plugin.Config.Save();
            Plugin.IpcProvider.SyncConfiguration();
        }

        if (!anyDragged && ImPlot.IsPlotHovered() &&
                ImGui.IsMouseClicked(ImGuiMouseButton.Left) && !keyShift) {
            _selPoint = -1;
        }

        ImPlot.PopPlotClipRect();
        ImPlot.EndPlot();
    }

    private static void DrawArrow(ImDrawListPtr dl, float px, float py, float angleDeg, uint color, float size = 0.5f) {
        var mat = Matrix3x2.CreateRotation(angleDeg * Angle.DegToRad);
        float h = size * 0.5f;

        var pts = new Vector2[4];
        for (int i = 0; i < 4; i++) {
            var rotated = Vector2.Transform(ArrowVertices2D[i] * h, mat) + new Vector2(px, py);
            pts[i] = ImPlot.PlotToPixels(rotated.X, rotated.Y);
        }

        dl.AddPolyline(ref pts[0], 4, color, ImDrawFlags.Closed, 1.5f);
    }

    private static void DrawNorthMarker(ImDrawListPtr dl, float limit) {
        var top = ImPlot.PlotToPixels(0f, limit * 0.86f);
        var bottom = ImPlot.PlotToPixels(0f, limit * 0.72f);
        var left = ImPlot.PlotToPixels(-limit * 0.035f, limit * 0.79f);
        var right = ImPlot.PlotToPixels(limit * 0.035f, limit * 0.79f);
        var label = "N";
        var labelSize = ImGui.CalcTextSize(label);

        dl.AddLine(bottom, top, 0x99FFFFFF, 1.5f);
        dl.AddLine(top, left, 0x99FFFFFF, 1.5f);
        dl.AddLine(top, right, 0x99FFFFFF, 1.5f);
        dl.AddText(top - new Vector2(labelSize.X * 0.5f, labelSize.Y + 4f), 0xCCFFFFFF, label);
    }

    private void DrawShapeToolbar(Formation? formation) {
        if (ImGui.Button("Generate Shape...##fi")) {
            ImGui.OpenPopup("Generate Shape##FormationShapeGenerator");
        }

        DrawShapeGeneratorModal(formation);
    }

    private static void ShowParam(string label, ref float val, string? tooltip = null) {
        ImGui.Text(label);
        ImGui.SameLine();
        ImGui.SetNextItemWidth(40);
        ImGui.DragFloat($"##shp_{label}", ref val, 0.1f, 0f, 100f, "%.1f");
        if (tooltip != null && ImGui.IsItemHovered()) ImGui.SetTooltip(tooltip);
    }

    private void DrawShapeGeneratorModal(Formation? formation) {
        ImGui.SetNextWindowSize(new Vector2(460f, 0f), ImGuiCond.FirstUseEver);
        if (!ImGui.BeginPopupModal("Generate Shape##FormationShapeGenerator", ImGuiWindowFlags.AlwaysAutoResize))
            return;

        ImGui.Text("Shape");
        ImGui.SameLine();
        ImGui.SetNextItemWidth(240);
        int shapeTypeInt = (int)_shapeType;
        if (ImGui.Combo("##shapeType", ref shapeTypeInt, FormationShapeGenerator.ShapeNames, FormationShapeGenerator.ShapeNames.Length)) {
            _shapeType = (FormationShapeType)shapeTypeInt;
            if (_shapeN < 1) _shapeN = 8;
            if (_shapeRadius < 0.1f) _shapeRadius = 5f;
        }

        ImGui.Text("Point count");
        ImGui.SameLine();
        ImGui.SetNextItemWidth(110);
        ImGui.DragInt("##shapePointCount", ref _shapeN, 0.2f, 1, 64);

        ImGui.Text("Anchor mode");
        ImGuiUtil.HelpMarker("""
            Shape only: every generated point lies on the selected shape.
            Anchor at center: point 1, or the first character in the selected group, is placed at the center. Remaining characters form the shape around it.
            """);
        ImGui.SameLine();
        ImGui.SetNextItemWidth(180);
        ImGui.Combo("##shapeAnchorMode", ref _shapeAnchorMode, FormationShapeGenerator.AnchorModeNames, FormationShapeGenerator.AnchorModeNames.Length);

        DrawShapeAssignmentControls();

        ImGui.Separator();
        DrawShapeParameterControls();

        ImGui.Separator();
        ImGui.Text("Rotation offset");
        ImGui.SameLine();
        ImGui.SetNextItemWidth(110);
        ImGui.DragFloat("##shapeAngleOffset", ref _shapeAngleOff, 1f, -180f, 180f, "%.0f deg");

        ImGui.Text("Facing");
        ImGui.SameLine();
        ImGui.SetNextItemWidth(160);
        ImGui.Combo("##shapeFaceMode", ref _faceMode, FormationShapeGenerator.FaceModeNames, FormationShapeGenerator.FaceModeNames.Length);

        ImGui.Checkbox("Append to existing points##shapeAppend", ref _appendMode);

        ImGui.Separator();
        bool disabled = formation == null;
        if (disabled) ImGui.BeginDisabled();
        if (ImGui.Button("Generate##shapeGenerate", new Vector2(120, 0)) && formation != null) {
            GenerateShape(formation);
            ImGui.CloseCurrentPopup();
        }
        if (disabled) ImGui.EndDisabled();

        ImGui.SameLine();
        if (ImGui.Button("Cancel##shapeCancel", new Vector2(100, 0))) {
            ImGui.CloseCurrentPopup();
        }

        ImGui.EndPopup();
    }

    private void DrawShapeAssignmentControls() {
        var groupNames = Plugin.Config.CidsGroups.Select(g => g.Name).ToList();
        if (!string.IsNullOrWhiteSpace(_shapeAssignGroupSelected)
            && Plugin.Config.CidsGroups.All(g => !g.Name.Equals(_shapeAssignGroupSelected, StringComparison.OrdinalIgnoreCase)))
            _shapeAssignGroupSelected = string.Empty;

        ImGui.Text("Assign from group");
        ImGui.SameLine();
        ImGui.SetNextItemWidth(240);
        ImGui.BeginDisabled(groupNames.Count == 0);
        if (_shapeGroupCombo.Draw("##shapeAssignGroup", groupNames, ref _shapeAssignGroupSelected)) {
            var group = GetShapeAssignmentGroup();
            if (group != null)
                _shapeN = Math.Clamp(group.Cids.Count, 1, 64);
        }
        ImGui.EndDisabled();

        ImGui.SameLine();
        ImGui.BeginDisabled(string.IsNullOrWhiteSpace(_shapeAssignGroupSelected));
        if (ImGui.Button("Clear##shapeAssignGroupClear"))
            _shapeAssignGroupSelected = string.Empty;
        ImGui.EndDisabled();

        var selectedGroup = GetShapeAssignmentGroup();
        if (selectedGroup != null) {
            var assigned = Math.Min(_shapeN, selectedGroup.Cids.Count);
            var centerText = _shapeAnchorMode == (int)FormationShapeAnchorMode.AnchorAtCenter && assigned > 0
                ? " First group character becomes the center point."
                : string.Empty;
            ImGui.TextDisabled($"Will assign {assigned} of {selectedGroup.Cids.Count} group character(s) directly.{centerText}");
        } else if (groupNames.Count == 0) {
            ImGui.TextDisabled("No character groups available.");
        } else {
            ImGui.TextDisabled("Generated points will be unassigned.");
        }
    }

    private void DrawShapeParameterControls() {
        switch (_shapeType) {
            case FormationShapeType.Circle:
            case FormationShapeType.FigureEight:
            case FormationShapeType.Heart:
            case FormationShapeType.Arc:
            case FormationShapeType.Lissajous:
                ShowModalFloat("Radius / scale", ref _shapeRadius);
                break;
            case FormationShapeType.Rectangle:
                ShowModalFloat("Width", ref _shapeWidth);
                ShowModalFloat("Depth", ref _shapeDepth);
                break;
            case FormationShapeType.Line:
            case FormationShapeType.Chevron:
                ShowModalFloat("Spacing", ref _shapeSpacing);
                break;
            case FormationShapeType.Cross:
                ShowModalFloat("Spacing", ref _shapeSpacing);
                ShowModalFloat("Arm length", ref _shapeWidth);
                break;
            case FormationShapeType.StaggeredLine:
            case FormationShapeType.Zigzag:
                ShowModalFloat("Step spacing", ref _shapeSpacing);
                ShowModalFloat("Amplitude / depth", ref _shapeRadius);
                break;
            case FormationShapeType.Spiral:
                ShowModalFloat("Radial step", ref _shapeRadius);
                ShowModalFloat("Rotations", ref _shapeRadius2);
                break;
            case FormationShapeType.LogarithmicSpiral:
                ShowModalFloat("A parameter", ref _shapeRadius);
                ShowModalFloat("B parameter", ref _shapeRadius2);
                break;
            case FormationShapeType.Polygon:
            case FormationShapeType.StarPolygon:
                ShowModalFloat("Radius", ref _shapeRadius);
                ShowModalInt("Sides", ref _shapeParamInt, 3, 32);
                break;
            case FormationShapeType.Rose:
                ShowModalFloat("Radius", ref _shapeRadius);
                ShowModalInt("Petals", ref _shapeParamInt, 1, 32);
                break;
            case FormationShapeType.Star:
                ShowModalFloat("Outer radius", ref _shapeRadius);
                ShowModalFloat("Inner radius", ref _shapeRadius2);
                ShowModalInt("Points", ref _shapeParamInt, 2, 32);
                break;
            case FormationShapeType.SpokedWheel:
                ShowModalFloat("Outer radius", ref _shapeRadius);
                ShowModalFloat("Inner radius", ref _shapeRadius2);
                ShowModalInt("Spokes", ref _shapeParamInt, 1, 32);
                break;
            case FormationShapeType.Ellipse:
                ShowModalFloat("Radius X", ref _shapeRadius);
                ShowModalFloat("Radius Z", ref _shapeRadius2);
                break;
            case FormationShapeType.SineWave:
                ShowModalFloat("Amplitude", ref _shapeRadius);
                ShowModalFloat("Wavelength", ref _shapeRadius2);
                ShowModalFloat("Total length", ref _shapeWidth);
                break;
            case FormationShapeType.Grid:
                ShowModalFloat("X spacing", ref _shapeSpacing);
                ShowModalFloat("Z spacing", ref _shapeWidth);
                ShowModalInt("Columns", ref _shapeParamInt, 1, 32);
                break;
            case FormationShapeType.Hypotrochoid:
                ShowModalFloat("R", ref _shapeRadius);
                ShowModalFloat("r", ref _shapeRadius2);
                ShowModalFloat("d", ref _shapeWidth);
                ShowModalFloat("Rotations", ref _shapeDepth);
                break;
            case FormationShapeType.RingWithCenter:
                ShowModalFloat("Radius", ref _shapeRadius);
                ShowModalInt("Center points", ref _shapeParamInt, 1, 16);
                break;
        }
    }

    private static void ShowModalFloat(string label, ref float value) {
        ImGui.Text(label);
        ImGui.SameLine(150);
        ImGui.SetNextItemWidth(140);
        ImGui.DragFloat($"##shape_{label}", ref value, 0.1f, 0f, 100f, "%.1f");
    }

    private static void ShowModalInt(string label, ref int value, int min, int max) {
        ImGui.Text(label);
        ImGui.SameLine(150);
        ImGui.SetNextItemWidth(140);
        ImGui.DragInt($"##shape_{label}", ref value, 0.1f, min, max);
    }

    private void SnapshotParty(Formation formation) {
        var player = DalamudApi.ObjectTable.LocalPlayer;
        if (player == null) return;

        // Find the party leader's GameObject to use as origin
        IGameObject? leaderObj = null;
        var leaderMember = DalamudApi.PartyList.FirstOrDefault(m => m.IsPartyLeader());
        if (leaderMember != null)
            leaderObj = DalamudApi.ObjectTable.FirstOrDefault(o => o.EntityId == leaderMember.EntityId);

        var originPos = leaderObj?.Position ?? player.Position;
        var originRot = leaderObj?.Rotation ?? player.Rotation;

        if (!_appendMode) formation.Points.Clear();

        foreach (var member in DalamudApi.PartyList) {
            var obj = DalamudApi.ObjectTable.FirstOrDefault(o => o.EntityId == member.EntityId);
            if (obj == null) continue;

            var (offset, angleDegrees) = FormationMath.ToMopRelative(obj.Position, obj.Rotation, originPos, originRot);

            var point = new FormationPoint {
                Offset = new Vector3(offset.X, 0, offset.Z),
                Angle = angleDegrees,
            };

            if (member.ContentId != 0)
                point.Cids.Add((ulong)member.ContentId);

            formation.Points.Add(point);
        }

        _selPoint = -1;
        Plugin.Config.Save();
        Plugin.IpcProvider.SyncConfiguration();
    }
}
