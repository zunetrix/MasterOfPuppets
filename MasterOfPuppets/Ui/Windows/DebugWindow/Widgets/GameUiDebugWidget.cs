using System;
using System.Globalization;
using System.Numerics;

using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;

using MasterOfPuppets.Camera;
using MasterOfPuppets.Movement;
using MasterOfPuppets.Util.ImGuiExt;

namespace MasterOfPuppets.Debug;

public sealed class GameUiDebugWidget : Widget {
    public override string Title => "Game Ui Debug";

    private bool _enableCamHack = false;
    private bool _showWorldMark = false;
    private float _cameraYOffset = 10000;

    public GameUiDebugWidget(WidgetContext ctx) : base(ctx) {
    }

    //TODO: create a world drawer to render markers on main Draw before windows system
    public override void Draw() {
        var position = DalamudApi.ObjectTable[0].Position;
        var rotation = DalamudApi.ObjectTable[0].Rotation;
        var positionAngle = Angle.FromDirectionXZ(position);

        // DalamudApi.PluginLog.Warning($"rotation {rotation}, rotation rad: {rotation.Radians().Rad} angle from position {positionAngle.Rad}");
        ImGui.Checkbox("Show World Mark##ShowWorldMark", ref _showWorldMark);
        if (_showWorldMark) {
            DrawScreenCricle(position, ImGui.ColorConvertFloat4ToU32(Style.Colors.Green));
        }

        ImGui.Text($"Camera Height Offset: {GameCameraManager.CurrentY}");
        ImGui.SetNextItemWidth(150 * ImGuiHelpers.GlobalScale);
        if (ImGui.DragFloat("##CameraYOffset", ref _cameraYOffset, 1f, 0f, GameCameraManager.MaxYOffset, "%.0f")) {
            float YOffset = Math.Clamp(_cameraYOffset, 0f, GameCameraManager.MaxYOffset);
            GameCameraManager.SetHeight(YOffset, true);
        }
        ImGui.SameLine();
        if (ImGuiUtil.IconButton(FontAwesomeIcon.Undo, "##ResetCameraOffsetBtn", "Reset")) {
            GameCameraManager.SetHeight(GameCameraManager.MaxYOffset, true);
        }

        if (ImGui.Checkbox("Toggle Cam Hack##ToggleCamHack", ref _enableCamHack)) {
            if (_enableCamHack) {
                GameCameraManager.SetHeight(_cameraYOffset, true);
            } else {
                GameCameraManager.Disable();
            }
        }
    }

    public static void DrawScreenCricle(Vector3 position, uint color = 0xFF33FF33) {
        bool visible = DalamudApi.GameGui.WorldToScreen(position, out Vector2 screenPos);
        if (!visible)
            return;

        ImGui.GetWindowDrawList().AddCircleFilled(screenPos, 3f, color);
        ImGui.GetWindowDrawList().AddText(screenPos + new Vector2(10, -8), color,
            $"{position.ToString("G", CultureInfo.InvariantCulture)} [{(position - DalamudApi.ObjectTable[0].Position).Length():N2}]");
    }
}
