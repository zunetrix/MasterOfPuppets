using System;
using System.Linq;
using System.Numerics;

using Dalamud.Game.ClientState.Objects.Types;

using MasterOfPuppets.Movement;

namespace MasterOfPuppets.Formations;

public sealed class FormationAnchorPoseTracker {
    public const float PositionDeadZone = 0.02f;
    public const float RotationDeadZoneRadians = 0.0175f;
    public const long StationaryAfterMs = 200;

    private Vector3 _motionReferencePosition;
    private float _motionReferenceRotation;
    private long _lastSignificantMotionMs;

    public FormationAnchorPoseTracker(Vector3 position, float rotation, long nowMs) {
        AcceptedPosition = position;
        AcceptedRotation = rotation;
        _motionReferencePosition = position;
        _motionReferenceRotation = rotation;
        _lastSignificantMotionMs = nowMs;
    }

    public Vector3 AcceptedPosition { get; private set; }
    public float AcceptedRotation { get; private set; }
    public bool IsMoving { get; private set; }

    public void Update(Vector3 position, float rotation, long nowMs) {
        if (!IsMoving) {
            if (!HasSignificantDelta(AcceptedPosition, AcceptedRotation, position, rotation))
                return;

            IsMoving = true;
            _lastSignificantMotionMs = nowMs;
            _motionReferencePosition = position;
            _motionReferenceRotation = rotation;
        } else if (HasSignificantDelta(
                       _motionReferencePosition,
                       _motionReferenceRotation,
                       position,
                       rotation)) {
            _lastSignificantMotionMs = nowMs;
            _motionReferencePosition = position;
            _motionReferenceRotation = rotation;
        } else if (nowMs - _lastSignificantMotionMs >= StationaryAfterMs) {
            IsMoving = false;
        }

        AcceptedPosition = position;
        AcceptedRotation = rotation;
    }

    private static bool HasSignificantDelta(
        Vector3 previousPosition,
        float previousRotation,
        Vector3 nextPosition,
        float nextRotation) {
        var positionDeltaX = nextPosition.X - previousPosition.X;
        var positionDeltaZ = nextPosition.Z - previousPosition.Z;
        var positionDelta = MathF.Sqrt(positionDeltaX * positionDeltaX + positionDeltaZ * positionDeltaZ);
        var rotationDelta = MathF.Atan2(
            MathF.Sin(nextRotation - previousRotation),
            MathF.Cos(nextRotation - previousRotation));
        return positionDelta >= PositionDeadZone
            || MathF.Abs(rotationDelta) >= RotationDeadZoneRadians;
    }
}

/// <summary>
/// Keeps a Natural formation slot attached to its live anchor. Anchor
/// acquisition is throttled, while position and rotation reads are performed
/// every framework tick from the cached game object.
/// </summary>
public sealed class FormationTrackingSession {
    private const long ReacquireIntervalMs = 250;
    private const long AnchorLossTimeoutMs = 1500;

    private readonly Plugin _plugin;
    private ActiveSession? _active;

    public FormationTrackingSession(Plugin plugin) {
        _plugin = plugin;
    }

    public bool IsActive => _active != null;

    public void Start(
        Formation formation,
        int destinationPointIndex,
        int anchorPointIndex,
        ulong? anchorContentId,
        ulong? anchorGameObjectId,
        string anchorName,
        Vector3 fallbackAnchorPosition,
        float fallbackAnchorRotation,
        float fallbackAnchorActorRotation,
        bool normalizeAnchorRotation,
        string trackingKey) {
        if (!TryComputeTarget(
                formation,
                destinationPointIndex,
                anchorPointIndex,
                fallbackAnchorPosition,
                fallbackAnchorRotation,
                out var target))
            return;

        var now = Environment.TickCount64;
        var sameSession = _active != null
            && string.Equals(_active.TrackingKey, trackingKey, StringComparison.Ordinal)
            && _active.AnchorContentId == anchorContentId
            && _active.AnchorGameObjectId == anchorGameObjectId
            && string.Equals(_active.AnchorName, anchorName, StringComparison.OrdinalIgnoreCase)
            && _active.NormalizeAnchorRotation == normalizeAnchorRotation;
        if (sameSession) {
            // Macro-loop refreshes may still arrive, but the live framework
            // session already owns the formation geometry and anchor cache.
            return;
        }

        _active = new ActiveSession(
            formation.Clone(),
            destinationPointIndex,
            anchorPointIndex,
            anchorContentId,
            anchorGameObjectId,
            anchorName,
            normalizeAnchorRotation,
            trackingKey,
            new FormationAnchorPoseTracker(fallbackAnchorPosition, fallbackAnchorRotation, now),
            new FormationAnchorLocomotionTracker(fallbackAnchorPosition, fallbackAnchorActorRotation, now),
            null,
            now - ReacquireIntervalMs,
            now);

        FormationLocalMovementExecutor.MoveToComputed(
            _plugin,
            target.Position,
            target.Rotation,
            SimpleMovementMode.Natural,
            trackingKey);
    }

