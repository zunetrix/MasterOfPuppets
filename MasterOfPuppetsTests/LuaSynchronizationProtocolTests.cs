using MasterOfPuppets;
using MasterOfPuppets.LuaScripting.Synchronization;

using System.Text;

using Xunit;

public sealed class LuaSynchronizationProtocolTests {
    [Fact]
    public void DedicatedLuaVariableUpdate_ParsesCompleteFormationReflow() {
        var variables = ChatWatcher.ParseLuaVariableUpdateArguments([
            "-var=$rows=2;$columns=5;$horizontal=0.5;$vertical=0.4",
        ]);

        Assert.Equal("2", variables["rows"]);
        Assert.Equal("5", variables["columns"]);
        Assert.Equal("0.5", variables["horizontal"]);
        Assert.Equal("0.4", variables["vertical"]);
    }

    [Theory]
    [InlineData("Leader Character", "Leader Character@Sargatanas", true)]
    [InlineData("Leader Character@Sargatanas", "Leader Character@Sargatanas", true)]
    [InlineData("Remote Puppet@Sargatanas", "Leader Character@Sargatanas", false)]
    [InlineData("", "Leader Character@Sargatanas", false)]
    public void ReadableLuaRun_OnlyChatSenderRelaysAuthoritativeRoster(
        string senderName,
        string localName,
        bool expected) {
        Assert.Equal(expected, ChatWatcher.ShouldRelayReadableLuaRun(senderName, localName));
    }

    [Fact]
    public void FrameworkUpdateCadence_ThrottlesDistributedHousekeeping() {
        var cadence = new LuaFrameworkUpdateCadence();

        Assert.True(cadence.ShouldUpdateLaunches(1_000));
        Assert.True(cadence.ShouldTickSessions(1_000));
        Assert.False(cadence.ShouldUpdateLaunches(1_049));
        Assert.True(cadence.ShouldUpdateLaunches(1_050));
        Assert.False(cadence.ShouldTickSessions(1_249));
        Assert.True(cadence.ShouldTickSessions(1_250));
    }

    [Fact]
    public void ReplayWindow_RejectsDuplicateStaleFutureAndMalformedMessages() {
        var now = DateTimeOffset.FromUnixTimeMilliseconds(1_800_000_000_000);
        var window = new LuaReplayWindow(maximumEntries: 2);
        var id = "11111111-2222-3333-4444-555555555555";

        Assert.True(window.TryAccept(id, now.ToUnixTimeMilliseconds(), now, out _));
        Assert.False(window.TryAccept(id, now.ToUnixTimeMilliseconds(), now, out var replay));
        Assert.Contains("replay", replay);
        Assert.False(window.TryAccept(Guid.NewGuid().ToString(), (now - TimeSpan.FromMinutes(3)).ToUnixTimeMilliseconds(), now, out var stale));
        Assert.Contains("stale", stale);
        Assert.False(window.TryAccept(Guid.NewGuid().ToString(), (now + TimeSpan.FromMinutes(1)).ToUnixTimeMilliseconds(), now, out var future));
        Assert.Contains("future", future);
        Assert.False(window.TryAccept("not-a-guid", now.ToUnixTimeMilliseconds(), now, out var malformed));
        Assert.Contains("ID", malformed);

        Assert.True(window.TryAccept(Guid.NewGuid().ToString(), now.ToUnixTimeMilliseconds(), now, out _));
        Assert.True(window.TryAccept(Guid.NewGuid().ToString(), now.ToUnixTimeMilliseconds(), now, out _));
        Assert.Equal(2, window.Count);
    }

