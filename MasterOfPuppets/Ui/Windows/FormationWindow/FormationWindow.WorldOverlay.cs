using System;
using System.Linq;
using System.Numerics;

using Dalamud.Bindings.ImGui;

using MasterOfPuppets.Formations;

namespace MasterOfPuppets;

public partial class FormationWindow {
    // DrawWorldArrow: CreateRotationY is CCW when viewed from above in math space,
    // but FFXIV Z is inverted so it effectively becomes CW - pass worldRot directly.
    private static bool DrawWorldArrow(ImDrawListPtr dl, Vector3 worldPos, float worldRot, uint color, float sizeM) {
        if (!DalamudApi.GameGui.WorldToScreen(worldPos, out _)) return false;

        var mat = Matrix4x4.CreateRotationY(worldRot);
        float h = sizeM * 0.5f;

        var pts = new Vector2[4];
        for (int i = 0; i < 4; i++) {
            var v = Vector3.Transform(ArrowVertices3D[i] * h, mat) + worldPos;
            DalamudApi.GameGui.WorldToScreen(v, out pts[i]);
        }

        dl.AddPolyline(ref pts[0], 4, color, ImDrawFlags.Closed, 2f);
        return true;
    }

    private void DrawWorldOverlay(Formation formation) {
        var player = DalamudApi.ObjectTable.LocalPlayer;
        if (player == null) return;
        var bdl = ImGui.GetBackgroundDrawList(ImGui.GetMainViewport());
        var playerPos = player.Position;
        float leaderRot = player.Rotation;
        var localCid = DalamudApi.PlayerState.ContentId;
        var myPoint = formation.Points.FirstOrDefault(p => p.Cids.Contains(localCid));

        for (int i = 0; i < formation.Points.Count; i++) {
            var pt = formation.Points[i];

            Vector3 worldPos;
            float facingRad;
            if (formation.ExecutionMode == FormationExecutionMode.RelativeToLocalAssignedPoint && myPoint != null) {
                (worldPos, facingRad) = FormationMath.GetMopRelativeWorld(myPoint, pt, playerPos, leaderRot);
            } else {
                (worldPos, facingRad) = FormationMath.ToMopWorld(pt, playerPos, leaderRot);
            }

            uint c = i == _selPoint ? 0xFFFFAA00u : 0xFF3388FFu;

            if (!DrawWorldArrow(bdl, worldPos, facingRad, c, _markerSizeWorld)) continue;
            if (DalamudApi.GameGui.WorldToScreen(worldPos, out var centerPx)) {
                var label = (i + 1).ToString();
                var labelSz = ImGui.CalcTextSize(label);
                bdl.AddText(centerPx - labelSz * 0.5f, 0xFFFFFFFF, label);
            }
        }
    }
}
