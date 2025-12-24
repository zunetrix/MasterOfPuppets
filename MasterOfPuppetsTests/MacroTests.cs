using Xunit;
using System.Collections.Generic;

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
}
