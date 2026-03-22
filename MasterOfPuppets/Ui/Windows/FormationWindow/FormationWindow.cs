using System;
using System.Numerics;

using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Windowing;

using MasterOfPuppets.Formations;
using MasterOfPuppets.Util.ImGuiExt;

namespace MasterOfPuppets;

public partial class FormationWindow : Window {
    private Plugin Plugin { get; }

    //  Left panel
    private string _searchFilter = string.Empty;
    private int _selFormation = -1;
    private string _newFmName = string.Empty;

    //  Plot
    private int _selPoint = -1;
    private bool _needsAxisReset = true;

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
    private float _arrowSize = 0.5f;
    private float _markerSizeWorld = 1.5f;

    //  Right panel
    private float _rightPanelWidth = 300f;
    private readonly ImGuiComboSearch _charCombo = new();
    private readonly ImGuiComboSearch _groupCombo = new();
    private string _charSelected = string.Empty;
    private string _groupSelected = string.Empty;

    private Formation? SelectedFormation =>
        _selFormation >= 0 && _selFormation < Plugin.Config.Formations.Count
            ? Plugin.Config.Formations[_selFormation] : null;

    public FormationWindow(Plugin plugin) : base("Formations (WIP)###FormationsWindow") {
        Plugin = plugin;
        Size = new Vector2(960, 620);
        SizeCondition = ImGuiCond.FirstUseEver;
    }

    public override void Draw() {
        float leftW = 250f * ImGuiHelpers.GlobalScale;
        float splitterW = 6f * ImGuiHelpers.GlobalScale;
        float minRightW = 300f * ImGuiHelpers.GlobalScale;

        if (_rightPanelWidth <= 0f)
            _rightPanelWidth = minRightW;

        var avail = ImGui.GetContentRegionAvail();
        float spacing = ImGui.GetStyle().ItemSpacing.X;
        float minMidW = 100f * ImGuiHelpers.GlobalScale;
        float maxRightW = avail.X - leftW - splitterW - minMidW - spacing * 3;
        _rightPanelWidth = Math.Clamp(_rightPanelWidth, minRightW, maxRightW);
        float midW = MathF.Max(avail.X - leftW - splitterW - _rightPanelWidth - spacing * 3, minMidW);
        float h = avail.Y;

        ImGui.BeginChild("##fwil", new Vector2(leftW, h), true);
        DrawLeftPanel();
        ImGui.EndChild();

        ImGui.SameLine();

        ImGui.BeginChild("##fwim", new Vector2(midW, h), true);
        DrawMiddlePanel();
        ImGui.EndChild();

        ImGui.SameLine();

        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, Vector2.Zero);
        ImGui.InvisibleButton("##fwisplit", new Vector2(splitterW, h));
        if (ImGui.IsItemHovered()) ImGui.SetMouseCursor(ImGuiMouseCursor.ResizeEw);
        if (ImGui.IsItemActive() && ImGui.IsMouseDragging(ImGuiMouseButton.Left)) {
            _rightPanelWidth -= ImGui.GetIO().MouseDelta.X;
            _rightPanelWidth = Math.Clamp(_rightPanelWidth, minRightW, maxRightW);
        }
        ImGui.PopStyleVar();

        ImGui.SameLine();

        ImGui.BeginChild("##fwir", new Vector2(_rightPanelWidth, h), true);
        DrawRightPanel();
        ImGui.EndChild();
    }

    private void AddFormation() {
        if (string.IsNullOrWhiteSpace(_newFmName)) return;
        Plugin.Config.Formations.Add(new Formation { Name = _newFmName });
        _selFormation = Plugin.Config.Formations.Count - 1;
        _selPoint = -1;
        _needsAxisReset = true;
        _newFmName = string.Empty;
        Plugin.Config.Save();
    }
}
