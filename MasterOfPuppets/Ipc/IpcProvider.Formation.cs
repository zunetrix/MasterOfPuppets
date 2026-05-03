using System;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;

using Dalamud.Interface.ImGuiNotification;

using MasterOfPuppets.Formations;

namespace MasterOfPuppets.Ipc;

internal partial class IpcProvider {

    /// <summary>
    /// Broadcasts a formation execution to all local clients.
    /// The anchor's world position, facing, and content ID are included so each
    /// client can place its assigned point relative to the anchor's saved point.
    /// </summary>
    public void ExecuteFormation(string name, bool useTargetAnchor = false) {
        _ = DalamudApi.Framework.RunOnFrameworkThread(() => ExecuteFormationOnFrameworkThread(name, useTargetAnchor));
    }

    private void ExecuteFormationOnFrameworkThread(string name, bool useTargetAnchor = false) {
        var player = DalamudApi.ObjectTable.LocalPlayer;
        if (player == null) return;

        var formation = Plugin.Config.Formations.FirstOrDefault(f =>
            string.Equals(f.Name, name, StringComparison.OrdinalIgnoreCase));
        if (formation == null) {
            DalamudApi.ShowNotification($"Formation '{name}' not found.", NotificationType.Error, 5000);
            return;
        }

        var issuerCid = DalamudApi.PlayerState.ContentId;
        if (issuerCid == 0)
            return;

        if (GetAssignedPoint(formation, issuerCid) == null) {
            DalamudApi.ShowNotification($"Current character is not assigned to formation '{name}'.", NotificationType.Error, 5000);
            return;
        }

        if (!TryGetFormationAnchor(useTargetAnchor, issuerCid, out var anchorPos, out var anchorRot, out var anchorCid))
            return;

        BroadCast(IpcMessage.Create(IpcMessageType.ExecuteFormation,
            name,
            anchorPos.X.ToString("G", CultureInfo.InvariantCulture),
            anchorPos.Y.ToString("G", CultureInfo.InvariantCulture),
            anchorPos.Z.ToString("G", CultureInfo.InvariantCulture),
            anchorRot.ToString("G", CultureInfo.InvariantCulture),
            anchorCid.ToString(CultureInfo.InvariantCulture)).Serialize(), includeSelf: true);
    }

    [IpcHandle(IpcMessageType.ExecuteFormation)]
    private void HandleExecuteFormation(IpcMessage message) {
        _ = DalamudApi.Framework.RunOnFrameworkThread(() => HandleExecuteFormationOnFrameworkThread(message));
    }

