using MasterOfPuppets.LuaScripting;
using MasterOfPuppets.LuaScripting.Automation;

using Xunit;

public sealed class MirrorRunTargetValidationTests {
    [Fact]
    public void SelectedLoadedPlayer_IsAcceptedAsExplicitImmutableTarget() {
        var selected = Player("Target Example@Moogle", 100, 200, 300);
        var initiator = Player("Initiator Example@Moogle", 101, 201, 301);

        var decision = MirrorRunTargetValidator.Decide(selected, initiator);

        Assert.True(decision.Success, decision.Error);
        Assert.Equal("Target Example@Moogle", decision.Identity.Name);
        Assert.Equal(100UL, decision.Identity.GameObjectId);
        Assert.Equal(200U, decision.Identity.EntityId);
        Assert.Equal(300UL, decision.Identity.ContentId);
        Assert.True(decision.Identity.WasExplicitlySelected);
        Assert.Equal("selected-player", decision.Identity.Source);
    }

    [Fact]
    public void NoSelection_UsesValidatedLocalInitiator() {
        var decision = MirrorRunTargetValidator.Decide(
            default,
            Player("Initiator Example@Moogle", 101, 201, 301));

        Assert.True(decision.Success, decision.Error);
        Assert.Equal("Initiator Example@Moogle", decision.Identity.Name);
        Assert.False(decision.Identity.WasExplicitlySelected);
        Assert.Equal("local-initiator", decision.Identity.Source);
    }

    [Fact]
    public void SelectedNonPlayer_IsRejectedWithoutInitiatorFallback() {
        var selected = new MirrorRunTargetCandidate(
            true,
            false,
            true,
            500,
            600,
            0,
            "Training Dummy",
            "BattleNpc");

        var decision = MirrorRunTargetValidator.Decide(
            selected,
            Player("Initiator Example@Moogle", 101, 201, 301));

        Assert.False(decision.Success);
        Assert.Equal(default, decision.Identity);
        Assert.Contains(LuaScriptCatalog.MirrorScriptV2Name, decision.Error);
        Assert.DoesNotContain(LuaScriptCatalog.DefaultMirrorTargetCombatName, decision.Error);
        Assert.Contains("loaded real-player target", decision.Error);
        Assert.Contains("BattleNpc", decision.Error);
    }

    [Fact]
    public void SelectedUnloadedPlayer_IsRejectedWithoutInitiatorFallback() {
        var selected = Player("Target Example@Moogle", 100, 200, 300) with { IsLoaded = false };

        var decision = MirrorRunTargetValidator.Decide(
            selected,
            Player("Initiator Example@Moogle", 101, 201, 301));

        Assert.False(decision.Success);
        Assert.Contains("loaded real-player target", decision.Error);
    }

    [Fact]
    public void NoSelection_WithUnloadedInitiator_IsRejected() {
        var initiator = Player("Initiator Example@Moogle", 101, 201, 301) with { IsLoaded = false };

        var decision = MirrorRunTargetValidator.Decide(default, initiator);

        Assert.False(decision.Success);
        Assert.Contains(LuaScriptCatalog.MirrorScriptV2Name, decision.Error);
        Assert.DoesNotContain(LuaScriptCatalog.DefaultMirrorTargetCombatName, decision.Error);
        Assert.Contains("local player is not fully loaded", decision.Error);
    }

    [Theory]
    [InlineData(0UL, 200U, "Target Example@Moogle")]
    [InlineData(0xE0000000UL, 200U, "Target Example@Moogle")]
    [InlineData(100UL, 0U, "Target Example@Moogle")]
    [InlineData(100UL, 0xE0000000U, "Target Example@Moogle")]
    [InlineData(100UL, 200U, "  ")]
    public void SelectedPlayer_WithInvalidIdentityEvidence_IsRejected(
        ulong gameObjectId,
        uint entityId,
        string name) {
        var decision = MirrorRunTargetValidator.Decide(
            Player(name, gameObjectId, entityId, 300),
            Player("Initiator Example@Moogle", 101, 201, 301));

        Assert.False(decision.Success);
    }

    [Fact]
    public void ScopeRouting_AppliesOnlyToMirrorScriptV2() {
        Assert.True(MirrorRunTargetValidator.AppliesTo(LuaScriptCatalog.MirrorScriptV2Name));
        Assert.True(MirrorRunTargetValidator.AppliesTo("mirror script v2"));
        Assert.False(MirrorRunTargetValidator.AppliesTo(LuaScriptCatalog.DefaultMirrorTargetCombatName));
        Assert.False(MirrorRunTargetValidator.AppliesTo("Dynamic Conga Line"));
        Assert.False(MirrorRunTargetValidator.AppliesTo(null));
    }

    [Theory]
    [InlineData(LuaScriptCatalog.MirrorScriptV2Name, true)]
    [InlineData(LuaScriptCatalog.DefaultMirrorTargetJobName, true)]
    [InlineData(LuaScriptCatalog.DefaultMirrorTargetCombatName, true)]
    [InlineData(LuaScriptCatalog.DefaultMirrorTargetEmotesName, true)]
    [InlineData("Dynamic Conga Line", false)]
    [InlineData(null, false)]
    public void PlayerRunTargetRouting_CoversEveryTargetBoundMirrorScript(
        string? scriptName,
        bool expected) {
        Assert.Equal(expected, MirrorRunTargetValidator.RequiresPlayerRunTarget(scriptName));
    }

    [Fact]
    public void TargetValidation_UsesTheCallingScriptNameInErrors() {
        var decision = MirrorRunTargetValidator.Decide(
            new MirrorRunTargetCandidate(
                true, false, true, 500, 600, 0, "Training Dummy", "BattleNpc"),
            Player("Initiator Example@Moogle", 101, 201, 301),
            LuaScriptCatalog.DefaultMirrorTargetJobName);

        Assert.False(decision.Success);
        Assert.Contains(LuaScriptCatalog.DefaultMirrorTargetJobName, decision.Error);
        Assert.DoesNotContain(LuaScriptCatalog.MirrorScriptV2Name, decision.Error);
    }

    private static MirrorRunTargetCandidate Player(
        string name,
        ulong gameObjectId,
        uint entityId,
        ulong contentId) =>
        new(
            true,
            true,
            true,
            gameObjectId,
            entityId,
            contentId,
            name,
            "Player");
}
