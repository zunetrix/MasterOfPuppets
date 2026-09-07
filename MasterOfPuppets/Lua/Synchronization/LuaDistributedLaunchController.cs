using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using MasterOfPuppets.Formations;
using MasterOfPuppets.Ipc;
using MasterOfPuppets.LuaScripting.Runs;
using MasterOfPuppets.LuaScripting.Coordination;
using MasterOfPuppets.Movement;

namespace MasterOfPuppets.LuaScripting.Synchronization;

/// <summary>
/// Opt-in framework-thread bridge from the validated v6 launch manifest to the
/// compact readiness protocol. It stages only the local character, exchanges
/// phase frames through Chat Sync, and starts Lua after conductor GO.
/// </summary>
internal sealed class LuaDistributedLaunchController {
    private static readonly TimeSpan GoLeadTime = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan HeartbeatInterval = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan ClockProbeInterval = TimeSpan.FromSeconds(30);
    private readonly Plugin _plugin;
    private readonly LuaDistributedSessionRegistry _sessions;
    private readonly Dictionary<string, PendingLaunch> _pending = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, LaunchedRun> _launched = new(StringComparer.OrdinalIgnoreCase);

    public LuaDistributedLaunchController(Plugin plugin, LuaDistributedSessionRegistry sessions) {
        _plugin = plugin;
        _sessions = sessions;
    }

    public IReadOnlyList<LuaDistributedClockDiagnostics> ClockDiagnostics() =>
        _pending.Values.Cast<PhaseOwner>()
            .Concat(_launched.Values)
            .Select(owner => new LuaDistributedClockDiagnostics(
                owner.RunToken,
                owner.Clock.Snapshot(),
                owner.LastClockProbeAt))
            .ToArray();

    public bool QueueLaunch(
        LuaScriptDefinition script,
        LuaChatSyncEnvelope envelope,
        string conductor,
        string effectiveRunTargetName,
        IReadOnlyDictionary<string, string> effectiveVariables,
        out string error) {
        error = string.Empty;
        if (!_plugin.Config.LuaDistributedReadinessEnabled)
            return false;
        if (envelope.ParticipantCids.Count > LuaDistributedWireCodec.MaximumRosterCount) {
            DalamudApi.PluginLog.Information(
                $"[LuaSync] Synchronized roster has {envelope.ParticipantCids.Count} participants (exceeding staging limit of {LuaDistributedWireCodec.MaximumRosterCount}); using direct synchronized launch.");
            return false;
        }
        var localCid = DalamudApi.PlayerState.ContentId;
        var conductorCid = ResolveExactCid(conductor);
        if (localCid == 0 || conductorCid == 0) {
            error = "distributed staging requires exact configured conductor and local content IDs";
            return true;
        }
        var protocolRunId = LuaScriptManager.CreateRunId(envelope.StartUtcTicks, envelope.Seed, envelope.ScriptHash);
        var runToken = LuaDistributedWireCodec.CreateRunToken(protocolRunId);
        if (_pending.ContainsKey(runToken) || _launched.ContainsKey(runToken))
            return true;

        var now = DateTimeOffset.UtcNow;
        var timeoutSeconds = Math.Clamp(_plugin.Config.LuaReadinessTimeoutSeconds, 5, 120);
        var prepare = new LuaDistributedWireEnvelope {
            Kind = LuaDistributedMessageKind.Prepare,
            MessageId = Guid.NewGuid(),
            CreatedUnixMilliseconds = now.ToUnixTimeMilliseconds(),
            RunToken = runToken,
            SenderContentId = conductorCid,
            BundleHash = envelope.BundleHash,
            ParticipantCids = envelope.ParticipantCids,
            ReadyTimeoutMilliseconds = timeoutSeconds * 1000,
            TimeoutPolicy = ResolveTimeoutPolicy(_plugin.Config.LuaReadinessTimeoutPolicy),
        };
        if (!_sessions.TryAccept(prepare, Normalize(conductor), now, out _, out error))
            return true;

        if (!_plugin.LuaScriptManager.TryAcquireStagingLease($"{protocolRunId}-stage", out var stagingLease, out error)) {
            _sessions.Stop(runToken, error);
            return true;
        }

        var slot = envelope.ParticipantCids.IndexOf(localCid);
        var pending = new PendingLaunch(
            script,
            envelope,
            Normalize(conductor),
            conductorCid,
            localCid,
            protocolRunId,
            runToken,
            effectiveRunTargetName,
            new Dictionary<string, string>(effectiveVariables, StringComparer.OrdinalIgnoreCase),
            new LuaLocalReadinessTracker(now + TimeSpan.FromMilliseconds(Math.Max(0, slot) * 100)),
            stagingLease!,
            new LuaRunCoordinationState(
                localCid,
                envelope.ParticipantCids,
                localCid == conductorCid,
                isDistributed: true,
                conductorContentId: conductorCid));
        _pending.Add(runToken, pending);
        pending.Coordination.ConfigureTransport(
            (key, value, sequence) => SendSharedVariable(pending, key, value, sequence),
            (messageId, topic, payload, schemaVersion, sequence, targetContentId) =>
                SendParticipantMessage(
                    pending,
                    messageId,
                    topic,
                    payload,
                    schemaVersion,
                    sequence,
                    targetContentId));

        if (!string.IsNullOrWhiteSpace(script.ParticipantFormation)) {
            var anchorName = string.IsNullOrWhiteSpace(effectiveRunTargetName) ? conductor : effectiveRunTargetName;
            var anchor = FormationAnchorReference.Named(anchorName);
            if (!FormationLocalMovementExecutor.ExecuteChatSyncedFormation(
                    _plugin,
                    script.ParticipantFormation,
                    anchor,
                    SimpleMovementMode.Precise)) {
                SendPhase(pending, LuaDistributedMessageKind.Error, detail: "initial formation staging failed");
                pending.StagingLease.Dispose();
                _pending.Remove(runToken);
                error = "initial formation staging failed";
            }
        }
        return true;
    }

