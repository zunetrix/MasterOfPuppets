using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;

using MasterOfPuppets.Formations;

namespace MasterOfPuppets.Ipc;

internal partial class IpcProvider {

    /// <summary>
    /// Broadcasts a formation execution to all local clients (excluding the leader).
    /// The leader's world position, facing, and content ID are included so each
    /// client can rotate its assigned offset into world space.
    /// </summary>
    public void ExecuteFormation(string name, FormationExecutionMode? modeOverride = null) {
        var player = DalamudApi.ObjectTable.LocalPlayer;
        if (player == null) return;
        var formation = Plugin.Config.Formations.FirstOrDefault(f =>
            string.Equals(f.Name, name, StringComparison.OrdinalIgnoreCase));
        var mode = modeOverride ?? formation?.ExecutionMode ?? FormationExecutionMode.LeaderOrigin;
        var orderedCids = mode == FormationExecutionMode.ClientOrder
            ? string.Join(",", GetConnectedPeers()
                .Select(peer => peer.ContentId)
                .Append(DalamudApi.PlayerState.ContentId)
                .Distinct()
                .OrderBy(cidValue => cidValue)
                .Select(cidValue => cidValue.ToString(CultureInfo.InvariantCulture)))
            : string.Empty;
        var pos = player.Position;
        var rot = player.Rotation;
        var cid = DalamudApi.PlayerState.ContentId;
        BroadCast(IpcMessage.Create(IpcMessageType.ExecuteFormation,
            name,
            pos.X.ToString("G", CultureInfo.InvariantCulture),
            pos.Y.ToString("G", CultureInfo.InvariantCulture),
            pos.Z.ToString("G", CultureInfo.InvariantCulture),
            rot.ToString("G", CultureInfo.InvariantCulture),
            cid.ToString(CultureInfo.InvariantCulture),
            mode.ToString(),
            orderedCids).Serialize(), includeSelf: false);
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
        var mode = FormationExecutionMode.LeaderOrigin;
        if (message.StringData.Length >= 7)
            Enum.TryParse(message.StringData[6], ignoreCase: true, out mode);
        var clientOrder = message.StringData.Length >= 8
            ? ParseClientOrder(message.StringData[7])
            : [];

        var formation = Plugin.Config.Formations.FirstOrDefault(f =>
            string.Equals(f.Name, name, StringComparison.OrdinalIgnoreCase));
        if (formation == null) {
            DalamudApi.PluginLog.Warning($"[ExecuteFormation] Formation '{name}' not found.");
            return;
        }

        var playerCid = DalamudApi.PlayerState.ContentId;
        if (playerCid == leaderCid) return;

        var point = GetPointForExecutionMode(formation, mode, playerCid, clientOrder);
        if (point == null) return;

        var leaderPos = new Vector3(lx, ly, lz);
        Vector3 worldPos;
        float facingRad;

        if (mode == FormationExecutionMode.RelativeToLocalAssignedPoint) {
            var anchorPoint = formation.Points.FirstOrDefault(p => p.GetEffectiveCids(Plugin.Config.CidsGroups).Contains(leaderCid));
            if (anchorPoint == null)
                return;

            (worldPos, facingRad) = FormationMath.GetMopRelativeWorld(anchorPoint, point, leaderPos, leaderRot);
        } else {
            (worldPos, facingRad) = FormationMath.ToMopWorld(point, leaderPos, leaderRot);
        }
        // DalamudApi.PluginLog.Warning($"[ExecuteFormation] leaderPos: {leaderPos} worldPos: {worldPos} faceDirection: {facingRad}");

        // Plugin.MovementManager.MoveTo(worldPos, facingRad.Radians());
        Plugin.SimpleInputMovement.MoveTo(worldPos, precision: Plugin.Config.FormationMovePrecision, faceDirection: facingRad);

        // Member faces the same direction as the leader (north offset = 0)
        // Plugin.MovementManager.MoveTo(worldPos, leaderRot.Radians());
    }

    private FormationPoint? GetPointForExecutionMode(Formation formation, FormationExecutionMode mode, ulong playerCid, IReadOnlyList<ulong> clientOrder) {
        if (mode == FormationExecutionMode.ClientOrder) {
            var orderIndex = -1;
            for (int i = 0; i < clientOrder.Count; i++) {
                if (clientOrder[i] == playerCid) {
                    orderIndex = i;
                    break;
                }
            }
            return orderIndex >= 0 && orderIndex < formation.Points.Count
                ? formation.Points[orderIndex]
                : null;
        }

        return formation.Points.FirstOrDefault(p => p.GetEffectiveCids(Plugin.Config.CidsGroups).Contains(playerCid));
    }

    private static IReadOnlyList<ulong> ParseClientOrder(string csv) {
        if (string.IsNullOrWhiteSpace(csv))
            return [];

        return csv
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(s => ulong.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var cid) ? cid : 0)
            .Where(cid => cid != 0)
            .ToList();
    }
}
