using System.Linq;
using System.Numerics;

using MasterOfPuppets.Movement;

namespace MasterOfPuppets.Formations;

public static class FormationLocalMovementExecutor {
    public static bool ExecuteFormationGoto(
        Plugin plugin,
        string formationName,
        int destinationPointIndex,
        FormationAnchorReference anchor,
        SimpleMovementMode movementMode,
        string logPrefix = "mopformationgoto") {
        if (!TryGetFormation(plugin, formationName, logPrefix, out var formation))
            return false;

        if (!FormationAnchorResolver.TryResolve(plugin, formation, anchor, out var resolvedAnchor, out var anchorFailure, out var failureKind)) {
            LogAnchorFailure(logPrefix, anchorFailure, failureKind);
            return false;
        }

        return ExecutePointOneAnchoredMove(
            plugin,
            formation,
            destinationPointIndex,
            resolvedAnchor.Position,
            resolvedAnchor.Rotation,
            movementMode,
            logPrefix);
    }

    public static bool ExecuteChatSyncedFormation(
        Plugin plugin,
        string formationName,
        FormationAnchorReference anchor,
        SimpleMovementMode movementMode) {
        const string logPrefix = "mopformation";
        if (!TryGetFormation(plugin, formationName, logPrefix, out var formation))
            return false;

        var localCid = DalamudApi.PlayerState.ContentId;
        var destinationPointIndex = FormationExecution.GetAssignedPointIndex(formation, localCid, plugin.Config.CidsGroups);
        if (destinationPointIndex < 0) {
            DalamudApi.PluginLog.Debug($"[{logPrefix}] local character is not assigned to formation \"{formationName}\"");
            return false;
        }

        if (anchor.Kind == FormationAnchorKind.Default && destinationPointIndex == FormationPointMovement.AnchorPointIndex) {
            DalamudApi.PluginLog.Debug($"[{logPrefix}] local character is point 1 anchor for formation \"{formationName}\"; no movement needed");
            return true;
        }

        if (!FormationAnchorResolver.TryResolve(plugin, formation, anchor, out var resolvedAnchor, out var anchorFailure, out var failureKind)) {
            LogAnchorFailure(logPrefix, anchorFailure, failureKind);
            return false;
        }

        return ExecutePointOneAnchoredMove(
            plugin,
            formation,
            destinationPointIndex,
            resolvedAnchor.Position,
            resolvedAnchor.Rotation,
            movementMode,
            logPrefix);
    }

    public static bool ExecutePointOneAnchoredMove(
        Plugin plugin,
        Formation formation,
        int destinationPointIndex,
        Vector3 anchorWorldPosition,
        float anchorWorldRotation,
        SimpleMovementMode movementMode,
        string logPrefix) {
        var move = FormationPointMovement.BuildPointOneAnchoredWorldMove(
            formation,
            destinationPointIndex,
            anchorWorldPosition,
            anchorWorldRotation);
        if (move == null) {
            DalamudApi.PluginLog.Warning($"[{logPrefix}] point {destinationPointIndex + 1} is not valid for formation \"{formation.Name}\"");
            return false;
        }

        MoveToComputed(plugin, move.Value.Position, move.Value.Rotation, movementMode);
        DalamudApi.PluginLog.Debug($"[{logPrefix}] formation=\"{formation.Name}\" point={destinationPointIndex + 1} movementMode={movementMode}");
        return true;
    }

    public static void MoveToComputed(
        Plugin plugin,
        Vector3 position,
        float rotation,
        SimpleMovementMode movementMode = SimpleMovementMode.Precise) {
        plugin.SimpleInputMovement.MoveTo(
            position,
            precision: plugin.Config.FormationMovePrecision,
            faceDirection: rotation,
            movementMode: movementMode,
            stopOnStuck: plugin.Config.StopOnStuck,
            stuckTolerance: plugin.Config.StuckTolerance,
            stuckTimeoutMs: plugin.Config.StuckTimeoutMs);
    }

    private static bool TryGetFormation(
        Plugin plugin,
        string formationName,
        string logPrefix,
        out Formation formation) {
        formation = plugin.Config.Formations.FirstOrDefault(f =>
            string.Equals(f.Name, formationName, System.StringComparison.OrdinalIgnoreCase))!;
        if (formation != null)
            return true;

        DalamudApi.PluginLog.Warning($"[{logPrefix}] formation not found: \"{formationName}\"");
        return false;
    }

    private static void LogAnchorFailure(
        string logPrefix,
        string anchorFailure,
        FormationAnchorFailureKind failureKind) {
        var message = $"[{logPrefix}] {anchorFailure}";
        if (IsTransientAnchorFailure(failureKind)) {
            DalamudApi.PluginLog.Debug(message);
        } else {
            DalamudApi.PluginLog.Warning(message);
        }
    }

    public static bool IsTransientAnchorFailure(FormationAnchorFailureKind failureKind) =>
        failureKind is FormationAnchorFailureKind.NoTargetSelected
            or FormationAnchorFailureKind.AnchorNameEmpty
            or FormationAnchorFailureKind.AnchorNotVisible;
}
