using System;
using System.Linq;
using System.Numerics;

using Dalamud.Bindings.ImGui;
using Dalamud.Bindings.ImPlot;

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
            // a = bearing CW from south (+Z). Our preview convention: 0=north, CW+.
            // Outward: arrow faces away from center = same direction as point position.
            //   Direction of (sin(a), cos(a)) in game XZ = bearing (180° - a*RadToDeg) in our convention.
            // Inward: opposite of outward = outward + 180° = 360° - a*RadToDeg.
            float ang = _faceMode switch {
                1 => 360f - a * Angle.RadToDeg,    // Inward: toward center
                2 => 0f,                             // North: all face north
                _ => 180f - a * Angle.RadToDeg,     // Outward: away from center
            };
            formation.Points.Add(new FormationPoint {
                Offset = new Vector3(_shapeRadius * MathF.Sin(a), 0, _shapeRadius * MathF.Cos(a)),
                Angle = ang,
            });
        }
        _selPoint = -1;
        Plugin.Config.Save();
        Plugin.IpcProvider.SyncConfiguration();
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
        Plugin.IpcProvider.SyncConfiguration();
    }
}
