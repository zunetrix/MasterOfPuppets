using System;
using System.Globalization;
using System.Linq;
using System.Numerics;

using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Windowing;

using MasterOfPuppets.Formations;
using MasterOfPuppets.Movement;
using MasterOfPuppets.Util.ImGuiExt;

namespace MasterOfPuppets;

public class FormationWindow : Window {
    private Plugin Plugin { get; }

    //  Left panel
    private string _searchFilter = string.Empty;
    private int _selFormation = -1;
    private string _newFmName = string.Empty;

    //  Canvas
    private float _scale = 50f;   // pixels per meter
    private Vector2 _pan = Vector2.Zero;
    private int _selPoint = -1;
    private bool _draggingPt;
    private Vector2 _dragStartMouse;
    private Vector2 _dragStartOffset;  // X and Z of the point when drag began

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

    public FormationWindow(Plugin plugin) : base("Formations###FormationsWindow") {
        Plugin = plugin;
        Size = new Vector2(960, 620);
        SizeCondition = ImGuiCond.FirstUseEver;
    }

    public override void Draw() {
        const float leftW = 220f;
        const float splitterW = 6f;
        const float minRightW = 150f;
        var avail = ImGui.GetContentRegionAvail();
        float spacing = ImGui.GetStyle().ItemSpacing.X;
        _rightPanelWidth = MathF.Max(minRightW, MathF.Min(_rightPanelWidth, avail.X - leftW - splitterW - 80f));
        float midW = MathF.Max(avail.X - leftW - splitterW - _rightPanelWidth - spacing * 3, 100f);
        float h = avail.Y;

        ImGui.BeginChild("##fwl", new Vector2(leftW, h), true);
        DrawLeftPanel();
        ImGui.EndChild();

        ImGui.SameLine();

        ImGui.BeginChild("##fwm", new Vector2(midW, h), true);
        DrawMiddlePanel();
        ImGui.EndChild();

        ImGui.SameLine();

        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(0, 0));
        ImGui.InvisibleButton("##fwsplit", new Vector2(splitterW, h));
        if (ImGui.IsItemHovered()) ImGui.SetMouseCursor(ImGuiMouseCursor.ResizeEw);
        if (ImGui.IsItemActive() && ImGui.IsMouseDragging(ImGuiMouseButton.Left)) {
            _rightPanelWidth -= ImGui.GetIO().MouseDelta.X;
            _rightPanelWidth = MathF.Max(minRightW, MathF.Min(_rightPanelWidth, avail.X - leftW - splitterW - 80f));
        }
        ImGui.PopStyleVar();

        ImGui.SameLine();

