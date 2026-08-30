using Xunit;
using System;
using System.Collections.Generic;
using System.Numerics;

using MasterOfPuppets;
using MasterOfPuppets.Extensions;
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
                    Actions = "/moptarget \"$target\"\n/echo $me\n/echo $mop_origin\n/echo $mop_origin_target\n/echo $mop_origin_ftarget\n/echo $ftarget\n/echo $job $class\n/echo level $level\n/echo $world\n/echo leader $leader"
                }
            }
        };

        var result = macro.GetCidActions(
            1,
            runtimeVariables: new MacroRuntimeVariables
            {
                Me = "Current Character@World",
                Target = "Target Character@World",
                FocusTarget = "Focus Target@World",
                Job = "DNC",
                Level = "90",
                World = "Hyperion",
                Leader = "Leader Character@World",
                MopOrigin = "Origin Character@World",
                MopOriginTarget = "Origin Target@World",
                MopOriginFocusTarget = "Origin Focus Target@World",
            });

        Assert.Equal(
            new[] {
                "/moptarget \"Target Character@World\"",
                "/echo Current Character@World",
                "/echo Origin Character@World",
                "/echo Origin Target@World",
                "/echo Origin Focus Target@World",
                "/echo Focus Target@World",
                "/echo DNC DNC",
                "/echo level 90",
                "/echo Hyperion",
                "/echo leader Leader Character@World",
            },
            result);
    }

    [Fact]
    public void RuntimeVariables_ToDictionary_Exposes_New_Local_Variables()
    {
        var vars = new MacroRuntimeVariables
        {
            Me = "Current Character@World",
            Target = "Target Character@World",
            FocusTarget = "Focus Target@World",
            Job = "SGE",
            Level = "100",
            World = "Hyperion",
            Leader = "Leader Character@World",
            GlobalDelaySeconds = 0.5,
        };

        var dict = vars.ToDictionary();

        Assert.Equal("Current Character@World", dict["me"]);
        Assert.Equal("Target Character@World", dict["target"]);
        Assert.Equal("Focus Target@World", dict["ftarget"]);
        Assert.Equal("SGE", dict["job"]);
        Assert.Equal("SGE", dict["class"]);
        Assert.Equal("100", dict["level"]);
        Assert.Equal("Hyperion", dict["world"]);
        Assert.Equal("Leader Character@World", dict["leader"]);
        Assert.Equal("0.5", dict["globaldelay"]);
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
    public void Nested_Variables_Are_Substituted_Across_Passes()
    {
        var macro = new Macro
        {
            Variables = "$anchor=\"$mop_origin_target\"",
            Commands = new List<Command> {
                new Command {
                    Cids = new() { 1 },
                    Actions = "/mopformationgoto \"Circle\" 2 anchor=\"$anchor\" fallback=\"$mop_origin\""
                }
            }
        };

        var result = macro.GetCidActions(
            1,
            runtimeVariables: new MacroRuntimeVariables
            {
                MopOrigin = "Leader Name@World",
                MopOriginTarget = "Target Dummy@World",
            });

        Assert.Equal(new[] { "/mopformationgoto \"Circle\" 2 anchor=\"Target Dummy@World\" fallback=\"Leader Name@World\"" }, result);
    }

    [Fact]
    public void Macro_Import_String_Deserializes_Correctly()
    {
        string b64 = "H4sIAAAAAAAC/3WVTW/bMAyG7/0VgtDDBhQZJZGiNGwDigIbctlpWPfhHrzEKQzEduE63aHof5/kxEiBUclFrx5Sfkkb1POFUvpr3TX6vdI37bjZN+qNs2/V9WEa9FWm3+r7x0R/p7VaYmaS1Odh7OqpHXqd9N0cfjPshzHFPx8jfqQlrOwp/ueswkn9mpU7qdukzAqSeJkPWm+Gfr3NIbP8Xo9t/WffZC/6crc8WH1U1WLcrGg2X+mqvxzrbXt4TBhWTPpkrevqfnuu5uhxrurVbv6ZAAYxBkIybMGaq/+Rd55DxCAiQxg9y1megKiA2GDpWQk6EYUA3loRRSYwXkIcozceJITR+eAlhwzIzmAsIXSiQyZyNkh1sU1VeZSy2DtChMKBznCUzRt2zkmdZ2NSF6mAKL9NGXkTnCllMUcZoQewJRvBFhCSj+KBnL5Fb4OMnDcAMrLpvZSyKIRYQjG6AorIpmQDqIAgpYklO2BHTKUPIMqdDzZ657CEUK4ruOQP5SxEpkAl5FG2gYTWip2PwJ7kHmYUAIvImxKKhorI+SLyJYcMBsoOzYncLSH6yzgcHtbHWXnevd7kSTwP5nfd8NDu1FSP981U9RnnrW54asZmnyb2UzMNCtJ/GdCV/jB9yhN7if1bt1O+Fqo+q6bftrvjUp2HfvXqBqi0no285Cvo4uUfq90WzdIGAAA=";
        string decompressed = b64.Decompress();
        Assert.NotNull(decompressed);
        var macro = decompressed.JsonDeserialize<Macro>();
        Assert.NotNull(macro);
        Assert.Equal("Circle (32) Auto", macro.Name);
        Assert.Single(macro.Commands);
        Assert.Equal(48, macro.Commands[0].Cids.Count);
    }

    [Fact]
    public void ExecutionPlan_Uses_Updated_Variable_For_Later_Actions()
    {
        var macro = new Macro
        {
            Variables = "$speed=1",
            Commands = new List<Command> {
                new Command {
                    Cids = new() { 1 },
                    Actions = "/echo $speed\n/mopwait $speed\n/moploop"
                }
            }
        };

        var plan = macro.CreateCidExecutionPlan(1);

        Assert.Equal("/echo 1", plan.ResolveAction(plan.ActionTemplates[0]));
        plan.UpdateVariables(new Dictionary<string, string> { ["speed"] = "2.5" });
        Assert.Equal("/mopwait 2.5", plan.ResolveAction(plan.ActionTemplates[1]));
        Assert.Equal("/moploop", plan.ResolveAction(plan.ActionTemplates[2]));
    }

    [Fact]
    public void MopFormationMove_Uses_Normal_GlobalDelay()
    {
        Assert.False(MacroHandler.CommandSkipsGlobalDelay("mopformationmove"));
    }

    [Fact]
    public void MopPhaseWait_Skips_GlobalDelay()
    {
        Assert.True(MacroHandler.CommandSkipsGlobalDelay("mopphasewait"));
    }

    [Fact]
    public void MacroPhaseClock_Compensates_For_Elapsed_Work()
    {
        long timestamp = 0;
        var clock = new MacroPhaseClock(() => timestamp, timestampFrequency: 1000);

        timestamp = 250;
        Assert.Equal(TimeSpan.FromMilliseconds(500), clock.Advance(0.75));

        timestamp = 800;
        Assert.Equal(TimeSpan.FromMilliseconds(700), clock.Advance(0.75));
    }

    [Fact]
    public void MacroPhaseClock_Does_Not_Rebase_An_Overdue_Phase()
    {
        long timestamp = 0;
        var clock = new MacroPhaseClock(() => timestamp, timestampFrequency: 1000);

        timestamp = 1000;
        Assert.Equal(TimeSpan.Zero, clock.Advance(0.75));

        timestamp = 1100;
        Assert.InRange(clock.Advance(0.75).TotalMilliseconds, 399.99, 400.01);
    }

    [Theory]
    [InlineData(-0.01)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void MacroPhaseClock_Rejects_Invalid_Intervals(double seconds)
    {
        var clock = new MacroPhaseClock(() => 0, timestampFrequency: 1000);

        Assert.Throws<ArgumentOutOfRangeException>(() => clock.Advance(seconds));
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
        Assert.Equal(FormationAnchorKind.Default, result.Anchor.Kind);
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
    [InlineData("natural", SimpleMovementMode.Natural)]
    public void ParseFormationMoveCommandArgs_Accepts_All_Movement_Modes(string token, SimpleMovementMode expectedMode)
    {
        var result = MacroHandler.ParseFormationMoveCommandArgs($"\"Circle\" forward 1 0 {token}");

        Assert.NotNull(result);
        Assert.Equal(expectedMode, result.MovementMode);
    }

    [Theory]
    [InlineData("hybrid")]
    [InlineData("steered")]
    public void ParseFormationMoveCommandArgs_Rejects_Removed_Modes(string removedMode)
    {
        var result = MacroHandler.ParseFormationMoveCommandArgs($"\"Circle\" forward 1 0 {removedMode}");

        Assert.NotNull(result);
        Assert.Equal(removedMode, result.InvalidArgument);
        Assert.Equal(FormationAnchorKind.Default, result.Anchor.Kind);
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
    public void ParseFormationGotoCommandArgs_Defaults_To_Default_Precise()
    {
        var result = MacroHandler.ParseFormationGotoCommandArgs("\"Circle\" 2");

        Assert.NotNull(result);
        Assert.Equal("Circle", result.FormationName);
        Assert.Equal(1, result.PointIndex);
        Assert.Equal(FormationAnchorKind.Default, result.Anchor.Kind);
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

    [Theory]
    [InlineData(FormationAnchorKind.FocusTarget, 100UL, 100UL, true)]
    [InlineData(FormationAnchorKind.Target, 100UL, 100UL, true)]
    [InlineData(FormationAnchorKind.Named, 100UL, 100UL, true)]
    [InlineData(FormationAnchorKind.Self, 100UL, 100UL, false)]
    [InlineData(FormationAnchorKind.FocusTarget, 101UL, 100UL, false)]
    [InlineData(FormationAnchorKind.FocusTarget, null, 100UL, false)]
    public void FormationGoto_Skips_Local_NonSelf_Anchor(
        FormationAnchorKind anchorKind,
        ulong? anchorGameObjectId,
        ulong localGameObjectId,
        bool expected)
    {
        Assert.Equal(
            expected,
            FormationLocalMovementExecutor.ShouldSkipLocalAnchor(
                anchorKind,
                anchorGameObjectId,
                localGameObjectId));
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
    [InlineData("natural", SimpleMovementMode.Natural)]
    public void ParseFormationGotoCommandArgs_Accepts_All_Movement_Modes(string token, SimpleMovementMode expectedMode)
    {
        var result = MacroHandler.ParseFormationGotoCommandArgs($"\"Circle\" 4 target {token}");

        Assert.NotNull(result);
        Assert.Equal(expectedMode, result.MovementMode);
    }

    [Theory]
    [InlineData("hybrid")]
    [InlineData("steered")]
    public void ParseFormationGotoCommandArgs_Rejects_Removed_Modes(string removedMode)
    {
        var result = MacroHandler.ParseFormationGotoCommandArgs($"\"Circle\" 4 target {removedMode}");

        Assert.NotNull(result);
        Assert.Equal(removedMode, result.InvalidArgument);
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
    public void ParseFormationGotoCommandArgs_Defaults_To_PointOne_Anchor_When_Omitted_Or_Empty()
    {
        var omitted = MacroHandler.ParseFormationGotoCommandArgs("\"Circle\" 4 natural");
        var emptyString = MacroHandler.ParseFormationGotoCommandArgs("\"Circle\" 4 \"\" natural");

        Assert.NotNull(omitted);
        Assert.Equal(FormationAnchorKind.Default, omitted.Anchor.Kind);
        Assert.Equal(SimpleMovementMode.Natural, omitted.MovementMode);

        Assert.NotNull(emptyString);
        Assert.Equal(FormationAnchorKind.Default, emptyString.Anchor.Kind);
        Assert.Equal(SimpleMovementMode.Natural, emptyString.MovementMode);
    }

    [Theory]
    [InlineData("<t>")]
    [InlineData("[t]")]
    public void ParseFormationGotoCommandArgs_Parses_Target_Placeholders_As_Target(string token)
    {
        var result = MacroHandler.ParseFormationGotoCommandArgs($"\"Circle\" 4 {token} natural");

        Assert.NotNull(result);
        Assert.Equal(FormationAnchorKind.Target, result.Anchor.Kind);
        Assert.Equal(SimpleMovementMode.Natural, result.MovementMode);
    }

    [Theory]
    [InlineData("<f>")]
    [InlineData("[f]")]
    [InlineData("<focus>")]
    [InlineData("[focus]")]
    public void ParseFormationGotoCommandArgs_Parses_Focus_Placeholders_As_FocusTarget(string token)
    {
        var result = MacroHandler.ParseFormationGotoCommandArgs($"\"Circle\" 4 {token} natural");

        Assert.NotNull(result);
        Assert.Equal(FormationAnchorKind.FocusTarget, result.Anchor.Kind);
        Assert.Equal(SimpleMovementMode.Natural, result.MovementMode);
    }

    [Fact]
    public void ParseFormationGotoCommandArgs_Parses_Fallback_Anchor()
    {
        var result = MacroHandler.ParseFormationGotoCommandArgs("\"Circle\" 4 anchor=\"<t>\" fallback=\"sender\" natural");

        Assert.NotNull(result);
        Assert.Equal(FormationAnchorKind.Target, result.Anchor.Kind);
        Assert.NotNull(result.Fallback);
        Assert.Equal(FormationAnchorKind.Sender, result.Fallback.Kind);
        Assert.Equal(SimpleMovementMode.Natural, result.MovementMode);
    }

    [Fact]
    public void TheBees_FormationAndMacro_RoundTrip_Test()
    {
        ulong[] cids = Enumerable.Range(1, 32).Select(i => (ulong)(1000000000000000 + i)).ToArray();

        var formation = new Formation { Name = "The Bees!" };
        // Point 1: Center anchor (0, 0, 0)
        formation.Points.Add(new FormationPoint
        {
            Offset = Vector3.Zero,
            Angle = 0f
        });

        // Ring 1 (Points 2-9): Radius 0.6, Clockwise (Tangent)
        var ring1 = FormationShapeGenerator.Generate(new FormationShapeSpec
        {
            Type = FormationShapeType.Circle,
            Count = 8,
            Radius = 0.6f,
            AnchorMode = FormationShapeAnchorMode.ShapeOnly,
            AnchorNorthernmostPoint = false,
            FaceMode = FormationShapeFaceMode.Tangent
        });

        // Ring 2 (Points 10-17): Radius 1.0, Counter-Clockwise (Reverse Tangent)
        var ring2 = FormationShapeGenerator.Generate(new FormationShapeSpec
        {
            Type = FormationShapeType.Circle,
            Count = 8,
            Radius = 1.0f,
            AnchorMode = FormationShapeAnchorMode.ShapeOnly,
            AnchorNorthernmostPoint = false,
            FaceMode = FormationShapeFaceMode.ReverseTangent
        });

        // Ring 3 (Points 18-25): Radius 1.4, Clockwise (Tangent)
        var ring3 = FormationShapeGenerator.Generate(new FormationShapeSpec
        {
            Type = FormationShapeType.Circle,
            Count = 8,
            Radius = 1.4f,
            AnchorMode = FormationShapeAnchorMode.ShapeOnly,
            AnchorNorthernmostPoint = false,
            FaceMode = FormationShapeFaceMode.Tangent
        });

        // Ring 4 (Points 26-33): Radius 2.0, Counter-Clockwise (Reverse Tangent)
        var ring4 = FormationShapeGenerator.Generate(new FormationShapeSpec
        {
            Type = FormationShapeType.Circle,
            Count = 8,
            Radius = 2.0f,
            AnchorMode = FormationShapeAnchorMode.ShapeOnly,
            AnchorNorthernmostPoint = false,
            FaceMode = FormationShapeFaceMode.ReverseTangent
        });

        formation.Points.AddRange(ring1);
        formation.Points.AddRange(ring2);
        formation.Points.AddRange(ring3);
        formation.Points.AddRange(ring4);

        var formationBlob = FormationShareCode.Export(formation, true);
        Assert.True(FormationShareCode.TryImport(formationBlob, out var importedFormation, out var error), error);
        Assert.Equal(33, importedFormation.Points.Count);

        var macro = new Macro
        {
            Name = "Orbit Circle",
            Tags = ["Formations", "Test"],
            Color = new Vector4(0.95f, 0.75f, 0.10f, 1f),
            IconId = 60001,
            Variables = "$phase = .30\n$formation = Orbit Circle\n$anchor = $mop_origin_target\n$leader = Leader Character@World\n$mode = natural\n$jump = yes"
        };

        for (int charIdx = 0; charIdx < 32; charIdx++)
        {
            var cid = cids[charIdx];
            int ringIdx = charIdx / 8;
            int slotInRing = charIdx % 8;
            int ringBasePt = 2 + (ringIdx * 8);
            bool isReverse = (ringIdx % 2 == 1);
            int jumpBeat = ringIdx switch { 0 => 1, 1 => 3, 2 => 5, _ => 7 };

            var lines = new List<string>
            {
                "/mopif \"$mop_origin_target\" != \"\" /moptarget \"$mop_origin_target\"",
                "/mopif \"$mop_origin_target\" == \"\" /moptarget \"$leader\"",
                "/moploopstart",
                "/mopif \"$mop_origin_target\" != \"\" && me == \"$mop_origin_target\"",
                "    /mopphasewait $phase",
                "/mopelseif \"$mop_origin_target\" == \"\" && me == \"$mop_origin\"",
                "    /mopphasewait $phase",
                "/mopelse"
            };

            for (int step = 0; step < 8; step++)
            {
                int ptOffset = isReverse ? (8 + slotInRing - step) % 8 : (slotInRing + step) % 8;
                int ptNum = ringBasePt + ptOffset;
                lines.Add($"    /mopif \"$anchor\" != \"\" /mopformationgoto \"$formation\" {ptNum} anchor=\"$anchor\" fallback=\"$leader\" $mode");
                lines.Add($"    /mopif \"$anchor\" == \"\" /mopformationgoto \"$formation\" {ptNum} anchor=\"$mop_origin\" fallback=\"$leader\" $mode");

                if (step == 0 && slotInRing == 0)
                    lines.Add("    /ac \"Peloton\"");

                if (step == 4)
                    lines.Add("    /gaction \"sprint\"");

                if (step == jumpBeat)
                    lines.Add("    /mopif \"$jump\" == \"yes\" /gaction \"jump\"");

                lines.Add("    /mopphasewait $phase");
            }
            lines.Add("/mopendif");
            lines.Add("/moploopend");

            macro.Commands.Add(new Command
            {
                Cids = [cid],
                Actions = string.Join("\n", lines)
            });
        }

        var macroJson = macro.JsonSerialize();
        var macroBlob = macroJson.Compress();
        var decompressed = macroBlob.Decompress();
        var importedMacro = decompressed.JsonDeserialize<Macro>();

        Assert.NotNull(importedMacro);
        Assert.Equal("Orbit Circle", importedMacro.Name);
        Assert.Equal(32, importedMacro.Commands.Count);

        // Test fence stripped variations
        var variations = new[]
        {
            macroBlob,
            macroBlob + "\r\n",
            $"```text\r\n{macroBlob}\r\n```",
            $"```\r\n{macroBlob}\r\n```",
            $"```text\n{macroBlob}\n```",
            $"```\n{macroBlob}\n```"
        };

        foreach (var v in variations)
        {
            var stripped = System.Text.RegularExpressions.Regex.Replace(
                v.Trim(),
                @"^```[a-zA-Z]*\r?\n?|```$",
                string.Empty,
                System.Text.RegularExpressions.RegexOptions.Multiline).Trim();
            var dec = stripped.Decompress();
            var parsed = dec.JsonDeserialize<Macro>();
            Assert.NotNull(parsed);
            Assert.Equal("Orbit Circle", parsed.Name);
        }
    }

    [Fact]
    public void OrbitRing_FormationAndMacro_Test()
    {
        var formation = new Formation { Name = "Orbit Ring" };
        formation.Points.Add(new FormationPoint
        {
            Offset = Vector3.Zero,
            Angle = 0f
        });

        // 4 Concentric Rings with expanded radii (+0.3 each):
        // Ring 1 (Points 2-9): Radius 0.9, Clockwise (Tangent)
        var ring1 = FormationShapeGenerator.Generate(new FormationShapeSpec
        {
            Type = FormationShapeType.Circle,
            Count = 8,
            Radius = 0.9f,
            AnchorMode = FormationShapeAnchorMode.ShapeOnly,
            AnchorNorthernmostPoint = false,
            FaceMode = FormationShapeFaceMode.Tangent
        });

        // Ring 2 (Points 10-17): Radius 1.3, Counter-Clockwise (Reverse Tangent)
        var ring2 = FormationShapeGenerator.Generate(new FormationShapeSpec
        {
            Type = FormationShapeType.Circle,
            Count = 8,
            Radius = 1.3f,
            AnchorMode = FormationShapeAnchorMode.ShapeOnly,
            AnchorNorthernmostPoint = false,
            FaceMode = FormationShapeFaceMode.ReverseTangent
        });

        // Ring 3 (Points 18-25): Radius 1.7, Clockwise (Tangent)
        var ring3 = FormationShapeGenerator.Generate(new FormationShapeSpec
        {
            Type = FormationShapeType.Circle,
            Count = 8,
            Radius = 1.7f,
            AnchorMode = FormationShapeAnchorMode.ShapeOnly,
            AnchorNorthernmostPoint = false,
            FaceMode = FormationShapeFaceMode.Tangent
        });

        // Ring 4 (Points 26-33): Radius 2.3, Counter-Clockwise (Reverse Tangent)
        var ring4 = FormationShapeGenerator.Generate(new FormationShapeSpec
        {
            Type = FormationShapeType.Circle,
            Count = 8,
            Radius = 2.3f,
            AnchorMode = FormationShapeAnchorMode.ShapeOnly,
            AnchorNorthernmostPoint = false,
            FaceMode = FormationShapeFaceMode.ReverseTangent
        });

        formation.Points.AddRange(ring1);
        formation.Points.AddRange(ring2);
        formation.Points.AddRange(ring3);
        formation.Points.AddRange(ring4);

        var cids = Enumerable.Range(1, 32).Select(i => (ulong)(1000000000000000 + i)).ToList();

        for (int i = 0; i < 32; i++)
        {
            formation.Points[i + 1].Cids = [cids[i]];
        }

        var formationBlob = FormationShareCode.Export(formation, true);
        Assert.True(FormationShareCode.TryImport(formationBlob, out var importedFormation, out var formError), formError);
        Assert.Equal(33, importedFormation.Points.Count);

        var macro = new Macro
        {
            Name = "Orbit Ring",
            Tags = ["Formations", "Test"],
            Color = new Vector4(0.35f, 0.75f, 0.95f, 1f),
            IconId = 60002,
            Variables = "$phase = .20\n$formation = Orbit Ring\n$anchor = $mop_origin_target\n$leader = Leader Character@World\n$mode = natural\n$jump = yes"
        };

        for (int charIdx = 0; charIdx < 32; charIdx++)
        {
            var cid = cids[charIdx];
            int ringIdx = charIdx / 8;
            int slotInRing = charIdx % 8;
            int ringBasePt = 2 + (ringIdx * 8);
            bool isReverse = (ringIdx % 2 == 1);
            int jumpBeat = ringIdx switch { 0 => 1, 1 => 3, 2 => 5, _ => 7 };

            var lines = new List<string>
            {
                "/mopif \"$mop_origin_target\" != \"\" /moptarget \"$mop_origin_target\"",
                "/mopif \"$mop_origin_target\" == \"\" /moptarget \"$leader\"",
                "/moploopstart",
                "/mopif \"$mop_origin_target\" != \"\" && me == \"$mop_origin_target\"",
                "    /mopphasewait $phase",
                "/mopelseif \"$mop_origin_target\" == \"\" && me == \"$mop_origin\"",
                "    /mopphasewait $phase",
                "/mopelse"
            };

            for (int step = 0; step < 8; step++)
            {
                int ptOffset = isReverse ? (8 + slotInRing - step) % 8 : (slotInRing + step) % 8;
                int ptNum = ringBasePt + ptOffset;
                lines.Add($"    /mopif \"$anchor\" != \"\" /mopformationgoto \"$formation\" {ptNum} anchor=\"$anchor\" fallback=\"$leader\" $mode");
                lines.Add($"    /mopif \"$anchor\" == \"\" /mopformationgoto \"$formation\" {ptNum} anchor=\"$mop_origin\" fallback=\"$leader\" $mode");

                if (step == 0 && slotInRing == 0)
                    lines.Add("    /ac \"Peloton\"");

                if (step == 4)
                    lines.Add("    /gaction \"sprint\"");

                if (step == jumpBeat)
                    lines.Add("    /mopif \"$jump\" == \"yes\" /gaction \"jump\"");

                lines.Add("    /mopphasewait $phase");
            }
            lines.Add("/mopendif");
            lines.Add("/moploopend");

            macro.Commands.Add(new Command
            {
                Cids = [cid],
                Actions = string.Join("\n", lines)
            });
        }

        var macroBlob = macro.JsonSerialize().Compress();
        var decompressed = macroBlob.Decompress();
        var importedMacro = decompressed.JsonDeserialize<Macro>();
        Assert.NotNull(importedMacro);
        Assert.Equal("Orbit Ring", importedMacro.Name);
        Assert.Equal(32, importedMacro.Commands.Count);
    }
}
