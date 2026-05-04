using MasterOfPuppets;
using MasterOfPuppets.Ipc;

using Xunit;

public class PartyInvitePromptMatcherTests {
    private const string PartyInviteAddonRow = "Join <string(lstr1)>'s party?";

    [Fact]
    public void TryBuildExtractionExpression_Uses_String_Payload_As_Inviter_Slot() {
        Assert.True(PartyInvitePromptMatcher.TryBuildExtractionExpression(
            PartyInviteAddonRow,
            out var expression,
            out var reason));

        Assert.Equal("Join ", expression.Prefix);
        Assert.Equal("'s party?", expression.Suffix);
        Assert.Equal("party invite sheet expression parsed", reason);
    }

    [Fact]
    public void TryBuildExtractionExpression_Rejects_Row_With_No_String_Payload() {
        Assert.False(PartyInvitePromptMatcher.TryBuildExtractionExpression(
            "Join 's party?",
            out var expression,
            out var reason));

        Assert.Equal("Join 's party?", expression.SourceText);
        Assert.Equal(string.Empty, expression.Prefix);
        Assert.Equal(string.Empty, expression.Suffix);
        Assert.Equal("party invite sheet expression has no string payload", reason);
    }

    [Fact]
    public void ParsePartyInvitePrompt_Extracts_Inviter_From_Addon_Row_Expression() {
        var result = PartyInvitePromptMatcher.ParsePartyInvitePrompt(
            "Join Alice \ue0bb Diabolos's party?",
            PartyInviteAddonRow);

        Assert.True(result.IsPartyInvite);
        Assert.True(result.SheetExpressionAvailable);
        Assert.Equal("Alice \ue0bb Diabolos", result.InviterSegment);
        Assert.Equal("party invite sheet expression parsed", result.SheetExpressionReason);
    }

    [Fact]
    public void ParsePartyInvitePrompt_Rejects_When_Sheet_Expression_Is_Unavailable() {
        var result = PartyInvitePromptMatcher.ParsePartyInvitePrompt(
            "Join Alice@Diabolos's party?",
            string.Empty);

        Assert.False(result.IsPartyInvite);
        Assert.False(result.SheetExpressionAvailable);
        Assert.Equal("party invite sheet expression unavailable", result.Reason);
    }

    [Fact]
    public void ParsePartyInvitePrompt_Rejects_NonParty_Prompt() {
        var result = PartyInvitePromptMatcher.ParsePartyInvitePrompt(
            "Accept Teleport to Limsa Lominsa?",
            PartyInviteAddonRow);

        Assert.False(result.IsPartyInvite);
        Assert.True(result.SheetExpressionAvailable);
        Assert.Equal("prompt does not start with expression prefix", result.Reason);
    }

    [Fact]
    public void TryGetPartyInviteInviterSegment_Extracts_Inviter_With_Sheet_Expression() {
        Assert.True(PartyInvitePromptMatcher.TryGetPartyInviteInviterSegment(
            "Join Alice@Diabolos's party?",
            PartyInviteAddonRow,
            out var inviter));
        Assert.Equal("Alice@Diabolos", inviter);
    }

    [Fact]
    public void LegacyPartyInviteHelpers_Do_Not_Parse_English_Without_Sheet_Expression() {
        Assert.False(PartyInvitePromptMatcher.ShouldAcceptPartyInvite(
            "Join Alice@Diabolos's party?",
            onlyFromCharacters: false,
            characterNames: []));

        Assert.False(PartyInvitePromptMatcher.IsInviterInConnectedConfiguredCharacters(
            "Join Alice@Diabolos's party?",
            [new Character { Cid = 1001, Name = "Alice@Diabolos" }],
            [Peer(1001, "Alice", "Diabolos", DateTime.UtcNow)]));
    }

    [Fact]
    public void ConnectedConfiguredInvite_Accepts_When_Peer_Is_Configured_By_Cid() {
        var now = DateTime.UtcNow;

        var decision = PartyInvitePromptMatcher.EvaluateConnectedConfiguredInvite(
            "Join Alice@Diabolos's party?",
            PartyInviteAddonRow,
            [new Character { Cid = 1001, Name = "Alice@Diabolos" }],
            [Peer(1001, "Alice", "Diabolos", now)],
            now);

        Assert.True(decision.ShouldAccept);
        Assert.Contains("cid:1001", decision.Reason);
    }

    [Fact]
    public void ConnectedConfiguredInvite_Accepts_When_Prompt_Uses_Special_World_Separator() {
        var now = DateTime.UtcNow;

        var decision = PartyInvitePromptMatcher.EvaluateConnectedConfiguredInvite(
            "Join Alice \ue0bb Diabolos's party?",
            PartyInviteAddonRow,
            [new Character { Cid = 1001, Name = "Alice@Diabolos" }],
            [Peer(1001, "Alice", "Diabolos", now)],
            now);

        Assert.True(decision.ShouldAccept);
        Assert.Equal("Alice \ue0bb Diabolos", decision.Parse.InviterSegment);
        Assert.Contains("home world", decision.Reason);
    }