        ImGui.BeginChild("##fwr", new Vector2(_rightPanelWidth, h), true);
        DrawRightPanel();
        ImGui.EndChild();
    }

    // =========================================================================
    // Left panel – formation list
    // =========================================================================

    private void DrawLeftPanel() {
        if (ImGuiUtil.IconButton(FontAwesomeIcon.Plus, "##fladd_s", "New formation"))
            ImGui.OpenPopup("##flnew");
        if (ImGui.BeginPopup("##flnew")) {
            ImGui.Text("Name:");
            ImGui.SameLine();
            ImGui.SetNextItemWidth(150);
            bool enter = ImGui.InputText("##flnewname", ref _newFmName, 64, ImGuiInputTextFlags.EnterReturnsTrue);
            if ((enter || ImGui.Button("Create")) && !string.IsNullOrWhiteSpace(_newFmName)) {
                AddFormation();
                ImGui.CloseCurrentPopup();
            }
            ImGui.EndPopup();
        }
        ImGui.SameLine();
        ImGui.SetNextItemWidth(-1);
        ImGui.InputText("##flsrch", ref _searchFilter, 64);

        var formations = Plugin.Config.Formations;
        ImGui.BeginChild("##fllist", new Vector2(-1, -1), false);
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
    // Middle panel – shape toolbar + canvas
    // =========================================================================

    private void DrawMiddlePanel() {
        var formation = SelectedFormation;

        DrawShapeToolbar(formation);
        ImGui.SameLine();
        ImGui.Text("Pt:");
        ImGui.SameLine();
        ImGui.SetNextItemWidth(38);
        ImGui.DragFloat("##mszp", ref _markerSizePlot, 0.5f, 5f, 80f, "%.0f");
        ImGui.SameLine();
        ImGui.Text("W:");
        ImGui.SameLine();
        ImGui.SetNextItemWidth(38);
        ImGui.DragFloat("##mszw", ref _markerSizeWorld, 0.5f, 2f, 30f, "%.0f");
        ImGui.SameLine();
        if (ImGui.Button("From Party##fw") && formation != null)
            SnapshotParty(formation);
        ImGui.SameLine();
        ImGui.Checkbox("World##fwwov", ref _worldOverlay);

        var canvasSize = ImGui.GetContentRegionAvail();
        if (canvasSize.X < 10 || canvasSize.Y < 10) return;

        var canvasMin = ImGui.GetCursorScreenPos();
        ImGui.InvisibleButton("##fwcanvas", canvasSize,
            ImGuiButtonFlags.MouseButtonLeft | ImGuiButtonFlags.MouseButtonRight);

        bool hovered = ImGui.IsItemHovered();
        bool active = ImGui.IsItemActive();
        var dl = ImGui.GetWindowDrawList();
        var canvasMax = canvasMin + canvasSize;
        var center = canvasMin + canvasSize * 0.5f + _pan;

        dl.AddRectFilled(canvasMin, canvasMax, 0xFF1A1A1A);
        dl.AddRect(canvasMin, canvasMax, 0xFF555555);
        DrawGrid(dl, canvasMin, canvasMax, center);

        if (formation == null) {
            const string msg = "Select a formation";
            var ts = ImGui.CalcTextSize(msg);
            dl.AddText(canvasMin + canvasSize * 0.5f - ts * 0.5f, 0xFF888888, msg);
            return;
        }

        var mousePos = ImGui.GetMousePos();
        var mouseGame = CanvasToGame(mousePos, center);

        // Scroll to zoom
        if (hovered && ImGui.GetIO().MouseWheel != 0)
            _scale = Math.Clamp(_scale * (ImGui.GetIO().MouseWheel > 0 ? 1.15f : 1f / 1.15f), 4f, 200f);

        // Right-drag to pan
        if (active && ImGui.IsMouseDragging(ImGuiMouseButton.Right, 1f)) {
            _pan += ImGui.GetMouseDragDelta(ImGuiMouseButton.Right);
            ImGui.ResetMouseDragDelta(ImGuiMouseButton.Right);
        }

        // Hit-test points
        int hoveredPt = -1;
        if (hovered) {
            for (int i = 0; i < formation.Points.Count; i++) {
                var off = formation.Points[i].Offset;
                if (Vector2.Distance(mousePos, GameToCanvas(off.X, off.Z, center)) < 12f) {
                    hoveredPt = i;
                    break;
                }
            }
        }

        // Left-click: select or Shift-add
        if (hovered && ImGui.IsMouseClicked(ImGuiMouseButton.Left)) {
            if (hoveredPt >= 0) {
                _selPoint = hoveredPt;
                _draggingPt = true;
                _dragStartMouse = mousePos;
                var off = formation.Points[hoveredPt].Offset;
                _dragStartOffset = new Vector2(off.X, off.Z);
            } else if (ImGui.GetIO().KeyShift) {
                formation.Points.Add(new FormationPoint {
                    Offset = new Vector3(mouseGame.X, 0, mouseGame.Y),
                });
                _selPoint = formation.Points.Count - 1;
                Plugin.Config.Save();
            } else {
                _selPoint = -1;
            }
        }

        // Drag selected point
        if (_draggingPt) {
            if (ImGui.IsMouseDown(ImGuiMouseButton.Left) &&
                _selPoint >= 0 && _selPoint < formation.Points.Count) {
                var delta = (mousePos - _dragStartMouse) / _scale;
                formation.Points[_selPoint].Offset.X = _dragStartOffset.X + delta.X;
                formation.Points[_selPoint].Offset.Z = _dragStartOffset.Y + delta.Y;
            } else {
                _draggingPt = false;
                Plugin.Config.Save();
            }
        }

        // Axes
        dl.AddLine(new Vector2(canvasMin.X, center.Y), new Vector2(canvasMax.X, center.Y), 0x33FFFFFF);
        dl.AddLine(new Vector2(center.X, canvasMin.Y), new Vector2(center.X, canvasMax.Y), 0x33FFFFFF);

        // Points
        for (int i = 0; i < formation.Points.Count; i++) {
            var pt = formation.Points[i];
            DrawPointMarker(dl, GameToCanvas(pt.Offset.X, pt.Offset.Z, center),
                pt.Angle, i == _selPoint, i == hoveredPt, i, _markerSizePlot);
        }

        if (_worldOverlay) DrawWorldOverlay(formation);

        if (hovered && hoveredPt < 0)
            ImGui.SetTooltip("Shift+Click  → add point\nRight-drag   → pan\nScroll       → zoom");
    }

    private void DrawShapeToolbar(Formation? formation) {
        ImGui.SetNextItemWidth(75);
        ImGui.Combo("##shtype", ref _shapeType, ShapeNames, ShapeNames.Length);
        ImGui.SameLine();

        if (_shapeType == 0) {
            ImGui.Text("N:");
            ImGui.SameLine();
            ImGui.SetNextItemWidth(38);
            ImGui.DragInt("##shn", ref _shapeN, 0.1f, 2, 32);
            ImGui.SameLine();
        }

        ImGui.Text("R:");
        ImGui.SameLine();
        ImGui.SetNextItemWidth(46);
        ImGui.DragFloat("##shr", ref _shapeRadius, 0.1f, 0.5f, 50f, "%.1f");
        ImGui.SameLine();

        ImGui.Text("A\u00b0:");
        ImGui.SameLine();
        ImGui.SetNextItemWidth(46);
        ImGui.DragFloat("##sha", ref _shapeAngleOff, 1f, -180f, 180f, "%.0f");
        ImGui.SameLine();

        ImGui.SetNextItemWidth(68);
        ImGui.Combo("##shface", ref _faceMode, FaceNames, FaceNames.Length);
        ImGui.SameLine();

        ImGui.Checkbox("Ap##shapp", ref _appendMode);
        if (ImGui.IsItemHovered()) ImGui.SetTooltip("Append to existing points");
        ImGui.SameLine();

        if (ImGui.Button("Gen") && formation != null)
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

    private static void DrawPointMarker(
            ImDrawListPtr dl, Vector2 pos, float angleDeg,
            bool selected, bool hovered, int index, float r = 36f) {
        float rad = angleDeg * Angle.DegToRad;
        var fwd = new Vector2(MathF.Sin(rad), -MathF.Cos(rad));
        var right = new Vector2(MathF.Cos(rad), MathF.Sin(rad));

        var p0 = pos + fwd * r;
        var p1 = pos - fwd * 0.4f * r + right * 0.6f * r;
        var p2 = pos - fwd * 0.4f * r - right * 0.6f * r;

        uint fill = selected ? 0xFFFFAA00u : hovered ? 0xFF88BBFFu : 0xFF3377CCu;
        dl.AddTriangleFilled(p0, p1, p2, fill);
        dl.AddTriangle(p0, p1, p2, 0xFFFFFFFF);

        var label = (index + 1).ToString();
        var ts = ImGui.CalcTextSize(label);
        dl.AddText(pos - fwd * 0.2f * r - ts * 0.5f, 0xFFFFFFFF, label);
    }

    private void DrawWorldOverlay(Formation formation) {
        var player = DalamudApi.ObjectTable.LocalPlayer;
        if (player == null) return;

        var fgDl = ImGui.GetForegroundDrawList();
        var playerPos = player.Position;

        // FFXIV rotation: 0 = south (+Z).  Rotation matrix (CW viewed from above):
        //   newX =  X * cosR + Z * sinR
        //   newZ = -X * sinR + Z * cosR
        float cosR = MathF.Cos(player.Rotation);
        float sinR = MathF.Sin(player.Rotation);

        // Fallback screen forward (player facing direction via WorldToScreen)
        DalamudApi.GameGui.WorldToScreen(playerPos, out var originPx);
        var screenFwdFallback = DalamudApi.GameGui.WorldToScreen(
                new Vector3(playerPos.X + sinR, playerPos.Y, playerPos.Z + cosR), out var fwdOriginPx)
            ? Vector2.Normalize(fwdOriginPx - originPx)
            : new Vector2(0f, -1f);

        for (int i = 0; i < formation.Points.Count; i++) {
            var pt = formation.Points[i];

            // Rotate offset by player yaw so the whole formation follows the leader's facing
            var worldPos = playerPos + new Vector3(
                 pt.Offset.X * cosR + pt.Offset.Z * sinR,
                 pt.Offset.Y,
                -pt.Offset.X * sinR + pt.Offset.Z * cosR);
            if (!DalamudApi.GameGui.WorldToScreen(worldPos, out var screenPos)) continue;

            // Plugin angle: 0 = north → absolute world direction = (sin A, 0, -cos A).
            // Apply the same yaw rotation so facing also follows the leader.
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
            uint color = i == _selPoint ? 0xFFFFAA00u : 0xFF3388FFu;
            var p0 = screenPos + fwd * r;
            var p1 = screenPos - fwd * 0.4f * r + right * 0.6f * r;
            var p2 = screenPos - fwd * 0.4f * r - right * 0.6f * r;
            fgDl.AddTriangleFilled(p0, p1, p2, color);
            fgDl.AddTriangle(p0, p1, p2, 0xFFFFFFFF);
            var label = (i + 1).ToString();
            fgDl.AddText(screenPos - ImGui.CalcTextSize(label) * 0.5f, 0xFFFFFFFF, label);
        }
    }

    private void DrawGrid(ImDrawListPtr dl, Vector2 min, Vector2 max, Vector2 center) {
        float meterStep = 1f;
        float screenStep = _scale;
        while (screenStep < 15f) { meterStep *= 5f; screenStep *= 5f; }

        const uint color = 0x22FFFFFF;

        float xMinW = (min.X - center.X) / _scale;
        float xMaxW = (max.X - center.X) / _scale;
        float firstX = MathF.Ceiling(xMinW / meterStep) * meterStep;
        for (float wx = firstX; wx <= xMaxW + meterStep; wx += meterStep) {
            float sx = center.X + wx * _scale;
            dl.AddLine(new Vector2(sx, min.Y), new Vector2(sx, max.Y), color);
        }

        float zMinW = (min.Y - center.Y) / _scale;
        float zMaxW = (max.Y - center.Y) / _scale;
        float firstZ = MathF.Ceiling(zMinW / meterStep) * meterStep;
        for (float wz = firstZ; wz <= zMaxW + meterStep; wz += meterStep) {
            float sy = center.Y + wz * _scale;
            dl.AddLine(new Vector2(min.X, sy), new Vector2(max.X, sy), color);
        }
    }

    // game (X=east, Z=south) → canvas screen position
    private Vector2 GameToCanvas(float gx, float gz, Vector2 center) =>
        new(center.X + gx * _scale, center.Y + gz * _scale);

    // canvas screen position → game (.X = game X, .Y = game Z)
    private Vector2 CanvasToGame(Vector2 screen, Vector2 center) =>
        new((screen.X - center.X) / _scale, (screen.Y - center.Y) / _scale);

    // =========================================================================
    // Right panel – point editor
    // =========================================================================

    private void DrawRightPanel() {
        var formation = SelectedFormation;
        if (formation == null) { ImGui.TextDisabled("No formation selected"); return; }

        //  Header: Execute | Delete | Name
        if (ImGuiUtil.IconButton(FontAwesomeIcon.Play, "##execfm", "Execute formation"))
            Plugin.IpcProvider.ExecuteFormation(formation.Name);
        ImGui.SameLine();
        if (ImGuiUtil.DangerIconButton(FontAwesomeIcon.Trash, "##delfm", "Delete formation (Ctrl+Click)") && ImGui.GetIO().KeyCtrl) {
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
        if (ImGui.InputText("##fwname", ref name, 64)) formation.Name = name;
        if (ImGui.IsItemDeactivatedAfterEdit()) Plugin.Config.Save();

        ImGui.Spacing();

        var pt2 = _selPoint >= 0 && _selPoint < formation.Points.Count
            ? formation.Points[_selPoint] : null;

        //  Points list
        ImGui.SetNextItemOpen(true, ImGuiCond.Once);
        if (ImGui.CollapsingHeader($"Points ({formation.Points.Count})##fwpts")) {
            if (formation.Points.Count == 0) {
                ImGui.TextDisabled("Shift+Click canvas to add");
            } else {
                if (ImGui.BeginTable("##fwpttbl", 4,
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
                        if (ImGui.Selectable($"##fwrow{i}", i == _selPoint, ImGuiSelectableFlags.SpanAllColumns))
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
            if (ImGui.Button("Clear All##fwclear", new Vector2(-1, 0)) && ImGui.GetIO().KeyCtrl) {
                formation.Points.Clear();
                _selPoint = -1;
                Plugin.Config.Save();
            }
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("Ctrl+Click to clear all points");
        }

        //  Point Editor
        ImGui.SetNextItemOpen(true, ImGuiCond.Once);
        if (ImGui.CollapsingHeader("Point Editor##fwptedit")) {
            if (pt2 == null) {
                ImGui.TextDisabled("Select a point");
            } else {
                ImGui.Text($"#{_selPoint + 1} / {formation.Points.Count}");
                ImGui.SameLine();
                if (ImGuiUtil.DangerIconButton(FontAwesomeIcon.Trash, "##fwdelpt", "Delete point (Ctrl+Click)") && ImGui.GetIO().KeyCtrl) {
                    formation.Points.RemoveAt(_selPoint);
                    _selPoint = formation.Points.Count > 0
                        ? Math.Clamp(_selPoint, 0, formation.Points.Count - 1) : -1;
                    Plugin.Config.Save();
                }
                ImGui.Text("X:"); ImGui.SameLine(); ImGui.SetNextItemWidth(-1);
                ImGui.DragFloat("##ptx", ref pt2.Offset.X, 0.001f, -500f, 500f, "%.3f");
                if (ImGui.IsItemDeactivatedAfterEdit()) Plugin.Config.Save();
                ImGui.Text("Z:"); ImGui.SameLine(); ImGui.SetNextItemWidth(-1);
                ImGui.DragFloat("##ptz", ref pt2.Offset.Z, 0.001f, -500f, 500f, "%.3f");
                if (ImGui.IsItemDeactivatedAfterEdit()) Plugin.Config.Save();
                ImGui.Text("A\u00b0:"); ImGui.SameLine(); ImGui.SetNextItemWidth(-1);
                ImGui.DragFloat("##pta", ref pt2.Angle, 1f, -360f, 360f, "%.1f\u00b0");
                if (ImGui.IsItemDeactivatedAfterEdit()) Plugin.Config.Save();
            }
        }

        //  Characters
        if (ImGui.CollapsingHeader("Characters##fwchars")) {
            if (pt2 == null) {
                ImGui.TextDisabled("Select a point");
            } else {
                int delIdx = -1;
                float delW = ImGui.GetFrameHeight();
                var availChars = Plugin.Config.Characters
                    .Where(c => !pt2.Cids.Contains(c.Cid)).Select(c => c.Name).ToList();
                ImGui.SetNextItemWidth(-1);
                if (_charCombo.Draw("##fwcharcombo", availChars, ref _charSelected)) {
                    var found = Plugin.Config.Characters.FirstOrDefault(c => c.Name == _charSelected);
                    if (found != null) { pt2.Cids.Add(found.Cid); Plugin.Config.Save(); }
                    _charSelected = string.Empty;
                }
                if (ImGui.BeginTable("##fwchartbl", 3,
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
                        if (ImGuiUtil.DangerIconButton(FontAwesomeIcon.Trash, $"##fwdc{i}", "Remove")) delIdx = i;
                    }
                    ImGui.EndTable();
                }
                if (delIdx >= 0) { pt2.Cids.RemoveAt(delIdx); Plugin.Config.Save(); }
            }
        }

        //  Groups
        if (ImGui.CollapsingHeader("Groups##fwgrps")) {
            if (pt2 == null) {
                ImGui.TextDisabled("Select a point");
            } else {
                int delIdx = -1;
                float delW = ImGui.GetFrameHeight();
                var availGroups = Plugin.Config.CidsGroups
                    .Where(g => !pt2.GroupIds.Contains(g.Name)).Select(g => g.Name).ToList();
                ImGui.SetNextItemWidth(-1);
                if (_groupCombo.Draw("##fwgrpcombo", availGroups, ref _groupSelected)) {
                    if (!string.IsNullOrEmpty(_groupSelected)) { pt2.GroupIds.Add(_groupSelected); Plugin.Config.Save(); }
                    _groupSelected = string.Empty;
                }
                if (ImGui.BeginTable("##fwgrptbl", 3,
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
                        if (ImGuiUtil.DangerIconButton(FontAwesomeIcon.Trash, $"##fwdg{i}", "Remove")) delIdx = i;
                    }
                    ImGui.EndTable();
                }
                if (delIdx >= 0) { pt2.GroupIds.RemoveAt(delIdx); Plugin.Config.Save(); }
            }
        }
    }
}
