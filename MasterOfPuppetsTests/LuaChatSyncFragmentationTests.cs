using System.Text;

using MasterOfPuppets;
using MasterOfPuppets.Ipc;
using MasterOfPuppets.LuaScripting.Runs;
using MasterOfPuppets.LuaScripting.Synchronization;
using MasterOfPuppets.Util;

using Xunit;

public sealed class LuaChatSyncFragmentationTests {
    private static readonly ulong[] CatwalkRoster = [
        18014498545172021, 18014498567996160, 18014498564173337, 18014498573073575,
        18014498563678948, 18014498564936865, 18014498571136757, 18014498576354931,
        18014498563154967, 18014498570473149, 18014498571153678, 18014498578296334,
        18014498563665055, 18014498570473433, 18014498571161831, 18014498578296489,
        18014498563667141, 18014498567553281, 18014498571153779, 18014498578305144,
        18014498563676673, 18014498572667643, 18014498571146002, 18014498578447585,
        18014498563880622, 18014498576354403, 18014498571136822, 18014498578447641,
        18014498563975016, 18014498567531790, 18014498571145699, 18014498578454220,
    ];

    [Fact]
    public void CatwalkLaunch_ExceedingOneMessage_IsFragmentedAndReassembledOutOfOrder() {
        var envelope = CatwalkEnvelope();
        var token = IpcProvider.EncodeLuaChatSyncEnvelope(envelope);
        var legacyCommand = $"/cwl2 mopluarun \"Opposite Rows (32)\" {token}";
        Assert.True(Encoding.UTF8.GetByteCount(legacyCommand) > 500);

        Assert.True(LuaChatSyncFragmentCodec.TryCreateCommands(
            "/cwl2",
            "Opposite Rows (32)",
            envelope.MessageId,
            token,
            out var commands,
            out var error), error);
        Assert.InRange(commands.Count, 2, LuaChatSyncFragmentCodec.MaximumFragments);
        Assert.All(commands, command =>
            Assert.InRange(Encoding.UTF8.GetByteCount(command), 1, LuaChatSyncFragmentCodec.MaximumChatBytes));

        var fragments = commands
            .Select(ParseCommand)
            .Reverse()
            .ToArray();
        var assembler = new LuaChatSyncFragmentAssembler();
        var now = DateTimeOffset.UtcNow;
        string completedScript = string.Empty;
        string completedToken = string.Empty;
        for (var index = 0; index < fragments.Length; index++) {
            var status = assembler.Accept(
                fragments[index],
                "Kazuko Aura@Sargatanas",
                now.AddMilliseconds(index),
                out completedScript,
                out completedToken,
                out error);
            Assert.Equal(index + 1 == fragments.Length
                ? LuaChatSyncAssemblyStatus.Complete
                : LuaChatSyncAssemblyStatus.Pending, status);
            Assert.Empty(error);
        }

        Assert.Equal("Opposite Rows (32)", completedScript);
        Assert.Equal(token, completedToken);
        Assert.True(IpcProvider.TryDecodeLuaChatSyncEnvelope(completedToken, out var decoded));
        Assert.Equal(CatwalkRoster, decoded.ParticipantCids);
        Assert.Equal("0.25", decoded.Variables["horizontal"]);
        Assert.Equal("5.0", decoded.Variables["vertical"]);
        Assert.Equal(envelope.MessageId, decoded.MessageId);
        Assert.Equal(0, assembler.Count);
    }

