using System;
using System.Linq;
using System.Numerics;

using Dalamud.Bindings.ImGui;
using Dalamud.Interface.ImGuiFileDialog;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Windowing;

using MasterOfPuppets.Formations;
using MasterOfPuppets.Util.ImGuiExt;

namespace MasterOfPuppets;

public partial class FormationWindow : Window {
    private Plugin Plugin { get; }

    //  Left panel
    private readonly FileDialogManager _fileDialogManager = new();
    private string _searchFilter = string.Empty;
    private int _selFormation = -1;
    private string _newFmName = string.Empty;
    private int _renamingIdx = -1;
    private string _renameBuffer = string.Empty;
    private bool _renamingFocusPending;

    //  Plot
    private int _selPoint = -1;

    //  Shape generator
    private enum ShapeType {
        Circle, Rectangle, Line, StaggeredLine, FigureEight, Spiral,
        Polygon, Star, Rose, Heart, Ellipse, Arc, SineWave,
        Zigzag, Grid, SpokedWheel, Hypotrochoid, Lissajous,
        StarPolygon, LogarithmicSpiral, Chevron, RingWithCenter, Cross
    }

    private static readonly string[] ShapeNames = [
        "Circle", "Rectangle", "Line", "Staggered Line", "Figure 8", "Spiral",
        "Polygon", "Star", "Rose", "Heart", "Ellipse", "Arc", "Sine Wave",
        "Zigzag", "Grid", "Spoked Wheel", "Hypotrochoid", "Lissajous",
        "Star Poly", "Log Spiral", "Chevron", "Ring Center", "Cross"
    ];
    private static readonly string[] FaceNames = ["Outward", "Inward", "North", "Tangent"];
    private ShapeType _shapeType = ShapeType.Circle;
    private int _shapeN = 8;
    private float _shapeRadius = 5f;
    private float _shapeRadius2 = 3f; // Inner radius or Radius Y
    private float _shapeWidth = 8f;
    private float _shapeDepth = 4f;
    private float _shapeSpacing = 1.5f;
    private float _shapeAngleOff;
    private int _shapeParamInt = 4; // Cols, Petals, sides, etc.
    private int _faceMode;
    private bool _appendMode;

    //  World overlay
    private bool _worldOverlay;
    private float _arrowSize = 0.5f;
    private float _markerSizeWorld = 1.0f;

    //  Right panel
    private float _leftPanelWidth;
    private float _rightPanelWidth = 300f;
    private readonly ImGuiComboSearch _charCombo = new();
    private readonly ImGuiComboSearch _groupCombo = new();
    private string _charSelected = string.Empty;
    private string _groupSelected = string.Empty;

    /// <summary>
    /// Canonical arrow vertices - tip at (0, 1) in local space.
    /// Angle = 0 points north in both plot-space and world-space.
    /// </summary>
    public static readonly Vector2[] ArrowVertices2D = [new(0, 1), new(1, -1), new(0, -0.5f), new(-1, -1)];

    /// <summary>World-space equivalent - tip at (0, 0, 1) = south at rot=0,
    /// but CreateRotationY(worldRot) in FFXIV space rotates it to face north at rot=0.</summary>
    public static readonly Vector3[] ArrowVertices3D = [new(0, 0, 1), new(1, 0, -1), new(0, 0, -0.5f), new(-1, 0, -1)];

    private Formation? SelectedFormation =>
        _selFormation >= 0 && _selFormation < Plugin.Config.Formations.Count
            ? Plugin.Config.Formations[_selFormation] : null;

    public FormationWindow(Plugin plugin) : base("Formations###FormationsWindow") {
        Plugin = plugin;
        Size = ImGuiHelpers.ScaledVector2(960, 620);
        SizeCondition = ImGuiCond.FirstUseEver;
    }

    public override void PreDraw() {
        _fileDialogManager.Draw();
        base.PreDraw();
    }

    public override void Draw() {
        float splitterW = 6f * ImGuiHelpers.GlobalScale;
        float minLeftW = 150f * ImGuiHelpers.GlobalScale;
        float minRightW = 300f * ImGuiHelpers.GlobalScale;
        float minMidW = 100f * ImGuiHelpers.GlobalScale;

        if (_leftPanelWidth <= 0f) _leftPanelWidth = 250f * ImGuiHelpers.GlobalScale;
        if (_rightPanelWidth <= 0f) _rightPanelWidth = minRightW;

        var avail = ImGui.GetContentRegionAvail();
        float spacing = ImGui.GetStyle().ItemSpacing.X;

        float totalFixed = splitterW * 2 + minMidW + spacing * 4;

        float maxLeftW = MathF.Max(avail.X - totalFixed - minRightW, minLeftW);
        float maxRightW = MathF.Max(avail.X - totalFixed - _leftPanelWidth, minRightW);

        _leftPanelWidth = Math.Clamp(_leftPanelWidth, minLeftW, maxLeftW);
        _rightPanelWidth = Math.Clamp(_rightPanelWidth, minRightW, maxRightW);

        float midW = MathF.Max(avail.X - _leftPanelWidth - splitterW * 2 - _rightPanelWidth - spacing * 4, minMidW);
        float h = avail.Y;

        // Left panel
        ImGui.BeginChild("##fwil", new Vector2(_leftPanelWidth, h), true);
        DrawLeftPanel();
        ImGui.EndChild();
        ImGui.SameLine();

        // Left splitter
        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, Vector2.Zero);
        ImGui.InvisibleButton("##fwisplitl", new Vector2(splitterW, h));
        if (ImGui.IsItemHovered()) ImGui.SetMouseCursor(ImGuiMouseCursor.ResizeEw);
        if (ImGui.IsItemActive() && ImGui.IsMouseDragging(ImGuiMouseButton.Left)) {
            _leftPanelWidth += ImGui.GetIO().MouseDelta.X;
            _leftPanelWidth = Math.Clamp(_leftPanelWidth, minLeftW, maxLeftW);
        }
        ImGui.PopStyleVar();
        ImGui.SameLine();

        // Middle panel
        ImGui.BeginChild("##fwim", new Vector2(midW, h), true);
        DrawMiddlePanel();
        ImGui.EndChild();
        ImGui.SameLine();

        // Right splitter
        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, Vector2.Zero);
        ImGui.InvisibleButton("##fwisplit", new Vector2(splitterW, h));
        if (ImGui.IsItemHovered()) ImGui.SetMouseCursor(ImGuiMouseCursor.ResizeEw);
        if (ImGui.IsItemActive() && ImGui.IsMouseDragging(ImGuiMouseButton.Left)) {
            _rightPanelWidth -= ImGui.GetIO().MouseDelta.X;
            _rightPanelWidth = Math.Clamp(_rightPanelWidth, minRightW, maxRightW);
        }
        ImGui.PopStyleVar();
        ImGui.SameLine();

        // Right panel
        ImGui.BeginChild("##fwir", new Vector2(_rightPanelWidth, h), true);
        DrawRightPanel();
        ImGui.EndChild();
    }

    private void AddFormation() {
        if (string.IsNullOrWhiteSpace(_newFmName)) return;
        var trimmed = _newFmName.Trim();
        if (Plugin.Config.Formations.Any(f => f.Name.Equals(trimmed, StringComparison.OrdinalIgnoreCase))) return;
        Plugin.Config.Formations.Add(new Formation { Name = trimmed });
        _selFormation = Plugin.Config.Formations.Count - 1;
        _selPoint = -1;
        _newFmName = string.Empty;

        Plugin.Config.Save();
        Plugin.IpcProvider.SyncConfiguration();
    }
}
