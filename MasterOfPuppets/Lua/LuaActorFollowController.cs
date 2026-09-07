using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

using Dalamud.Game.ClientState.Objects.Types;

using MasterOfPuppets.Extensions.Dalamud;
using MasterOfPuppets.Formations;
using MasterOfPuppets.Movement;
using MasterOfPuppets.LuaScripting.Watches;

namespace MasterOfPuppets.LuaScripting;

/// <summary>
/// Lua-owned actor following. It consumes names and movement options directly;
/// it does not execute macros or load saved formation definitions.
/// </summary>
internal sealed class LuaActorFollowController {
    private const float WalkingSpeed = 2.1f;
    private const float RunningSpeed = 6.0f;
    private const float SprintingSpeed = 7.8f;
    private const float WalkInferenceMaximumSpeed = 2.8f;
    private const float RunInferenceMinimumSpeed = 3.4f;
    private const float LocomotionInferenceMinimumSpeed = 0.35f;
    private const long SprintRetryIntervalMs = 3000;

    private readonly Plugin _plugin;
    private readonly RigidFormationPoseTracker _rigidPose = new();
    private LuaActorFollowRequest? _request;
    private string? _trackingKey;
    private ulong _runTargetObjectId;
    private uint _runTargetEntityId;
    private string _runTargetName = string.Empty;
    private ulong _currentAnchorObjectId;
    private FormationAnchorLocomotionTracker? _anchorLocomotion;
    private bool _leaderWalking;
    private bool _walkModeClassified;
    private bool _catchingUp;
    private bool _lastLeaderSprinting;
    private bool? _savedWalking;
    private long _lastSprintAttemptMs;

    public LuaActorFollowController(Plugin plugin) {
        _plugin = plugin;
    }

    public bool IsActive => _trackingKey != null;

    public void PauseMovement() {
        if (_trackingKey != null && _plugin.SimpleInputMovement.IsActiveLiveFormationMove(_trackingKey))
            _plugin.SimpleInputMovement.StopMove();
    }

    public void Start(
        string runId,
        LuaActorFollowRequest request,
        ulong runTargetObjectId,
        uint runTargetEntityId,
        string runTargetName) {
        request = request.Validate();
        var trackingKey = $"LuaActorFollow:{runId}";
        var canUpdateRigidRequest = _request?.RigidFormation == true
            && request.RigidFormation
            && string.Equals(_trackingKey, trackingKey, StringComparison.Ordinal)
            && _request.AnchorCandidates.SequenceEqual(request.AnchorCandidates, StringComparer.OrdinalIgnoreCase);
        if (canUpdateRigidRequest) {
            _request = request;
            return;
        }

        Stop(stopMovement: true);
        _request = request;
        _trackingKey = trackingKey;
        _runTargetObjectId = runTargetObjectId;
        _runTargetEntityId = runTargetEntityId;
        _runTargetName = FormationCharacterName.NormalizeWorldSeparator(runTargetName);
        _currentAnchorObjectId = 0;
        _anchorLocomotion = null;
        _rigidPose.Reset();
        _leaderWalking = SimpleMovementWalkState.IsWalking;
        _walkModeClassified = false;
        _catchingUp = false;
        _lastLeaderSprinting = false;
        _lastSprintAttemptMs = 0;
        if (request.MirrorWalkRun)
            _savedWalking ??= SimpleMovementWalkState.IsWalking;
    }

