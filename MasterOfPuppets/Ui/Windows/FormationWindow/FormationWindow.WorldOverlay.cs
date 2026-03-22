using System;
using System.Linq;
using System.Numerics;

using Dalamud.Bindings.ImGui;

using MasterOfPuppets.Formations;
using MasterOfPuppets.Movement;

namespace MasterOfPuppets;

public partial class FormationWindow {

    // =========================================================================
    // World overlay - projects formation into 3D game world
    // =========================================================================

    private void DrawWorldOverlay(Formation formation) {
        var player = DalamudApi.ObjectTable.LocalPlayer;
        if (player == null) return;

        var bdl = ImGui.GetBackgroundDrawList(ImGui.GetMainViewport());
        var playerPos = player.Position;
        float R = player.Rotation;

        var localCid = DalamudApi.PlayerState.ContentId;
        var myPoint = formation.Points.FirstOrDefault(p => p.Cids.Contains(localCid));
        var myOffset = myPoint?.Offset ?? Vector3.Zero;
        float myAngle = myPoint?.Angle ?? 0f;

        // ToAbsolute: rotate canonical-north offsets into world space.
        // CreateRotationY(R + π) is identity at R=π (north)
        var mat = Matrix4x4.CreateRotationY(R + MathF.PI);

        for (int i = 0; i < formation.Points.Count; i++) {
            var pt = formation.Points[i];

            var worldPos = playerPos + Vector3.Transform(pt.Offset - myOffset, mat);
            uint c = i == _selPoint ? 0xFFFFAA00u : 0xFF3388FFu;

            // Angle: canonical 0°(north) → FFXIV π − relAngle offset
            float relAngle = myPoint != null ? pt.Angle - myAngle : pt.Angle;
            float worldRot = R - relAngle * Angle.DegToRad;

            if (!DrawWorldArrow(bdl, worldPos, worldRot, c, _markerSizeWorld)) continue;

            if (DalamudApi.GameGui.WorldToScreen(worldPos, out var centerPx)) {
                var label = (i + 1).ToString();
                var labelSz = ImGui.CalcTextSize(label);
                bdl.AddText(centerPx - labelSz * 0.5f, 0xFFFFFFFF, label);
            }
        }
    }

    /// <summary>
    /// Projects arrow into screen space by rotating each 3D vertex independently
    /// with WorldToScreen - perspective-accurate.
    /// <paramref name="worldRot"/> uses CreateRotationY convention: tip starts at +Z (south),
    /// π rotates the tip to -Z (north).
    /// </summary>
    private static bool DrawWorldArrow(ImDrawListPtr dl, Vector3 worldPos, float worldRot, uint color, float sizeM) {
        if (!DalamudApi.GameGui.WorldToScreen(worldPos, out _)) return false;

        var mat = Matrix4x4.CreateRotationY(worldRot);

        // Arrow tip at +Z (south at worldRot=0); after rotation worldRot=π → tip at north
        var local = new Vector3[] {
            new(0, 0, 1), new(1, 0, -1), new(0, 0, -0.5f), new(-1, 0, -1),
        };
        var pts = new Vector2[4];
        for (int i = 0; i < 4; i++) {
            var v = Vector3.Transform(local[i] * (sizeM * 0.5f), mat) + worldPos;
            DalamudApi.GameGui.WorldToScreen(v, out pts[i]);
        }

        dl.AddConvexPolyFilled(ref pts[0], 4, color);
        dl.AddPolyline(ref pts[0], 4, 0xFFFFFFFF, ImDrawFlags.Closed, 1.5f);
        return true;
    }
}