    [Fact]
    public void ThirtyTwoParticipantProtocol_RequiresSettledRoster_ThenSchedulesFutureGo() {
        var preparedAt = DateTimeOffset.FromUnixTimeSeconds(1_800_000_000);
        var roster = Enumerable.Range(1, 32).Select(value => (ulong)value).ToArray();
        var coordinator = Coordinator(roster, preparedAt);
        coordinator.BeginStaging(preparedAt);

        foreach (var cid in roster)
            Assert.True(coordinator.ObserveStage(new LuaStageObservation(cid, 0.05f, 0.02f, 0.01f, preparedAt), out _));
        Assert.Equal(0, coordinator.Snapshot.ReadyCount);
        Assert.False(coordinator.TryScheduleGo(
            preparedAt + TimeSpan.FromMilliseconds(200),
            TimeSpan.FromSeconds(2),
            LuaReadinessTimeoutPolicy.Abort,
            out _,
            out var waiting));
        Assert.Contains("waiting", waiting);

        var settledAt = preparedAt + TimeSpan.FromMilliseconds(600);
        foreach (var cid in roster)
            Assert.True(coordinator.ObserveStage(new LuaStageObservation(cid, 0.04f, 0.01f, 0.005f, settledAt), out _));
        Assert.Equal(LuaDistributedRunPhase.Ready, coordinator.Snapshot.Phase);
        Assert.Equal(32, coordinator.Snapshot.ReadyCount);

        Assert.True(coordinator.TryScheduleGo(
            settledAt,
            TimeSpan.FromSeconds(2),
            LuaReadinessTimeoutPolicy.Abort,
            out var goEpoch,
            out _));
        Assert.Equal(settledAt + TimeSpan.FromSeconds(2), goEpoch);
        coordinator.Tick(goEpoch - TimeSpan.FromMilliseconds(1));
        Assert.Equal(LuaDistributedRunPhase.GoScheduled, coordinator.Snapshot.Phase);
        coordinator.Tick(goEpoch);
        Assert.Equal(LuaDistributedRunPhase.Running, coordinator.Snapshot.Phase);
        Assert.All(coordinator.Snapshot.Participants, participant =>
            Assert.Equal(LuaParticipantProtocolState.Running, participant.State));
    }

    [Fact]
    public void ReadinessRegressionAndTimeoutPolicy_AreExplicit() {
        var preparedAt = DateTimeOffset.FromUnixTimeSeconds(1_800_000_000);
        var coordinator = Coordinator([10, 20], preparedAt, readyTimeout: TimeSpan.FromSeconds(3));
        coordinator.BeginStaging(preparedAt);
        foreach (var cid in new ulong[] { 10, 20 }) {
            coordinator.ObserveStage(new LuaStageObservation(cid, 0.01f, 0.01f, 0, preparedAt), out _);
            coordinator.ObserveStage(new LuaStageObservation(cid, 0.01f, 0.01f, 0, preparedAt + TimeSpan.FromSeconds(1)), out _);
        }
        Assert.Equal(LuaDistributedRunPhase.Ready, coordinator.Snapshot.Phase);

        Assert.True(coordinator.ObserveStage(
            new LuaStageObservation(20, 0.5f, 0.01f, 0, preparedAt + TimeSpan.FromSeconds(1.1)),
            out _));
        Assert.Equal(LuaDistributedRunPhase.Staging, coordinator.Snapshot.Phase);
        Assert.False(coordinator.TryScheduleGo(
            preparedAt + TimeSpan.FromSeconds(3.1),
            TimeSpan.FromSeconds(1),
            LuaReadinessTimeoutPolicy.Abort,
            out _,
            out var reason));
        Assert.Equal(LuaDistributedRunPhase.Failed, coordinator.Snapshot.Phase);
        Assert.Contains("aborted", reason);
    }

    [Fact]
    public void HeartbeatTimeouts_MarkOnlySilentActiveParticipants() {
        var preparedAt = DateTimeOffset.FromUnixTimeSeconds(1_800_000_000);
        var coordinator = Coordinator([10, 20], preparedAt);
        coordinator.BeginStaging(preparedAt);
        foreach (var cid in new ulong[] { 10, 20 }) {
            coordinator.ObserveStage(new LuaStageObservation(cid, 0, 0, 0, preparedAt), out _);
            coordinator.ObserveStage(new LuaStageObservation(cid, 0, 0, 0, preparedAt + TimeSpan.FromSeconds(1)), out _);
        }
        coordinator.TryScheduleGo(preparedAt + TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1), LuaReadinessTimeoutPolicy.Abort, out var go, out _);
        coordinator.Tick(go);
        Assert.True(coordinator.Heartbeat(10, go + TimeSpan.FromSeconds(1.5)));

        var timedOut = coordinator.MarkHeartbeatTimeouts(go + TimeSpan.FromSeconds(2.1), TimeSpan.FromSeconds(2));

