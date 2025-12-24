using Xunit;

public class CommandTests
{

    [Fact]
    public void Removes_Line_And_Block_Comments()
    {
        var cmd = new Command
        {
            Actions = @"
                /cmd1
                # comment
                /* block
                   comment */
                /cmd2
            "
        };

        var result = cmd.GetActionList();

        Assert.Equal(new[] { "/cmd1", "/cmd2" }, result);
    }

    [Fact]
    public void Extracts_And_Substitutes_Variables()
    {
        var cmd = new Command
        {
            Actions = @"
                $name = ""Test""
                $time=0.5
                /moptarget $name
                /mopwait $time
            "
        };

        var result = cmd.GetActionList();

        Assert.Equal(new[] {
            "/moptarget Test",
            "/mopwait 0.5"
        }, result);
    }

    [Fact]
    public void Supports_Unquoted_Variable_Values()
    {
        var cmd = new Command
        {
            Actions = @"
                $x=10
                /move $x
            "
        };

        var result = cmd.GetActionList();

        Assert.Equal(new[] { "/move 10" }, result);
    }

    [Fact]
    public void Ignores_Variable_Definitions_In_Output()
    {
        var cmd = new Command
        {
            Actions = @"
                $a=1
                $b=2
            "
        };

        var result = cmd.GetActionList();

        Assert.Empty(result);
    }

    [Fact]
    public void Variable_Names_Are_Case_Sensitive()
    {
        var cmd = new Command
        {
            Actions = @"
            $Time=1
            /wait $time
        "
        };

        var result = cmd.GetActionList();

        Assert.Equal(new[] { "/wait $time" }, result);
    }

    [Fact]
    public void Variable_Is_Replaced_When_Case_Matches()
    {
        var cmd = new Command
        {
            Actions = @"
            $time=1
            /wait $time
        "
        };

        var result = cmd.GetActionList();

        Assert.Equal(new[] { "/wait 1" }, result);
    }

    [Fact]
    public void Does_Not_Substitute_Partial_Matches()
    {
        var cmd = new Command
        {
            Actions = @"
                $a=1
                /test $ab
            "
        };

        var result = cmd.GetActionList();

        Assert.Equal(new[] { "/test $ab" }, result);
    }

    [Fact]
    public void Keeps_Number_Of_Actions_Consistent()
    {
        var cmd = new Command
        {
            Actions = @"
                $x=1
                /a
                /b $x
                /c
            "
        };

        var result = cmd.GetActionList();

        Assert.Equal(3, result.Length);
    }
}
