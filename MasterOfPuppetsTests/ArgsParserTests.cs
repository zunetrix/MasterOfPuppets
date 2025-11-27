using Xunit;
using System.Collections.Generic;

using MasterOfPuppets.Util;

namespace MasterOfPuppetsTests;

public class ArgsParserTests
{
    [Fact]
    public void EmptyChatArgs_ReturnsEmptyList()
    {
        var result = ArgumentParser.ParseChatArgs("");
        Assert.Empty(result);
    }

    [Theory]
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
    public void ParseChatArgs_ReturnsExpectedTokens(string input, params string[] expected)
    {
        var result = ArgumentParser.ParseChatArgs(input);

        Assert.Equal(expected.Length, result.Count);

        for (int i = 0; i < expected.Length; i++)
        {
            Assert.Equal(expected[i], result[i]);
        }
    }

    [Theory]
    // /mop
    [InlineData("run \"Macro Name\"", "run", "Macro Name")]
    [InlineData("run 1", "run", "1")]
    [InlineData("run \"1\"", "run", "1")]

    // /mopbr
    [InlineData("text with space", "text with space")]
    [InlineData("\"text with space\"", "text with space")]
    [InlineData("/clap", "/clap")]
    [InlineData("/moptarget \"Character Name\"", "/moptarget \"Character Name\"")]
    [InlineData("/ac heal <t>", "/ac heal <t>")]

    // /mopbrc
    [InlineData("\"Character Name\" /clap", "Character Name", "/clap")]
    [InlineData("\"Character Name\" some text", "Character Name", "some", "text")]
    [InlineData("\"Character Name\" \"some text\"", "Character Name", "some text")]
    [InlineData("\"Character Name\" /moptarget \"Character Name2\"", "Character Name", "/moptarget \"Character Name2\"")]
    public void ParseCommandArgs_ReturnsExpectedTokens(string input, params string[] expected)
    {
        var result = ArgumentParser.ParseCommandArgs(input);

        Assert.Equal(expected.Length, result.Count);

        for (int i = 0; i < expected.Length; i++)
        {
            Assert.Equal(expected[i], result[i]);
        }
    }

    [Fact]
    public void EmptyCommandArgs_ReturnsEmptyList()
    {
        var result = ArgumentParser.ParseCommandArgs("");
        Assert.Empty(result);
    }

    [Fact(DisplayName = "Command with character name and command")]
    public void CommandWithQuotedName_ReturnsTwoArgs()
    {
        // /mopbrc
        var result = ArgumentParser.ParseCommandArgs("\"Character Name1\" /moptarget \"Character Name2\"");
        var expectedResult = new List<string> { "Character Name1", "/moptarget \"Character Name2\"" };
        Assert.Equal(expectedResult, result);
    }
}

