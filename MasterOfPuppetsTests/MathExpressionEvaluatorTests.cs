using Xunit;
using MasterOfPuppets;

namespace MasterOfPuppetsTests;

public class MathExpressionEvaluatorTests
{
    [Theory]
    [InlineData("7 * 0.8", "5.6")]
    [InlineData("7*0.8", "5.6")]
    [InlineData("0.8", "0.8")]
    [InlineData("3 + 1", "4")]
    [InlineData("(3 + 1) * 2.5", "10")]
    [InlineData("10 / 4", "2.5")]
    [InlineData("2 ^ 3", "8")]
    [InlineData("-1.5 + 3", "1.5")]
    [InlineData("7 % 2", "1")]
    [InlineData("1 + 2 * 3", "7")]
    public void Evaluates_Arithmetic_Expressions(string input, string expected)
    {
        Assert.True(MathExpressionEvaluator.TryEvaluate(input, out var result), $"expected '{input}' to evaluate");
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("/clap")]
    [InlineData("some name")]
    [InlineData("hello world")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("2 +")]
    public void Leaves_Non_Arithmetic_Input_Untouched(string input)
    {
        Assert.False(MathExpressionEvaluator.TryEvaluate(input, out _), $"expected '{input}' NOT to evaluate");
    }

    [Fact]
    public void Macro_Definition_Computes_TotalWait_From_Interval()
    {
        var macro = new Macro
        {
            Variables = "$emote=/surprised\n$interval=0.80\n$totalWait=7 * $interval",
            Commands = new List<Command> {
                new Command {
                    Cids = new() { 1 },
                    Actions = "/mopphasewait $totalWait\n$emote"
                }
            }
        };

        var actions = macro.GetCidActions(1);

        Assert.Equal(
            new[] { "/mopphasewait 5.6", "/surprised" },
            actions);
    }

    [Fact]
    public void Macro_Per_Character_Offset_Computes_From_Interval()
    {
        // Character at position 3 of 7: offset = 3 * interval.
        var macro = new Macro
        {
            Variables = "$interval=0.80\n$offset=3 * $interval\n$totalWait=7 * $interval",
            Commands = new List<Command> {
                new Command {
                    Cids = new() { 1 },
                    Actions = "/mopphasewait $offset\n$emote\n/mopphasewait $totalWait"
                }
            }
        };

        var actions = macro.GetCidActions(1, inlineVars: new Dictionary<string, string> { ["emote"] = "/dance" });

        Assert.Equal(
            new[] { "/mopphasewait 2.4", "/dance", "/mopphasewait 5.6" },
            actions);
    }

    [Fact]
    public void Calc_Token_Evaluates_Inline_Math()
    {
        // {calc} is evaluated at dispatch time (after $-variable substitution), so the
        // token body can reference already-substituted variables.
        var result = MacroTokenProcessor.Process("/mopphasewait {calc(0.8 * 7)}");

        Assert.Equal("/mopphasewait 5.6", result);
    }

    [Fact]
    public void Calc_Token_Leaves_Invalid_Expression_Untouched()
    {
        // A non-arithmetic calc body is left as-is rather than breaking the action.
        var actions = MacroTokenProcessor.Process("/moptarget \"{calc(Foo @ X)}\"");

        Assert.Equal("/moptarget \"{calc(Foo @ X)}\"", actions);
    }

    [Fact]
    public void Rotating_Emote_Blob_Resolves_For_Character_K()
    {
        // Mirrors the exportable blob: ONE command targets all 7 characters with identical
        // text, and $assignmentIndex/$assignmentCount give each character its own stagger lane.
        // The two phase-waits per loop (offset + tail) sum to one full cycle (totalWait) so the
        // absolute phase clock advances exactly one cycle per iteration — no drift.
        // 5th listed cid -> assignmentIndex 4 -> offset 3.2; totalWait = count(7) * 0.8 = 5.6;
        // tail = 5.6 - 3.2 = 2.4.
        var cids = new List<ulong> { 101, 102, 103, 104, 105, 106, 107 };
        var macro = new Macro
        {
            Variables = "$emote=/surprised\n$interval=0.80",
            Commands = new List<Command> {
                new Command {
                    Cids = cids,
                    Actions = "$offset=$assignmentIndex * $interval\n$totalWait=$assignmentCount * $interval\n$tail=$totalWait - $offset\n/mopphasewait $offset\n$emote\n/mopphasewait $tail\n/moploop",
                }
            }
        };

        var actions = macro.GetCidActions(cids[4]);
        Assert.Equal(
            new[] { "/mopphasewait 3.2", "/surprised", "/mopphasewait 2.4", "/moploop" },
            actions);
    }

