using Xunit;
using System.Collections.Generic;

using MasterOfPuppets;
using MasterOfPuppets.Formations;
using MasterOfPuppets.Movement;
using MasterOfPuppets.Util;

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
                    Actions = "/moptarget \"$target\"\n/echo $me\n/echo $mop_origin\n/echo $mop_origin_target"
                }
            }
        };

        var result = macro.GetCidActions(
            1,
            runtimeVariables: new MacroRuntimeVariables
            {
                Me = "Current Character@World",
                Target = "Target Character@World",
                MopOrigin = "Origin Character@World",
                MopOriginTarget = "Origin Target@World",
            });

        Assert.Equal(
            new[] {
                "/moptarget \"Target Character@World\"",
                "/echo Current Character@World",
                "/echo Origin Character@World",
                "/echo Origin Target@World",
            },
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
            runtimeVariables: new MacroRuntimeVariables
            {
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
            runtimeVariables: new MacroRuntimeVariables
            {
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
            inlineVars: new Dictionary<string, string> { ["target"] = "[t]" },
            runtimeVariables: new MacroRuntimeVariables
            {
                Target = "Selected Target@World",
            });

        Assert.Equal(new[] { "/mopmoverelativeto 0 0 2 \"Selected Target@World\"" }, result);
    }

    [Fact]
    public void MopFormationMove_Uses_Normal_GlobalDelay()
    {
        Assert.False(MacroHandler.CommandSkipsGlobalDelay("mopformationmove"));
    }

    [Theory]
    [InlineData(FormationAnchorFailureKind.NoTargetSelected, true)]
    [InlineData(FormationAnchorFailureKind.NoFocusTargetSelected, true)]
    [InlineData(FormationAnchorFailureKind.AnchorNameEmpty, true)]
    [InlineData(FormationAnchorFailureKind.AnchorNotVisible, true)]
    [InlineData(FormationAnchorFailureKind.ConfigurationError, false)]
    [InlineData(FormationAnchorFailureKind.Unsupported, false)]
    public void FormationAnchorFailures_Classify_Transient_Runtime_Misses(
        FormationAnchorFailureKind failureKind,
        bool expectedTransient)
    {
        Assert.Equal(expectedTransient, FormationLocalMovementExecutor.IsTransientAnchorFailure(failureKind));
    }

    [Fact]
    public void ParseFormationMoveCommandArgs_Defaults_To_Precise()
    {
        var result = MacroHandler.ParseFormationMoveCommandArgs("\"Circle\" forward 1 0");

        Assert.NotNull(result);
        Assert.Equal("Circle", result.FormationName);
        Assert.False(result.Reverse);
        Assert.Equal(1, result.Step);
        Assert.Equal(0, result.SequenceIndex);
        Assert.Equal(SimpleMovementMode.Precise, result.MovementMode);
        Assert.Equal(FormationMoveAnchorMode.Self, result.AnchorMode);
        Assert.Equal(FormationAnchorKind.Self, result.Anchor.Kind);
    }

    [Fact]
    public void ParseFormationMoveCommandArgs_Accepts_Explicit_Continuous()
    {
        var result = MacroHandler.ParseFormationMoveCommandArgs("\"Circle\" forward 1 0 continuous");

        Assert.NotNull(result);
        Assert.Equal(SimpleMovementMode.Continuous, result.MovementMode);
    }

    [Fact]
    public void ParseFormationMoveCommandArgs_Accepts_Precise_Flag()
    {
        var result = MacroHandler.ParseFormationMoveCommandArgs("\"Circle\" backward 2 3 precise");

        Assert.NotNull(result);
        Assert.True(result.Reverse);
        Assert.Equal(2, result.Step);
        Assert.Equal(3, result.SequenceIndex);
        Assert.Equal(SimpleMovementMode.Precise, result.MovementMode);
        Assert.Equal(FormationMoveAnchorMode.Self, result.AnchorMode);
    }

    [Theory]
    [InlineData("continuous", SimpleMovementMode.Continuous)]
    [InlineData("precise", SimpleMovementMode.Precise)]
    [InlineData("forward", SimpleMovementMode.Forward)]
    public void ParseFormationMoveCommandArgs_Accepts_All_Movement_Modes(string token, SimpleMovementMode expectedMode)
    {
        var result = MacroHandler.ParseFormationMoveCommandArgs($"\"Circle\" forward 1 0 {token}");

        Assert.NotNull(result);
        Assert.Equal(expectedMode, result.MovementMode);
    }

    [Fact]
    public void ParseFormationMoveCommandArgs_Rejects_Removed_Hybrid_Mode()
    {
        var result = MacroHandler.ParseFormationMoveCommandArgs("\"Circle\" forward 1 0 hybrid");

        Assert.NotNull(result);
        Assert.Equal("hybrid", result.InvalidArgument);
        Assert.Equal(FormationAnchorKind.Self, result.Anchor.Kind);
    }

    [Fact]
    public void ParseFormationMoveCommandArgs_Accepts_Target_Anchor()
    {
        var result = MacroHandler.ParseFormationMoveCommandArgs("\"Circle\" forward 1 0 target");

        Assert.NotNull(result);
        Assert.Equal(SimpleMovementMode.Precise, result.MovementMode);
        Assert.Equal(FormationMoveAnchorMode.Target, result.AnchorMode);
        Assert.Equal(FormationAnchorKind.Target, result.Anchor.Kind);
    }

    [Fact]
    public void ParseFormationMoveCommandArgs_Accepts_Ftarget_Anchor()
    {
        var result = MacroHandler.ParseFormationMoveCommandArgs("\"Circle\" forward 1 0 ftarget");

        Assert.NotNull(result);
        Assert.Equal(SimpleMovementMode.Precise, result.MovementMode);
        Assert.Equal(FormationMoveAnchorMode.FocusTarget, result.AnchorMode);
        Assert.Equal(FormationAnchorKind.FocusTarget, result.Anchor.Kind);
    }

    [Fact]
    public void ParseFormationMoveCommandArgs_Accepts_Focustarget_Anchor()
    {
        var result = MacroHandler.ParseFormationMoveCommandArgs("\"Circle\" forward 1 0 focustarget");

        Assert.NotNull(result);
        Assert.Equal(SimpleMovementMode.Precise, result.MovementMode);
        Assert.Equal(FormationMoveAnchorMode.FocusTarget, result.AnchorMode);
        Assert.Equal(FormationAnchorKind.FocusTarget, result.Anchor.Kind);
    }

    [Fact]
    public void ParseFormationMoveCommandArgs_Accepts_Precise_Ftarget_In_Either_Order()
    {
        var ftargetLast = MacroHandler.ParseFormationMoveCommandArgs("\"Circle\" forward 1 0 precise ftarget");
        var ftargetFirst = MacroHandler.ParseFormationMoveCommandArgs("\"Circle\" forward 1 0 ftarget precise");

        Assert.NotNull(ftargetLast);
        Assert.NotNull(ftargetFirst);
        Assert.Equal(SimpleMovementMode.Precise, ftargetLast.MovementMode);
        Assert.Equal(FormationMoveAnchorMode.FocusTarget, ftargetLast.AnchorMode);
        Assert.Equal(SimpleMovementMode.Precise, ftargetFirst.MovementMode);
        Assert.Equal(FormationMoveAnchorMode.FocusTarget, ftargetFirst.AnchorMode);
    }

    [Fact]
    public void ParseFormationMoveCommandArgs_Accepts_Precise_Target_In_Either_Order()
    {
        var targetLast = MacroHandler.ParseFormationMoveCommandArgs("\"Circle\" forward 1 0 precise target");
        var targetFirst = MacroHandler.ParseFormationMoveCommandArgs("\"Circle\" forward 1 0 target precise");

        Assert.NotNull(targetLast);
        Assert.NotNull(targetFirst);
        Assert.Equal(SimpleMovementMode.Precise, targetLast.MovementMode);
        Assert.Equal(FormationMoveAnchorMode.Target, targetLast.AnchorMode);
        Assert.Equal(SimpleMovementMode.Precise, targetFirst.MovementMode);
        Assert.Equal(FormationMoveAnchorMode.Target, targetFirst.AnchorMode);
    }

    [Fact]
    public void ParseFormationMoveCommandArgs_Accepts_Named_Anchor()
    {
        var result = MacroHandler.ParseFormationMoveCommandArgs("\"Circle\" forward 1 0 \"Anchor Character@World\"");

        Assert.NotNull(result);
        Assert.Equal(SimpleMovementMode.Precise, result.MovementMode);
        Assert.Equal(FormationAnchorKind.Named, result.Anchor.Kind);
        Assert.Equal("Anchor Character@World", result.Anchor.Name);
        Assert.Null(result.InvalidArgument);
    }

    [Fact]
    public void ParseFormationGotoCommandArgs_Defaults_To_Self_Precise()
    {
        var result = MacroHandler.ParseFormationGotoCommandArgs("\"Circle\" 2");

        Assert.NotNull(result);
        Assert.Equal("Circle", result.FormationName);
        Assert.Equal(1, result.PointIndex);
        Assert.Equal(MacroHandler.FormationGotoAnchorKind.Self, result.AnchorKind);
        Assert.Equal(FormationAnchorKind.Self, result.Anchor.Kind);
        Assert.Null(result.AnchorName);
        Assert.Equal(SimpleMovementMode.Precise, result.MovementMode);
    }

    [Fact]
    public void ParseFormationGotoCommandArgs_Accepts_Explicit_Continuous()
    {
        var result = MacroHandler.ParseFormationGotoCommandArgs("\"Circle\" 2 continuous");

        Assert.NotNull(result);
        Assert.Equal(SimpleMovementMode.Continuous, result.MovementMode);
    }

    [Fact]
    public void ParseFormationGotoCommandArgs_Accepts_Quoted_Named_Anchor()
    {
        var result = MacroHandler.ParseFormationGotoCommandArgs("\"Circle\" 3 anchor=\"Anchor Character@World\" precise");

        Assert.NotNull(result);
        Assert.Equal(2, result.PointIndex);
        Assert.Equal(MacroHandler.FormationGotoAnchorKind.Named, result.AnchorKind);
        Assert.Equal("Anchor Character@World", result.AnchorName);
        Assert.Equal(SimpleMovementMode.Precise, result.MovementMode);
    }

    [Fact]
    public void ParseFormationGotoCommandArgs_Accepts_Target_And_Self_Anchors()
    {
        var target = MacroHandler.ParseFormationGotoCommandArgs("\"Circle\" 4 target");
        var self = MacroHandler.ParseFormationGotoCommandArgs("\"Circle\" 4 anchor=self");

        Assert.NotNull(target);
        Assert.NotNull(self);
        Assert.Equal(MacroHandler.FormationGotoAnchorKind.Target, target.AnchorKind);
        Assert.Equal(MacroHandler.FormationGotoAnchorKind.Self, self.AnchorKind);
    }

    [Fact]
    public void ParseFormationGotoCommandArgs_Accepts_Ftarget_Anchor()
    {
        var result = MacroHandler.ParseFormationGotoCommandArgs("\"Circle\" 4 ftarget");

        Assert.NotNull(result);
        Assert.Equal(MacroHandler.FormationGotoAnchorKind.FocusTarget, result.AnchorKind);
        Assert.Equal(FormationAnchorKind.FocusTarget, result.Anchor.Kind);
    }

    [Fact]
    public void ParseFormationGotoCommandArgs_Accepts_Focustarget_Anchor()
    {
        var result = MacroHandler.ParseFormationGotoCommandArgs("\"Circle\" 4 focustarget");

        Assert.NotNull(result);
        Assert.Equal(MacroHandler.FormationGotoAnchorKind.FocusTarget, result.AnchorKind);
        Assert.Equal(FormationAnchorKind.FocusTarget, result.Anchor.Kind);
    }

    [Fact]
    public void ParseFormationGotoCommandArgs_Accepts_Bare_Named_Anchor()
    {
        var result = MacroHandler.ParseFormationGotoCommandArgs("\"Circle\" 4 \"Anchor Character@World\" precise");

        Assert.NotNull(result);
        Assert.Equal(FormationAnchorKind.Named, result.Anchor.Kind);
        Assert.Equal("Anchor Character@World", result.Anchor.Name);
        Assert.Equal(SimpleMovementMode.Precise, result.MovementMode);
    }

    [Theory]
    [InlineData("continuous", SimpleMovementMode.Continuous)]
    [InlineData("precise", SimpleMovementMode.Precise)]
    [InlineData("forward", SimpleMovementMode.Forward)]
    public void ParseFormationGotoCommandArgs_Accepts_All_Movement_Modes(string token, SimpleMovementMode expectedMode)
    {
        var result = MacroHandler.ParseFormationGotoCommandArgs($"\"Circle\" 4 target {token}");

        Assert.NotNull(result);
        Assert.Equal(expectedMode, result.MovementMode);
    }

    [Fact]
    public void ParseFormationGotoCommandArgs_Rejects_Removed_Hybrid_Mode()
    {
        var result = MacroHandler.ParseFormationGotoCommandArgs("\"Circle\" 4 target hybrid");

        Assert.NotNull(result);
        Assert.Equal("hybrid", result.InvalidArgument);
        Assert.Equal(MacroHandler.FormationGotoAnchorKind.Target, result.AnchorKind);
    }

    [Fact]
    public void ParseChatArgs_Parses_Mopformation_Without_Slash()
    {
        var result = ArgumentParser.ParseChatArgs("mopformation \"Tight Circle\" target precise");

        Assert.Equal(["mopformation", "Tight Circle", "target", "precise"], result);
    }

    [Fact]
    public void ParseChatArgs_Does_Not_Treat_Slash_MopFormation_As_ChatSync_Command()
    {
        var result = ArgumentParser.ParseChatArgs("/mop formation \"Tight Circle\"");

        Assert.Single(result);
        Assert.NotEqual("mopformation", result[0]);
    }

    [Fact]
    public void ParseFormationAnchorAndArrival_Defaults_To_PointOne_Anchor_For_Chat()
    {
        var result = FormationAnchorArgumentParser.ParseAnchorAndArrival(
            [],
            FormationAnchorReference.Default);

        Assert.Equal(FormationAnchorKind.Default, result.Anchor.Kind);
        Assert.Equal(SimpleMovementMode.Precise, result.MovementMode);
    }

    [Fact]
    public void ParseFormationAnchorAndArrival_Preserves_Self_Anchor_For_Local_MopFormation()
    {
        var result = FormationAnchorArgumentParser.ParseAnchorAndArrival(
            ["precise"],
            FormationAnchorReference.Self);

        Assert.Equal(FormationAnchorKind.Self, result.Anchor.Kind);
        Assert.Equal(SimpleMovementMode.Precise, result.MovementMode);
        Assert.Null(result.InvalidArgument);
    }

    [Fact]
    public void ParseFormationAnchorAndArrival_Accepts_Target_And_Precise_In_Either_Order()
    {
        var targetLast = FormationAnchorArgumentParser.ParseAnchorAndArrival(
            ["precise", "target"],
            FormationAnchorReference.Self);
        var targetFirst = FormationAnchorArgumentParser.ParseAnchorAndArrival(
            ["target", "precise"],
            FormationAnchorReference.Self);

        Assert.Equal(FormationAnchorKind.Target, targetLast.Anchor.Kind);
        Assert.Equal(SimpleMovementMode.Precise, targetLast.MovementMode);
        Assert.Equal(FormationAnchorKind.Target, targetFirst.Anchor.Kind);
        Assert.Equal(SimpleMovementMode.Precise, targetFirst.MovementMode);
    }

    [Fact]
    public void ParseFormationAnchorAndArrival_Accepts_Explicit_Continuous()
    {
        var result = FormationAnchorArgumentParser.ParseAnchorAndArrival(
            ["continuous"],
            FormationAnchorReference.Self);

        Assert.Equal(FormationAnchorKind.Self, result.Anchor.Kind);
        Assert.Equal(SimpleMovementMode.Continuous, result.MovementMode);
    }

    [Fact]
    public void ParseFormationGotoCommandArgs_Rejects_Invalid_Point_Number()
    {
        var result = MacroHandler.ParseFormationGotoCommandArgs("\"Circle\" 0");

        Assert.NotNull(result);
        Assert.Equal(-1, result.PointIndex);
        Assert.Equal("0", result.InvalidArgument);
    }
}
