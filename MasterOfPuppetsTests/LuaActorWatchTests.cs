using MasterOfPuppets;
using MasterOfPuppets.LuaScripting;
using MasterOfPuppets.LuaScripting.Events;
using MasterOfPuppets.LuaScripting.Snapshots;
using MasterOfPuppets.LuaScripting.Watches;

using Xunit;

public sealed class LuaActorWatchTests {
    [Theory]
    [InlineData(223u, false, false, false)]
    [InlineData(223u, true, false, true)]
    [InlineData(223u, false, true, true)]
    [InlineData(0u, true, true, false)]
    public void PersistentEmoteIdRequiresActiveNativeLoopState(
        uint emoteId,
        bool controllerReportsLoop,
        bool modeReportsLoop,
        bool expected) {
        Assert.Equal(
            expected,
            DalamudLuaGameSnapshotCapture.IsEmoteLooping(emoteId, controllerReportsLoop, modeReportsLoop));
    }

    [Theory]
    [InlineData(1, "action")]
    [InlineData(5, "general_action")]
    [InlineData(11, "pet_action")]
    [InlineData(14, "pvp_action")]
    [InlineData(255, "unknown")]
    public void NativeActionTypesExposeStableLuaKinds(byte actionType, string expected) {
        Assert.Equal(expected, LuaActorWatchService.ActionKind(actionType));
    }

    [Fact]
    public void NativeEmotePayloadPreservesSourceTargetAndPersistence() {
        var data = new Dictionary<string, string>();
        LuaActorWatchService.AddEmoteFields(
            data,
            new EmoteObservation(200, 42, 900, true, DateTimeOffset.UnixEpoch));

        Assert.Equal("200", data["source_entity_id"]);
        Assert.Equal("42", data["emote_id"]);
        Assert.Equal("900", data["target_id"]);
        Assert.Equal("true", data["is_persistent"]);
    }

    [Fact]
    public void NativeCombatPayloadPreservesTypedActionAndTargetMetadata() {
        var data = new Dictionary<string, string>();
        LuaActorWatchService.AddCombatActionFields(
            data,
            new CombatActionObservation(
                200,
                1234,
                5,
                77,
                8,
                9,
                900,
                ["901", "902"],
                new System.Numerics.Vector3(1, 2, 3),
                DateTimeOffset.UnixEpoch),
            isGroundTargeted: true);

        Assert.Equal("200", data["source_entity_id"]);
        Assert.Equal("1234", data["action_id"]);
        Assert.Equal("general_action", data["action_kind"]);
        Assert.Equal("901,902", data["target_ids"]);
        Assert.Equal("true", data["is_ground_targeted"]);
        Assert.Equal("1", data["target_x"]);
        Assert.Equal("2", data["target_y"]);
        Assert.Equal("3", data["target_z"]);
    }

    [Theory]
    [InlineData("Athena Potato@Sargatanas", "Athena Potato@Sargatanas", true)]
    [InlineData("Athena Potato@Sargatanas", "Athena Potato@Midgardsormr", false)]
    [InlineData("Athena Potato", "Athena Potato@Sargatanas", true)]
    [InlineData("Athena", "Athena Potato@Sargatanas", true)]
    public void PlayerQuery_WorldQualifiedNamesDoNotDegradeToBaseName(
        string query,
        string actorName,
        bool expected) {
        Assert.Equal(expected, LuaActorQueryResolver.MatchesPlayerName(query, actorName));
    }

    [Fact]
    public void StateDiff_IgnoresOrdinaryPositionMovement() {
        var previous = State(positionY: 10f);
        var moved = State(positionY: 12f);

        Assert.Equal(LuaActorWatchChange.None, moved.ChangesFrom(previous));
    }

    [Fact]
    public void StateDiff_ReportsOnlyMirroringRelevantChanges() {
        var previous = State();
        var changed = State(emoteId: 42, emoteLooping: true, weaponDrawn: true, targetId: 99);

        var changes = changed.ChangesFrom(previous);

        Assert.True(changes.HasFlag(LuaActorWatchChange.Emote));
        Assert.True(changes.HasFlag(LuaActorWatchChange.Weapon));
        Assert.True(changes.HasFlag(LuaActorWatchChange.Target));
        Assert.False(changes.HasFlag(LuaActorWatchChange.Pose));
    }

