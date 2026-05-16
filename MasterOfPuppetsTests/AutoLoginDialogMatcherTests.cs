using MasterOfPuppets;

using Xunit;

namespace MasterOfPuppetsTests;

public class AutoLoginDialogMatcherTests {
    [Fact]
    public void IsMissingLastLoggedOutCharacterSelectOk_MatchesKnownPreLoginDialog() {
        var text = """
                   The character you last logged out with in this play environment could not be found on the
                   current data center.Please connect to another data center from the Data Center Selection screen.
                   """;

        Assert.True(AutoLoginDialogMatcher.IsMissingLastLoggedOutCharacterSelectOk(text));
    }

    [Fact]
    public void IsMissingLastLoggedOutCharacterSelectOk_DoesNotMatchQueueDialog() {
        var text = """
                   The server is currently congested.
                   Players in queue: 15.
                   """;

        Assert.False(AutoLoginDialogMatcher.IsMissingLastLoggedOutCharacterSelectOk(text));
    }
}
