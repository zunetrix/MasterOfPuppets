using System;
using System.Linq;
using System.Numerics;

using Dalamud.Bindings.ImGui;
using Dalamud.Bindings.ImPlot;

using MasterOfPuppets.Extensions.Dalamud;
using MasterOfPuppets.Formations;
using MasterOfPuppets.Movement;

namespace MasterOfPuppets;

public partial class FormationWindow {

    // =========================================================================
    // Middle panel - shape toolbar + ImPlot canvas
    // =========================================================================

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
        ImGui.Checkbox("World##fiwov", ref _worldOverlay);

        if (formation == null) {
            ImGui.TextDisabled("Select a formation");
            return;
        }

        DrawPlot(formation);

        if (_worldOverlay) DrawWorldOverlay(formation);
    }

    private void DrawPlot(Formation formation) {
        // Compute axis range; guard against NaN/Inf from corrupted offsets
        float limit = 8f;
        if (formation.Points.Count > 0) {
            float max = formation.Points.Max(p => MathF.Max(MathF.Abs(p.Offset.X), MathF.Abs(p.Offset.Z)));
            if (float.IsFinite(max)) limit = MathF.Max(max + 3f, 8f);
        }

        // Only reset axis limits when formation changes (allows free zoom otherwise)
        if (_needsAxisReset) {
            ImPlot.SetNextAxisLimits(ImAxis.X1, -limit, limit, ImPlotCond.Always);
            ImPlot.SetNextAxisLimits(ImAxis.Y1, -limit, limit, ImPlotCond.Always);
            _needsAxisReset = false;
        }

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
        bool keyAlt = ImGui.GetIO().KeyAlt;
        bool keyCtrl = ImGui.GetIO().KeyCtrl;
        bool keyShift = ImGui.GetIO().KeyShift;

        for (int i = 0; i < formation.Points.Count; i++) {
            var pt = formation.Points[i];
            bool selected = i == _selPoint;
            uint color = selected ? 0xFFFFAA00u : 0xFF3377CCu;

            // Plot coords: X = game X (east), Y = −game Z (north = up)
            double x = pt.Offset.X;
            double y = -pt.Offset.Z;

            // Arrow drawn in plot-space - scales naturally with zoom
            DrawArrow(dl, (float)x, (float)y, pt.Angle, color, _arrowSize);

            // Label centered on point in screen space
            var ptPx = ImPlot.PlotToPixels((float)x, (float)y);
            var label = $"{i + 1}";
            var labelSz = ImGui.CalcTextSize(label);
            ImGui.GetForegroundDrawList().AddText(ptPx - labelSz * 0.5f, 0xFFFFFFFF, label);

            // Click radius derived from plot-space size so it scales with zoom
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
                    // Alt+drag: adjust rotation via horizontal mouse movement, don't move position
                    x = origX;
                    y = origY;
                    pt.Angle += ImGui.GetIO().MouseDelta.X;
                    if (ImGui.IsMouseReleased(ImGuiMouseButton.Left)) {
                        Plugin.Config.Save();
                        Plugin.IpcProvider.SyncConfiguration();
                    }
                } else if (ImGui.GetMouseDragDelta(ImGuiMouseButton.Left).LengthSquared() > 4f) {
                    if (keyCtrl) {
                        // Ctrl: snap to 1-unit grid
                        x = Math.Round(x, MidpointRounding.AwayFromZero);
                        y = Math.Round(y, MidpointRounding.AwayFromZero);
                    } else if (keyShift) {
                        // Shift: snap to 0.25-unit grid
                        x = Math.Round(x * 4, MidpointRounding.AwayFromZero) / 4;
                        y = Math.Round(y * 4, MidpointRounding.AwayFromZero) / 4;
                    }
                    pt.Offset.X = (float)x;
                    pt.Offset.Z = -(float)y;
                    if (ImGui.IsMouseReleased(ImGuiMouseButton.Left)) {
                        Plugin.Config.Save();
                        Plugin.IpcProvider.SyncConfiguration();
                    }
                }
            }
        }

        // Shift+Click on empty area → add point
        if (!anyDragged && ImPlot.IsPlotHovered() &&
                ImGui.IsMouseClicked(ImGuiMouseButton.Left) && keyShift) {
            var mp = ImPlot.GetPlotMousePos();
            formation.Points.Add(new FormationPoint {
                Offset = new Vector3((float)mp.X, 0f, -(float)mp.Y),
            });
            _selPoint = formation.Points.Count - 1;
            Plugin.Config.Save();
            Plugin.IpcProvider.SyncConfiguration();
        }

        // Click on empty area → deselect
        if (!anyDragged && ImPlot.IsPlotHovered() &&
                ImGui.IsMouseClicked(ImGuiMouseButton.Left) && !keyShift) {
            _selPoint = -1;
        }

        ImPlot.PopPlotClipRect();
        ImPlot.EndPlot();
    }

    /// <summary>Arrow drawn in plot-space coordinates - scales naturally with zoom.
    /// Vertices are rotated in plot-space then converted to pixels via ImPlot.PlotToPixels.
    /// At angleDeg=0 the tip points north (+Y_plot). Clockwise = east at 90°.</summary>
    private static void DrawArrow(ImDrawListPtr dl, float px, float py, float angleDeg, uint color, float size = 0.5f) {
        float rad = angleDeg * Angle.DegToRad;
        float cosA = MathF.Cos(rad), sinA = MathF.Sin(rad);
        float h = size * 0.5f;

        // arrow vertices in local plot-space (forward = north = +Y_plot)
        (float lx, float ly)[] local = [(0f, 1f), (1f, -1f), (0f, -0.5f), (-1f, -1f)];

        var pts = new Vector2[4];
        for (int i = 0; i < 4; i++) {
            float lx = local[i].lx * h, ly = local[i].ly * h;
            // CW rotation: tip (0, 1) at angle α → (sin α, cos α) in plot-space
            pts[i] = ImPlot.PlotToPixels(px + lx * cosA + ly * sinA,
                                         py - lx * sinA + ly * cosA);
        }

        dl.AddConvexPolyFilled(ref pts[0], 4, color);
        dl.AddPolyline(ref pts[0], 4, 0xFFFFFFFF, ImDrawFlags.Closed, 1.5f);
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

        // Try to find the party leader to use as origin (0,0)
        var leader = DalamudApi.PartyList.FirstOrDefault(m => m.IsPartyLeader());
        var originPos = player.Position;

        if (leader != null) {
            var leaderObj = DalamudApi.ObjectTable.FirstOrDefault(o => o.EntityId == leader.EntityId);
            if (leaderObj != null) originPos = leaderObj.Position;
        }

        if (!_appendMode) formation.Points.Clear();

        foreach (var member in DalamudApi.PartyList) {
            var obj = DalamudApi.ObjectTable.FirstOrDefault(o => o.EntityId == member.EntityId);
            if (obj == null) continue;

            var offset = obj.Position - originPos;
            var pt = new FormationPoint {
                Offset = new Vector3(offset.X, 0, offset.Z),
                Angle = obj.Rotation * Angle.RadToDeg, // Capture current facing
            };
            if (member.ContentId != 0) pt.Cids.Add((ulong)member.ContentId);
            formation.Points.Add(pt);
        }

        _selPoint = -1;
        Plugin.Config.Save();
        Plugin.IpcProvider.SyncConfiguration();
    }
}
