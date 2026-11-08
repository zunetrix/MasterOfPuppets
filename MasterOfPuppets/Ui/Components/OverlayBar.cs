using System;
using System.Linq;
using System.Numerics;

using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;

using MasterOfPuppets.Util.ImGuiExt;

namespace MasterOfPuppets;

/// <summary>
/// Shared pieces of the screen-docked overlays (<see cref="AetheryteOverlayWindow"/>, <see cref="HousingOverlayWindow"/>):
/// window flags, side docking and the category / action buttons.
/// </summary>
internal static class OverlayBar {
    public const ImGuiWindowFlags BaseFlags =
        ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.AlwaysAutoResize |
        ImGuiWindowFlags.NoFocusOnAppearing | ImGuiWindowFlags.NoScrollbar;

    private static float Scale => ImGuiHelpers.GlobalScale;
    public static float ScreenMargin => 8f * Scale;
    public static float ButtonWidth => 150f * Scale;
    public static float IconButtonSize => 44f * Scale;

    /// <summary>Window flags for the dock mode; when docked also sets the next window position (call from PreDraw).</summary>
    public static ImGuiWindowFlags ApplyDock(AetheryteOverlayDock dock, float offsetY) {
        if (dock == AetheryteOverlayDock.Free) return BaseFlags;

        var viewport = ImGui.GetMainViewport();
        var y = viewport.Pos.Y + viewport.Size.Y * (0.5f + Math.Clamp(offsetY, -0.5f, 0.5f));
        var (x, pivotX) = dock == AetheryteOverlayDock.Left
            ? (viewport.Pos.X + ScreenMargin, 0f)
            : (viewport.Pos.X + viewport.Size.X - ScreenMargin, 1f);
        ImGui.SetNextWindowPos(new Vector2(x, y), ImGuiCond.Always, new Vector2(pivotX, 0.5f));
        return BaseFlags | ImGuiWindowFlags.NoMove;
    }

    /// <summary>Bar button: icon + label, or a square icon-only button.</summary>
    public static bool DrawButton(string id, FontAwesomeIcon icon, string label, Vector4 iconColor, bool iconsOnly) =>
        iconsOnly
            ? DrawIconOnlyButton(id, icon, iconColor)
            : ImGuiUtil.DrawCmdButton(icon, label, null, iconColor, ButtonWidth);

    /// <summary>Square icon-only button, same look as <see cref="ImGuiUtil.DrawCmdButton"/>.</summary>
    private static bool DrawIconOnlyButton(string id, FontAwesomeIcon icon, Vector4 iconColor) {
        var size = new Vector2(IconButtonSize, IconButtonSize);
        var rounding = 6f * Scale;
        var pos = ImGui.GetCursorScreenPos();

        bool clicked;
        using (ImGuiUtil.PushCmdButtonColors())
        using (ImRaii.PushStyle(ImGuiStyleVar.FrameRounding, rounding)) {
            clicked = ImGui.Button($"##overlayButton_{id}", size);
        }

        var hovered = ImGui.IsItemHovered();
        var drawList = ImGui.GetWindowDrawList();
        var borderCol = ImGuiUtil.GetCmdButtonBorderColor(hovered);
        drawList.AddRect(pos, pos + size, borderCol, rounding, ImDrawFlags.None, hovered ? 1.2f : 0.5f);

        using (ImRaii.PushFont(UiBuilder.IconFont)) {
            var iconStr = icon.ToIconString();
            var glyphSize = ImGui.CalcTextSize(iconStr);
            drawList.AddText(pos + (size - glyphSize) * 0.5f, ImGui.ColorConvertFloat4ToU32(ThemeManager.ReadableIconColor(iconColor)), iconStr);
        }

        return clicked;
    }

    /// <summary>Per-character overlay toggle (shared by all overlays; unknown characters default to shown).</summary>
    public static bool IsEnabledForCurrentCharacter(Configuration config) {
        var charConfig = config.Characters.FirstOrDefault(c => c.Cid == DalamudApi.PlayerState.ContentId);
        return charConfig == null || charConfig.ShowTeleportOverlay;
    }
}
