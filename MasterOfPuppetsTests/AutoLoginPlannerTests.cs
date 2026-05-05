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
        Assert.Equal("Diabolos", candidate.Value.HomeWorldName);
    }

    [Fact]
    public void BuildWorldQueue_UsesCurrentWorldForMatchingCharacterFirst() {
        var queue = AutoLoginPlanner.BuildWorldQueue(
            [new AutoLoginCandidate(42, "Test Character")],
            [
                LobbyEntry(42, "Test Character", currentWorld: "Halicarnassus", homeWorld: "Diabolos"),
                LobbyEntry(100, "Other Character", currentWorld: "Diabolos", homeWorld: "Diabolos"),
            ],
            ["Diabolos", "Halicarnassus", "Cuchulainn"]);

        Assert.Equal(["Halicarnassus", "Diabolos", "Cuchulainn"], queue);
    }

    [Fact]
    public void BuildWorldQueue_MatchesByNameWhenContentIdIsMissing() {
        var queue = AutoLoginPlanner.BuildWorldQueue(
            [new AutoLoginCandidate(0, "Test Character")],
            [LobbyEntry(42, "Test Character", currentWorld: "Halicarnassus", homeWorld: "Diabolos")],
            ["Diabolos", "Halicarnassus"]);

        Assert.Equal(["Halicarnassus", "Diabolos"], queue);
    }

    [Fact]
    public void BuildWorldQueue_ScansVisibleWorldsWhenLobbyEntriesUnavailable() {
        var queue = AutoLoginPlanner.BuildWorldQueue(
            [new AutoLoginCandidate(42, "Test Character", "Golem")],
            [],
            ["Diabolos", "Halicarnassus"]);

        Assert.Equal(["Diabolos", "Halicarnassus"], queue);
    }

    [Fact]
    public void BuildWorldQueue_DoesNotUseConfiguredHomeWorldForSelection() {
        var queue = AutoLoginPlanner.BuildWorldQueue(
            [new AutoLoginCandidate(42, "Test Character", "Halicarnassus")],
            [],
            ["Diabolos", "Halicarnassus"]);

        Assert.Equal(["Diabolos", "Halicarnassus"], queue);
    }

    [Fact]
    public void BuildWorldQueue_DeduplicatesLobbyCurrentWorldsAgainstVisibleFallback() {
        var queue = AutoLoginPlanner.BuildWorldQueue(
            [new AutoLoginCandidate(42, "Test Character")],
            [LobbyEntry(42, "Test Character", currentWorld: "Diabolos", homeWorld: "Golem")],
            ["Diabolos", "Halicarnassus", "Diabolos"]);

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

    [Fact]
    public void TryResolveDirectCharacterListTarget_SelectsVisibleWhitelistedCandidateConfirmedByLobby() {
        var found = AutoLoginPlanner.TryResolveDirectCharacterListTarget(
            [
                new AutoLoginCandidate(42, "Test Character"),
                new AutoLoginCandidate(100, "Other Character"),
            ],
            [
                LobbyEntry(42, "Test Character", currentWorld: "Halicarnassus", homeWorld: "Diabolos"),
                LobbyEntry(100, "Other Character", currentWorld: "Diabolos", homeWorld: "Diabolos"),
            ],
            ["Other Character", "Test Character"],
            out var target,
            out var index,
            out var reason);

        Assert.True(found, reason);
        Assert.Equal("Other Character", target.CharacterName);
        Assert.Equal("Diabolos", target.WorldName);
        Assert.Equal(0, index);
    }

    [Fact]
    public void TryResolveDirectCharacterListTarget_SelectsWhitelistedPartialLobbyCharacter() {
        var found = AutoLoginPlanner.TryResolveDirectCharacterListTarget(
            [
                new AutoLoginCandidate(42, "Test Character"),
                new AutoLoginCandidate(100, "Other Character"),
            ],
            [LobbyEntry(100, "Other Character", currentWorld: "Diabolos", homeWorld: "Diabolos")],
            ["Other Character"],
            out var target,
            out var index,
            out var reason);

        Assert.True(found, reason);
        Assert.Equal("Other Character", target.CharacterName);
        Assert.Equal("Diabolos", target.WorldName);
        Assert.Equal(0, index);
    }

    [Fact]
    public void TryResolveDirectCharacterListTarget_DoesNotSelectNonWhitelistedVisibleCharacter() {
        var found = AutoLoginPlanner.TryResolveDirectCharacterListTarget(
            [
                new AutoLoginCandidate(42, "Test Character"),
                new AutoLoginCandidate(100, "Other Character"),
            ],
            [LobbyEntry(999, "Not Configured", currentWorld: "Diabolos", homeWorld: "Diabolos")],
            ["Not Configured"],
            out var target,
            out var index,
            out var reason);

        Assert.False(found);
        Assert.Equal(default, target);
        Assert.Equal(-1, index);
        Assert.Contains("visible whitelisted", reason);
    }

    [Fact]
    public void TryResolveDirectCharacterListTarget_SelectsVisibleWhitelistedCharacterMissingFromLobby() {
        var found = AutoLoginPlanner.TryResolveDirectCharacterListTarget(
            [new AutoLoginCandidate(42, "Test Character", "Diabolos")],
            [LobbyEntry(999, "Not Configured", currentWorld: "Diabolos", homeWorld: "Diabolos")],
            ["Test Character"],
            out var target,
            out var index,
            out var reason);

        Assert.True(found, reason);
        Assert.Equal("Test Character", target.CharacterName);
        Assert.Equal(string.Empty, target.WorldName);
        Assert.Equal(0, index);
        Assert.Contains("without lobby confirmation", reason);
    }

    [Fact]
    public void TryResolveDirectCharacterListTarget_FallsBackToVisibleCandidateWhenLobbyUnavailable() {
        var found = AutoLoginPlanner.TryResolveDirectCharacterListTarget(
            [
                new AutoLoginCandidate(42, "Test Character"),
                new AutoLoginCandidate(100, "Other Character"),
            ],
            [],
            ["Other Character", "Test Character"],
            out var target,
            out var index,
            out var reason);

        Assert.True(found, reason);
        Assert.Equal("Other Character", target.CharacterName);
        Assert.Equal(string.Empty, target.WorldName);
        Assert.Equal(0, index);
        Assert.Contains("lobby data was unavailable", reason);
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