        Assert.Equal([20UL], timedOut);
        Assert.Equal(LuaParticipantProtocolState.Running, coordinator.Snapshot.Participants.Single(value => value.ContentId == 10).State);
        Assert.Equal(LuaParticipantProtocolState.Missing, coordinator.Snapshot.Participants.Single(value => value.ContentId == 20).State);
    }

    [Fact]
    public void LateGo_RecoversOriginalPhaseWithinGrace_AndRejectsOlderJoin() {
        var preparedAt = DateTimeOffset.FromUnixTimeSeconds(1_800_000_000);
        var goEpoch = preparedAt.AddSeconds(2);
        var recovered = Coordinator([10], preparedAt);
        recovered.BeginStaging(preparedAt);
        recovered.ObserveStage(new LuaStageObservation(10, 0, 0, 0, preparedAt), out _);
        recovered.ObserveStage(new LuaStageObservation(10, 0, 0, 0, preparedAt.AddSeconds(1)), out _);

        Assert.True(recovered.AcceptScheduledGo(
            goEpoch.AddSeconds(3),
            goEpoch,
            LuaReadinessTimeoutPolicy.Abort,
            out var recoveryError), recoveryError);
        recovered.Tick(goEpoch.AddSeconds(3));
        Assert.Equal(LuaDistributedRunPhase.Running, recovered.Snapshot.Phase);
        Assert.Contains("late-start recovery", recovered.Snapshot.Detail);

        var rejected = Coordinator([10], preparedAt);
        rejected.BeginStaging(preparedAt);
        rejected.ObserveStage(new LuaStageObservation(10, 0, 0, 0, preparedAt), out _);
        rejected.ObserveStage(new LuaStageObservation(10, 0, 0, 0, preparedAt.AddSeconds(1)), out _);
        Assert.False(rejected.AcceptScheduledGo(
            goEpoch.AddSeconds(6),
            goEpoch,
            LuaReadinessTimeoutPolicy.Abort,
            out var tooLate));
        Assert.Contains("late", tooLate);
    }

    [Fact]
    public void ClockEstimator_RecoversOffset_AndBoundsNoisyCorrections() {
        var estimator = new LuaClockOffsetEstimator();

        Assert.True(estimator.TryAdd(new LuaClockExchange(10.00, 10.12, 10.13, 10.05), out var estimate, out _));
        Assert.True(estimate.IsReady);
        Assert.Equal(0.1, estimate.OffsetSeconds, precision: 6);
        Assert.Equal(0.04, estimate.RoundTripSeconds, precision: 6);
        Assert.Equal(20.1, estimator.ToSharedSeconds(20), precision: 6);

        Assert.True(estimator.TryAdd(new LuaClockExchange(30.0, 30.8, 30.81, 30.9), out var noisy, out _));
        Assert.InRange(Math.Abs(noisy.OffsetSeconds - 0.1), 0, 0.050001);
        Assert.False(estimator.TryAdd(new LuaClockExchange(5, 4, 3, 2), out _, out var invalid));
        Assert.Contains("monotonic", invalid);
    }

    [Fact]
    public void DistributedWirePrepare_RoundTripsThirtyTwoActorsWithinChatLimit() {
        var now = DateTimeOffset.FromUnixTimeMilliseconds(1_800_000_000_000);
        var roster = Enumerable.Range(1, 32).Select(value => (ulong)value).ToArray();
        var original = new LuaDistributedWireEnvelope {
            Kind = LuaDistributedMessageKind.Prepare,
            MessageId = Guid.Parse("11111111-2222-3333-4444-555555555555"),
            CreatedUnixMilliseconds = now.ToUnixTimeMilliseconds(),
            RunToken = LuaDistributedWireCodec.CreateRunToken("run-1"),
            SenderContentId = 1,
            BundleHash = new string('a', 64),
            ParticipantCids = roster,
            ReadyTimeoutMilliseconds = 30_000,
            TimeoutPolicy = LuaReadinessTimeoutPolicy.Abort,
        };

        var token = LuaDistributedWireCodec.Encode(original);

        Assert.True(LuaDistributedWireCodec.TryDecode(token, out var decoded, out var error), error);
        Assert.Equal(original.Kind, decoded.Kind);
        Assert.Equal(original.MessageId, decoded.MessageId);
        Assert.Equal(original.RunToken, decoded.RunToken);
        Assert.Equal(original.BundleHash, decoded.BundleHash);
        Assert.Equal(roster, decoded.ParticipantCids);
        Assert.InRange(Encoding.UTF8.GetByteCount($"/cwl2 mopluaprep {token}"), 1, 500);
    }

    [Fact]
    public void DistributedWire_ValidatesPayloadAndDetectsCorruption() {
        var now = DateTimeOffset.FromUnixTimeMilliseconds(1_800_000_000_000);
        var ready = new LuaDistributedWireEnvelope {
            Kind = LuaDistributedMessageKind.Ready,
            MessageId = Guid.NewGuid(),
            CreatedUnixMilliseconds = now.ToUnixTimeMilliseconds(),
            RunToken = LuaDistributedWireCodec.CreateRunToken("run-2"),
            SenderContentId = 42,
            PositionError = 0.05f,
            FacingErrorRadians = 0.02f,
            Speed = 0.01f,
        };
        var token = LuaDistributedWireCodec.Encode(ready);
        var corruptIndex = token.Length / 2;
        var replacement = token[corruptIndex] == 'A' ? 'B' : 'A';
        var corrupted = token[..corruptIndex] + replacement + token[(corruptIndex + 1)..];

        Assert.False(LuaDistributedWireCodec.TryDecode(corrupted, out _, out var checksum));
        Assert.Contains("checksum", checksum);
        Assert.Throws<ArgumentException>(() => LuaDistributedWireCodec.Encode(ready with { PositionError = -1 }));
        Assert.Throws<ArgumentException>(() => LuaDistributedWireCodec.Encode(new LuaDistributedWireEnvelope {
            Kind = LuaDistributedMessageKind.Prepare,
            MessageId = Guid.NewGuid(),
            CreatedUnixMilliseconds = now.ToUnixTimeMilliseconds(),
            RunToken = ready.RunToken,
            SenderContentId = 42,
            BundleHash = new string('b', 64),
            ParticipantCids = Enumerable.Range(1, 33).Select(value => (ulong)value).ToArray(),
            ReadyTimeoutMilliseconds = 10_000,
        }));
    }

    [Fact]
    public void DistributedWire_ClockExchangeAndSharedGo_RoundTrip() {
        var now = DateTimeOffset.FromUnixTimeMilliseconds(1_800_000_000_000);
        var runToken = LuaDistributedWireCodec.CreateRunToken("clock-run");
        var probe = new LuaDistributedWireEnvelope {
            Kind = LuaDistributedMessageKind.ClockProbe,
            MessageId = Guid.NewGuid(),
            CreatedUnixMilliseconds = now.ToUnixTimeMilliseconds(),
            RunToken = runToken,
            SenderContentId = 2,
            ProbeLocalSendSeconds = 123.25,
        };
        Assert.True(LuaDistributedWireCodec.TryDecode(
            LuaDistributedWireCodec.Encode(probe),
            out var decodedProbe,
            out var probeError), probeError);
        Assert.Equal(123.25, decodedProbe.ProbeLocalSendSeconds);

        var reply = probe with {
            Kind = LuaDistributedMessageKind.ClockReply,
            MessageId = Guid.NewGuid(),
            SenderContentId = 1,
            CorrelationMessageId = probe.MessageId,
            RemoteReceiveSeconds = 200.1,
            RemoteSendSeconds = 200.11,
        };
        Assert.True(LuaDistributedWireCodec.TryDecode(
            LuaDistributedWireCodec.Encode(reply),
            out var decodedReply,
            out var replyError), replyError);
        Assert.Equal(probe.MessageId, decodedReply.CorrelationMessageId);
        Assert.Equal(200.1, decodedReply.RemoteReceiveSeconds);
        Assert.Equal(200.11, decodedReply.RemoteSendSeconds);

        var go = probe with {
            Kind = LuaDistributedMessageKind.Go,
            MessageId = Guid.NewGuid(),
            SenderContentId = 1,
            GoUnixMilliseconds = now.AddSeconds(2).ToUnixTimeMilliseconds(),
            GoSharedClockSeconds = 202.5,
            TimeoutPolicy = LuaReadinessTimeoutPolicy.Abort,
        };
        Assert.True(LuaDistributedWireCodec.TryDecode(
            LuaDistributedWireCodec.Encode(go),
            out var decodedGo,
            out var goError), goError);
        Assert.Equal(202.5, decodedGo.GoSharedClockSeconds);
        Assert.Throws<ArgumentException>(() => LuaDistributedWireCodec.Encode(go with { GoSharedClockSeconds = 0 }));
    }

    [Fact]
    public void DistributedWire_SharedVariableAndParticipantMessage_RoundTrip() {
        var now = DateTimeOffset.FromUnixTimeMilliseconds(1_800_000_000_000);
        var shared = new LuaDistributedWireEnvelope {
            Kind = LuaDistributedMessageKind.SharedVariable,
            MessageId = Guid.NewGuid(),
            CreatedUnixMilliseconds = now.ToUnixTimeMilliseconds(),
            RunToken = LuaDistributedWireCodec.CreateRunToken("coordination-run"),
            SenderContentId = 1,
            CoordinationSequence = 9,
            Name = "scene",
            Detail = "finale",
        };
        Assert.True(LuaDistributedWireCodec.TryDecode(
            LuaDistributedWireCodec.Encode(shared), out var decodedShared, out var sharedError), sharedError);
        Assert.Equal("scene", decodedShared.Name);
        Assert.Equal("finale", decodedShared.Detail);
        Assert.Equal(9, decodedShared.CoordinationSequence);

        var message = shared with {
            Kind = LuaDistributedMessageKind.ParticipantMessage,
            MessageId = Guid.NewGuid(),
            SenderContentId = 2,
            CoordinationSequence = 12,
            TargetContentId = 3,
            SchemaVersion = 2,
            Name = "cue",
            Detail = "places",
        };
        var encoded = LuaDistributedWireCodec.Encode(message);
        Assert.True(LuaDistributedWireCodec.TryDecode(encoded, out var decodedMessage, out var messageError), messageError);
        Assert.Equal(3UL, decodedMessage.TargetContentId);
        Assert.Equal(2, decodedMessage.SchemaVersion);
        Assert.Equal("cue", decodedMessage.Name);
        Assert.InRange(Encoding.UTF8.GetByteCount($"/cwl2 mopluaphase {encoded}"), 1, 500);
        Assert.Throws<ArgumentException>(() => LuaDistributedWireCodec.Encode(message with { SchemaVersion = 0 }));
    }

    [Fact]
    public void DistributedSenderPolicy_RequiresExactNameWorldForClaimedCid() {
        var local = new LuaDistributedSenderIdentity(1, "Alice Example@Moogle");
        LuaDistributedSenderIdentity[] configured = [new(2, "Bob Example@Omega")];

        Assert.True(LuaDistributedSenderPolicy.MatchesClaimedContentId(
            "Bob Example@Omega", 2, local, configured, out _));
        Assert.False(LuaDistributedSenderPolicy.MatchesClaimedContentId(
            "Bob Example@Moogle", 2, local, configured, out var mismatch));
        Assert.Contains("does not exactly match", mismatch);
        Assert.True(LuaDistributedSenderPolicy.MatchesClaimedContentId(
            "Alice Example@Moogle", 1, local, configured, out _));
    }

    [Fact]
    public void DistributedSessionRegistry_ConsumesPreparedReadyAndConductorGoFrames() {
        var now = DateTimeOffset.FromUnixTimeMilliseconds(1_800_000_000_000);
        var registry = new LuaDistributedSessionRegistry();
        var token = LuaDistributedWireCodec.CreateRunToken("registry-run");
        var prepare = new LuaDistributedWireEnvelope {
            Kind = LuaDistributedMessageKind.Prepare,
            MessageId = Guid.NewGuid(),
            CreatedUnixMilliseconds = now.ToUnixTimeMilliseconds(),
            RunToken = token,
            SenderContentId = 1,
            BundleHash = new string('c', 64),
            ParticipantCids = [1, 2],
            ReadyTimeoutMilliseconds = 10_000,
            TimeoutPolicy = LuaReadinessTimeoutPolicy.Abort,
        };
        Assert.True(registry.TryAccept(prepare, "Alice Example@Moogle", now, out _, out var prepareError), prepareError);

        foreach (var (cid, sender) in new[] { (1UL, "Alice Example@Moogle"), (2UL, "Bob Example@Omega") }) {
            foreach (var at in new[] { now, now.AddMilliseconds(600) }) {
                var ready = new LuaDistributedWireEnvelope {
                    Kind = LuaDistributedMessageKind.Ready,
                    MessageId = Guid.NewGuid(),
                    CreatedUnixMilliseconds = at.ToUnixTimeMilliseconds(),
                    RunToken = token,
                    SenderContentId = cid,
                    PositionError = 0.01f,
                    FacingErrorRadians = 0.01f,
                    Speed = 0,
                };
                Assert.True(registry.TryAccept(ready, sender, at, out _, out var readyError), readyError);
            }
        }
        Assert.Equal(LuaDistributedRunPhase.Ready, Assert.Single(registry.Snapshot()).Protocol.Phase);

        var goCreated = now.AddMilliseconds(700);
        var go = new LuaDistributedWireEnvelope {
            Kind = LuaDistributedMessageKind.Go,
            MessageId = Guid.NewGuid(),
            CreatedUnixMilliseconds = goCreated.ToUnixTimeMilliseconds(),
            RunToken = token,
            SenderContentId = 1,
            GoUnixMilliseconds = now.AddSeconds(2).ToUnixTimeMilliseconds(),
            TimeoutPolicy = LuaReadinessTimeoutPolicy.Abort,
        };
        Assert.False(registry.TryAccept(go, "Bob Example@Omega", goCreated, out _, out var conductorError));
        Assert.Contains("conductor", conductorError);
        Assert.True(registry.TryAccept(go, "Alice Example@Moogle", goCreated, out var scheduled, out var goError), goError);
        Assert.Equal(LuaDistributedRunPhase.GoScheduled, scheduled!.Protocol.Phase);
        registry.Tick(now.AddSeconds(2));
        Assert.Equal(LuaDistributedRunPhase.Running, Assert.Single(registry.Snapshot()).Protocol.Phase);
        registry.Tick(now.AddSeconds(8), TimeSpan.FromSeconds(5));
        var stopped = Assert.Single(registry.Snapshot());
        Assert.Equal(LuaDistributedRunPhase.Stopped, stopped.Protocol.Phase);
        Assert.Contains("conductor heartbeat timeout", stopped.Protocol.Detail);
        Assert.All(stopped.Protocol.Participants, participant =>
            Assert.Equal(LuaParticipantProtocolState.Missing, participant.State));
    }

    [Fact]
    public void DistributedSessionRegistry_AllowsParticipantProbe_ButOnlyConductorReply() {
        var now = DateTimeOffset.FromUnixTimeMilliseconds(1_800_000_000_000);
        var registry = new LuaDistributedSessionRegistry();
        var token = LuaDistributedWireCodec.CreateRunToken("registry-clock-run");
        var prepare = new LuaDistributedWireEnvelope {
            Kind = LuaDistributedMessageKind.Prepare,
            MessageId = Guid.NewGuid(),
            CreatedUnixMilliseconds = now.ToUnixTimeMilliseconds(),
            RunToken = token,
            SenderContentId = 1,
            BundleHash = new string('d', 64),
            ParticipantCids = [1, 2],
            ReadyTimeoutMilliseconds = 10_000,
            TimeoutPolicy = LuaReadinessTimeoutPolicy.Abort,
        };
        Assert.True(registry.TryAccept(prepare, "Alice Example@Moogle", now, out _, out _));

        var probe = new LuaDistributedWireEnvelope {
            Kind = LuaDistributedMessageKind.ClockProbe,
            MessageId = Guid.NewGuid(),
            CreatedUnixMilliseconds = now.AddMilliseconds(10).ToUnixTimeMilliseconds(),
            RunToken = token,
            SenderContentId = 2,
            ProbeLocalSendSeconds = 10,
        };
        Assert.True(registry.TryAccept(probe, "Bob Example@Omega", now.AddMilliseconds(10), out _, out _));

        var reply = probe with {
            Kind = LuaDistributedMessageKind.ClockReply,
            MessageId = Guid.NewGuid(),
            SenderContentId = 1,
            CorrelationMessageId = probe.MessageId,
            RemoteReceiveSeconds = 20,
            RemoteSendSeconds = 20.01,
        };
        Assert.False(registry.TryAccept(reply, "Bob Example@Omega", now.AddMilliseconds(20), out _, out var rejected));
        Assert.Contains("conductor", rejected);
        Assert.True(registry.TryAccept(reply, "Alice Example@Moogle", now.AddMilliseconds(20), out _, out _));
    }

    [Fact]
    public void DistributedSessionRegistry_RestrictsSharedWritesAndMessageTargetsToRoster() {
        var now = DateTimeOffset.FromUnixTimeMilliseconds(1_800_000_000_000);
        var registry = new LuaDistributedSessionRegistry();
        var token = LuaDistributedWireCodec.CreateRunToken("registry-coordination-run");
        var prepare = new LuaDistributedWireEnvelope {
            Kind = LuaDistributedMessageKind.Prepare,
            MessageId = Guid.NewGuid(),
            CreatedUnixMilliseconds = now.ToUnixTimeMilliseconds(),
            RunToken = token,
            SenderContentId = 1,
            BundleHash = new string('e', 64),
            ParticipantCids = [1, 2],
            ReadyTimeoutMilliseconds = 10_000,
            TimeoutPolicy = LuaReadinessTimeoutPolicy.Abort,
        };
        Assert.True(registry.TryAccept(prepare, "Alice Example@Moogle", now, out _, out _));
        var shared = new LuaDistributedWireEnvelope {
            Kind = LuaDistributedMessageKind.SharedVariable,
            MessageId = Guid.NewGuid(),
            CreatedUnixMilliseconds = now.AddMilliseconds(10).ToUnixTimeMilliseconds(),
            RunToken = token,
            SenderContentId = 1,
            CoordinationSequence = 1,
            Name = "scene",
            Detail = "finale",
        };
        Assert.False(registry.TryAccept(shared with { SenderContentId = 2 }, "Bob Example@Omega", now, out _, out var conductor));
        Assert.Contains("conductor", conductor);
        Assert.True(registry.TryAccept(shared, "Alice Example@Moogle", now, out _, out _));

        var message = shared with {
            Kind = LuaDistributedMessageKind.ParticipantMessage,
            SenderContentId = 2,
            TargetContentId = 999,
            SchemaVersion = 1,
            Name = "cue",
            Detail = "places",
        };
        Assert.False(registry.TryAccept(message, "Bob Example@Omega", now, out _, out var roster));
        Assert.Contains("roster", roster);
        Assert.True(registry.TryAccept(message with { TargetContentId = 1 }, "Bob Example@Omega", now, out _, out _));
    }

    [Fact]
    public void LocalReadinessTracker_RequiresSettlingAndReportsRegression() {
        var start = DateTimeOffset.FromUnixTimeSeconds(1_800_000_000);
        var tracker = new LuaLocalReadinessTracker(start.AddMilliseconds(200), TimeSpan.FromMilliseconds(500));

        Assert.Equal(LuaLocalReadinessSignal.None, tracker.Observe(start, movementActive: false));
        Assert.Equal(LuaLocalReadinessSignal.Stage, tracker.Observe(start.AddMilliseconds(200), movementActive: false));
        Assert.Equal(LuaLocalReadinessSignal.None, tracker.Observe(start.AddMilliseconds(699), movementActive: false));
        Assert.Equal(LuaLocalReadinessSignal.Ready, tracker.Observe(start.AddMilliseconds(700), movementActive: false));
        Assert.Equal(LuaLocalReadinessSignal.Regression, tracker.Observe(start.AddMilliseconds(800), movementActive: true));
        Assert.Equal(LuaLocalReadinessSignal.Stage, tracker.Observe(start.AddMilliseconds(900), movementActive: false));
    }

    private static LuaDistributedRunCoordinator Coordinator(
        IReadOnlyList<ulong> roster,
        DateTimeOffset preparedAt,
        TimeSpan? readyTimeout = null) => new(new LuaDistributedRunDescriptor(
            "run-1",
            new string('a', 64),
            "Alice Example@Moogle",
            roster,
            preparedAt,
            readyTimeout ?? TimeSpan.FromSeconds(10)));
}
