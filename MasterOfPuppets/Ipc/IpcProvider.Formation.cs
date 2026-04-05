using System;
using System.Globalization;
using System.Linq;
using System.Numerics;

using MasterOfPuppets.Extensions;
using MasterOfPuppets.Movement;

namespace MasterOfPuppets.Ipc;

internal partial class IpcProvider {

    /// <summary>
    /// Broadcasts a formation execution to all local clients (excluding the leader).
    /// The leader's world position, facing, and content ID are included so each
    /// client can rotate its assigned offset into world space.
    /// </summary>
    public void ExecuteFormation(string name) {
        var player = DalamudApi.ObjectTable.LocalPlayer;
        if (player == null) return;
        var pos = player.Position;
        var rot = player.Rotation;
        var cid = DalamudApi.PlayerState.ContentId;
        BroadCast(IpcMessage.Create(IpcMessageType.ExecuteFormation,
            name,
            pos.X.ToString("G", CultureInfo.InvariantCulture),
            pos.Y.ToString("G", CultureInfo.InvariantCulture),
            pos.Z.ToString("G", CultureInfo.InvariantCulture),
            rot.ToString("G", CultureInfo.InvariantCulture),
            cid.ToString(CultureInfo.InvariantCulture)).Serialize(), includeSelf: false);
    }

    [IpcHandle(IpcMessageType.ExecuteFormation)]
    private void HandleExecuteFormation(IpcMessage message) {
        if (message.StringData == null || message.StringData.Length < 6) return;
        var name = message.StringData[0];
        if (!float.TryParse(message.StringData[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float lx)) return;
        if (!float.TryParse(message.StringData[2], NumberStyles.Float, CultureInfo.InvariantCulture, out float ly)) return;
        if (!float.TryParse(message.StringData[3], NumberStyles.Float, CultureInfo.InvariantCulture, out float lz)) return;
        if (!float.TryParse(message.StringData[4], NumberStyles.Float, CultureInfo.InvariantCulture, out float leaderRot)) return;
        if (!ulong.TryParse(message.StringData[5], NumberStyles.Integer, CultureInfo.InvariantCulture, out ulong leaderCid)) return;

        var formation = Plugin.Config.Formations.FirstOrDefault(f =>
            string.Equals(f.Name, name, StringComparison.OrdinalIgnoreCase));
        if (formation == null) {
            DalamudApi.PluginLog.Warning($"[ExecuteFormation] Formation '{name}' not found.");
            return;
        }

        var playerCid = DalamudApi.PlayerState.ContentId;
        if (playerCid == leaderCid) return;

        var point = formation.Points.FirstOrDefault(p =>
            p.GetEffectiveCids(Plugin.Config.CidsGroups).Contains(playerCid));
        if (point == null) return;

        var leaderPos = new Vector3(lx, ly, lz);
        var worldPos = point.Offset.ApplyLeaderRotation(leaderRot, leaderPos);

        // Match DrawWorldOverlay: leaderRot + point.Angle * DegToRad
        float facingRad = leaderRot + point.Angle * Angle.DegToRad;
        // Plugin.MovementManager.MoveTo(worldPos, facingRad.Radians());
        Plugin.SimpleInputMovement.MoveTo(worldPos, faceDirection: facingRad);

        // Member faces the same direction as the leader (north offset = 0)
        // Plugin.MovementManager.MoveTo(worldPos, leaderRot.Radians());
    }
}