    public void OnPhaseAccepted(LuaDistributedWireEnvelope envelope) {
        if (envelope.Kind == LuaDistributedMessageKind.Stop) {
            if (_pending.Remove(envelope.RunToken, out var stopped))
                stopped.StagingLease.Dispose();
            return;
        }
        if (envelope.Kind == LuaDistributedMessageKind.ClockProbe) {
            ReplyToClockProbe(envelope);
            return;
        }
        if (envelope.Kind == LuaDistributedMessageKind.ClockReply) {
            AcceptClockReply(envelope);
            return;
        }
        if (envelope.Kind == LuaDistributedMessageKind.SharedVariable) {
            var owner = FindOwner(envelope.RunToken);
            owner?.Coordination.ApplyVariable(
                envelope.Name,
                envelope.Detail,
                envelope.CoordinationSequence,
                envelope.SenderContentId,
                DateTimeOffset.UtcNow,
                out _);
            return;
        }
        if (envelope.Kind == LuaDistributedMessageKind.ParticipantMessage) {
            var owner = FindOwner(envelope.RunToken);
            owner?.Coordination.ApplyMessage(new LuaParticipantMessageSnapshot(
                envelope.MessageId,
                envelope.Name,
                envelope.Detail,
                envelope.SchemaVersion,
                envelope.CoordinationSequence,
                envelope.SenderContentId,
                envelope.TargetContentId,
                DateTimeOffset.UtcNow), out _);
            return;
        }
        if (envelope.Kind != LuaDistributedMessageKind.Go
            || !_pending.TryGetValue(envelope.RunToken, out var pending)
            || pending.Started)
            return;

        var go = DateTimeOffset.FromUnixTimeMilliseconds(envelope.GoUnixMilliseconds);
        if (envelope.TimeoutPolicy == LuaReadinessTimeoutPolicy.ContinueWithReadyParticipants) {
            var localState = _sessions.Snapshot()
                .FirstOrDefault(session => session.RunToken.Equals(envelope.RunToken, StringComparison.OrdinalIgnoreCase))?
                .Protocol.Participants.FirstOrDefault(participant => participant.ContentId == pending.LocalCid)?.State;
            if (localState == LuaParticipantProtocolState.Missing) {
                pending.StagingLease.Dispose();
                _pending.Remove(envelope.RunToken);
                return;
            }
        }
        var localNowSeconds = MonotonicSeconds();
        var clockEstimate = pending.Clock.Snapshot();
        var effectiveDelaySeconds = clockEstimate.IsReady && envelope.GoSharedClockSeconds > 0
            ? envelope.GoSharedClockSeconds - pending.Clock.ToSharedSeconds(localNowSeconds)
            : (go - DateTimeOffset.UtcNow).TotalSeconds;
        var localGoStartSeconds = localNowSeconds + effectiveDelaySeconds;
        var delay = TimeSpan.FromSeconds(effectiveDelaySeconds);
        if (delay < TimeSpan.Zero)
            delay = TimeSpan.Zero;
        double SharedElapsedSeconds() {
            var localSeconds = MonotonicSeconds();
            var estimate = pending.Clock.Snapshot();
            return estimate.IsReady && envelope.GoSharedClockSeconds > 0
                ? Math.Max(0, pending.Clock.ToSharedSeconds(localSeconds) - envelope.GoSharedClockSeconds)
                : Math.Max(0, localSeconds - localGoStartSeconds);
        }
        pending.Started = true;
        pending.StagingLease.Dispose();
        _plugin.LuaScriptManager.StartScript(
            pending.Script.Name,
            pending.Script.Hash,
            pending.Script.Source,
            0,
            pending.Envelope.RunTargetEntityId,
            pending.EffectiveRunTargetName,
            pending.Envelope.ParticipantCids,
            pending.Envelope.StartUtcTicks,
            pending.Envelope.Seed,
            pending.EffectiveVariables,
            localStartDelay: delay,
            modules: pending.Script.Modules,
            requiredResources: pending.Script.RequiredResources,
            declaredCapabilities: pending.Script.DeclaredCapabilities,
            conductorName: pending.Conductor,
            sharedTimeSeconds: SharedElapsedSeconds,
            coordination: pending.Coordination);
        _launched[pending.RunToken] = new LaunchedRun(
            pending.RunToken,
            pending.ProtocolRunId,
            pending.LocalCid,
            pending.Conductor,
            pending.ConductorCid,
            pending.Clock,
            pending.Coordination,
            DateTimeOffset.UtcNow) {
            LastClockProbeAt = pending.LastClockProbeAt,
            LastClockProbeMessageId = pending.LastClockProbeMessageId,
            LastClockProbeSendSeconds = pending.LastClockProbeSendSeconds,
        };
        _pending.Remove(pending.RunToken);
    }

