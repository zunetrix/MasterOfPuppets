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
    [InlineData("moprun \"My Macro Name\" -var=$emote=/ac \"standard step\";$emote2=/clap", "moprun", "My Macro Name", "-var=$emote=/ac \"standard step\";$emote2=/clap")]
    [InlineData("moprun \"My Macro Name\" -var=$var1=/clap;$var2=0.5;$var3=\"Character Name\";$emote=/clap;$emote2=\"/clap\"", "moprun", "My Macro Name", "-var=$var1=/clap;$var2=0.5;$var3=\"Character Name\";$emote=/clap;$emote2=\"/clap\"")]
    [InlineData("moprun \"My -- Macro\" -var=$x=1", "moprun", "My -- Macro", "-var=$x=1")]
    [InlineData("mopstop", "mopstop")]
    [InlineData("mopbr Text", "mopbr", "Text")]
    [InlineData("mopbr \"Text with spaces\"", "mopbr", "Text with spaces")]
    [InlineData("mopbr /command", "mopbr", "/command")]
    [InlineData("mopbr /gs change 6", "mopbr", "/gs change 6")]
    [InlineData("mopbr /command param", "mopbr", "/command param")]
    [InlineData("mopbr /command \"param with space\"", "mopbr", "/command \"param with space\"")]
    [InlineData("mopbr /ac heal [t]", "mopbr", "/ac heal <t>")]
    [InlineData("mopbr /mopmove 10.01 11.02 12.03", "mopbr", "/mopmove 10.01 11.02 12.03")]
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
    [InlineData("run \"My Macro Name\" -var=$var1=/clap;$var2=0.5;$var3=\"Character Name\";$emote=/clap;$emote2=\"/clap\"", "run", "My Macro Name", "-var=$var1=/clap;$var2=0.5;$var3=\"Character Name\";$emote=/clap;$emote2=\"/clap\"")]
    [InlineData("run \"My -- Macro\" -var=$x=1", "run", "My -- Macro", "-var=$x=1")]
    [InlineData("run 1", "run", "1")]
    [InlineData("run \"1\"", "run", "1")]
    [InlineData("move \"10.01 11.02 12.03\"", "move", "10.01 11.02 12.03")]

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

    [Theory]
    [InlineData("Formation", "Formation")]
    [InlineData("Formation \"Alpha\"", "Formation \\\"Alpha\\\"")]
    [InlineData("\"Quoted\"", "\\\"Quoted\\\"")]
    public void EscapeQuotedArgument_EscapesDoubleQuotes(string input, string expected)
    {
        var result = ArgumentParser.EscapeQuotedArgument(input);
        Assert.Equal(expected, result);
    }

    [Fact(DisplayName = "Command with character name and command")]
    public void CommandWithQuotedName_ReturnsTwoArgs()
    {
        // /mopbrc
        var result = ArgumentParser.ParseCommandArgs("\"Character Name1\" /moptarget \"Character Name2\"");
        var expectedResult = new List<string> { "Character Name1", "/moptarget \"Character Name2\"" };
        Assert.Equal(expectedResult, result);
    }

    [Fact]
    public void ParseInlineVars_SemicolonSeparated_ReturnsAllPairs()
    {
        var result = ArgumentParser.ParseInlineVars(
            "-var=$var1=/clap;$var2=0.5;$var3=\"Character Name\";$emote=/clap;$emote2=\"/clap\"");

        Assert.Equal(5, result.Count);
        Assert.Equal("/clap", result["var1"]);
        Assert.Equal("0.5", result["var2"]);
        Assert.Equal("Character Name", result["var3"]);
        Assert.Equal("/clap", result["emote"]);
        Assert.Equal("/clap", result["emote2"]);
    }

    [Fact]
    public void ParseInlineVars_WrongPrefix_ReturnsEmpty()
    {
        var result = ArgumentParser.ParseInlineVars("--flags=$x=1;$y=2");
        Assert.Empty(result);
    }

    [Fact]
    public void ParseInlineVars_EmptyString_ReturnsEmpty()
    {
        var result = ArgumentParser.ParseInlineVars(string.Empty);
        Assert.Empty(result);
    }

    [Fact]
    public void ParseInlineVars_PrefixOnly_ReturnsEmpty()
    {
        // -var= with nothing after it
        Assert.Empty(ArgumentParser.ParseInlineVars("-var="));
    }

    [Fact]
    public void ParseInlineVars_SingleVar_ReturnsOnePair()
    {
        var result = ArgumentParser.ParseInlineVars("-var=$x=hello");
        Assert.Single(result);
        Assert.Equal("hello", result["x"]);
    }

    [Fact]
    public void ParseInlineVars_UnderscoreInName_Parsed()
    {
        var result = ArgumentParser.ParseInlineVars("-var=$my_var=hello");
        Assert.Equal("hello", result["my_var"]);
    }

    [Fact]
    public void ParseInlineVars_SingleQuotedValue_StripsQuotes()
    {
        // single-quoted value preserving spaces
        var result = ArgumentParser.ParseInlineVars("-var=$x='hello world'");
        Assert.Equal("hello world", result["x"]);
    }

    [Fact]
    public void ParseInlineVars_ValueContainsEquals_CapturedFully()
    {
        // unquoted value that itself contains '=' should be captured up to ';' or end
        var result = ArgumentParser.ParseInlineVars("-var=$x=a=b;$y=c");
        Assert.Equal("a=b", result["x"]);
        Assert.Equal("c", result["y"]);
    }

    [Fact]
    public void ParseInlineVars_DuplicateKey_LastValueWins()
    {
        var result = ArgumentParser.ParseInlineVars("-var=$x=first;$x=second");
        Assert.Single(result);
        Assert.Equal("second", result["x"]);
    }

    [Fact]
    public void ParseInlineVars_UnquotedValueWithSpaces_CapturedFully()
    {
        // value like /ac "standard step" has spaces - should capture the full thing
        var result = ArgumentParser.ParseInlineVars("-var=$cmd=/ac \"standard step\"");
        Assert.Single(result);
        Assert.Equal("/ac \"standard step\"", result["cmd"]);
    }

    [Fact]
    public void ParseInlineVars_UnquotedValueWithSpacesThenNextVar_BothCaptured()
    {
        var result = ArgumentParser.ParseInlineVars("-var=$cmd=/ac \"standard step\";$emote=/clap");
        Assert.Equal(2, result.Count);
        Assert.Equal("/ac \"standard step\"", result["cmd"]);
        Assert.Equal("/clap", result["emote"]);
    }

    [Fact]
    public void ParseInlineVars_TargetPlaceholder_IsCaptured()
    {
        var result = ArgumentParser.ParseInlineVars("-var=$target=<t>;$delay=0.5");

        Assert.Equal(2, result.Count);
        Assert.Equal("<t>", result["target"]);
        Assert.Equal("0.5", result["delay"]);
    }
}
