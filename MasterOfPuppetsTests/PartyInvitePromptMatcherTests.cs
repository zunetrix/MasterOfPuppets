using MasterOfPuppets;

using Xunit;

public class PartyInvitePromptMatcherTests {
    [Theory]
    [InlineData("Join Alice@Diabolos's party?", "Alice@Diabolos")]
    [InlineData("Join Alice Diabolos's party?", "Alice@Diabolos")]
    [InlineData("Join Alice (Diabolos)'s party?", "Alice@Diabolos")]
    public void IsInviterInCharacterList_Matches_Name_And_World(string prompt, string savedCharacter) {
        Assert.True(PartyInvitePromptMatcher.IsInviterInCharacterList(prompt, [savedCharacter]));
    }

    [Fact]
    public void IsInviterInCharacterList_Matches_NameOnly_Prompt_To_Saved_NameWorld() {
        Assert.True(PartyInvitePromptMatcher.IsInviterInCharacterList(
            "Join Alice's party?",
            ["Alice@Diabolos"]));
    }

    [Fact]
    public void IsInviterInCharacterList_Does_Not_NameFallback_When_Prompt_Has_World() {
        Assert.False(PartyInvitePromptMatcher.IsInviterInCharacterList(
            "Join Alice@Diabolos's party?",
            ["Alice@Excalibur"]));
    }

    [Fact]
    public void IsInviterInCharacterList_Does_Not_Match_NonParty_Prompt() {
        Assert.False(PartyInvitePromptMatcher.IsInviterInCharacterList(
            "Accept Teleport to Limsa Lominsa?",
            ["Alice@Diabolos"]));
    }

    [Fact]
    public void ShouldAcceptPartyInvite_Allows_PartyInvite_When_Whitelist_Disabled() {
        Assert.True(PartyInvitePromptMatcher.ShouldAcceptPartyInvite(
            "Join Alice@Diabolos's party?",
            onlyFromCharacters: false,
            characterNames: []));
    }

    [Fact]
    public void ShouldAcceptPartyInvite_Blocks_NonParty_Prompt_When_Whitelist_Disabled() {
        Assert.False(PartyInvitePromptMatcher.ShouldAcceptPartyInvite(
            "Accept Teleport to Limsa Lominsa?",
            onlyFromCharacters: false,
            characterNames: []));
    }

    [Fact]
    public void TryGetPartyInviteInviterSegment_Extracts_Inviter() {
        Assert.True(PartyInvitePromptMatcher.TryGetPartyInviteInviterSegment(
            "Join Alice@Diabolos's party?",
            out var inviter));
        Assert.Equal("Alice@Diabolos", inviter);
    }
}
