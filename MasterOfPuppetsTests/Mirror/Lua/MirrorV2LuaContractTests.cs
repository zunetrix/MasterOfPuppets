using MasterOfPuppets.LuaScripting.Runtime;

using Xunit;

namespace MasterOfPuppetsTests.Mirror.Lua;

public sealed class MirrorV2LuaContractTests {
    private static readonly string ScriptPath = Path.Combine(
        FindRepositoryRoot(AppContext.BaseDirectory),
        "MasterOfPuppets", "Lua", "Scripts", "mirror_v2.lua");

    [Fact]
    public void Script_IsValidLua() {
        var result = LuaScriptValidator.Validate(File.ReadAllText(ScriptPath), "@mirror_v2.lua");

        Assert.True(result.IsValid, result.DisplayMessage);
    }

    [Fact]
    public void Script_RequiresImmutableLaunchIdentityAndBoundsTransientLoss() {
        var source = File.ReadAllText(ScriptPath);

        Assert.Contains("mop_run_target_launch_game_object_id", source, StringComparison.Ordinal);
        Assert.Contains("mop_run_target_launch_entity_id", source, StringComparison.Ordinal);
        Assert.Contains("event.name == \"actor.lost\" and data.watch_id == target_watch_id", source, StringComparison.Ordinal);
        Assert.Contains("TARGET_LOSS_GRACE_SECONDS", source, StringComparison.Ordinal);
        Assert.Contains("begin_target_loss_grace", source, StringComparison.Ordinal);
        Assert.Contains("accept_recovered_target", source, StringComparison.Ordinal);
        Assert.Contains("Queue pressure is not evidence that the target was lost", source, StringComparison.Ordinal);
        Assert.DoesNotContain("event pressure made target continuity unverifiable", source, StringComparison.Ordinal);
        Assert.Contains("mop.runtime.stop", source, StringComparison.Ordinal);
        Assert.Contains("mop.capabilities.require(\"mop.runtime\", \"2.2\")", source, StringComparison.Ordinal);
        Assert.Contains("mop.runtime.variable", source, StringComparison.Ordinal);
        Assert.Contains("mop.runtime.shared_time", source, StringComparison.Ordinal);
        Assert.DoesNotContain("mop.get_slot", source, StringComparison.Ordinal);
        Assert.DoesNotContain("mop.get_var", source, StringComparison.Ordinal);
        Assert.DoesNotContain("mop.is_running", source, StringComparison.Ordinal);
        Assert.DoesNotContain("mop.time()", source, StringComparison.Ordinal);
        Assert.Contains("mirror.v2.stop", source, StringComparison.Ordinal);
        Assert.Contains("target_stop_reason_code", source, StringComparison.Ordinal);
        Assert.Contains("legacy-unspecified", source, StringComparison.Ordinal);
        Assert.DoesNotContain("wait_visible", source, StringComparison.Ordinal);
        Assert.DoesNotContain("target_via_leader", source, StringComparison.Ordinal);
        Assert.DoesNotContain("set_anchor", source, StringComparison.Ordinal);
        Assert.DoesNotContain("switch_target", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Script_DoesNotUseRandomFallbackOrFunctionalDialogue() {
        var source = File.ReadAllText(ScriptPath);

        Assert.DoesNotContain("math.random", source, StringComparison.Ordinal);
        Assert.DoesNotContain("mop.say", source, StringComparison.Ordinal);
        Assert.DoesNotContain("mop.chat", source, StringComparison.Ordinal);
        Assert.Contains("enabled = false", source, StringComparison.Ordinal);
        Assert.Contains("request_fallback", source, StringComparison.Ordinal);
        Assert.Contains("mop.actions.fallback_candidates", source, StringComparison.Ordinal);
        Assert.Contains("candidate_masks", source, StringComparison.Ordinal);
        Assert.Contains("selected_count", source, StringComparison.Ordinal);
        Assert.Contains("unresolved", source, StringComparison.Ordinal);
        Assert.Contains("universe_signature", source, StringComparison.Ordinal);
    }

    [Fact]
    public void CoordinationPayloadsHaveExplicitNinetySixByteGuards() {
        var source = File.ReadAllText(ScriptPath);

        Assert.Contains("if #payload > 96", source, StringComparison.Ordinal);
        Assert.Contains("if #key > 32 or #value > 96", source, StringComparison.Ordinal);
        Assert.Contains("mop.messages.send(topic, payload, target_slot, PROTOCOL)", source, StringComparison.Ordinal);
        Assert.Contains("mop.messages.broadcast(topic, payload, PROTOCOL)", source, StringComparison.Ordinal);
        Assert.DoesNotContain("payload, nil, PROTOCOL", source, StringComparison.Ordinal);
    }

    [Fact]
    public void ContinuousStateUsesPerModuleAuthoritativeRevisions() {
        var source = File.ReadAllText(ScriptPath);

        Assert.Contains("set_shared(\"m2.s.\" .. module_id, payload)", source, StringComparison.Ordinal);
        Assert.Contains("owner.authoritative_revision", source, StringComparison.Ordinal);
        Assert.Contains("owner.authoritative_key", source, StringComparison.Ordinal);
        Assert.Contains("changed_shared(\"m2.s.\" .. owner.id)", source, StringComparison.Ordinal);
        Assert.DoesNotContain("set_shared(\"m2.state\"", source, StringComparison.Ordinal);
    }

    [Fact]
    public void EmoteFallbackSkipsRemainInsideTheOutcomeBarrier() {
        var source = File.ReadAllText(ScriptPath);

        Assert.Contains("\"2\", \"O\", emote_barrier.epoch, local_slot, \"skip\"", source, StringComparison.Ordinal);
        Assert.DoesNotContain("and not round.skipped[slot]", source, StringComparison.Ordinal);
    }

    [Fact]
    public void EmoteRetryUsesVerifiedWholeRosterResetWithoutReadinessTimeout() {
        var source = File.ReadAllText(ScriptPath);

        Assert.Contains("Missing readiness never expires", source, StringComparison.Ordinal);
        Assert.Contains("mop.actions.stop_emote()", source, StringComparison.Ordinal);
        Assert.Contains("\"2\", \"Z\", reset.epoch, local_slot", source, StringComparison.Ordinal);
        Assert.Contains("emote_barrier.phase = \"P\"", source, StringComparison.Ordinal);
        Assert.Contains("mop.runtime.broadcast_emote_resync", source, StringComparison.Ordinal);
        Assert.Contains("EMOTE_RETRY_MIN_SECONDS", source, StringComparison.Ordinal);
        Assert.Contains("remote_resync_sent", source, StringComparison.Ordinal);
        Assert.Contains("previous_phase ~= \"F\"", source, StringComparison.Ordinal);
        Assert.DoesNotContain("EMOTE_RESET_VERIFIED", source, StringComparison.Ordinal);
        Assert.DoesNotContain("emote_timeout", source, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void MovementInvalidatesCachedEmotesAndWithdrawsReadiness() {
        var source = File.ReadAllText(ScriptPath);

        Assert.Contains("target_state.is_moving", source, StringComparison.Ordinal);
        Assert.Contains("supersede_emote_if_invalid(target_state)", source, StringComparison.Ordinal);
        Assert.Contains("handle_local_emote_movement()", source, StringComparison.Ordinal);
        Assert.Contains("\"2\", \"U\", emote_barrier.epoch, local_slot", source, StringComparison.Ordinal);
        Assert.Contains("fields[2] == \"U\" and emote_barrier.phase == \"P\"", source, StringComparison.Ordinal);
        Assert.Contains("self_state == nil or self_state.is_moving", source, StringComparison.Ordinal);
    }

    [Fact]
    public void TargetJumpSequenceDispatchesOneNativeGeneralActionPerEdge() {
        var source = File.ReadAllText(ScriptPath);

        Assert.Contains("jump_sequence = number(actor.jump_sequence)", source, StringComparison.Ordinal);
        Assert.Contains("local last_target_jump_sequence", source, StringComparison.Ordinal);
        Assert.Contains("local function mirror_target_jump(current)", source, StringComparison.Ordinal);
        Assert.Contains("sequence <= last_target_jump_sequence", source, StringComparison.Ordinal);
        Assert.Contains("mop.actions.jump()", source, StringComparison.Ordinal);
        Assert.Contains("if local_is_target then return end", source, StringComparison.Ordinal);
    }

    [Fact]
    public void EmoteSuccessRequiresFreshPlaybackEdgeInsteadOfCachedSnapshot() {
        var source = File.ReadAllText(ScriptPath);

        Assert.Contains("expected_emote_id", source, StringComparison.Ordinal);
        Assert.Contains("number(data.emote_id) == emote_module.pending.expected_emote_id", source, StringComparison.Ordinal);
        Assert.Contains("Snapshot equality is not proof of a new emote entry", source, StringComparison.Ordinal);
        Assert.DoesNotContain("and emote_module.pending.verify(self_state)", source, StringComparison.Ordinal);
    }

    [Fact]
    public void ResetAlwaysClearsNativePersistentVisualBeforeAcceptingIdleState() {
        var source = File.ReadAllText(ScriptPath);

        Assert.Contains("stop_issued = false", source, StringComparison.Ordinal);
        Assert.Contains("if not reset.stop_issued then", source, StringComparison.Ordinal);
        Assert.Contains("if not local_is_target then mop.actions.stop_emote() end", source, StringComparison.Ordinal);
    }

    [Fact]
    public void DynamicRecipientDepartureShrinksOnlyTheActiveBarrier() {
        var source = File.ReadAllText(ScriptPath);

        Assert.Contains("MEMBER_SILENCE_SECONDS", source, StringComparison.Ordinal);
        Assert.Contains("emote_barrier.mask = mask_remove", source, StringComparison.Ordinal);
        Assert.Contains("only future event", source, StringComparison.Ordinal);
        Assert.Contains("participant_by_slot[slot] = tostring(participant.content_id", source, StringComparison.Ordinal);
        Assert.DoesNotContain("mask_add", source, StringComparison.Ordinal);
        Assert.DoesNotContain("$all_configured", source, StringComparison.Ordinal);
        Assert.DoesNotContain("mop.get_group", source, StringComparison.Ordinal);
    }

    [Fact]
    public void WalkModeIsMirroredWithReadback() {
        var source = File.ReadAllText(ScriptPath);

        Assert.Contains("local walk_module = module(\"walk\", \"k\")", source, StringComparison.Ordinal);
        Assert.Contains("is_walking = boolean(actor.is_walking)", source, StringComparison.Ordinal);
        Assert.Contains("local is_walking = target_state.is_walking", source, StringComparison.Ordinal);
        Assert.Contains("mop.actions.walk(is_walking and \"on\" or \"off\", \"local\")", source, StringComparison.Ordinal);
        Assert.Contains("state.is_walking == is_walking", source, StringComparison.Ordinal);
    }

    [Fact]
    public void EmotesAreExactOnlyWithoutFallbackSubstitution() {
        var source = File.ReadAllText(ScriptPath);

        Assert.Contains("local emote_module = module(\"emote\", \"e\")", source, StringComparison.Ordinal);
        Assert.DoesNotContain("fallback_required = true, fallback_kind = \"emote\"", source, StringComparison.Ordinal);
        Assert.Contains("\"2\", \"O\", epoch, local_slot, \"skip\"", source, StringComparison.Ordinal);
    }

    [Fact]
    public void VerifiedHeadgearAndVisorSurfacesAreMirroredWithReadback() {
        var source = File.ReadAllText(ScriptPath);

        Assert.Contains("mop.capabilities.require(\"mop.actions\", \"4.0\")", source, StringComparison.Ordinal);
        Assert.Contains("is_visor_toggled", source, StringComparison.Ordinal);
        Assert.Contains("mop.actions.visor(visor_enabled)", source, StringComparison.Ordinal);
        Assert.Contains("state.is_visor_toggled == visor_enabled", source, StringComparison.Ordinal);
        Assert.Contains("mop.actions.headgear_visible(headgear_visible)", source, StringComparison.Ordinal);
        Assert.Contains("state.is_headgear_visible == headgear_visible", source, StringComparison.Ordinal);
        Assert.Contains("mop.runtime.global_stop", source, StringComparison.Ordinal);
    }

    private static string FindRepositoryRoot(string start) {
        var current = new DirectoryInfo(start);
        while (current != null) {
            if (File.Exists(Path.Combine(current.FullName, "MasterOfPuppets.sln")))
                return current.FullName;
            current = current.Parent;
        }
        throw new DirectoryNotFoundException("Could not locate repository root.");
    }
}
