using MasterOfPuppets;

using Xunit;

namespace MasterOfPuppetsTests;

public class AutoLoginPlannerTests {
    [Fact]
    public void FromCharacter_StripsStoredWorldName() {
        var candidate = AutoLoginCandidate.FromCharacter(new Character {
            Cid = 42,
            Name = "Test Character@Diabolos",
        });

        Assert.NotNull(candidate);
        Assert.Equal(42UL, candidate.Value.ContentId);
        Assert.Equal("Test Character", candidate.Value.Name);
    }

    [Fact]
    public void BuildWorldQueue_UsesCurrentWorldForMatchingCharacter() {
        var queue = AutoLoginPlanner.BuildWorldQueue(
            [new AutoLoginCandidate(42, "Test Character")],
            [
                LobbyEntry(42, "Test Character", currentWorld: "Halicarnassus", homeWorld: "Diabolos"),
                LobbyEntry(100, "Other Character", currentWorld: "Diabolos", homeWorld: "Diabolos"),
            ],
            ["Diabolos", "Halicarnassus", "Maduin"]);

        Assert.Equal(["Halicarnassus", "Diabolos"], queue);
    }

    [Fact]
    public void BuildWorldQueue_DoesNotAddWorldsWithoutLobbyCharacters() {
        var queue = AutoLoginPlanner.BuildWorldQueue(
            [new AutoLoginCandidate(999, "Missing Character")],
            [LobbyEntry(42, "Other Character", currentWorld: "Halicarnassus", homeWorld: "Diabolos")],
            ["Diabolos", "Halicarnassus", "Maduin"]);

        Assert.Equal(["Halicarnassus"], queue);
    }

    [Fact]
    public void BuildWorldQueue_FallsBackToAllVisibleWorldsWhenLobbyEntriesUnavailable() {
        var queue = AutoLoginPlanner.BuildWorldQueue(
            [new AutoLoginCandidate(999, "Missing Character")],
            [],
            ["Diabolos", "Halicarnassus"]);

        Assert.Equal(["Diabolos", "Halicarnassus"], queue);
    }

    [Fact]
    public void TryFindVisibleCandidate_MatchesByCandidateOrderInVisibleList() {
        var found = AutoLoginPlanner.TryFindVisibleCandidate(
            [
                new AutoLoginCandidate(42, "Test Character"),
                new AutoLoginCandidate(100, "Other Character"),
            ],
            ["Not It", "Other Character", "Test Character"],
            out var characterName,
            out var index);

        Assert.True(found);
        Assert.Equal("Other Character", characterName);
        Assert.Equal(1, index);
    }

    private static AutoLoginLobbyEntry LobbyEntry(
        ulong cid,
        string name,
        string currentWorld,
        string homeWorld) =>
        new(
            cid,
            name,
            currentWorld,
            0,
            homeWorld,
            0,
            string.Empty,
            currentWorld);
}