    public void Update() {
        if (!_plugin.Config.LuaDistributedReadinessEnabled)
            return;
        var now = DateTimeOffset.UtcNow;
        ApplyTerminalSessionPolicies();
        foreach (var pending in _pending.Values.ToArray()) {
            ProbeClockIfDue(pending, now);
            var readiness = pending.Readiness.Observe(
                now,
                _plugin.SimpleInputMovement.IsMoving || _plugin.FormationTrackingSession.IsActive);
            switch (readiness) {
                case LuaLocalReadinessSignal.Stage:
                    SendPhase(pending, LuaDistributedMessageKind.Stage);
                    break;
                case LuaLocalReadinessSignal.Ready:
                    SendPhase(pending, LuaDistributedMessageKind.Ready);
                    break;
                case LuaLocalReadinessSignal.Regression:
                    SendPhase(pending, LuaDistributedMessageKind.Stage, positionError: 999f, speed: 1f);
                    break;
            }
            if (!pending.GoSent && IsLocalConductor(pending.Conductor)) {
                var policy = ResolveTimeoutPolicy(_plugin.Config.LuaReadinessTimeoutPolicy);
                if (_sessions.TryScheduleGo(pending.RunToken, now, GoLeadTime, policy, out var go, out var reason)) {
                    var sharedGo = MonotonicSeconds() + Math.Max(0, (go - now).TotalSeconds);
                    SendPhase(
                        pending,
                        LuaDistributedMessageKind.Go,
                        go: go,
                        timeoutPolicy: policy,
                        goSharedClockSeconds: sharedGo);
                    pending.GoSent = true;
                } else if (reason.Contains("aborted", StringComparison.OrdinalIgnoreCase)) {
                    SendPhase(pending, LuaDistributedMessageKind.Stop, detail: reason);
                    pending.GoSent = true;
                }
            }
        }

        foreach (var launched in _launched.Values.ToArray()) {
            ProbeClockIfDue(launched, now);
            var active = _plugin.LuaScriptManager.FindActiveSnapshot(launched.RunId);
            if (active != null) {
                if (now - launched.LastHeartbeatAt >= HeartbeatInterval) {
                    SendPhase(launched, LuaDistributedMessageKind.Heartbeat, participantState: LuaParticipantProtocolState.Running);
                    launched.LastHeartbeatAt = now;
                }
                continue;
            }
            var terminal = _plugin.LuaScriptManager.FindHistorySnapshot(launched.RunId);
            if (terminal == null)
                continue;
            var kind = terminal.State == LuaRunState.Completed
                ? LuaDistributedMessageKind.Complete
                : LuaDistributedMessageKind.Error;
            SendPhase(launched, kind, detail: terminal.Error?.Message ?? terminal.StopReason);
            _launched.Remove(launched.RunToken);
        }
    }

