using MasterOfPuppets.LuaScripting;
using MasterOfPuppets.LuaScripting.Coordination;
using MasterOfPuppets.LuaScripting.Runtime;

using Xunit;

public sealed class LuaCoordinationTests {
    [Fact]
    public void SharedVariables_AreConductorOwnedBoundedAndMonotonic() {
        var conductor = new LuaRunCoordinationState(10, [10, 20], isConductor: true);
        var participant = new LuaRunCoordinationState(20, [10, 20], isConductor: false);
        using var events = new MasterOfPuppets.LuaScripting.Events.LuaEventHub();
        conductor.AttachEvents(events);

        Assert.True(conductor.SetShared("scene", "finale").Ok);
        Assert.True(conductor.TryGetShared("SCENE", out var value));
        Assert.Equal("finale", value!.Value);
        Assert.True(events.TryRead("sync.variable", out var variableEvent));
        Assert.Equal("scene", variableEvent!.Data["key"]);
        Assert.False(participant.SetShared("scene", "spoofed").Ok);
        Assert.True(participant.ApplyVariable("scene", "one", 5, 10, DateTimeOffset.UtcNow, out _));
        Assert.False(participant.ApplyVariable("scene", "stale", 4, 10, DateTimeOffset.UtcNow, out var stale));
        Assert.Contains("out of order", stale);
        Assert.Throws<ArgumentException>(() => conductor.SetShared(new string('x', 33), "value"));
    }

    [Fact]
    public void ParticipantMessages_EnforceTargetOrderingSchemaAndBounds() {
        var state = new LuaRunCoordinationState(20, [10, 20, 30], isConductor: false);
        using var events = new MasterOfPuppets.LuaScripting.Events.LuaEventHub();
        state.AttachEvents(events);
        var first = new LuaParticipantMessageSnapshot(
            Guid.NewGuid(), "cue", "places", 1, 1, 10, 20, DateTimeOffset.UtcNow);

        Assert.True(state.ApplyMessage(first, out _));
        Assert.False(state.ApplyMessage(first with { MessageId = Guid.NewGuid() }, out var duplicate));
        Assert.Contains("out of order", duplicate);
        Assert.True(state.TryReadMessage("CUE", out var received));
        Assert.Equal("places", received!.Payload);
        Assert.True(events.TryRead("sync.message", out var messageEvent));
        Assert.Equal("cue", messageEvent!.Data["topic"]);
        Assert.True(state.ApplyMessage(first with {
            MessageId = Guid.NewGuid(),
            Sequence = 2,
            TargetContentId = 30,
        }, out _));
        Assert.False(state.TryReadMessage(null, out _));
        Assert.Throws<ArgumentOutOfRangeException>(() => state.SendMessage("cue", "payload", 0, 0));
        Assert.False(state.SendMessage("cue", "payload", 1, 999).Ok);
    }

    [Fact]
    public async Task CoordinationProvider_ExposesStructuredLocalSharedStateAndMessages() {
        var state = new LuaRunCoordinationState(10, [10, 20], isConductor: true);
        using var runner = new LuaScriptRunner(new LuaScriptContext(
            0,
            2,
            1,
            _ => { },
            DeclaredCapabilities: ["mop.coordination"],
            Coordination: state));

        await runner.RunAsync("""
            assert(mop.capabilities.has("mop.coordination", "2.0"))
            assert(mop.shared.set("scene", "finale").ok)
            local scene = mop.shared.get("scene")
            assert(scene.status == "value" and scene.value == "finale")
            assert(mop.shared.wait("scene", 1, 0).sequence == scene.sequence)
            assert(#mop.shared.list() == 1)
            assert(mop.messages.info().conductor and not mop.messages.info().distributed)
            assert(mop.messages.send("cue", "places", 0, 2).ok)
            local cue = mop.messages.next("cue", 1)
            assert(cue.status == "message" and cue.payload == "places")
            assert(cue.schema_version == 2 and cue.target_content_id == "10")
            assert(mop.messages.broadcast("stop", "2|X|target|0", 2).ok)
            local stop = mop.messages.next("stop", 1)
            assert(stop.schema_version == 2 and stop.target_content_id == "0")
            """, CancellationToken.None);
    }

    [Fact]
    public void CoordinationTransportFailure_DoesNotMutateLocalState() {
        var state = new LuaRunCoordinationState(10, [10], isConductor: true, isDistributed: true);
        state.ConfigureTransport(
            (_, _, _) => LuaCoordinationResult.Failure("offline"),
            (_, _, _, _, _, _) => LuaCoordinationResult.Failure("offline"));

        Assert.False(state.SetShared("scene", "finale").Ok);
        Assert.False(state.TryGetShared("scene", out _));
        Assert.False(state.SendMessage("cue", "places", 1, 0).Ok);
        Assert.False(state.TryReadMessage(null, out _));
    }

    [Fact]
    public void DynamicRosterUpdatePreservesConductorAndAdmitsOnlyCurrentSenders() {
        var state = new LuaRunCoordinationState(
            20,
            [10, 20],
            isConductor: false,
            isDistributed: true,
            conductorContentId: 10);

        Assert.False(state.ApplyVariable("scene", "spoofed", 1, 20, DateTimeOffset.UtcNow, out var spoofed));
        Assert.Contains("not the run conductor", spoofed);
        Assert.True(state.ApplyVariable("scene", "before-rejoin", 5, 10, DateTimeOffset.UtcNow, out _));
        Assert.True(state.TryGetShared("scene", out var beforeRejoin));
        Assert.True(state.TryUpdateParticipantRoster([10, 20, 30], out _));
        Assert.True(state.ApplyVariable("scene", "after-rejoin", 1, 10, DateTimeOffset.UtcNow, out _));
        Assert.True(state.TryGetShared("scene", out var afterRejoin));
        Assert.True(afterRejoin!.Sequence > beforeRejoin!.Sequence);
        Assert.Equal([10ul, 20ul, 30ul], state.ParticipantContentIds.ToArray());
        Assert.True(state.TryUpdateParticipantRoster([10, 40, 30], out _));
        Assert.Equal([10ul, 40ul, 30ul], state.ParticipantContentIds.ToArray());
        Assert.False(state.TryUpdateParticipantRoster([99, 40, 30], out var conductorChanged));
        Assert.Contains("slot zero", conductorChanged);

        var joined = new LuaParticipantMessageSnapshot(
            Guid.NewGuid(), "member", "hello", 2, 1, 30, 0, DateTimeOffset.UtcNow);
        Assert.True(state.ApplyMessage(joined, out _));
        Assert.True(state.TryUpdateParticipantRoster([10, 40, 30], out _));
        Assert.True(state.ApplyMessage(joined with {
            MessageId = Guid.NewGuid(),
            Sequence = 1,
        }, out _));
        Assert.False(state.ApplyMessage(joined, out var replayedId));
        Assert.Contains("already received", replayedId);
        Assert.False(state.ApplyMessage(joined with {
            MessageId = Guid.NewGuid(),
            SenderContentId = 50,
            Sequence = 1,
        }, out var outside));
        Assert.Contains("outside", outside);
    }
}