    [Fact]
    public void StateDiff_CoversEveryRequestedDetectionCategory() {
        var previous = State();
        var changed = State(
            jumping: true,
            jumpSequence: 1,
            targetId: 99,
            mountId: 10,
            emoteId: 20,
            emoteLooping: true,
            poseType: 2,
            poseState: 3,
            ornamentId: 30,
            facewearId: 40,
            sprinting: true,
            weaponDrawn: true);

        var changes = changed.ChangesFrom(previous);

        Assert.True(changes.HasFlag(LuaActorWatchChange.Jump));
        Assert.True(changes.HasFlag(LuaActorWatchChange.Sprint));
        Assert.True(changes.HasFlag(LuaActorWatchChange.Emote));
        Assert.True(changes.HasFlag(LuaActorWatchChange.Mount));
        Assert.True(changes.HasFlag(LuaActorWatchChange.Ornament));
        Assert.True(changes.HasFlag(LuaActorWatchChange.Facewear));
        Assert.True(changes.HasFlag(LuaActorWatchChange.Target));
        Assert.True(changes.HasFlag(LuaActorWatchChange.Pose));
        Assert.True(changes.HasFlag(LuaActorWatchChange.Weapon));
    }

    [Fact]
    public void JumpTracker_InfersOneRemoteJumpFromShortWindowVerticalRise() {
        var tracker = new LuaActorJumpTracker();
        var initial = tracker.Observe(State(positionY: 10f), 1_000);
        var rising = tracker.Observe(State(positionY: 10.20f), 1_100);
        var continued = tracker.Observe(State(positionY: 10.35f), 1_150);
        var landed = tracker.Observe(State(positionY: 10f), 1_800);

        Assert.False(initial.IsJumping);
        Assert.True(rising.IsJumping);
        Assert.Equal(1, rising.JumpSequence);
        Assert.Equal(1, continued.JumpSequence);
        Assert.False(landed.IsJumping);
    }

    [Fact]
    public void JumpTracker_UsesNativeRisingEdgesWithoutDuplicatePulses() {
        var tracker = new LuaActorJumpTracker();
        tracker.Observe(State(), 1_000);
        var started = tracker.Observe(State(jumping: true), 1_016);
        var airborne = tracker.Observe(State(jumping: true), 1_032);

        Assert.Equal(1, started.JumpSequence);
        Assert.Equal(1, airborne.JumpSequence);
    }

    [Fact]
    public void JumpTracker_DoesNotTreatSlowElevationChangesAsRemoteJumps() {
        var tracker = new LuaActorJumpTracker();
        tracker.Observe(State(positionY: 10f), 1_000);
        var slopeOne = tracker.Observe(State(positionY: 10.05f), 1_100);
        var slopeTwo = tracker.Observe(State(positionY: 10.10f), 1_200);
        var slopeThree = tracker.Observe(State(positionY: 10.15f), 1_300);

        Assert.False(slopeOne.IsJumping);
        Assert.False(slopeTwo.IsJumping);
        Assert.False(slopeThree.IsJumping);
        Assert.Equal(0, slopeThree.JumpSequence);
    }

    [Fact]
    public void MotionTracker_PublishesMovementEdgeAndShortStationaryDebounce() {
        var tracker = new LuaActorMotionTracker();
        var initial = tracker.Observe(State(positionX: 10f), 1_000);
        var moved = tracker.Observe(State(positionX: 10.05f), 1_050);
        var interpolationGap = tracker.Observe(State(positionX: 10.05f), 1_150);
        var stopped = tracker.Observe(State(positionX: 10.05f), 1_250);

        Assert.False(initial.IsMoving);
        Assert.True(moved.IsMoving);
        Assert.True(interpolationGap.IsMoving);
        Assert.False(stopped.IsMoving);
        Assert.True(moved.ChangesFrom(initial).HasFlag(LuaActorWatchChange.Movement));
    }

