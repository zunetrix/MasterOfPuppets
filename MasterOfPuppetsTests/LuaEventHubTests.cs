using MasterOfPuppets.LuaScripting;
using MasterOfPuppets.LuaScripting.Events;
using MasterOfPuppets.LuaScripting.Snapshots;

using Xunit;

public sealed class LuaEventHubTests {
    [Fact]
    public void BoundedHub_DropsOldest_AndReportsPressure() {
        using var hub = new LuaEventHub(capacity: 2);
        Assert.True(hub.Publish("one"));
        Assert.True(hub.Publish("two"));
        Assert.True(hub.Publish("three"));

        Assert.True(hub.TryRead(null, out var first));
        Assert.Equal("two", first?.Name);
        Assert.True(hub.TryRead(null, out var second));
        Assert.Equal("three", second?.Name);
        Assert.False(hub.TryRead(null, out _));
        var stats = hub.Snapshot();
        Assert.Equal(3, stats.Published);
        Assert.Equal(2, stats.Consumed);
        Assert.Equal(1, stats.Dropped);
    }

    [Fact]
    public async Task FilteredRead_PreservesUnrelatedEvents_AndTimesOutWithoutBusyPolling() {
        using var hub = new LuaEventHub();
        hub.Publish("movement.started");
        hub.Publish("chat.message", new Dictionary<string, string> { ["text"] = "hello" });

        var item = await hub.ReadAsync("chat.message", TimeSpan.FromSeconds(1), CancellationToken.None);
        var timeout = await hub.ReadAsync("missing", TimeSpan.FromMilliseconds(20), CancellationToken.None);
        Assert.True(hub.TryRead(null, out var preserved));

        Assert.Equal("hello", item?.Data["text"]);
        Assert.Null(timeout);
        Assert.Equal("movement.started", preserved?.Name);
        Assert.Equal(2, hub.Snapshot().Consumed);
    }

    [Fact]
    public async Task DataFilteredRead_PreservesSameNameEventsForOtherActorWatches() {
        using var hub = new LuaEventHub();
        hub.Publish("actor.jump", new Dictionary<string, string> { ["watch_id"] = "other" });
        hub.Publish("actor.jump", new Dictionary<string, string> { ["watch_id"] = "wanted" });

        var wanted = await hub.ReadAsync(
            "actor.jump",
            new Dictionary<string, string> { ["watch_id"] = "wanted" },
            TimeSpan.FromSeconds(1),
            CancellationToken.None);

        Assert.Equal("wanted", wanted?.Data["watch_id"]);
        Assert.True(hub.TryRead("actor.jump", out var other));
        Assert.Equal("other", other?.Data["watch_id"]);
    }

    [Fact]
    public void RawStreamInterests_AreExplicitAndReversible() {
        using var hub = new LuaEventHub();
        Assert.False(hub.IsInterested("combat.action"));
        hub.RegisterInterest("combat.action");
        Assert.True(hub.IsInterested("combat.action"));
        Assert.True(hub.UnregisterInterest("combat.action"));
        Assert.False(hub.IsInterested("combat.action"));
    }

    [Fact]
    public async Task EventsProvider_ReturnsTypedEventTimeoutAndStatistics() {
        using var hub = new LuaEventHub();
        hub.Publish("chat.message", new Dictionary<string, string> {
            ["speaker"] = "Alice Example@Moogle",
            ["text"] = "Places, everyone!",
        }, DateTimeOffset.FromUnixTimeMilliseconds(1_800_000_000_000));
        using var runner = new LuaScriptRunner(new LuaScriptContext(
            0,
            1,
            1,
            _ => { },
            DeclaredCapabilities: ["mop.events"],
            Events: hub));

        await runner.RunAsync("""
            assert(mop.capabilities.has("mop.events", "3.0.0"))
            assert(mop.events.subscribe("combat.action"))
            local event = mop.events.next("chat.message", 1)
            assert(event.status == "event" and event.name == "chat.message")
            assert(event.data.speaker == "Alice Example@Moogle")
            assert(event.data.text == "Places, everyone!")
            local missing = mop.events.next("chat.message", 0.02)
            assert(missing.status == "timeout")
            local stats = mop.events.stats()
            assert(stats.capacity == 256 and stats.published == 1 and stats.consumed == 1)
            assert(mop.events.unsubscribe("combat.action"))
            """, CancellationToken.None);
    }

    [Fact]
    public void GameTracker_EmitsTargetConditionAndParticipantVisibilityChanges() {
        var tracker = new LuaGameEventTracker();
        var participant = Actor("Performer One@Moogle", "100");
        var target = Actor("Stage Anchor", "200");

        Assert.Empty(tracker.Observe(Snapshot(null, false, participant)));
        var events = tracker.Observe(Snapshot(target, true, null));

        var targetEvent = Assert.Single(events, item => item.Name == "target.changed");
        Assert.Equal("selected", targetEvent.Data["kind"]);
        Assert.Equal("200", targetEvent.Data["current_id"]);
        var conditionEvent = Assert.Single(events, item => item.Name == "condition.changed");
        Assert.Equal("true", conditionEvent.Data["active"]);
        var lostEvent = Assert.Single(events, item => item.Name == "participant.lost");
        Assert.Equal("10", lostEvent.Data["content_id"]);
    }

    [Fact]
    public void GameTracker_ThrottlesSnapshotCaptureRequests() {
        var tracker = new LuaGameEventTracker(TimeSpan.FromMilliseconds(250));
        var start = DateTimeOffset.FromUnixTimeSeconds(1_800_000_000);

        Assert.True(tracker.ShouldObserve(start));
        Assert.False(tracker.ShouldObserve(start.AddMilliseconds(249)));
        Assert.True(tracker.ShouldObserve(start.AddMilliseconds(250)));
    }

    private static LuaGameSnapshot Snapshot(LuaActorSnapshot? target, bool condition, LuaActorSnapshot? participant) => new(
        null,
        target,
        null,
        null,
        Array.Empty<LuaActorSnapshot>(),
        [new LuaParticipantSnapshot(0, 10, "Performer One@Moogle", false, participant == null ? "configured" : "visible", participant)],
        1,
        2,
        0,
        true,
        false,
        false,
        new Dictionary<string, bool>(StringComparer.Ordinal) { ["Performing"] = condition });

    private static LuaActorSnapshot Actor(string name, string id) => new(
        name,
        "visible",
        "Player",
        id,
        uint.Parse(id),
        "0",
        default,
        0,
        false,
        true,
        false,
        true,
        false,
        0,
        0,
        0,
        "0",
        false,
        0,
        0,
        0,
        0,
        false,
        0,
        "Online",
        1,
        100,
        100,
        100,
        100,
        100,
        0,
        0,
        0,
        0,
        "None",
        false,
        0,
        "0",
        0,
        0);
}