    public void Update() {
        var request = _request;
        var trackingKey = _trackingKey;
        if (request == null || trackingKey == null)
            return;

        var anchor = ResolveFirstVisibleAnchor(request.AnchorCandidates);
        if (anchor == null) {
            if (_plugin.SimpleInputMovement.IsActiveLiveFormationMove(trackingKey))
                _plugin.SimpleInputMovement.StopMove();
            _currentAnchorObjectId = 0;
            _anchorLocomotion = null;
            return;
        }

        var now = Environment.TickCount64;
        if (_currentAnchorObjectId != anchor.GameObjectId || _anchorLocomotion == null) {
            _currentAnchorObjectId = anchor.GameObjectId;
            _anchorLocomotion = new FormationAnchorLocomotionTracker(anchor.Position, anchor.Rotation, now);
            _rigidPose.Reset();
            _walkModeClassified = false;
            _catchingUp = false;
            DalamudApi.PluginLog.Debug($"[Lua] actor follow attached to {GetActorName(anchor)}");
        }

        var useAnchorRelativeMovement = request.FaceAnchor
            && _anchorLocomotion.Update(anchor.Position, anchor.Rotation, now);
        Vector3 destination;
        float? facing;
        if (request.RigidFormation) {
            var leaderSprinting = HasSprintStatus(anchor);
            var pose = _rigidPose.Step(
                anchor.Position,
                anchor.Rotation,
                request.FormationRadius,
                GetMaximumTravelSpeed(leaderSprinting),
                now);
            destination = GetWorldDestination(pose.Position, pose.Rotation, request.RelativeOffset);
            destination = ApplyNeighborCorrection(destination, request, pose.Rotation);
            facing = request.FaceAnchor
                ? pose.Rotation + request.FacingOffsetRadians
                : null;

            var player = DalamudApi.ObjectTable.LocalPlayer;
            if (player != null) {
                var planarError = Distance2D(player.Position, destination);
                UpdateMirroredLocomotion(
                    request,
                    anchor,
                    pose.LeaderSpeed,
                    leaderSprinting,
                    planarError,
                    now);
            }
        } else {
            destination = GetWorldDestination(anchor.Position, anchor.Rotation, request.RelativeOffset);
            facing = request.FaceAnchor
                ? anchor.Rotation + request.FacingOffsetRadians
                : null;
        }

        _plugin.SimpleInputMovement.MoveTo(
            destination,
            precision: request.Precision,
            faceDirection: facing,
            movementMode: SimpleMovementMode.Natural,
            stopOnStuck: false,
            trackingKey: trackingKey,
            useFormationRelativeMovement: useAnchorRelativeMovement,
            usePursuitTarget: request.PursuitPrediction,
            allowHoldWhileTargetMoving: request.BrakeAtPosition,
            rateLimitTravelFacing: !request.ImmediateSteering);
    }

    public void Stop(bool stopMovement) {
        var trackingKey = _trackingKey;
        _request = null;
        _trackingKey = null;
        _runTargetObjectId = 0;
        _runTargetEntityId = 0;
        _runTargetName = string.Empty;
        _currentAnchorObjectId = 0;
        _anchorLocomotion = null;
        _rigidPose.Reset();
        _walkModeClassified = false;
        _catchingUp = false;
        _lastLeaderSprinting = false;
        _lastSprintAttemptMs = 0;
        if (_savedWalking.HasValue) {
            SimpleMovementWalkState.IsWalking = _savedWalking.Value;
            _savedWalking = null;
        }
        if (stopMovement
            && trackingKey != null
            && _plugin.SimpleInputMovement.IsActiveLiveFormationMove(trackingKey))
            _plugin.SimpleInputMovement.StopMove();
    }

    internal static Vector3 GetWorldDestination(
        Vector3 anchorPosition,
        float anchorRotation,
        Vector3 relativeOffset) {
        var cos = MathF.Cos(anchorRotation);
        var sin = MathF.Sin(anchorRotation);
        var rotated = new Vector3(
            relativeOffset.X * cos + relativeOffset.Z * sin,
            relativeOffset.Y,
            -relativeOffset.X * sin + relativeOffset.Z * cos);
        return anchorPosition + rotated;
    }

    internal static Vector3 ApplyNeighborCorrection(
        Vector3 globalDestination,
        Vector3 selfOffset,
        float formationRotation,
        IReadOnlyList<(Vector3 Position, Vector3 RelativeOffset)> neighbors,
        float weight,
        float maximumCorrection) {
        if (neighbors.Count == 0 || weight <= 0f || maximumCorrection <= 0f)
            return globalDestination;

        var expected = Vector3.Zero;
        foreach (var neighbor in neighbors) {
            var relative = selfOffset - neighbor.RelativeOffset;
            expected += GetWorldDestination(neighbor.Position, formationRotation, relative);
        }
        expected /= neighbors.Count;

        var correction = expected - globalDestination;
        correction.Y = 0f;
        var length = correction.Length();
        if (length > maximumCorrection && length > float.Epsilon)
            correction *= maximumCorrection / length;
        return globalDestination + correction * weight;
    }

    private Vector3 ApplyNeighborCorrection(
        Vector3 globalDestination,
        LuaActorFollowRequest request,
        float formationRotation) {
        if (request.FormationNeighbors is not { Count: > 0 } neighbors
            || request.NeighborCorrectionWeight <= 0f
            || request.MaximumNeighborCorrection <= 0f)
            return globalDestination;

        var resolved = new List<(Vector3 Position, Vector3 RelativeOffset)>(neighbors.Count);
        foreach (var neighbor in neighbors) {
            var actor = ResolveVisibleActor(neighbor.Name);
            if (actor != null)
                resolved.Add((actor.Position, neighbor.RelativeOffset));
        }
        return ApplyNeighborCorrection(
            globalDestination,
            request.RelativeOffset,
            formationRotation,
            resolved,
            request.NeighborCorrectionWeight,
            request.MaximumNeighborCorrection);
    }