    public void CancelAll(string reason) {
        foreach (var pending in _pending.Values)
            pending.StagingLease.Dispose();
        _pending.Clear();
        _launched.Clear();
        _sessions.StopAll(reason);
    }

    private void ApplyTerminalSessionPolicies() {
        foreach (var session in _sessions.Snapshot().Where(session =>
                     session.Protocol.Phase is LuaDistributedRunPhase.Stopped or LuaDistributedRunPhase.Failed)) {
            if (_pending.Remove(session.RunToken, out var pending))
                pending.StagingLease.Dispose();
            if (!_launched.Remove(session.RunToken, out var launched))
                continue;
            _plugin.LuaScriptManager.Stop(launched.RunId, session.Protocol.Detail, out _);
        }
    }

    private Guid? SendPhase(
        PhaseOwner owner,
        LuaDistributedMessageKind kind,
        string detail = "",
        DateTimeOffset? go = null,
        LuaReadinessTimeoutPolicy timeoutPolicy = LuaReadinessTimeoutPolicy.Abort,
        LuaParticipantProtocolState participantState = LuaParticipantProtocolState.Staging,
        float positionError = 0,
        float speed = 0,
        double goSharedClockSeconds = 0,
        Guid correlationMessageId = default,
        double probeLocalSendSeconds = 0,
        double remoteReceiveSeconds = 0,
        double remoteSendSeconds = 0,
        long coordinationSequence = 0,
        ulong targetContentId = 0,
        int schemaVersion = 0,
        string name = "",
        Guid messageId = default) {
        var prefix = _plugin.Config.DefaultChatSyncPrefix?.Trim();
        if (string.IsNullOrWhiteSpace(prefix))
            return null;
        var now = DateTimeOffset.UtcNow;
        if (messageId == Guid.Empty)
            messageId = Guid.NewGuid();
        var localSeconds = MonotonicSeconds();
        var envelope = new LuaDistributedWireEnvelope {
            Kind = kind,
            MessageId = messageId,
            CreatedUnixMilliseconds = now.ToUnixTimeMilliseconds(),
            RunToken = owner.RunToken,
            SenderContentId = owner.LocalCid,
            PositionError = positionError,
            FacingErrorRadians = 0,
            Speed = speed,
            GoUnixMilliseconds = go?.ToUnixTimeMilliseconds() ?? 0,
            GoSharedClockSeconds = goSharedClockSeconds,
            TimeoutPolicy = timeoutPolicy,
            ParticipantState = participantState,
            SharedClockSeconds = owner.Clock.Snapshot().IsReady
                ? owner.Clock.ToSharedSeconds(localSeconds)
                : localSeconds,
            CorrelationMessageId = correlationMessageId,
            ProbeLocalSendSeconds = probeLocalSendSeconds,
            RemoteReceiveSeconds = remoteReceiveSeconds,
            RemoteSendSeconds = remoteSendSeconds,
            CoordinationSequence = coordinationSequence,
            TargetContentId = targetContentId,
            SchemaVersion = schemaVersion,
            Name = name,
            Detail = LimitDetail(detail),
        };
        var token = LuaDistributedWireCodec.Encode(envelope);
        var command = $"{prefix} mopluaphase {token}";
        if (Encoding.UTF8.GetByteCount(command) > 500) {
            DalamudApi.PluginLog.Warning($"[LuaSync] phase command exceeded chat limit: {kind}");
            return null;
        }
        Chat.SendMessage(command);
        return messageId;
    }