    [Fact]
    public void Single_Command_Single_Template_Staggers_Each_Cid()
    {
        // Identical command text, one command, but each targeted character derives a different
        // offset (and matching tail) from its own $assignmentIndex. offset + tail = cycle(5.6).
        var macro = new Macro
        {
            Variables = "$emote=/surprised\n$interval=0.80",
            Commands = new List<Command> {
                new Command {
                    Cids = new() { 101, 102, 103, 104, 105, 106, 107 },
                    Actions = "$offset=$assignmentIndex * $interval\n$totalWait=$assignmentCount * $interval\n$tail=$totalWait - $offset\n/mopphasewait $offset\n$emote\n/mopphasewait $tail\n/moploop",
                }
            }
        };

        for (int i = 0; i < 7; i++) {
            ulong cid = (ulong)(101 + i);
            var actions = macro.GetCidActions(cid);
            var expectedOffset = (i * 0.8).ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
            var tail = (5.6 - i * 0.8).ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
            Assert.Equal(
                new[] { $"/mopphasewait {expectedOffset}", "/surprised", $"/mopphasewait {tail}", "/moploop" },
                actions);
        }
    }

    [Fact]
    public void CommandIndex_And_CommandCount_Are_Macro_Level()
    {
        // $commandIndex/$commandCount are macro-level: which command, how many commands.
        // Three commands, each targeting its own cid; each sees its own list position and count 3.
        var macro = new Macro
        {
            Variables = "$interval=0.1",
            Commands = new List<Command> {
                new Command { Cids = new() { 100 }, Actions = "/mopwait $offset\n/mopwait $total\n$offset=$commandIndex * $interval\n$total=$commandCount * $interval" },
                new Command { Cids = new() { 200 }, Actions = "/mopwait $offset\n/mopwait $total\n$offset=$commandIndex * $interval\n$total=$commandCount * $interval" },
                new Command { Cids = new() { 300 }, Actions = "/mopwait $offset\n/mopwait $total\n$offset=$commandIndex * $interval\n$total=$commandCount * $interval" },
            }
        };

        Assert.Equal("/mopwait 0", macro.GetCidActions(100)[0]);
        Assert.Equal("/mopwait 0.1", macro.GetCidActions(200)[0]);
        Assert.Equal("/mopwait 0.2", macro.GetCidActions(300)[0]);
        // total = commandCount * interval = 3 * 0.1 = 0.3 across every command
        Assert.All(new[] { 100UL, 200UL, 300UL }, cid => Assert.Equal("/mopwait 0.3", macro.GetCidActions(cid)[1]));
    }

    [Fact]
    public void Assignment_Index_Is_Author_Listing_Order_Over_Union_With_Groups()
    {
        // A command may target characters via direct cids AND via groups. The assignment lane
        // is the union in author-listing order (direct cids first, then group cids, dedup by
        // first-seen position).
        var groups = new List<CidGroup> {
            new() { Name = "G", Cids = new() { 600, 100 } }, // 100 already direct -> skipped for dedup
        };
        var macro = new Macro
        {
            Variables = "$interval=1",
            Commands = new List<Command> {
                new Command {
                    Cids = new() { 500, 100 },
                    GroupIds = new() { "G" },
                    Actions = "/mopwait $offset\n/mopwait $total\n$offset=$assignmentIndex\n$total=$assignmentCount",
                }
            }
        };

        // Union order: [500, 100, 600], count 3.
        Assert.Equal("/mopwait 0", macro.GetCidActions(500, groups)[0]);
        Assert.Equal("/mopwait 1", macro.GetCidActions(100, groups)[0]);
        Assert.Equal("/mopwait 2", macro.GetCidActions(600, groups)[0]);
        Assert.All(new[] { 500UL, 100UL, 600UL }, cid => Assert.Equal("/mopwait 3", macro.GetCidActions(cid, groups)[1]));
    }