    [Fact]
    public void MotionTracker_AccumulatesSmallRemoteInterpolationSteps() {
        var tracker = new LuaActorMotionTracker();
        var initial = tracker.Observe(State(positionX: 10f), 1_000);
        var tinyStep = tracker.Observe(State(positionX: 10.004f), 1_050);
        var cumulativeStep = tracker.Observe(State(positionX: 10.009f), 1_100);

        Assert.False(initial.IsMoving);
        Assert.False(tinyStep.IsMoving);
        Assert.True(cumulativeStep.IsMoving);
        Assert.True(cumulativeStep.ChangesFrom(tinyStep).HasFlag(LuaActorWatchChange.Movement));
    }

    [Fact]
    public async Task GameStateProvider_ExposesSharedActorWatchAndUnwatch() {
        using var events = new LuaEventHub();
        using var runner = new LuaScriptRunner(new LuaScriptContext(
            0,
            1,
            1,
            _ => { },
            RunId: "watch-test",
            DeclaredCapabilities: ["mop.game-state"],
            Events: events,
            WatchActor: (query, _) => Task.FromResult(new LuaActorWatchRegistration(
                "watch-1",
                query,
                "found",
                "Artemis Potato@Sargatanas",
                "Role-playing",
                State(positionX: 1, positionY: 2, positionZ: 3, emoteId: 7,
                    emoteLooping: true, targetId: 900, classJobId: 33)) {
                Revision = 4,
                Changes = LuaActorWatchChange.ClassJob | LuaActorWatchChange.Emote,
            }),
            UnwatchActor: (watchId, _) => Task.FromResult(watchId == "watch-1")));

        await runner.RunAsync("""
            local watched = mop.actors.watch("Artemis Potato@Sargatanas")
            assert(watched.status == "found" and watched.watch_id == "watch-1")
            assert(watched.count == 1)
            assert(watched.actor.emote_id == 7 and watched.actor.is_emote_looping)
            assert(watched.actor.target_game_object_id == "900")
            assert(watched.actor.online_status_name == "Role-playing")
            assert(watched.revision == 4 and watched.changes.class_job and watched.changes.emote)
            assert(watched.actor.position.x == 1 and watched.actor.position.y == 2 and watched.actor.position.z == 3)
            assert(watched.actor.jump_sequence == 0)
            assert(mop.actors.unwatch(watched.watch_id))
            """, CancellationToken.None);
    }

    [Fact]
    public async Task GameStateProvider_WaitsOnCachedArbitraryActorRevisionsAndJobs() {
        var calls = 0;
        Task<LuaActorWatchRegistration> Watch(string query, CancellationToken _) {
            calls++;
            return Task.FromResult(new LuaActorWatchRegistration(
                "watch-external",
                query,
                "found",
                "Athena Potato@Sargatanas",
                string.Empty,
                State(classJobId: 33)) {
                Revision = 7,
                Changes = LuaActorWatchChange.ClassJob,
            });
        }

        using var runner = new LuaScriptRunner(new LuaScriptContext(
            0,
            1,
            1,
            _ => { },
            RunId: "external-watch-test",
            DeclaredCapabilities: ["mop.game-state"],
            WatchActor: Watch));

        await runner.RunAsync("""
            assert(mop.capabilities.has("mop.game-state", "3.0.0"))
            local job = mop.actors.job("Athena Potato@Sargatanas")
            assert(job.status == "observed" and job.class_job_id == 33)
            assert(job.actor.name == "Athena Potato@Sargatanas")

            local changed = mop.actors.wait_changed("Athena Potato@Sargatanas", 6, 1)
            assert(changed.status == "changed" and changed.actor_status == "found")
            assert(changed.revision == 7 and changed.changes.class_job)

            local job_changed = mop.actors.wait_job_changed(
                "Athena Potato@Sargatanas", 24, 1, "100")
            assert(job_changed.status == "changed")
            assert(job_changed.previous_class_job_id == 24 and job_changed.class_job_id == 33)
            assert(job_changed.actor.game_object_id == "100")
            """, CancellationToken.None);

        Assert.Equal(3, calls);
    }