    private LuaCoordinationResult SendSharedVariable(
        PhaseOwner owner,
        string key,
        string value,
        long sequence) => SendPhase(
            owner,
            LuaDistributedMessageKind.SharedVariable,
            detail: value,
            coordinationSequence: sequence,
            name: key).HasValue
                ? LuaCoordinationResult.Success("broadcast")
                : LuaCoordinationResult.Failure("Chat Sync prefix is unavailable or the frame exceeded the chat limit");

    private LuaCoordinationResult SendParticipantMessage(
        PhaseOwner owner,
        Guid messageId,
        string topic,
        string payload,
        int schemaVersion,
        long sequence,
        ulong targetContentId) => SendPhase(
            owner,
            LuaDistributedMessageKind.ParticipantMessage,
            detail: payload,
            coordinationSequence: sequence,
            targetContentId: targetContentId,
            schemaVersion: schemaVersion,
            name: topic,
            messageId: messageId).HasValue
                ? LuaCoordinationResult.Success("sent")
                : LuaCoordinationResult.Failure("Chat Sync prefix is unavailable or the frame exceeded the chat limit");

    private void ProbeClockIfDue(PhaseOwner owner, DateTimeOffset now) {
        if (owner.LocalCid == owner.ConductorCid || now - owner.LastClockProbeAt < ClockProbeInterval)
            return;
        var localSend = MonotonicSeconds();
        var messageId = SendPhase(
            owner,
            LuaDistributedMessageKind.ClockProbe,
            probeLocalSendSeconds: localSend);
        if (!messageId.HasValue)
            return;
        owner.LastClockProbeAt = now;
        owner.LastClockProbeMessageId = messageId.Value;
        owner.LastClockProbeSendSeconds = localSend;
    }

    private void ReplyToClockProbe(LuaDistributedWireEnvelope envelope) {
        var owner = FindOwner(envelope.RunToken);
        if (owner == null || owner.LocalCid != owner.ConductorCid || envelope.SenderContentId == owner.LocalCid)
            return;
        var remoteReceive = MonotonicSeconds();
        SendPhase(
            owner,
            LuaDistributedMessageKind.ClockReply,
            correlationMessageId: envelope.MessageId,
            probeLocalSendSeconds: envelope.ProbeLocalSendSeconds,
            remoteReceiveSeconds: remoteReceive,
            remoteSendSeconds: MonotonicSeconds());
    }

    private void AcceptClockReply(LuaDistributedWireEnvelope envelope) {
        var owner = FindOwner(envelope.RunToken);
        if (owner == null
            || envelope.SenderContentId != owner.ConductorCid
            || envelope.CorrelationMessageId != owner.LastClockProbeMessageId
            || Math.Abs(envelope.ProbeLocalSendSeconds - owner.LastClockProbeSendSeconds) > 0.001)
            return;
        if (!owner.Clock.TryAdd(
                new LuaClockExchange(
                    envelope.ProbeLocalSendSeconds,
                    envelope.RemoteReceiveSeconds,
                    envelope.RemoteSendSeconds,
                    MonotonicSeconds()),
                out var estimate,
                out var reason)) {
            DalamudApi.PluginLog.Warning($"[LuaSync] rejected clock sample for {owner.RunToken}: {reason}");
            return;
        }
        DalamudApi.PluginLog.Debug(
            $"[LuaSync] clock {owner.RunToken}: offset={estimate.OffsetSeconds:F4}s "
            + $"rtt={estimate.RoundTripSeconds:F4}s jitter={estimate.JitterSeconds:F4}s samples={estimate.SampleCount}");
        owner.LastClockProbeMessageId = Guid.Empty;
    }

    private PhaseOwner? FindOwner(string runToken) {
        if (_pending.TryGetValue(runToken, out var pending))
            return pending;
        return _launched.TryGetValue(runToken, out var launched) ? launched : null;
    }

