using System.Text;

using MasterOfPuppets.Ipc;
using MasterOfPuppets.LuaScripting;
using MasterOfPuppets.LuaScripting.Automation;
using MasterOfPuppets.LuaScripting.Synchronization;

using Xunit;

public sealed class MirrorRunTargetTransportTests {
    private const string BundleHash = "6338b6ed83e7cb7db084db1caa3891f194e3e40f8e3a85d9d3cbc15d709774ff";

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 250)]
    [InlineData(7, 1750)]
    [InlineData(31, 7750)]
    public void StopRelay_IsDeterministicallyStaggeredByGlobalParticipantSlot(int slot, int milliseconds) {
        Assert.Equal(TimeSpan.FromMilliseconds(milliseconds), IpcProvider.GetMirrorStopRelayDelay(slot));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(32)]
    public void StopRelay_RejectsInvalidParticipantSlot(int slot) {
        Assert.Throws<ArgumentOutOfRangeException>(() => IpcProvider.GetMirrorStopRelayDelay(slot));
    }

    [Fact]
    public void StopChatFrame_RequiresOneExactSafeRunId() {
        Assert.True(IpcProvider.TryParseMirrorStopArguments(
            ["08df07c4d35d7322-55b509ab-85405fd0"], out var runId));
        Assert.Equal("08df07c4d35d7322-55b509ab-85405fd0", runId);
        Assert.False(IpcProvider.TryParseMirrorStopArguments([], out _));
        Assert.False(IpcProvider.TryParseMirrorStopArguments(["Mirror Script V2"], out _));
        Assert.False(IpcProvider.TryParseMirrorStopArguments(
            ["08df07c4d35d7322-55b509ab-85405fd0", "extra"], out _));
    }

    [Fact]
    public void ReservedMetadata_RoundTripsThroughLocalIpcVariableCodec() {
        var variables = MetadataVariables(Binding());

        var token = IpcProvider.EncodeLuaVariablesToken(variables);

        Assert.True(IpcProvider.TryDecodeLuaVariablesToken(token, out var decoded));
        AssertMetadata(decoded, Binding());
    }

    [Fact]
    public void ReservedMetadata_RoundTripsThroughChatEnvelopeWithinFragmentBudget() {
        var binding = Binding();
        var withMetadata = Envelope(MetadataVariables(binding));
        var baseline = Envelope(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));

        var token = IpcProvider.EncodeLuaChatSyncEnvelope(withMetadata);
        var baselineToken = IpcProvider.EncodeLuaChatSyncEnvelope(baseline);

        Assert.True(IpcProvider.TryDecodeLuaChatSyncEnvelope(token, out var decoded));
        AssertMetadata(decoded.Variables, binding);
        var fragments = FragmentCount(withMetadata, token);
        var baselineFragments = FragmentCount(baseline, baselineToken);
        Assert.InRange(fragments, 1, 8);
        Assert.True(
            fragments <= baselineFragments + 1,
            $"Mirror metadata increased Chat launch from {baselineFragments} to {fragments} fragments");
    }

    [Fact]
    public void AnchorOverrideMismatch_ClearsFrozenIdsAndPreservesSelectedSourceEvidence() {
        var identity = new MirrorRunTargetIdentity(
            "Selected Example@Moogle",
            100,
            200,
            300,
            true,
            "selected-player");

        var binding = MirrorRunTargetValidator.ResolveBinding(identity, "Anchor Example@Moogle");
        var variables = MetadataVariables(binding);

        Assert.Equal("Anchor Example@Moogle", binding.Name);
        Assert.Equal(0UL, binding.GameObjectId);
        Assert.Equal(0U, binding.EntityId);
        Assert.Equal(0UL, binding.ContentId);
        Assert.Equal("anchor-override", binding.Source);
        Assert.True(binding.WasExplicitlySelected);
        AssertMetadata(variables, binding);
    }

    [Fact]
    public void AnchorOverrideOnDifferentWorld_DoesNotReuseFrozenActorIds() {
        var identity = new MirrorRunTargetIdentity(
            "Selected Example@Moogle",
            100,
            200,
            300,
            true,
            "selected-player");

        var binding = MirrorRunTargetValidator.ResolveBinding(
            identity,
            "Selected Example@Omega");

        Assert.Equal(0UL, binding.GameObjectId);
        Assert.Equal(0U, binding.EntityId);
        Assert.Equal(0UL, binding.ContentId);
    }

    [Theory]
    [InlineData("Mirror Script V2", "mirror.v2.stop", "2|X|target|7", 2, 0ul, 42ul, 7, true)]
    [InlineData("Mirror Script V2", "mirror.v2.stop", "2|X|target|7|watch-lost", 2, 0ul, 42ul, 7, true)]
    [InlineData("Mirror Script V2", "mirror.v2.stop", "2|X|target|7|anything", 2, 0ul, 42ul, 7, false)]
    [InlineData("Mirror Target Combat", "mirror.v2.stop", "2|X|target|7", 2, 0ul, 42ul, 7, false)]
    [InlineData("Mirror Script V2", "mirror.v2.stop", "2|X|target|8", 2, 0ul, 42ul, 7, false)]
    [InlineData("Mirror Script V2", "mirror.v2.stop", "2|X|target|7|extra", 2, 0ul, 42ul, 7, false)]
    [InlineData("Mirror Script V2", "mirror.v2.stop", "2|X|target|7", 1, 0ul, 42ul, 7, false)]
    [InlineData("Mirror Script V2", "mirror.v2.stop", "2|X|target|7", 2, 42ul, 42ul, 7, false)]
    [InlineData("Mirror Script V2", "MIRROR.V2.STOP", "2|X|target|7", 2, 0ul, 42ul, 7, false)]
    public void StopTombstone_RequiresExactBroadcastShapeAndRosterSlot(
        string scriptName,
        string topic,
        string payload,
        int schema,
        ulong target,
        ulong sender,
        int slot,
        bool expected) {
        Assert.Equal(expected, IpcProvider.IsValidMirrorStopTombstoneMessage(
            scriptName, topic, payload, schema, target, sender, slot));
    }

    [Fact]
    public void EmoteResyncChatFrame_RequiresExactBoundedShape() {
        var id = Guid.NewGuid();

        Assert.True(IpcProvider.TryParseMirrorEmoteResyncArguments(
            ["08df07ab5b6bbfd0-294d2c8b-87d007c1", "16", "1", "123456789", id.ToString("D")],
            out var runId, out var emoteId, out var persistent, out var targetId, out var messageId));
        Assert.Equal("08df07ab5b6bbfd0-294d2c8b-87d007c1", runId);
        Assert.Equal(16u, emoteId);
        Assert.True(persistent);
        Assert.Equal(123456789ul, targetId);
        Assert.Equal(id, messageId);

        Assert.False(IpcProvider.TryParseMirrorEmoteResyncArguments(
            ["bad run id", "16", "1", "123456789", id.ToString("D")],
            out _, out _, out _, out _, out _));
        Assert.False(IpcProvider.TryParseMirrorEmoteResyncArguments(
            ["08df07ab5b6bbfd0-294d2c8b-87d007c1", "0", "1", "123456789", id.ToString("D")],
            out _, out _, out _, out _, out _));
    }

    private static MirrorRunTargetBinding Binding() => new(
        "Target Example@Moogle",
        100,
        200,
        300,
        "selected-player",
        true);

    private static Dictionary<string, string> MetadataVariables(in MirrorRunTargetBinding binding) {
        var variables = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        MirrorRunTargetValidator.WriteMetadata(variables, binding);
        return variables;
    }

    private static LuaChatSyncEnvelope Envelope(Dictionary<string, string> variables) => new() {
        MessageId = "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee",
        CreatedUnixMilliseconds = 1788194400000,
        StartUtcTicks = 639239000000000000,
        StartServerTimeSeconds = 1788194406,
        Seed = 793223713,
        BundleHash = BundleHash,
        RunTargetName = "Target Example@Moogle",
        RunTargetEntityId = 200,
        Variables = variables,
        ParticipantCids = Enumerable.Range(0, 48)
            .Select(index => 18014498545172021UL + (ulong)index)
            .ToList(),
    };

    private static int FragmentCount(LuaChatSyncEnvelope envelope, string token) {
        var command = $"/cwl2 mopluarun \"{LuaScriptCatalog.MirrorScriptV2Name}\" {token}";
        if (Encoding.UTF8.GetByteCount(command) <= LuaChatSyncFragmentCodec.MaximumChatBytes)
            return 1;
        Assert.True(LuaChatSyncFragmentCodec.TryCreateCommands(
            "/cwl2",
            LuaScriptCatalog.MirrorScriptV2Name,
            envelope.MessageId,
            token,
            out var commands,
            out var error), error);
        return commands.Count;
    }

    private static void AssertMetadata(
        IReadOnlyDictionary<string, string> actual,
        in MirrorRunTargetBinding expected) {
        Assert.Equal(expected.Name, actual[MirrorRunTargetValidator.LaunchNameKey]);
        Assert.Equal(expected.GameObjectId.ToString(), actual[MirrorRunTargetValidator.LaunchGameObjectIdKey]);
        Assert.Equal(expected.EntityId.ToString(), actual[MirrorRunTargetValidator.LaunchEntityIdKey]);
        Assert.Equal(expected.ContentId.ToString(), actual[MirrorRunTargetValidator.LaunchContentIdKey]);
        Assert.Equal(expected.Source, actual[MirrorRunTargetValidator.LaunchSourceKey]);
        Assert.Equal(expected.WasExplicitlySelected ? "true" : "false", actual[MirrorRunTargetValidator.LaunchWasSelectedKey]);
    }
}