    private void HandleExecuteFormationOnFrameworkThread(IpcMessage message) {
        if (message.StringData == null || message.StringData.Length < 6) return;
        var name = message.StringData[0];
        if (!float.TryParse(message.StringData[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float lx)) return;
        if (!float.TryParse(message.StringData[2], NumberStyles.Float, CultureInfo.InvariantCulture, out float ly)) return;
        if (!float.TryParse(message.StringData[3], NumberStyles.Float, CultureInfo.InvariantCulture, out float lz)) return;
        if (!float.TryParse(message.StringData[4], NumberStyles.Float, CultureInfo.InvariantCulture, out float anchorRot)) return;
        if (!ulong.TryParse(message.StringData[5], NumberStyles.Integer, CultureInfo.InvariantCulture, out ulong anchorCid)) return;

        var formation = Plugin.Config.Formations.FirstOrDefault(f =>
            string.Equals(f.Name, name, StringComparison.OrdinalIgnoreCase));
        if (formation == null) {
            DalamudApi.PluginLog.Warning($"[ExecuteFormation] Formation '{name}' not found.");
            return;
        }

        var playerCid = DalamudApi.PlayerState.ContentId;
        if (playerCid == anchorCid) return;

        var point = GetAssignedPoint(formation, playerCid);
        if (point == null) return;

        var anchorPoint = GetAssignedPoint(formation, anchorCid);
        if (anchorPoint == null) return;

        var anchorPos = new Vector3(lx, ly, lz);
        var (worldPos, facingRad) = FormationMath.GetMopRelativeWorld(anchorPoint, point, anchorPos, anchorRot);
        // DalamudApi.PluginLog.Warning($"[ExecuteFormation] anchorPos: {anchorPos} worldPos: {worldPos} faceDirection: {facingRad}");

        // Plugin.MovementManager.MoveTo(worldPos, facingRad.Radians());
        Plugin.SimpleInputMovement.MoveTo(worldPos, precision: Plugin.Config.FormationMovePrecision, faceDirection: facingRad);

        // Member faces the same direction as the anchor (north offset = 0)
        // Plugin.MovementManager.MoveTo(worldPos, anchorRot.Radians());
    }

    public Task ExecuteFormationMove(string name, bool reverse = false, int step = 1, int sequenceIndex = 0) =>
        DalamudApi.Framework.RunOnFrameworkThread(() => ExecuteFormationMoveOnFrameworkThread(name, reverse, step, sequenceIndex));

    private void ExecuteFormationMoveOnFrameworkThread(string name, bool reverse = false, int step = 1, int sequenceIndex = 0) {
        var player = DalamudApi.ObjectTable.LocalPlayer;
        if (player == null) return;

        var formation = Plugin.Config.Formations.FirstOrDefault(f =>
            string.Equals(f.Name, name, StringComparison.OrdinalIgnoreCase));
        if (formation == null) {
            DalamudApi.ShowNotification($"Formation '{name}' not found.", NotificationType.Error, 5000);
            return;
        }

        var issuerCid = DalamudApi.PlayerState.ContentId;
        if (issuerCid == 0)
            return;

        if (GetAssignedPoint(formation, issuerCid) == null) {
            DalamudApi.ShowNotification($"Current character is not assigned to formation '{name}'.", NotificationType.Error, 5000);
            return;
        }

        var anchorPos = player.Position;
        var anchorRot = player.Rotation;
        BroadCast(IpcMessage.Create(IpcMessageType.ExecuteFormationMove,
            name,
            anchorPos.X.ToString("G", CultureInfo.InvariantCulture),
            anchorPos.Y.ToString("G", CultureInfo.InvariantCulture),
            anchorPos.Z.ToString("G", CultureInfo.InvariantCulture),
            anchorRot.ToString("G", CultureInfo.InvariantCulture),
            issuerCid.ToString(CultureInfo.InvariantCulture),
            reverse ? "1" : "0",
            Math.Max(1, step).ToString(CultureInfo.InvariantCulture),
            sequenceIndex.ToString(CultureInfo.InvariantCulture)).Serialize(), includeSelf: true);
    }

    [IpcHandle(IpcMessageType.ExecuteFormationMove)]
    private void HandleExecuteFormationMove(IpcMessage message) {
        _ = DalamudApi.Framework.RunOnFrameworkThread(() => HandleExecuteFormationMoveOnFrameworkThread(message));
    }

    private void HandleExecuteFormationMoveOnFrameworkThread(IpcMessage message) {
        if (message.StringData == null || message.StringData.Length < 9) return;
        var name = message.StringData[0];
        if (!float.TryParse(message.StringData[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float lx)) return;
        if (!float.TryParse(message.StringData[2], NumberStyles.Float, CultureInfo.InvariantCulture, out float ly)) return;
        if (!float.TryParse(message.StringData[3], NumberStyles.Float, CultureInfo.InvariantCulture, out float lz)) return;
        if (!float.TryParse(message.StringData[4], NumberStyles.Float, CultureInfo.InvariantCulture, out float anchorRot)) return;
        if (!ulong.TryParse(message.StringData[5], NumberStyles.Integer, CultureInfo.InvariantCulture, out ulong anchorCid)) return;
        var reverse = message.StringData[6] == "1";
        if (!int.TryParse(message.StringData[7], NumberStyles.Integer, CultureInfo.InvariantCulture, out var step)) return;
        if (!int.TryParse(message.StringData[8], NumberStyles.Integer, CultureInfo.InvariantCulture, out var sequenceIndex)) return;

        var formation = Plugin.Config.Formations.FirstOrDefault(f =>
            string.Equals(f.Name, name, StringComparison.OrdinalIgnoreCase));
        if (formation == null) {
            DalamudApi.PluginLog.Warning($"[ExecuteFormationMove] Formation '{name}' not found.");
            return;
        }

        var playerCid = DalamudApi.PlayerState.ContentId;
        if (playerCid == anchorCid)
            return;

        var playerPointIndex = FormationExecution.GetAssignedPointIndex(formation, playerCid, Plugin.Config.CidsGroups);
        if (playerPointIndex < 0)
            return;

        var anchorPointIndex = FormationExecution.GetAssignedPointIndex(formation, anchorCid, Plugin.Config.CidsGroups);
        if (anchorPointIndex < 0)
            return;

        var move = FormationPath.BuildWorldMove(
            formation,
            anchorPointIndex,
            playerPointIndex,
            new Vector3(lx, ly, lz),
            anchorRot,
            step,
            reverse,
            sequenceIndex);
        if (move == null)
            return;

        Plugin.SimpleInputMovement.MoveTo(move.Value.Position, precision: Plugin.Config.FormationMovePrecision, faceDirection: move.Value.Rotation);
    }

    private FormationPoint? GetAssignedPoint(Formation formation, ulong playerCid) =>
        FormationExecution.GetAssignedPoint(formation, playerCid, Plugin.Config.CidsGroups);

    private bool TryGetFormationAnchor(bool useTargetAnchor, ulong issuerCid, out Vector3 position, out float rotation, out ulong cid) {
        position = default;
        rotation = default;
        cid = default;

        var player = DalamudApi.ObjectTable.LocalPlayer;
        if (player == null) return false;

        if (!useTargetAnchor) {
            position = player.Position;
            rotation = player.Rotation;
            cid = issuerCid;
            return cid != 0;
        }

        var targetObjectId = player.TargetObjectId;
        if (targetObjectId == 0) {
            DalamudApi.ShowNotification("No target selected for formation anchor.", NotificationType.Error, 5000);
            return false;
        }

        var target = player.TargetObject?.GameObjectId == targetObjectId
            ? player.TargetObject
            : DalamudApi.ObjectTable.FirstOrDefault(o => o?.GameObjectId == targetObjectId);
        if (target == null) {
            DalamudApi.ShowNotification("Could not resolve formation target.", NotificationType.Error, 5000);
            return false;
        }

        position = target.Position;
        rotation = target.Rotation;
        cid = issuerCid;
        return true;
    }
}