    [Fact]
    public void ConnectedConfiguredInvite_Accepts_NameOnly_Prompt_When_Configured_By_Cid() {
        var now = DateTime.UtcNow;

        var decision = PartyInvitePromptMatcher.EvaluateConnectedConfiguredInvite(
            "Join Alice's party?",
            PartyInviteAddonRow,
            [new Character { Cid = 1001, Name = "Alice@Diabolos" }],
            [Peer(1001, "Alice", "Diabolos", now)],
            now);

        Assert.True(decision.ShouldAccept);
        Assert.Contains("name-only prompt", decision.Reason);
    }

    [Fact]
    public void ConnectedConfiguredInvite_Rejects_Configured_But_Not_Connected() {
        var now = DateTime.UtcNow;

        var decision = PartyInvitePromptMatcher.EvaluateConnectedConfiguredInvite(
            "Join Alice@Diabolos's party?",
            PartyInviteAddonRow,
            [new Character { Cid = 1001, Name = "Alice@Diabolos" }],
            [],
            now);

        Assert.False(decision.ShouldAccept);
        Assert.Equal("no fresh connected peer matched inviter segment", decision.Reason);
    }

    [Fact]
    public void ConnectedConfiguredInvite_Rejects_Connected_But_Not_Configured() {
        var now = DateTime.UtcNow;

        var decision = PartyInvitePromptMatcher.EvaluateConnectedConfiguredInvite(
            "Join Alice@Diabolos's party?",
            PartyInviteAddonRow,
            [new Character { Cid = 2002, Name = "Bob@Diabolos" }],
            [Peer(1001, "Alice", "Diabolos", now)],
            now);

        Assert.False(decision.ShouldAccept);
        Assert.Equal("Alice@Diabolos/Diabolos cid=1001", decision.MatchedPeer);
        Assert.Contains("peer is not configured", decision.Reason);
    }

    [Fact]
    public void ConnectedConfiguredInvite_Accepts_NameOnly_Prompt_When_Peer_And_Config_Match() {
        var now = DateTime.UtcNow;

        var decision = PartyInvitePromptMatcher.EvaluateConnectedConfiguredInvite(
            "Join Alice's party?",
            PartyInviteAddonRow,
            [new Character { Name = "Alice@Diabolos" }],
            [Peer(1001, "Alice", "Diabolos", now)],
            now);

        Assert.True(decision.ShouldAccept);
        Assert.Contains("name-only prompt", decision.Reason);
        Assert.Contains("configured character", decision.Reason);
    }

    [Fact]
    public void ConnectedConfiguredInvite_Rejects_World_Mismatch_When_Prompt_Has_World() {
        var now = DateTime.UtcNow;

        var decision = PartyInvitePromptMatcher.EvaluateConnectedConfiguredInvite(
            "Join Alice@Excalibur's party?",
            PartyInviteAddonRow,
            [new Character { Cid = 1001, Name = "Alice@Diabolos" }],
            [Peer(1001, "Alice", "Diabolos", now)],
            now);

        Assert.False(decision.ShouldAccept);
        Assert.Equal("no fresh connected peer matched inviter segment", decision.Reason);
    }

    [Fact]
    public void ConnectedConfiguredInvite_Rejects_Stale_Peer() {
        var now = DateTime.UtcNow;

        var decision = PartyInvitePromptMatcher.EvaluateConnectedConfiguredInvite(
            "Join Alice@Diabolos's party?",
            PartyInviteAddonRow,
            [new Character { Cid = 1001, Name = "Alice@Diabolos" }],
            [Peer(1001, "Alice", "Diabolos", now - TimeSpan.FromSeconds(6))],
            now);

        Assert.False(decision.ShouldAccept);
        Assert.Equal("no fresh connected peer matched inviter segment", decision.Reason);
    }

    [Fact]
    public void ConnectedConfiguredInvite_Reports_Diagnostic_Counts_For_Rejection() {
        var now = DateTime.UtcNow;

        var decision = PartyInvitePromptMatcher.EvaluateConnectedConfiguredInvite(
            "Join Alice@Diabolos's party?",
            PartyInviteAddonRow,
            [new Character { Cid = 1001, Name = "Alice@Diabolos" }],
            [Peer(1001, "Alice", "Diabolos", now - TimeSpan.FromSeconds(6))],
            now);

        Assert.False(decision.ShouldAccept);
        Assert.Equal(1, decision.ConnectedPeerCount);
        Assert.Equal(0, decision.FreshPeerCount);
        Assert.Equal("no fresh connected peer matched inviter segment", decision.Reason);
    }

    private static PeerCharacterInfo Peer(ulong cid, string name, string homeWorld, DateTime lastSeen) =>
        new(cid, name, homeWorld, 0, homeWorld, 0, lastSeen);
}
