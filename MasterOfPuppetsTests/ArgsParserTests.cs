using Xunit;
using System.Collections.Generic;

using MasterOfPuppets.Util;

namespace MasterOfPuppetsTests;

public class ArgsParserTests {
    [Fact]
    public void EmptyArgs_ReturnsEmptyList() {
        var result = ArgumentParser.ParseChatArgs("");
        Assert.Empty(result);
    }


    [Fact(DisplayName = "Command with character name and command")]
    public void CommandWithQuotedName_ReturnsTwoArgs() {
        var result = ArgumentParser.ParseCommandArgs("\"Character Name1\" /moptarget \"Character Name2\"");
        var expectedResult = new List<string> { "Character Name1", "/moptarget \"Character Name2\"" };
        Assert.Equal(expectedResult, result);
    }

    [Theory]
    [InlineData("/mopbr text with space", "/mopbr", "text with space")]
    [InlineData("/mopbr /clap", "/mopbr", "/clap")]
    [InlineData("/mopbr /moptarget \"Character Name\"", "/mopbr", "/moptarget \"Character Name\"")]
    [InlineData("/mopbr /ac heal [t]", "/mopbr", "/ac heal <t>")]
    [InlineData("/mopbr \"text with space\"", "/mopbr", "text with space")]
    [InlineData("/mopbrc \"Character Name\" /clap", "/mopbrc", "Character Name", "/clap")]
    [InlineData("/mopbrc \"Character Name\" some text", "/mopbrc", "Character Name", "some text")]
    [InlineData("/mopbrc \"Character Name\" /moptarget \"Character Name2\"", "/mopbrc", "Character Name", "/moptarget \"Character Name2\"")]
    [InlineData("/mopstop", "/mopstop")]

    [InlineData("moprun \"My Macro Name\"", "moprun", "My Macro Name")]
    [InlineData("mopstop", "mopstop")]

    [InlineData("mopbr Text", "mopbr", "Text")]
    [InlineData("mopbr \"Text with spaces\"", "mopbr", "Text with spaces")]
    [InlineData("mopbr /command", "mopbr", "/command")]
    [InlineData("mopbr /command param", "mopbr", "/command param")]
    [InlineData("mopbr /command \"param with space\"", "mopbr", "/command \"param with space\"")]
    [InlineData("mopbr /ac heal [t]", "mopbr", "/ac heal <t>")]

    [InlineData("mopbrc \"Character Name\" /command", "mopbrc", "Character Name", "/command")]
    [InlineData("mopbrc \"Character Name\" /command \"param with space\"", "mopbrc", "Character Name", "/command \"param with space\"")]
    public void ParseChatArgs_ReturnsExpectedTokens(string input, params string[] expected) {
        var result = ArgumentParser.ParseChatArgs(input);

        Assert.Equal(expected.Length, result.Count);

        for (int i = 0; i < expected.Length; i++) {
            Assert.Equal(expected[i], result[i]);
        }
    }
}

