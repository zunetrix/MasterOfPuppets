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
        string logPrefix = "mopformationgoto",
        FormationAnchorReference? fallbackAnchor = null) {
        if (!TryGetFormation(plugin, formationName, logPrefix, out var formation))
            return false;

        if (!FormationAnchorResolver.TryResolve(plugin, formation, anchor, out var resolvedAnchor, out var anchorFailure, out var failureKind)) {
            if (fallbackAnchor != null && FormationAnchorResolver.TryResolve(plugin, formation, fallbackAnchor, out var fallbackResolved, out _, out _)) {
                resolvedAnchor = fallbackResolved;
            } else {
                LogAnchorFailure(logPrefix, anchorFailure, failureKind);
                return false;
            }
        }

        var localPlayer = DalamudApi.ObjectTable.LocalPlayer;
        if (localPlayer != null && ShouldSkipLocalAnchor(anchor.Kind, resolvedAnchor.GameObjectId, localPlayer.GameObjectId)) {
            if (plugin.FormationTrackingSession.IsActive)
                plugin.FormationTrackingSession.Stop();
            if (plugin.SimpleInputMovement.IsMoving)
                plugin.SimpleInputMovement.StopMove();
            DalamudApi.PluginLog.Debug($"[{logPrefix}] local character is the selected formation anchor; no movement needed");
            return true;
        }

        var localCid = DalamudApi.PlayerState.ContentId;
        var isPointOneUnassigned = formation.Points.Count > 0
            && (formation.Points[0].Cids == null || formation.Points[0].Cids.Count == 0)
            && (formation.Points[0].GroupIds == null || formation.Points[0].GroupIds.Count == 0);
        var assignedAnchorPointIndex = (!isPointOneUnassigned && resolvedAnchor.ContentId.HasValue)
            ? FormationExecution.GetAssignedPointIndex(formation, resolvedAnchor.ContentId.Value, plugin.Config.CidsGroups)
            : -1;
        var anchorPointIndex = ResolveAnchorPointIndex(formation, plugin.Config.CidsGroups, anchor, resolvedAnchor.ContentId, localCid);

        var normalizeAnchorRotation = ShouldNormalizeAssignedAnchorRotation(anchor, resolvedAnchor.ContentId, localCid, assignedAnchorPointIndex);
        var anchorRotation = ResolveAnchorFrameRotation(
            formation,
            anchorPointIndex,
            resolvedAnchor.Rotation,
            normalizeAnchorRotation);

        return ExecuteAnchoredMove(
            plugin,
            formation,
            destinationPointIndex,
            anchorPointIndex,
            resolvedAnchor.Position,
            anchorRotation,
            movementMode,
            logPrefix,
            resolvedAnchor,
            normalizeAnchorRotation);
    }

    public static bool ShouldSkipLocalAnchor(
        FormationAnchorKind anchorKind,
        ulong? anchorGameObjectId,
        ulong localGameObjectId) =>
        anchorKind != FormationAnchorKind.Self
        && anchorGameObjectId.HasValue
        && anchorGameObjectId.Value == localGameObjectId;

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

        if (!FormationAnchorResolver.TryResolve(plugin, formation, anchor, out var resolvedAnchor, out var anchorFailure, out var failureKind)) {
            LogAnchorFailure(logPrefix, anchorFailure, failureKind);
            return false;
        }

        var assignedAnchorPointIndex = resolvedAnchor.ContentId.HasValue
            ? FormationExecution.GetAssignedPointIndex(formation, resolvedAnchor.ContentId.Value, plugin.Config.CidsGroups)
            : -1;
        var anchorPointIndex = ResolveAnchorPointIndex(formation, plugin.Config.CidsGroups, anchor, resolvedAnchor.ContentId, localCid);
        if (anchor.Kind != FormationAnchorKind.Self) {
            var anchorCid = resolvedAnchor.ContentId;
            if (anchorCid == localCid) {
                if (plugin.FormationTrackingSession.IsActive)
                    plugin.FormationTrackingSession.Stop();
                if (plugin.SimpleInputMovement.IsMoving)
                    plugin.SimpleInputMovement.StopMove();
                DalamudApi.PluginLog.Debug($"[{logPrefix}] local character is the formation anchor for \"{formationName}\"; no movement needed");
                return true;
            }
        }

        var anchorRotation = ResolveAnchorFrameRotation(
            formation,
            anchorPointIndex,
            resolvedAnchor.Rotation,
            ShouldNormalizeAssignedAnchorRotation(anchor, resolvedAnchor.ContentId, localCid, assignedAnchorPointIndex));
        return ExecuteAnchoredMove(
            plugin,
            formation,
            destinationPointIndex,
            anchorPointIndex,
            resolvedAnchor.Position,
            anchorRotation,
            movementMode,
            logPrefix,
            resolvedAnchor,
            ShouldNormalizeAssignedAnchorRotation(anchor, resolvedAnchor.ContentId, localCid, assignedAnchorPointIndex));
    }

    public static int ResolveAnchorPointIndex(
        Formation formation,
        System.Collections.Generic.IReadOnlyList<CidGroup>? groups,
        FormationAnchorReference anchor,
        ulong? anchorCid,
        ulong localCid) {
        if (anchor.Kind == FormationAnchorKind.Self
            || anchor.Kind == FormationAnchorKind.Target
            || anchor.Kind == FormationAnchorKind.FocusTarget
            || anchor.Kind == FormationAnchorKind.Default)
            return FormationPointMovement.AnchorPointIndex;

        // If Point 1 has no assigned characters, Point 1 is the dynamic origin/leader slot (0, 0, 0).
        if (formation.Points.Count > 0
            && (formation.Points[0].Cids == null || formation.Points[0].Cids.Count == 0)
            && (formation.Points[0].GroupIds == null || formation.Points[0].GroupIds.Count == 0)) {
            return FormationPointMovement.AnchorPointIndex;
        }

        if (!anchorCid.HasValue)
            return FormationPointMovement.AnchorPointIndex;

        var assignedAnchorPointIndex = FormationExecution.GetAssignedPointIndex(formation, anchorCid.Value, groups);
        return assignedAnchorPointIndex >= 0 ? assignedAnchorPointIndex : FormationPointMovement.AnchorPointIndex;
    }

    public static float ResolveAnchorFrameRotation(
        Formation formation,
        int anchorPointIndex,
        float anchorActorRotation,
        bool normalizeAssignedAnchorRotation) {
        if (!normalizeAssignedAnchorRotation || anchorPointIndex < 0 || anchorPointIndex >= formation.Points.Count)
            return anchorActorRotation;

        return FormationMath.GetFormationFrameRotation(formation.Points[anchorPointIndex], anchorActorRotation);
    }

    private static bool ShouldNormalizeAssignedAnchorRotation(
        FormationAnchorReference anchor,
        ulong? anchorCid,
        ulong localCid,
        int assignedAnchorPointIndex) =>
        anchor.Kind != FormationAnchorKind.Self
        && anchorCid.HasValue
        && anchorCid.Value != localCid
        && assignedAnchorPointIndex >= 0;

    public static bool ExecuteAnchoredMove(
        Plugin plugin,
        Formation formation,
        int destinationPointIndex,
        int anchorPointIndex,
        Vector3 anchorWorldPosition,
        float anchorWorldRotation,
        SimpleMovementMode movementMode,
        string logPrefix,
        FormationResolvedAnchor? resolvedAnchor = null,
        bool normalizeAnchorRotation = false) {
        var move = FormationPointMovement.BuildAnchoredWorldMove(
            formation,
            destinationPointIndex,
            anchorPointIndex,
            anchorWorldPosition,
            anchorWorldRotation);
        if (move == null) {
            DalamudApi.PluginLog.Warning($"[{logPrefix}] point {destinationPointIndex + 1} is not valid for formation \"{formation.Name}\"");
            return false;
        }

        var trackingKey = $"{logPrefix}:{formation.Name}:{anchorPointIndex}:{destinationPointIndex}";
        if (SimpleInputMovement.UsesLiveFormationTracking(movementMode) && resolvedAnchor != null) {
            plugin.FormationTrackingSession.Start(
                formation,
                destinationPointIndex,
                anchorPointIndex,
                resolvedAnchor.ContentId,
                resolvedAnchor.GameObjectId,
                resolvedAnchor.Name,
                resolvedAnchor.Position,
                anchorWorldRotation,
                resolvedAnchor.Rotation,
                normalizeAnchorRotation,
                trackingKey);
        } else {
            plugin.FormationTrackingSession.Stop();
            MoveToComputed(
                plugin,
                move.Value.Position,
                move.Value.Rotation,
                movementMode,
                trackingKey);
        }
        DalamudApi.PluginLog.Debug($"[{logPrefix}] formation=\"{formation.Name}\" point={destinationPointIndex + 1} movementMode={movementMode}");
        return true;
    }

    public static void MoveToComputed(
        Plugin plugin,
        Vector3 position,
        float rotation,
        SimpleMovementMode movementMode = SimpleMovementMode.Precise,
        string? trackingKey = null,
        bool useFormationRelativeMovement = false) {
        plugin.SimpleInputMovement.MoveTo(
            position,
            precision: plugin.Config.FormationMovePrecision,
            faceDirection: rotation,
            movementMode: movementMode,
            stopOnStuck: plugin.Config.StopOnStuck,
            stuckTolerance: plugin.Config.StuckTolerance,
            stuckTimeoutMs: plugin.Config.StuckTimeoutMs,
            trackingKey: trackingKey,
            useFormationRelativeMovement: useFormationRelativeMovement);
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
            or FormationAnchorFailureKind.NoFocusTargetSelected
            or FormationAnchorFailureKind.AnchorNameEmpty
            or FormationAnchorFailureKind.AnchorNotVisible;
}