    private void UpdateMirroredLocomotion(
        LuaActorFollowRequest request,
        IGameObject anchor,
        float leaderSpeed,
        bool leaderSprinting,
        float planarError,
        long nowMs) {
        if (request.MirrorWalkRun) {
            var anchorIsLocal = anchor.GameObjectId == DalamudApi.ObjectTable.LocalPlayer?.GameObjectId;
            if (anchorIsLocal) {
                _leaderWalking = SimpleMovementWalkState.IsWalking;
                _walkModeClassified = true;
            } else if (leaderSprinting || leaderSpeed >= RunInferenceMinimumSpeed) {
                _leaderWalking = false;
                _walkModeClassified = true;
            } else if (leaderSpeed >= LocomotionInferenceMinimumSpeed
                       && leaderSpeed <= WalkInferenceMaximumSpeed) {
                _leaderWalking = true;
                _walkModeClassified = true;
            }

            var enterCatchUp = MathF.Max(1.0f, request.Precision * 6f);
            var leaveCatchUp = MathF.Max(0.35f, request.Precision * 2.5f);
            if (planarError >= enterCatchUp)
                _catchingUp = true;
            else if (planarError <= leaveCatchUp)
                _catchingUp = false;

            if (_walkModeClassified)
                SimpleMovementWalkState.IsWalking = _leaderWalking && !_catchingUp && !leaderSprinting;
        }

        if (request.MirrorSprint) {
            var localPlayer = DalamudApi.ObjectTable.LocalPlayer;
            var localSprinting = localPlayer != null && HasSprintStatus(localPlayer);
            if (leaderSprinting && !localSprinting && nowMs - _lastSprintAttemptMs >= SprintRetryIntervalMs) {
                _lastSprintAttemptMs = nowMs;
                GameActionManager.UseGeneralAction("Sprint");
            } else if (!leaderSprinting && _lastLeaderSprinting && localSprinting) {
                Chat.SendMessage("/statusoff Sprint");
            }
            _lastLeaderSprinting = leaderSprinting;
        }
    }

    private float GetMaximumTravelSpeed(bool leaderSprinting) {
        if (leaderSprinting)
            return SprintingSpeed;
        if (_walkModeClassified && _leaderWalking)
            return WalkingSpeed;
        return RunningSpeed;
    }

    private IGameObject? ResolveVisibleActor(string name) =>
        LuaActorQueryResolver.ResolvePlayer(
            DalamudApi.ObjectTable,
            name,
            DalamudApi.ObjectTable.LocalPlayer?.GameObjectId ?? 0).Actor;

    private static bool HasSprintStatus(IGameObject actor) {
        if (actor is not IBattleChara battle)
            return false;
        foreach (var status in battle.StatusList) {
            if (status.StatusId == 50)
                return true;
        }
        return false;
    }

    private static float Distance2D(Vector3 left, Vector3 right) {
        var x = left.X - right.X;
        var z = left.Z - right.Z;
        return MathF.Sqrt(x * x + z * z);
    }

    private IGameObject? ResolveFirstVisibleAnchor(IReadOnlyList<string> candidates) {
        var localPlayer = DalamudApi.ObjectTable.LocalPlayer;
        foreach (var candidate in candidates) {
            IGameObject? match = null;
            if (_runTargetEntityId != 0
                && _runTargetEntityId != 0xE0000000
                && FormationCharacterName.Matches(_runTargetName, candidate)) {
                match = DalamudApi.ObjectTable.FirstOrDefault(actor => actor.EntityId == _runTargetEntityId);
            }
            if (_runTargetObjectId != 0
                && match == null
                && FormationCharacterName.Matches(_runTargetName, candidate)) {
                match = DalamudApi.ObjectTable.FirstOrDefault(actor => actor.GameObjectId == _runTargetObjectId);
            }

            match ??= LuaActorQueryResolver.ResolvePlayer(
                DalamudApi.ObjectTable,
                candidate,
                localPlayer?.GameObjectId ?? 0).Actor;
            if (match != null && match.GameObjectId != localPlayer?.GameObjectId)
                return match;
        }

        return null;
    }

    private static string GetActorName(IGameObject actor) =>
        actor.GetPlayerNameWorld() ?? actor.Name.TextValue;

}