    [Fact]
    public async Task GameStateProvider_ActorEventsCoverEveryUniversalReactionWithoutConsumingOtherActors() {
        using var events = new LuaEventHub();
        events.Publish("actor.jump", new Dictionary<string, string> {
            ["watch_id"] = "watch-other",
            ["marker"] = "preserve",
        });
        var names = new[] {
            "actor.combat_action",
            "actor.general_action",
            "actor.jump",
            "actor.sprint",
            "actor.emote_played",
            "actor.emote_state",
            "actor.fashion_accessory",
            "actor.mount",
            "actor.facewear",
            "actor.target",
            "actor.idle_pose",
            "actor.weapon",
        };
        foreach (var name in names) {
            events.Publish(name, new Dictionary<string, string> {
                ["watch_id"] = "watch-external",
                ["marker"] = name,
            });
        }

        Task<LuaActorWatchRegistration> Watch(string query, CancellationToken _) => Task.FromResult(
            new LuaActorWatchRegistration(
                "watch-external",
                query,
                "found",
                "Athena Potato@Sargatanas",
                string.Empty,
                State(classJobId: 33)) { MatchCount = 1 });

        using var runner = new LuaScriptRunner(new LuaScriptContext(
            0,
            1,
            1,
            _ => { },
            RunId: "actor-reactions-test",
            DeclaredCapabilities: ["mop.game-state"],
            Events: events,
            WatchActor: Watch));

        await runner.RunAsync("""
            assert(mop.capabilities.has("mop.game-state", "4.0.0"))
            local query = "Athena Potato@Sargatanas"
            assert(mop.actors.next_event(query, "combat_action", 1).data.marker == "actor.combat_action")
            assert(mop.actors.next_event(query, "general_action", 1).data.marker == "actor.general_action")
            assert(mop.actors.next_event(query, "jump", 1).data.marker == "actor.jump")
            assert(mop.actors.next_event(query, "sprint", 1).data.marker == "actor.sprint")
            assert(mop.actors.next_event(query, "emote", 1).data.marker == "actor.emote_played")
            assert(mop.actors.next_event(query, "emote_state", 1).data.marker == "actor.emote_state")
            assert(mop.actors.next_event(query, "fashion_accessory", 1).data.marker == "actor.fashion_accessory")
            assert(mop.actors.next_event(query, "mount", 1).data.marker == "actor.mount")
            assert(mop.actors.next_event(query, "facewear", 1).data.marker == "actor.facewear")
            assert(mop.actors.next_event(query, "target", 1).data.marker == "actor.target")
            assert(mop.actors.next_event(query, "idle_pose", 1).data.marker == "actor.idle_pose")
            assert(mop.actors.next_event(query, "weapon", 1).data.marker == "actor.weapon")
            """, CancellationToken.None);

        Assert.True(events.TryRead("actor.jump", out var preserved));
        Assert.Equal("watch-other", preserved?.Data["watch_id"]);
    }

    [Fact]
    public async Task GameStateProvider_ReportsUnavailableNativeEdgeSourcesInsteadOfSilentlyTimingOut() {
        using var runner = new LuaScriptRunner(new LuaScriptContext(
            0,
            1,
            1,
            _ => { },
            RunId: "actor-event-health-test",
            DeclaredCapabilities: ["mop.game-state"],
            GetActorEventSources: () => new LuaActorEventSources(
                StateChanges: true,
                CombatActions: false,
                Emotes: false)));

        await runner.RunAsync("""
            local sources = mop.actors.event_sources()
            assert(sources.state_changes)
            assert(not sources.combat_actions and not sources.general_actions and not sources.emotes)
            local action = mop.actors.next_event("Any Player", "combat_action", 1)
            assert(action.status == "unavailable" and action.source == "combat_action_hook")
            local emote = mop.actors.next_event("Any Player", "emote", 1)
            assert(emote.status == "unavailable" and emote.source == "emote_hook")
            """, CancellationToken.None);
    }

