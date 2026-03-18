using System;
using System.Globalization;
using System.Linq;
using System.Numerics;

using MasterOfPuppets.Movement;

namespace MasterOfPuppets.Ipc;

internal partial class IpcProvider {

    /// <summary>
    /// Broadcasts a formation execution to all local clients.
    /// The leader's current world position is included so each client can resolve its assigned point.
    /// </summary>
    public void ExecuteFormation(string name) {
        var player = DalamudApi.ObjectTable.LocalPlayer;
        if (player == null) return;
        var pos = player.Position;
        BroadCast(IpcMessage.Create(IpcMessageType.ExecuteFormation,
            name,
            pos.X.ToString("G", CultureInfo.InvariantCulture),
            pos.Y.ToString("G", CultureInfo.InvariantCulture),
            pos.Z.ToString("G", CultureInfo.InvariantCulture)).Serialize(), includeSelf: true);
    }

    [IpcHandle(IpcMessageType.ExecuteFormation)]
    private void HandleExecuteFormation(IpcMessage message) {
        if (message.StringData == null || message.StringData.Length < 4) return;

        var name = message.StringData[0];
        if (!float.TryParse(message.StringData[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float lx)) return;
        if (!float.TryParse(message.StringData[2], NumberStyles.Float, CultureInfo.InvariantCulture, out float ly)) return;
        if (!float.TryParse(message.StringData[3], NumberStyles.Float, CultureInfo.InvariantCulture, out float lz)) return;

        var formation = Plugin.Config.Formations.FirstOrDefault(f =>
            string.Equals(f.Name, name, StringComparison.OrdinalIgnoreCase));
        if (formation == null) {
            DalamudApi.PluginLog.Warning($"[ExecuteFormation] Formation '{name}' not found.");
            return;
        }

        var playerCid = DalamudApi.PlayerState.ContentId;
        var point = formation.Points.FirstOrDefault(p =>
            p.GetEffectiveCids(Plugin.Config.CidsGroups).Contains(playerCid));
        if (point == null) return;

        var leaderPos = new Vector3(lx, ly, lz);
        Plugin.MovementManager.MoveTo(leaderPos + point.Offset, point.Angle.Degrees());
    }
}