    public void Update() {
        var session = _active;
        if (session == null)
            return;

        if (!_plugin.SimpleInputMovement.IsActiveLiveFormationMove(session.TrackingKey)) {
            Stop();
            return;
        }

        var now = Environment.TickCount64;
        var anchor = GetAnchor(session, now);
        if (anchor == null) {
            if (now - session.LastAnchorSeenMs >= AnchorLossTimeoutMs) {
                DalamudApi.PluginLog.Debug($"[FormationTracking] anchor lost for session {session.TrackingKey}; stopping");
                Stop();
                _plugin.SimpleInputMovement.StopMove();
            }
            return;
        }

        session.LastAnchorSeenMs = now;
        var useFormationRelativeMovement = session.AnchorLocomotionTracker.Update(
            anchor.Position,
            anchor.Rotation,
            now);
        var anchorRotation = session.NormalizeAnchorRotation
            ? FormationMath.GetFormationFrameRotation(session.Formation.Points[session.AnchorPointIndex], anchor.Rotation)
            : anchor.Rotation;
        session.PoseTracker.Update(anchor.Position, anchorRotation, now);
        if (!TryComputeTarget(
                session.Formation,
                session.DestinationPointIndex,
                session.AnchorPointIndex,
                session.PoseTracker.AcceptedPosition,
                session.PoseTracker.AcceptedRotation,
                out var target)) {
            Stop();
            _plugin.SimpleInputMovement.StopMove();
            return;
        }

        FormationLocalMovementExecutor.MoveToComputed(
            _plugin,
            target.Position,
            target.Rotation,
            SimpleMovementMode.Natural,
            session.TrackingKey,
            useFormationRelativeMovement);
    }

    public void Stop() {
        _active = null;
    }

    public static bool TryComputeTarget(
        Formation formation,
        int destinationPointIndex,
        int anchorPointIndex,
        Vector3 anchorPosition,
        float anchorRotation,
        out (Vector3 Position, float Rotation) target) {
        var move = FormationPointMovement.BuildAnchoredWorldMove(
            formation,
            destinationPointIndex,
            anchorPointIndex,
            anchorPosition,
            anchorRotation);
        target = move ?? default;
        return move.HasValue;
    }

    private IGameObject? GetAnchor(ActiveSession session, long now) {
        if (IsUsable(session.CachedAnchor, session.AnchorGameObjectId))
            return session.CachedAnchor;

        if (now - session.LastAcquireAttemptMs < ReacquireIntervalMs)
            return null;

        session.LastAcquireAttemptMs = now;
        var localPlayer = DalamudApi.ObjectTable.LocalPlayer;
        if (session.AnchorContentId.HasValue
            && session.AnchorContentId.Value == DalamudApi.PlayerState.ContentId
            && localPlayer != null) {
            session.CachedAnchor = localPlayer;
            return localPlayer;
        }

        IGameObject? match = null;
        if (session.AnchorGameObjectId is { } objectId && objectId != 0)
            match = DalamudApi.ObjectTable.FirstOrDefault(actor => actor.GameObjectId == objectId);

        if (match == null && !string.IsNullOrWhiteSpace(session.AnchorName)) {
            var normalizedName = FormationCharacterName.NormalizeWorldSeparator(session.AnchorName);
            match = DalamudApi.ObjectTable.FirstOrDefault(actor =>
                actor.Name.TextValue.Length > 0
                && FormationCharacterName.MatchScore(actor.Name.TextValue, normalizedName) >= 0);
        }

        session.CachedAnchor = match;
        return match;
    }

    private static bool IsUsable(IGameObject? actor, ulong? expectedGameObjectId) =>
        actor != null
        && actor.Address != nint.Zero
        && (!expectedGameObjectId.HasValue || expectedGameObjectId.Value == 0 || actor.GameObjectId == expectedGameObjectId.Value);

    private sealed class ActiveSession {
        public ActiveSession(
            Formation formation,
            int destinationPointIndex,
            int anchorPointIndex,
            ulong? anchorContentId,
            ulong? anchorGameObjectId,
            string anchorName,
            bool normalizeAnchorRotation,
            string trackingKey,
            FormationAnchorPoseTracker poseTracker,
            FormationAnchorLocomotionTracker anchorLocomotionTracker,
            IGameObject? cachedAnchor,
            long lastAcquireAttemptMs,
            long lastAnchorSeenMs) {
            Formation = formation;
            DestinationPointIndex = destinationPointIndex;
            AnchorPointIndex = anchorPointIndex;
            AnchorContentId = anchorContentId;
            AnchorGameObjectId = anchorGameObjectId;
            AnchorName = anchorName;
            NormalizeAnchorRotation = normalizeAnchorRotation;
            TrackingKey = trackingKey;
            PoseTracker = poseTracker;
            AnchorLocomotionTracker = anchorLocomotionTracker;
            CachedAnchor = cachedAnchor;
            LastAcquireAttemptMs = lastAcquireAttemptMs;
            LastAnchorSeenMs = lastAnchorSeenMs;
        }

        public Formation Formation { get; }
        public int DestinationPointIndex { get; }
        public int AnchorPointIndex { get; }
        public ulong? AnchorContentId { get; }
        public ulong? AnchorGameObjectId { get; }
        public string AnchorName { get; }
        public bool NormalizeAnchorRotation { get; }
        public string TrackingKey { get; }
        public FormationAnchorPoseTracker PoseTracker { get; }
        public FormationAnchorLocomotionTracker AnchorLocomotionTracker { get; }
        public IGameObject? CachedAnchor { get; set; }
        public long LastAcquireAttemptMs { get; set; }
        public long LastAnchorSeenMs { get; set; }
    }
}
