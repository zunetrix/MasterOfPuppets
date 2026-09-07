using Lua;
using Lua.Standard;
using MasterOfPuppets;
using MasterOfPuppets.LuaScripting;
using MasterOfPuppets.LuaScripting.Automation;
using MasterOfPuppets.LuaScripting.Runs;
using Xunit;

namespace MasterOfPuppetsTests;

public class MirrorCombatTargetTests {
    [Fact]
    public void PackagedDefaultIsAddedOnceWithoutChangingOriginalOrUserEdits() {
        var original = new LuaScriptDefinition { Name = "Mirror Target Combat", Source = "original" };
        // Configuration's constructor reads live Dalamud services. This unit
        // test exercises only its catalog, outside a running game client.
        var config = (Configuration)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(Configuration));
        config.LuaScripts = new();
        config.LuaScripts.Add(original);
        Assert.True(LuaScriptCatalog.EnsureMirrorCombatTarget(config));
        var added = Assert.Single(config.LuaScripts, script => script.Name == "Mirror Combat Target");
        Assert.True(Guid.TryParse(added.Id, out _));
        Assert.Equal(1, added.Revision);
        Assert.Equal(LuaResourceKind.GameActions, added.RequiredResources);
        Assert.Contains("mop.events", added.DeclaredCapabilities);
        Assert.Contains("$all_configured = true", added.Variables);
        added.Source = "user customization";
        Assert.False(LuaScriptCatalog.EnsureMirrorCombatTarget(config));
        Assert.Equal("user customization", added.Source);
        Assert.Same(original, config.LuaScripts[0]);
        Assert.Equal("original", original.Source);
        Assert.Equal(2, config.LuaScripts.Count);
    }

    [Fact]
    public void UsesTheSamePlayerAnchorRoutingAsTheOriginalMirror() {
        Assert.True(MirrorRunTargetValidator.AppliesTo("Mirror Combat Target"));
        Assert.True(MirrorRunTargetValidator.RequiresPlayerRunTarget("Mirror Combat Target"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MirrorsActionsAndHandlesAnchorChangesWithoutSelectingAnyTarget(bool initiallyHidden) {
        var source = LuaScriptCatalog.LoadPackagedScript("mirror_combat_target.lua");
        Assert.DoesNotContain("mop.actions.target", source);
        Assert.DoesNotContain("mop.actions.use_on", source);
        Assert.DoesNotContain("mop.actions.use_exact_on", source);
        using var state = LuaState.Create();
        state.OpenBasicLibrary();
        state.OpenStringLibrary();
        state.OpenTableLibrary();
        state.Environment["initially_hidden"] = initiallyHidden;
        await state.DoStringAsync("""
            now, steps, delivered, watch_calls = 0, 0, 0, 0
            ordinary, emotes, ground = 0, 0, 0
            anchor_name = "Source"
            local self_actor = { entity_id=1, game_object_id="1", is_loaded=true,
                target_game_object_id="99", pose_type=1, pose_state=0 }
            local source_actor = { entity_id=2, game_object_id="2", is_loaded=true,
                target_game_object_id="44", pose_type=1, pose_state=0 }
            local function event(name, data) return {status="event", name=name, data=data} end
            mop = {
                get_var=function(name)
                    if name=="anchor" then return anchor_name end
                    if name=="mop_run_target_explicit" then return "true" end
                end,
                get_run_target=function() return "Source" end,
                names_match=function(a,b) return a==b end,
                capabilities={require=function() end},
                log=function() end,
                time=function() return now end,
                is_running=function() return delivered<8 end,
                self={watch=function() return {status="found", actor=self_actor, watch_id="self"} end},
                actors={
                    watch=function(name)
                        watch_calls=watch_calls+1
                        if initially_hidden and watch_calls<3 then return {status="missing", watch_id="anchor"} end
                        return {status="found", actor=source_actor, watch_id="anchor"}
                    end,
                    unwatch=function() end
                },
                actions=setmetatable({
                    use=function(kind,id,scope)
                        assert(kind=="action" and id==42 and scope=="local")
                        ordinary=ordinary+1
                        return {ok=true}
                    end,
                    use_exact=function(kind,id,scope,persistent)
                        assert(kind=="emote" and id==90 and scope=="local" and persistent==false)
                        emotes=emotes+1
                        return {ok=true}
                    end,
                    use_ground_on=function(kind,id,target,x,y,z)
                        assert(kind=="action" and id==43 and target=="44" and x==1 and y==2 and z==3)
                        ground=ground+1
                        return {ok=true}
                    end
                }, {__index=function(_,key) error("Unexpected action, potentially retargeting: "..key) end}),
                events={
                    set_game_sampling=function() end,
                    next=function()
                        steps=steps+1
                        now=now+0.25
                        assert(steps<30, "script did not terminate")
                        if initially_hidden and watch_calls<3 then return {status="timeout"} end
                        delivered=delivered+1
                        if delivered==1 then return event("actor.combat_action", {
                            source_entity_id=2, global_sequence="1", action_id=42,
                            action_type=1, animation_target_id="44"}) end
                        if delivered==2 then return event("actor.emote_played", {
                            source_entity_id=2, emote_id=90, target_id="44", is_persistent=false}) end
                        if delivered==3 then return event("actor.combat_action", {
                            source_entity_id=2, global_sequence="2", action_id=43, action_type=1,
                            animation_target_id="44", is_ground_targeted=true, target_x=1, target_y=2, target_z=3}) end
                        if delivered==4 then
                            source_actor.target_game_object_id="55"
                            source_actor.watch_id="anchor"
                            return event("actor.state", source_actor)
                        end
                        if delivered==5 then return event("actor.lost", {watch_id="anchor"}) end
                        if delivered==6 then return event("actor.found", source_actor) end
                        if delivered==7 then
                            anchor_name="Second Source"
                            return event("run.variables-updated", {})
                        end
                        return {status="timeout"}
                    end
                }
            }
            """ + "\n" + source + """

            assert(ordinary==1, "ordinary action not mirrored exactly once")
            assert(emotes==1, "emote not mirrored exactly once")
            assert(ground==1, "ground action not mirrored exactly once")
            assert(self_actor.target_game_object_id=="99", "client selection changed")
            assert(anchor_name=="Second Source", "anchor update was not processed")
            """);
    }
}
