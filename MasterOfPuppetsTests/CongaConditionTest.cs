using Xunit;
using MasterOfPuppets;

namespace MasterOfPuppetsTests;

public class CongaConditionTest
{
    [Theory]
    [InlineData("\"Character Alpha\" != \"Character Beta\"", true)]
    [InlineData("\"Character Alpha\" == \"Character Alpha\"", true)]
    [InlineData("\"Character Alpha\" == \"Character Beta\"", false)]
    [InlineData("\"$var\" != \"\"", true)]
    [InlineData("\"\" != \"\"", false)]
    [InlineData("\"\" == \"\"", true)]
    [InlineData("\"test\" == \"test\" && \"a\" != \"b\"", true)]
    [InlineData("\"test\" == \"wrong\" || \"a\" != \"b\"", true)]
    [InlineData("1 == 10", false)]
    [InlineData("1 != 10", true)]
    [InlineData("\"W\" == \"WAR\"", false)]
    public void Test_Condition_Evaluation(string cond, bool expected)
    {
        bool res = MacroConditionEvaluator.Evaluate(cond, null);
        Assert.Equal(expected, res);
    }
}
