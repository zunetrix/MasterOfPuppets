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
        var plotRotation = (180f - angleDeg) * Angle.DegToRad;
        var mat = Matrix3x2.CreateRotation(plotRotation);
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
        ImGui.SetNextItemWidth(110);
        int shapeTypeInt = (int)_shapeType;
        if (ImGui.Combo("##shtypei", ref shapeTypeInt, ShapeNames, ShapeNames.Length)) {
            _shapeType = (ShapeType)shapeTypeInt;
            // Reset some defaults when changing types to avoid weirdness
            if (_shapeN < 1) _shapeN = 8;
            if (_shapeRadius < 0.1f) _shapeRadius = 5f;
        }

        ImGui.SameLine();
        ImGui.Text("N:");
        ImGui.SameLine();
        ImGui.SetNextItemWidth(38);
        ImGui.DragInt("##shni", ref _shapeN, 0.2f, 1, 64);
        if (ImGui.IsItemHovered()) ImGui.SetTooltip("Total number of points");

        ImGui.SameLine();
        // Context-sensitive parameters
        switch (_shapeType) {
            case ShapeType.Circle:
            case ShapeType.FigureEight:
            case ShapeType.Heart:
            case ShapeType.Arc:
            case ShapeType.Lissajous:
                ShowParam("R:", ref _shapeRadius, "Radius / Scale");
                break;
            case ShapeType.Rectangle:
                ShowParam("W:", ref _shapeWidth, "Width");
                ImGui.SameLine();
                ShowParam("D:", ref _shapeDepth, "Depth");
                break;
            case ShapeType.Line:
            case ShapeType.Chevron:
            case ShapeType.Cross:
                ShowParam("Sp:", ref _shapeSpacing, "Spacing");
                if (_shapeType == ShapeType.Cross) { ImGui.SameLine(); ShowParam("Len:", ref _shapeWidth, "Arm Length"); }
                break;
            case ShapeType.StaggeredLine:
            case ShapeType.Zigzag:
                ShowParam("Sp:", ref _shapeSpacing, "Step Spacing");
                ImGui.SameLine();
                ShowParam("Amp:", ref _shapeRadius, "Amplitude / Depth Offset");
                break;
            case ShapeType.Spiral:
            case ShapeType.LogarithmicSpiral:
                ShowParam("St:", ref _shapeRadius, _shapeType == ShapeType.Spiral ? "Radial Step" : "a-parameter");
                ImGui.SameLine();
                ShowParam("Rot:", ref _shapeRadius2, "Rotations (b-param for Log)");
                break;
            case ShapeType.Polygon:
            case ShapeType.Rose:
            case ShapeType.StarPolygon:
                ShowParam("R:", ref _shapeRadius, "Radius");
                ImGui.SameLine();
                ImGui.Text(_shapeType == ShapeType.Rose ? "Pet:" : "Sides:");
                ImGui.SameLine();
                ImGui.SetNextItemWidth(30);
                ImGui.DragInt("##shpint", ref _shapeParamInt, 0.1f, 1, 32);
                break;
            case ShapeType.Star:
            case ShapeType.SpokedWheel:
                ShowParam("R1:", ref _shapeRadius, "Outer Radius");
                ImGui.SameLine();
                ShowParam("R2:", ref _shapeRadius2, "Inner Radius");
                ImGui.SameLine();
                ImGui.Text(_shapeType == ShapeType.Star ? "Pts:" : "Spoke:");
                ImGui.SameLine();
                ImGui.SetNextItemWidth(30);
                ImGui.DragInt("##shpint", ref _shapeParamInt, 0.1f, 1, 32);
                break;
            case ShapeType.Ellipse:
                ShowParam("RX:", ref _shapeRadius, "Radius X");
                ImGui.SameLine();
                ShowParam("RZ:", ref _shapeRadius2, "Radius Z");
                break;
            case ShapeType.SineWave:
                ShowParam("Amp:", ref _shapeRadius, "Amplitude");
                ImGui.SameLine();
                ShowParam("Wave:", ref _shapeRadius2, "Wavelength");
                ImGui.SameLine();
                ShowParam("Len:", ref _shapeWidth, "Total Length");
                break;
            case ShapeType.Grid:
                ShowParam("SpX:", ref _shapeSpacing, "X Spacing");
                ImGui.SameLine();
                ShowParam("SpZ:", ref _shapeWidth, "Z Spacing");
                ImGui.SameLine();
                ImGui.Text("Col:");
                ImGui.SameLine();
                ImGui.SetNextItemWidth(30);
                ImGui.DragInt("##shpint", ref _shapeParamInt, 0.1f, 1, 32);
                break;
            case ShapeType.Hypotrochoid:
                ShowParam("R:", ref _shapeRadius); ImGui.SameLine();
                ShowParam("r:", ref _shapeRadius2); ImGui.SameLine();
                ShowParam("d:", ref _shapeWidth); ImGui.SameLine();
                ShowParam("Rot:", ref _shapeDepth);
                break;
            case ShapeType.RingWithCenter:
                ShowParam("R:", ref _shapeRadius);
                ImGui.SameLine();
                ImGui.Text("Ctr:");
                ImGui.SameLine();
                ImGui.SetNextItemWidth(30);
                ImGui.DragInt("##shpint", ref _shapeParamInt, 0.1f, 1, 16);
                break;
        }

        ImGui.SameLine();
        ImGui.Text("A\u00b0:");
        ImGui.SameLine();
        ImGui.SetNextItemWidth(40);
        ImGui.DragFloat("##shai", ref _shapeAngleOff, 1f, -180f, 180f, "%.0f");

        ImGui.SameLine();
        ImGui.SetNextItemWidth(70);
        ImGui.Combo("##shfacei", ref _faceMode, FaceNames, FaceNames.Length);

        ImGui.SameLine();
        ImGui.Checkbox("Ap##shappi", ref _appendMode);
        if (ImGui.IsItemHovered()) ImGui.SetTooltip("Append to existing points");

        ImGui.SameLine();
        if (ImGui.Button("Gen##i") && formation != null)
            GenerateShape(formation);
    }

    private static void ShowParam(string label, ref float val, string? tooltip = null) {
        ImGui.Text(label);
        ImGui.SameLine();
        ImGui.SetNextItemWidth(40);
        ImGui.DragFloat($"##shp_{label}", ref val, 0.1f, 0f, 100f, "%.1f");
        if (tooltip != null && ImGui.IsItemHovered()) ImGui.SetTooltip(tooltip);
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