    private ulong ResolveExactCid(string name) {
        var normalized = Normalize(name);
        var localName = FormationCharacterName.FormatPlayerNameWorld(
            DalamudApi.PlayerState.CharacterName,
            DalamudApi.PlayerState.HomeWorld.ValueNullable?.Name.ToString());
        if (FormationCharacterName.Matches(localName, normalized))
            return DalamudApi.PlayerState.ContentId;
        return _plugin.Config.Characters
            .Where(character => character.Cid != 0)
            .FirstOrDefault(character => FormationCharacterName.Matches(character.Name, normalized))
            ?.Cid ?? 0;
    }

    private bool IsLocalConductor(string conductor) {
        var local = FormationCharacterName.FormatPlayerNameWorld(
            DalamudApi.PlayerState.CharacterName,
            DalamudApi.PlayerState.HomeWorld.ValueNullable?.Name.ToString());
        return FormationCharacterName.Matches(local, conductor);
    }

    private static LuaReadinessTimeoutPolicy ResolveTimeoutPolicy(string? value) => value?.Trim().ToLowerInvariant() switch {
        "continue_ready" or "continue-with-ready" or "continue_with_ready_participants" => LuaReadinessTimeoutPolicy.ContinueWithReadyParticipants,
        "continue_all" or "continue-all" => LuaReadinessTimeoutPolicy.ContinueAll,
        _ => LuaReadinessTimeoutPolicy.Abort,
    };

    private static string Normalize(string value) => FormationCharacterName.NormalizeWorldSeparator(value);
    private static double MonotonicSeconds() => Environment.TickCount64 / 1000.0;
    private static string LimitDetail(string value) {
        value ??= string.Empty;
        while (Encoding.UTF8.GetByteCount(value) > LuaDistributedWireCodec.MaximumDetailBytes)
            value = value[..^1];
        return value;
    }

    private abstract class PhaseOwner(
        string runToken,
        ulong localCid,
        string conductor,
        ulong conductorCid,
        LuaClockOffsetEstimator clock,
        LuaRunCoordinationState coordination) {
        public string RunToken { get; } = runToken;
        public ulong LocalCid { get; } = localCid;
        public string Conductor { get; } = conductor;
        public ulong ConductorCid { get; } = conductorCid;
        public LuaClockOffsetEstimator Clock { get; } = clock;
        public LuaRunCoordinationState Coordination { get; } = coordination;
        public DateTimeOffset LastClockProbeAt { get; set; } = DateTimeOffset.MinValue;
        public Guid LastClockProbeMessageId { get; set; }
        public double LastClockProbeSendSeconds { get; set; }
    }

    private sealed class PendingLaunch(
        LuaScriptDefinition script,
        LuaChatSyncEnvelope envelope,
        string conductor,
        ulong conductorCid,
        ulong localCid,
        string protocolRunId,
        string runToken,
        string effectiveRunTargetName,
        IReadOnlyDictionary<string, string> effectiveVariables,
        LuaLocalReadinessTracker readiness,
        LuaResourceLease stagingLease,
        LuaRunCoordinationState coordination) : PhaseOwner(
            runToken,
            localCid,
            conductor,
            conductorCid,
            new LuaClockOffsetEstimator(),
            coordination) {
        public LuaScriptDefinition Script { get; } = script;
        public LuaChatSyncEnvelope Envelope { get; } = envelope;
        public string ProtocolRunId { get; } = protocolRunId;
        public string EffectiveRunTargetName { get; } = effectiveRunTargetName;
        public IReadOnlyDictionary<string, string> EffectiveVariables { get; } = effectiveVariables;
        public LuaLocalReadinessTracker Readiness { get; } = readiness;
        public LuaResourceLease StagingLease { get; } = stagingLease;
        public bool GoSent { get; set; }
        public bool Started { get; set; }
    }

    private sealed class LaunchedRun(
        string runToken,
        string runId,
        ulong localCid,
        string conductor,
        ulong conductorCid,
        LuaClockOffsetEstimator clock,
        LuaRunCoordinationState coordination,
        DateTimeOffset lastHeartbeatAt) : PhaseOwner(runToken, localCid, conductor, conductorCid, clock, coordination) {
        public string RunId { get; } = runId;
        public DateTimeOffset LastHeartbeatAt { get; set; } = lastHeartbeatAt;
    }
}