    [Fact]
    public void FragmentAssembler_IgnoresIdenticalDuplicatesAndRejectsConflicts() {
        var envelope = CatwalkEnvelope();
        var token = IpcProvider.EncodeLuaChatSyncEnvelope(envelope);
        Assert.True(LuaChatSyncFragmentCodec.TryCreateCommands(
            "/cwl2",
            "Opposite Rows (32)",
            envelope.MessageId,
            token,
            out var commands,
            out var error), error);
        var first = ParseCommand(commands[0]);
        var assembler = new LuaChatSyncFragmentAssembler();
        var now = DateTimeOffset.UtcNow;

        Assert.Equal(LuaChatSyncAssemblyStatus.Pending,
            assembler.Accept(first, "Kazuko Aura@Sargatanas", now, out _, out _, out error));
        Assert.Empty(error);
        Assert.Equal(LuaChatSyncAssemblyStatus.Pending,
            assembler.Accept(first, "Kazuko Aura@Sargatanas", now, out _, out _, out error));
        Assert.Empty(error);

        var conflicting = first with { Payload = first.Payload[..^1] + (first.Payload[^1] == 'A' ? "B" : "A") };
        Assert.Equal(LuaChatSyncAssemblyStatus.Rejected,
            assembler.Accept(conflicting, "Kazuko Aura@Sargatanas", now, out _, out _, out error));
        Assert.Contains("conflicting", error);
        Assert.Equal(0, assembler.Count);
    }

    [Fact]
    public void FragmentAssembler_BoundsAndExpiresIncompleteAssemblies() {
        var envelope = CatwalkEnvelope();
        var token = IpcProvider.EncodeLuaChatSyncEnvelope(envelope);
        Assert.True(LuaChatSyncFragmentCodec.TryCreateCommands(
            "/cwl2",
            "Opposite Rows (32)",
            envelope.MessageId,
            token,
            out var commands,
            out var error), error);
        var first = ParseCommand(commands[0]);
        var assembler = new LuaChatSyncFragmentAssembler(TimeSpan.FromSeconds(1));
        var now = DateTimeOffset.UtcNow;
        Assert.Equal(LuaChatSyncAssemblyStatus.Pending,
            assembler.Accept(first, "Kazuko Aura@Sargatanas", now, out _, out _, out error));

        var different = first with { MessageId = Guid.NewGuid() };
        Assert.Equal(LuaChatSyncAssemblyStatus.Pending,
            assembler.Accept(different, "Kazuko Aura@Sargatanas", now.AddSeconds(2), out _, out _, out error));
        Assert.Equal(1, assembler.Count);
    }

    [Fact]
    public void ChatWatcher_RecognizesValidFragmentsAsInternalProtocolMessages() {
        var envelope = CatwalkEnvelope();
        var token = IpcProvider.EncodeLuaChatSyncEnvelope(envelope);
        Assert.True(LuaChatSyncFragmentCodec.TryCreateCommands(
            "/cwl2",
            "Opposite Rows (32)",
            envelope.MessageId,
            token,
            out var commands,
            out var error), error);
        var parsed = ArgumentParser.ParseChatArgs(commands[0]["/cwl2 ".Length..]);

        Assert.True(ChatWatcher.IsInternalLuaSyncEnvelope(parsed));
    }

    private static LuaChatSyncFragment ParseCommand(string command) {
        var parsed = ArgumentParser.ParseChatArgs(command["/cwl2 ".Length..]);
        Assert.Equal(LuaChatSyncFragmentCodec.CommandName, parsed[0]);
        Assert.True(LuaChatSyncFragmentCodec.TryParseArguments(
            parsed.Skip(1).ToArray(),
            out var fragment,
            out var error), error);
        return fragment;
    }

    private static LuaChatSyncEnvelope CatwalkEnvelope() => new() {
        MessageId = "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee",
        CreatedUnixMilliseconds = 1788112800000,
        StartUtcTicks = 639237288000000000,
        StartServerTimeSeconds = 1788112806,
        Seed = 793223713,
        BundleHash = "6338b6ed83e7cb7db084db1caa3891f194e3e40f8e3a85d9d3cbc15d709774ff",
        RequiredResources = LuaResourceKind.ChatActionBudget | LuaResourceKind.GameActions,
        RunTargetName = "Kazuko Aura@Sargatanas",
        RunTargetEntityId = 0x12345678,
        Variables = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) {
            ["anchor"] = "Kazuko Aura@Sargatanas",
            ["group"] = "32 Ordered",
            ["visible_only"] = "true",
            ["horizontal"] = "0.25",
            ["vertical"] = "5.0",
            ["mop_run_target_explicit"] = "true",
        },
        ParticipantCids = CatwalkRoster.ToList(),
    };
}