    [Fact]
    public async Task GameStateProvider_VisibilityAndProximityWaitsUseCachedWatches() {
        var targetCalls = 0;
        Task<LuaActorWatchRegistration> Watch(string query, CancellationToken _) {
            if (query == LuaActorWatchService.SelfQuery) {
                return Task.FromResult(new LuaActorWatchRegistration(
                    "watch-self", query, "found", "Artemis Potato@Sargatanas", string.Empty,
                    State(gameObjectId: 100, entityId: 200, positionX: 0)) { Revision = 1 });
            }

            targetCalls++;
            return Task.FromResult(targetCalls < 3
                ? new LuaActorWatchRegistration(
                    "watch-target", query, "found", "Athena Potato@Sargatanas", string.Empty,
                    State(gameObjectId: 300, entityId: 400, positionX: 0.5f)) { Revision = 1 }
                : new LuaActorWatchRegistration(
                    "watch-target", query, "missing", "Athena Potato@Sargatanas", string.Empty, null) {
                    Revision = 2,
                    Changes = LuaActorWatchChange.Availability,
                });
        }

        using var runner = new LuaScriptRunner(new LuaScriptContext(
            0,
            1,
            1,
            _ => { },
            RunId: "cached-waits-test",
            DeclaredCapabilities: ["mop.game-state"],
            CaptureGameSnapshot: _ => throw new InvalidOperationException("broad snapshot must not be used"),
            WatchActor: Watch));

        await runner.RunAsync("""
            local visible = mop.actors.wait_visible("Athena Potato@Sargatanas", 1)
            assert(visible.status == "found" and visible.actor.game_object_id == "300")
            local close = mop.actors.wait_proximity("Athena Potato@Sargatanas", 1, 1)
            assert(close.status == "found" and close.distance == 0.5)
            local lost = mop.actors.wait_lost("Athena Potato@Sargatanas", 1)
            assert(lost.status == "lost" and lost.revision == 2)
            """, CancellationToken.None);

        Assert.Equal(3, targetCalls);
    }

    [Fact]
    public async Task GameStateProvider_ArbitraryActorJobWaitReportsIdentityFailuresExplicitly() {
        Task<LuaActorWatchRegistration> Watch(string query, CancellationToken _) => Task.FromResult(query switch {
            "duplicate" => new LuaActorWatchRegistration(
                "watch-ambiguous", query, "ambiguous", string.Empty, string.Empty, null) { MatchCount = 2 },
            "missing" => new LuaActorWatchRegistration(
                "watch-missing", query, "missing", string.Empty, string.Empty, null),
            _ => new LuaActorWatchRegistration(
                "watch-other", query, "found", "Other Player@World", string.Empty,
                State(gameObjectId: 999, classJobId: 33)) { Revision = 1 },
        });

        using var runner = new LuaScriptRunner(new LuaScriptContext(
            0,
            1,
            1,
            _ => { },
            RunId: "actor-identity-test",
            DeclaredCapabilities: ["mop.game-state"],
            WatchActor: Watch));

        await runner.RunAsync("""
            assert(mop.actors.job("duplicate").status == "ambiguous")
            local visible = mop.actors.wait_visible("duplicate", 1)
            assert(visible.status == "ambiguous" and visible.count == 2)
            local lost = mop.actors.wait_lost("duplicate", 1)
            assert(lost.status == "ambiguous" and lost.count == 2)
            local proximity = mop.actors.wait_proximity("duplicate", 3, 1)
            assert(proximity.status == "ambiguous" and proximity.count == 2)
            assert(mop.actors.wait_job_changed("missing", 24, 1).status == "actor_lost")
            local rebound = mop.actors.wait_job_changed("other", 24, 1, "100")
            assert(rebound.status == "actor_changed")
            assert(rebound.actor.game_object_id == "999")
            """, CancellationToken.None);
    }

    private static LuaActorWatchState State(
        ulong gameObjectId = 100,
        uint entityId = 200,
        float positionX = 0f,
        float positionY = 10f,
        float positionZ = 0f,
        bool jumping = false,
        long jumpSequence = 0,
        ulong targetId = 0,
        uint mountId = 0,
        uint emoteId = 0,
        bool emoteLooping = false,
        byte poseType = 0,
        byte poseState = 0,
        uint ornamentId = 0,
        uint facewearId = 0,
        bool sprinting = false,
        bool weaponDrawn = false,
        uint classJobId = 1) => new(
        gameObjectId,
        entityId,
        targetId,
        positionX,
        positionY,
        positionZ,
        true,
        false,
        true,
        false,
        jumping,
        jumpSequence,
        mountId,
        0,
        emoteId,
        0,
        emoteLooping,
        poseType,
        poseState,
        ornamentId,
        facewearId,
        sprinting,
        weaponDrawn,
        0,
        classJobId);
}
