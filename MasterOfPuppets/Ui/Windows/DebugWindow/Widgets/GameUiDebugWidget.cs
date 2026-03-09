using System.Globalization;
using System.Numerics;

using Dalamud.Bindings.ImGui;

using MasterOfPuppets.Movement;

namespace MasterOfPuppets.Debug;

public sealed class GameUiDebugWidget : Widget {
    public override string Title => "Game Ui Debug";

    public GameUiDebugWidget(WidgetContext ctx) : base(ctx) {
    }

    //TODO: create a world drawer to render markers on main Draw before windows system
    public override void Draw() {
        var position = DalamudApi.ObjectTable[0].Position;
        var rotation = DalamudApi.ObjectTable[0].Rotation;
        var positionAngle = Angle.FromDirectionXZ(position);

        // DalamudApi.PluginLog.Warning($"rotation {rotation}, rotation rad: {rotation.Radians().Rad} angle from position {positionAngle.Rad}");

        DrawScreenCricle(position, ImGui.ColorConvertFloat4ToU32(Style.Colors.Green));
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
