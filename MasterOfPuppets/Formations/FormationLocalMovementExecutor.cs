using System;
using System.Linq;
using System.Numerics;

using MasterOfPuppets.Extensions.Dalamud;
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
        var isPointOneUnassigned = FormationAnchorRules.IsPointOneUnassigned(formation);
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
        var anchorPosition = IsExternalOriginAnchor(anchor, resolvedAnchor.ContentId)
            ? FormationPointMovement.AdjustExternalOriginPosition(
                formation,
                anchorPointIndex,
                resolvedAnchor.Position,
                anchorRotation)
            : resolvedAnchor.Position;

        return ExecuteAnchoredMove(
            plugin,
            formation,
            destinationPointIndex,
            anchorPointIndex,
            anchorPosition,
            anchorRotation,
            movementMode,
            logPrefix,
            resolvedAnchor,
            normalizeAnchorRotation,
            IsExternalOriginAnchor(anchor, resolvedAnchor.ContentId));
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
        SimpleMovementMode movementMode,
        FormationAnchorReference? fallbackAnchor = null) {
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
            if (FormationAnchorRules.ShouldUseLeaderFallbackOnTargetlessAnchor(formation, anchor.Kind)
                && fallbackAnchor != null
                && FormationAnchorResolver.TryResolve(plugin, formation, fallbackAnchor, out var fallbackResolved, out _, out _)) {
                resolvedAnchor = fallbackResolved;
                anchor = fallbackAnchor;
            } else {
                LogAnchorFailure(logPrefix, anchorFailure, failureKind);
                return false;
            }
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
        var anchorPosition = IsExternalOriginAnchor(anchor, resolvedAnchor.ContentId)
            ? FormationPointMovement.AdjustExternalOriginPosition(
                formation,
                anchorPointIndex,
                resolvedAnchor.Position,
                anchorRotation)
            : resolvedAnchor.Position;
        return ExecuteAnchoredMove(
            plugin,
            formation,
            destinationPointIndex,
            anchorPointIndex,
            anchorPosition,
            anchorRotation,
            movementMode,
            logPrefix,
            resolvedAnchor,
            ShouldNormalizeAssignedAnchorRotation(anchor, resolvedAnchor.ContentId, localCid, assignedAnchorPointIndex),
            IsExternalOriginAnchor(anchor, resolvedAnchor.ContentId));
    }

    public static bool ExecuteChatSyncedFormationSnapshot(
        Plugin plugin,
        string formationName,
        FormationResolvedAnchor resolvedAnchor,
        ulong anchorContentId,
        SimpleMovementMode movementMode,
        string eligibleMemberBits) {
        const string logPrefix = "mopformation";
        if (!TryGetFormation(plugin, formationName, logPrefix, out var formation))
            return false;

        var localCid = DalamudApi.PlayerState.ContentId;
        var destinationPointIndex = FormationExecution.GetAssignedPointIndex(formation, localCid, plugin.Config.CidsGroups);
        if (destinationPointIndex < 0)
            return false;
        if (!FormationChatSyncCodec.IsEligibleMember(
                formation,
                plugin.Config.CidsGroups,
                localCid,
                eligibleMemberBits)) {
            DalamudApi.PluginLog.Debug(
                $"[{logPrefix}] local formation member was not visible to the command issuer; ignoring snapshot");
            return false;
        }

        var assignedAnchorPointIndex = anchorContentId == 0
            ? -1
            : FormationExecution.GetAssignedPointIndex(formation, anchorContentId, plugin.Config.CidsGroups);
        if (assignedAnchorPointIndex >= 0 && anchorContentId == localCid) {
            plugin.FormationTrackingSession.Stop();
            if (plugin.SimpleInputMovement.IsMoving)
                plugin.SimpleInputMovement.StopMove();
            return true;
        }

        var anchorPointIndex = assignedAnchorPointIndex >= 0
            ? assignedAnchorPointIndex
            : FormationPointMovement.AnchorPointIndex;
        var normalizeAnchorRotation = assignedAnchorPointIndex >= 0;
        var anchorRotation = ResolveAnchorFrameRotation(
            formation,
            anchorPointIndex,
            resolvedAnchor.Rotation,
            normalizeAnchorRotation);
        var externalOrigin = assignedAnchorPointIndex < 0;
        var anchorPosition = externalOrigin
            ? FormationPointMovement.AdjustExternalOriginPosition(
                formation,
                anchorPointIndex,
                resolvedAnchor.Position,
                anchorRotation)
            : resolvedAnchor.Position;
        resolvedAnchor = resolvedAnchor with {
            ContentId = anchorContentId == 0 ? null : anchorContentId,
        };
        return ExecuteAnchoredMove(
            plugin,
            formation,
            destinationPointIndex,
            anchorPointIndex,
            anchorPosition,
            anchorRotation,
            movementMode,
            logPrefix,
            resolvedAnchor,
            normalizeAnchorRotation,
            externalOrigin);
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

    private static bool IsExternalOriginAnchor(FormationAnchorReference anchor, ulong? anchorCid) =>
        anchor.Kind != FormationAnchorKind.Self && !anchorCid.HasValue;

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
        bool normalizeAnchorRotation = false,
        bool externalOrigin = false) {
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
            var trackingAnchorPosition = externalOrigin
                ? resolvedAnchor.Position
                : anchorWorldPosition;
            plugin.FormationTrackingSession.Start(
                formation,
                destinationPointIndex,
                anchorPointIndex,
                resolvedAnchor.ContentId,
                resolvedAnchor.GameObjectId,
                resolvedAnchor.Name,
                trackingAnchorPosition,
                anchorWorldRotation,
                resolvedAnchor.Rotation,
                normalizeAnchorRotation,
                trackingKey,
                externalOrigin);
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

    public static bool ExecuteFormationPetPlace(
        Plugin plugin,
        string formationName,
        int destinationPointIndex,
        FormationAnchorReference anchor,
        string logPrefix = "moppetformationplace",
        FormationAnchorReference? fallbackAnchor = null) {
        if (!TryGetFormation(plugin, formationName, logPrefix, out var formation))
            return false;

        if (destinationPointIndex < 0 || destinationPointIndex >= formation.Points.Count) {
            DalamudApi.PluginLog.Warning($"[{logPrefix}] point {destinationPointIndex + 1} is not valid for formation \"{formationName}\"");
            return false;
        }

        if (!FormationAnchorResolver.TryResolve(plugin, formation, anchor, out var resolvedAnchor, out var anchorFailure, out var failureKind)) {
            if (FormationAnchorRules.ShouldUseLeaderFallbackOnTargetlessAnchor(formation, anchor.Kind)
                && fallbackAnchor != null
                && FormationAnchorResolver.TryResolve(plugin, formation, fallbackAnchor, out var fallbackResolved, out _, out _)) {
                resolvedAnchor = fallbackResolved;
                anchor = fallbackAnchor;
            } else {
                LogAnchorFailure(logPrefix, anchorFailure, failureKind);
                return false;
            }
        }

        var localCid = DalamudApi.PlayerState.ContentId;
        var isPointOneUnassigned = FormationAnchorRules.IsPointOneUnassigned(formation);
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

        var move = FormationPointMovement.BuildAnchoredWorldMove(
            formation,
            destinationPointIndex,
            anchorPointIndex,
            resolvedAnchor.Position,
            anchorRotation);
        if (move == null) {
            DalamudApi.PluginLog.Warning($"[{logPrefix}] point {destinationPointIndex + 1} is not valid for formation \"{formation.Name}\"");
            return false;
        }

        var destPoint = formation.Points[destinationPointIndex];
        var destCids = destPoint.GetEffectiveCids(plugin.Config.CidsGroups);

        // Determine who is at the destination point.
        // Each client targets that character and uses /petaction place <t> to move its own carbuncle
        // to that character's world position. This is the only mechanism FFXIV supports for placing
        // a battle pet at an arbitrary world location.
        Dalamud.Game.ClientState.Objects.Types.IGameObject? destActor = null;
        bool isSelf = destCids.Contains(localCid);

        if (!isSelf) {
            var destinationNames = plugin.Config.Characters
                .Where(character => destCids.Contains(character.Cid))
                .Select(character => character.Name)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .ToArray();

            foreach (var obj in DalamudApi.ObjectTable) {
                if (obj == null) continue;
                var actorName = obj.GetPlayerNameWorld() ?? obj.Name.TextValue;
                if (destinationNames.Any(configName => FormationCharacterName.Matches(configName, actorName))) {
                    destActor = obj;
                    break;
                }
            }

            if (destActor == null) {
                DalamudApi.PluginLog.Debug($"[{logPrefix}] dest point {destinationPointIndex + 1} character not in object table — skipping expected=[{string.Join(", ", destinationNames)}]");
                return true;
            }
        }

        GameActionManager.PlacePet(move.Value.Position, destActor, isSelf);
        DalamudApi.PluginLog.Debug($"[{logPrefix}] placed pet at formation=\"{formation.Name}\" point={destinationPointIndex + 1} isSelf={isSelf} target='{destActor?.Name}'");
        return true;
    }
}
