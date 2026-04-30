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

        ImGui.Separator();

        using (ImGuiGroupPanel.BeginGroupPanel("Render")) {
            // bool disableModels = Context.Plugin.Config.DisableModels;
            // if (ImGui.Checkbox("Disable Models", ref disableModels)) {
            //     var rm = Context.Plugin.GameRenderManager;
            //     Context.Plugin.Config.DisableModels = disableModels;

            //     rm.RenderModels.ApplyHook(disableModels);
            //     rm.RenderModel5.ApplyHook(disableModels);
            //     rm.ModelRenderer.ApplyHook(disableModels);
            //     rm.RenderHuman.ApplyHook(disableModels);
            //     rm.RenderCharaBase.ApplyHook(disableModels);
            //     rm.RenderCharaBaseMat.ApplyHook(disableModels);

            //     Context.Plugin.Config.Save();
            // }

            // HookCheckbox("Disable VFX", Context.Plugin.GameRenderManager.RenderVfxObject);
            // HookCheckbox("Disable Character Animations", Context.Plugin.GameRenderManager.CharaAnimations);
            // HookCheckbox("Disable Terrain", Context.Plugin.GameRenderManager.RenderTerrain);
            // HookCheckbox("Disable Water", Context.Plugin.GameRenderManager.RenderWater);
            // HookCheckbox("Disable Lights", Context.Plugin.GameRenderManager.RenderLights);
            // HookCheckbox("Disable Camera Matrices", Context.Plugin.GameRenderManager.CameraMatrices);

            bool disableRendering = Context.Plugin.Config.DisableRendering;
            if (ImGui.Checkbox("Disable Rendering", ref disableRendering)) {
                Context.Plugin.GameRenderManager.DisableRendering(disableRendering);
                Context.Plugin.Config.Save();
            }
        }
    }

    private static void DrawScreenCricle(Vector3 position, uint color = 0xFF33FF33) {
        bool visible = DalamudApi.GameGui.WorldToScreen(position, out Vector2 screenPos);
        if (!visible)
            return;

        ImGui.GetWindowDrawList().AddCircleFilled(screenPos, 3f, color);
        ImGui.GetWindowDrawList().AddText(screenPos + new Vector2(10, -8), color,
            $"{position.ToString("G", CultureInfo.InvariantCulture)} [{(position - DalamudApi.ObjectTable[0].Position).Length():N2}]");
    }

    // private static void HookCheckbox<T>(string label, HookEntry<T> entry) where T : Delegate {
    //     var value = entry.IsEnabled;
    //     if (ImGui.Checkbox(label, ref value))
    //         entry.Toggle();
    // }
}
