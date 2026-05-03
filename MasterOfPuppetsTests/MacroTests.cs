using Xunit;
using System.Collections.Generic;

using MasterOfPuppets;

public class MacroTests
{

    [Fact]
    public void Returns_Actions_For_Specific_Cid()
    {
        var macro = new Macro
        {
            Commands = new List<Command> {
                new Command {
                    Cids = new() { 1 },
                    Actions = "/a"
                },
                new Command {
                    Cids = new() { 2 },
                    Actions = "/b"
                }
            }
        };

        var result = macro.GetCidActions(2);

        Assert.Equal(new[] { "/b" }, result);
    }

    [Fact]
    public void SanitizeActions_Removes_Run_And_Extra_Loop()
    {
        var macro = new Macro
        {
            Commands = new List<Command> {
                new Command {
                    Actions = @"
                        /mop run
                        /moploop
                        /a
                        /moploop
                    "
                }
            }
        };

        macro.SanitizeActions();

        var result = macro.Commands[0].Actions.Split('\n');

        Assert.Equal(new[] { "/a", "/moploop" }, result);
    }

    [Fact]
    public void RuntimeVariables_Are_Substituted_When_No_Configured_Variable_Overrides_Them()
    {
        var macro = new Macro
        {
            Commands = new List<Command> {
                new Command {
                    Cids = new() { 1 },
                    Actions = "/moptarget \"$target\"\n/echo $me"
                }
            }
        };

        var result = macro.GetCidActions(
            1,
            runtimeVariables: new MacroRuntimeVariables {
                Me = "Current Character@World",
                Target = "Target Character@World",
            });

        Assert.Equal(
            new[] { "/moptarget \"Target Character@World\"", "/echo Current Character@World" },
            result);
    }

    [Fact]
    public void InlineVariables_Override_Command_Macro_And_RuntimeVariables()
    {
        var macro = new Macro
        {
            Variables = "$target=\"macro target\"",
            Commands = new List<Command> {
                new Command {
                    Cids = new() { 1 },
                    Actions = "$target=\"command target\"\n/moptarget \"$target\""
                }
            }
        };

        var result = macro.GetCidActions(
            1,
            inlineVars: new Dictionary<string, string> { ["target"] = "inline target" },
            runtimeVariables: new MacroRuntimeVariables {
                Target = "runtime target",
            });

        Assert.Equal(new[] { "/moptarget \"inline target\"" }, result);
    }

    [Fact]
    public void CommandVariables_Override_Macro_And_RuntimeVariables()
    {
        var macro = new Macro
        {
            Variables = "$target=\"macro target\"",
            Commands = new List<Command> {
                new Command {
                    Cids = new() { 1 },
                    Actions = "$target=\"command target\"\n/moptarget \"$target\""
                }
            }
        };

        var result = macro.GetCidActions(
            1,
            runtimeVariables: new MacroRuntimeVariables {
                Target = "runtime target",
            });

        Assert.Equal(new[] { "/moptarget \"command target\"" }, result);
    }

    [Fact]
    public void InlineTargetPlaceholder_Resolves_To_RuntimeTarget()
    {
        var macro = new Macro
        {
            Commands = new List<Command> {
                new Command {
                    Cids = new() { 1 },
                    Actions = "/mopmoverelativeto 0 0 2 \"$target\""
                }
            }
        };

        var result = macro.GetCidActions(
            1,
            inlineVars: new Dictionary<string, string> { ["target"] = "<t>" },
            runtimeVariables: new MacroRuntimeVariables {
                Target = "Selected Target@World",
            });

        Assert.Equal(new[] { "/mopmoverelativeto 0 0 2 \"Selected Target@World\"" }, result);
    }

    [Fact]
    public void MopFormationMove_Uses_Normal_GlobalDelay()
    {
        Assert.False(MacroHandler.CommandSkipsGlobalDelay("mopformationmove"));
    }

    [Fact]
    public void ParseFormationMoveCommandArgs_Defaults_To_Continuous()
    {
        var result = MacroHandler.ParseFormationMoveCommandArgs("\"Circle\" forward 1 0");

        Assert.NotNull(result);
        Assert.Equal("Circle", result.FormationName);
        Assert.False(result.Reverse);
        Assert.Equal(1, result.Step);
        Assert.Equal(0, result.SequenceIndex);
        Assert.Equal(MasterOfPuppets.Movement.MovementArrivalMode.Continuous, result.ArrivalMode);
    }

    [Fact]
    public void ParseFormationMoveCommandArgs_Accepts_Precise_Flag()
    {
        var result = MacroHandler.ParseFormationMoveCommandArgs("\"Circle\" backward 2 3 precise");

        Assert.NotNull(result);
        Assert.True(result.Reverse);
        Assert.Equal(2, result.Step);
        Assert.Equal(3, result.SequenceIndex);
        Assert.Equal(MasterOfPuppets.Movement.MovementArrivalMode.Precise, result.ArrivalMode);
    }

    [Fact]
    public void ParseFormationMoveCommandArgs_Invalid_ArrivalMode_Falls_Back_To_Continuous()
    {
        var result = MacroHandler.ParseFormationMoveCommandArgs("\"Circle\" forward 1 0 careful");

        Assert.NotNull(result);
        Assert.Equal(MasterOfPuppets.Movement.MovementArrivalMode.Continuous, result.ArrivalMode);
        Assert.Equal("careful", result.InvalidArrivalModeArgument);
    }
}
