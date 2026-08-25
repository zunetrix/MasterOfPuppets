using Xunit;
using MasterOfPuppets;
using MasterOfPuppets.Extensions;
using System.Collections.Generic;

namespace MasterOfPuppetsTests;

public class TestMacroImportDirect {
    [Fact]
    public void TestDirectImport() {
        var macro = new Macro
        {
            Name = "Direct Import Test",
            Variables = "$phase = .20\n$mode = natural"
        };
        for (int i = 0; i < 4; i++)
        {
            macro.Commands.Add(new Command
            {
                Cids = [(ulong)(1000000000000000 + i + 1)],
                Actions = "/mopwalkon\n/mopphasewait $phase"
            });
        }

        var blob = macro.JsonSerialize().Compress();
        var decompressed = blob.Decompress();
        var deserialized = decompressed.JsonDeserialize<Macro>();

        Assert.NotNull(deserialized);
        Assert.Equal("Direct Import Test", deserialized.Name);
        Assert.Equal(4, deserialized.Commands.Count);
        Assert.False(string.IsNullOrEmpty(deserialized.Variables), "Variables is empty!");
    }
}
