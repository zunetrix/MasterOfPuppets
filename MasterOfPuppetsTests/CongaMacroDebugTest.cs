using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using MasterOfPuppets;
using MasterOfPuppets.Extensions;

namespace MasterOfPuppetsTests;

public class CongaMacroDebugTest
{
    [Fact]
    public void Conga_Macro_ActionStructure_RoundTrip_Test()
    {
        string[] characterNames = ["Character Alpha", "Character Beta", "Character Gamma", "Character Delta"];
        var macro = new Macro
        {
            Name = "Conga: Target-Based Auto Line",
            Tags = ["Conga", "Test"]
        };

        for (int i = 0; i < characterNames.Length; i++)
        {
            var lines = new List<string>
            {
                "/mopwalkon",
                "/mopif \"$mop_origin_target\" != \"\"",
                "    /mopif me != \"$mop_origin_target\""
            };

            if (i == 0)
            {
                lines.Add("        /mopformationgoto \"CongaSlot\" 2 anchor=\"$mop_origin_target\" natural");
            }
            else
            {
                for (int c = 1; c <= i; c++)
                {
                    int prevIdx = i - c;
                    string prevName = characterNames[prevIdx];
                    string prefix = c == 1 ? $"        /mopif visible \"{prevName}\"" : $"        /mopelseif visible \"{prevName}\"";
                    lines.Add(prefix);
                    lines.Add($"            /mopformationgoto \"CongaSlot\" 2 anchor=\"{prevName}@World\" natural");
                }
                lines.Add("        /mopelse");
                lines.Add("            /mopformationgoto \"CongaSlot\" 2 anchor=\"$mop_origin_target\" natural");
                lines.Add("        /mopendif");
            }

            lines.Add("    /mopendif");
            lines.Add("/mopelse");
            lines.Add("    /mopif me != \"$mop_origin\"");

            if (i == 0)
            {
                lines.Add("        /mopformationgoto \"CongaSlot\" 2 anchor=\"$mop_origin\" natural");
            }
            else
            {
                for (int c = 1; c <= i; c++)
                {
                    int prevIdx = i - c;
                    string prevName = characterNames[prevIdx];
                    string prefix = c == 1 ? $"        /mopif visible \"{prevName}\"" : $"        /mopelseif visible \"{prevName}\"";
                    lines.Add(prefix);
                    lines.Add($"            /mopformationgoto \"CongaSlot\" 2 anchor=\"{prevName}@World\" natural");
                }
                lines.Add("        /mopelse");
                lines.Add("            /mopformationgoto \"CongaSlot\" 2 anchor=\"$mop_origin\" natural");
                lines.Add("        /mopendif");
            }

            lines.Add("    /mopendif");
            lines.Add("/mopendif");

            macro.Commands.Add(new Command
            {
                Cids = [(ulong)(1000000000000000 + i + 1)],
                Actions = string.Join("\n", lines)
            });
        }

        var blob = macro.JsonSerialize().Compress();
        var deserialized = blob.Decompress().JsonDeserialize<Macro>();

        Assert.NotNull(deserialized);
        Assert.Equal("Conga: Target-Based Auto Line", deserialized.Name);
        Assert.Equal(4, deserialized.Commands.Count);
        Assert.Contains("/mopwalkon", deserialized.Commands[0].Actions);
        Assert.Contains("/mopformationgoto \"CongaSlot\" 2 anchor=\"$mop_origin_target\" natural", deserialized.Commands[0].Actions);
        Assert.Contains("visible \"Character Alpha\"", deserialized.Commands[1].Actions);
    }
}
