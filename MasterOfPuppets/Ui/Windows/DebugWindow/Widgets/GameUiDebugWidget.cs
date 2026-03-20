using System;
using System.Globalization;
using System.Numerics;

using Dalamud.Bindings.ImGui;

using MasterOfPuppets.Camera;
using MasterOfPuppets.Movement;

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

        ImGui.Text($"CurrentY: {GameCameraManager.CurrentY}");
        if (ImGui.InputFloat("Camera Y Offset (100000000)##CameraYOffset", ref _cameraYOffset, step: 1000, stepFast: 1000)) {
            float YOffset = Math.Clamp(_cameraYOffset, 0f, 100000000f);
            GameCameraManager.SetHeight(YOffset, true);
        }

        if (ImGui.Checkbox("Toggle Cam Hack##ToggleCamHack", ref _enableCamHack)) {
            if (_enableCamHack) {
                GameCameraManager.SetHeight(_cameraYOffset, true);
            } else {
                GameCameraManager.Disable();
            }
        }
    }

    private void DrawScreenCricle(Vector3 position, uint color) {
        bool visible = DalamudApi.GameGui.WorldToScreen(position, out Vector2 screenPos);
        if (!visible)
            return;

        ImGui.GetWindowDrawList().AddCircleFilled(screenPos, 3f, color);
        ImGui.GetWindowDrawList().AddText(screenPos + new Vector2(10, -8), color,
            $"{position.ToString("G", CultureInfo.InvariantCulture)} [{(position - DalamudApi.ObjectTable[0].Position).Length():N2}]");
    }



}