    [Fact]
    public void Group_Assigned_Characters_Get_Assignment_Lanes()
    {
        // Characters reached purely through a group still receive their own stagger lane.
        var groups = new List<CidGroup> {
            new() { Name = "Trio", Cids = new() { 11, 22, 33 } },
        };
        var macro = new Macro
        {
            Variables = "$interval=0.5",
            Commands = new List<Command> {
                new Command {
                    GroupIds = new() { "Trio" },
                    Actions = "$offset=$assignmentIndex * $interval\n/mopwait $offset",
                }
            }
        };

        Assert.Equal("/mopwait 0", macro.GetCidActions(11, groups)[0]);
        Assert.Equal("/mopwait 0.5", macro.GetCidActions(22, groups)[0]);
        Assert.Equal("/mopwait 1", macro.GetCidActions(33, groups)[0]);
    }

    [Fact]
    public void Command_And_Assignment_Vars_Are_Authoritative()
    {
        // Author-declared auto-vars MUST NOT shadow the engine values, else the stagger
        // corrupts. Inline commandVars are lower precedence than the injected auto-vars.
        var macro = new Macro
        {
            Variables = "$interval=0.1",
            Commands = new List<Command> {
                new Command {
                    Cids = new() { 7, 8 },
                    Actions = "$offset=$assignmentIndex * $interval\n$commandCount=999\n$commandIndex=99\n$assignmentCount=123\n$assignmentIndex=88\n/mopwait $offset",
                }
            }
        };

        Assert.Equal("/mopwait 0", macro.GetCidActions(7)[0]);    // assignmentIndex 0, not 88
        Assert.Equal("/mopwait 0.1", macro.GetCidActions(8)[0]);  // assignmentIndex 1, not 88
    }

    [Fact]
    public void GlobalDelay_Variable_Resolves_From_Runtime()
    {
        // $globaldelay is exposed from the runtime variables and is usable in arithmetic.
        var macro = new Macro
        {
            Commands = new List<Command> {
                new Command { Cids = new() { 1 }, Actions = "/mopwait $globaldelay\n/mopwait $doubled\n$doubled=$globaldelay * 2" },
            }
        };

        var runtime = new MacroRuntimeVariables { GlobalDelaySeconds = 0.5 };
        var actions = macro.GetCidActions(1, runtimeVariables: runtime);

        Assert.Equal("/mopwait 0.5", actions[0]);
        Assert.Equal("/mopwait 1", actions[1]);
    }

    [Fact]
    public void Evaluator_Rejects_Overly_Long_Input()
    {
        var longInput = string.Join(" + ", Enumerable.Repeat("1", 300));
        Assert.True(longInput.Length > 512);
        Assert.False(MathExpressionEvaluator.TryEvaluate(longInput, out _));
    }

    [Fact]
    public void Evaluator_Rejects_Deeply_Nested_Parentheses()
    {
        var nested = new string('(', 200) + "1" + new string(')', 200);
        Assert.False(MathExpressionEvaluator.TryEvaluate(nested, out _));
    }

    [Fact]
    public void Evaluator_Still_Handles_Legitimate_Depth_And_Length()
    {
        Assert.True(MathExpressionEvaluator.TryEvaluate("(((1 + 2) * (3 + 4)))", out var result));
        Assert.Equal("21", result);

        var normal = string.Join(" + ", Enumerable.Repeat("1", 30));
        Assert.True(MathExpressionEvaluator.TryEvaluate(normal, out _));
    }

    [Fact]
    public void Live_Setvar_Interval_Recomputes_Derived_Values()
    {
        // Changing $interval on a running macro flows through UpdateVariables onto the
        // execution plan; the derived offsets re-evaluate at next action resolution.
        var macro = new Macro
        {
            Variables = "$interval=0.80\n$count=7\n$totalWait=$count * $interval",
            Commands = new List<Command> {
                new Command {
                    Cids = new() { 1 },
                    Actions = "/mopphasewait $totalWait"
                }
            }
        };

        var plan = macro.CreateCidExecutionPlan(1);
        Assert.Equal("/mopphasewait 5.6", plan.ResolveAction(plan.ActionTemplates[0]));

        plan.UpdateVariables(new Dictionary<string, string> { ["interval"] = "1.0" });
        Assert.Equal("/mopphasewait 7", plan.ResolveAction(plan.ActionTemplates[0]));
    }
}
