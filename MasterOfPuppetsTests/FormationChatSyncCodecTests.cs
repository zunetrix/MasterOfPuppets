using System;
using System.Numerics;

using MasterOfPuppets;
using MasterOfPuppets.Formations;

using Xunit;

namespace MasterOfPuppetsTests;

public class FormationChatSyncCodecTests {
    [Fact]
    public void TargetSnapshot_RoundTripsVersionedTransform() {
        var source = new FormationChatAnchorPayload(
            FormationChatSyncCodec.CurrentSchemaVersion,
            Guid.NewGuid().ToString("D"),
            DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            "Circle 3.5 (32)",
            129,
            "AQ==",
            10.25f,
            2.5f,
            -30.75f,
            1.2f,
            "External Target",
            0,
            1234,
            "precise");

        var encoded = FormationChatSyncCodec.Encode(source);

        Assert.True(FormationChatSyncCodec.TryDecode(encoded, out var decoded));
        Assert.Equal(source, decoded);
    }

    [Fact]
    public void TargetSnapshot_RejectsUnsupportedSchema() {
        var source = new FormationChatAnchorPayload(
            FormationChatSyncCodec.CurrentSchemaVersion + 1,
            Guid.NewGuid().ToString("D"),
            DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            "Circle",
            129,
            "AQ==",
            1f,
            2f,
            3f,
            0f,
            "External Target",
            0,
            1234,
            "precise");

        Assert.False(FormationChatSyncCodec.TryDecode(FormationChatSyncCodec.Encode(source), out _));
    }

    [Fact]
    public void TargetSnapshot_RejectsMissingTerritoryScope() {
        var source = new FormationChatAnchorPayload(
            FormationChatSyncCodec.CurrentSchemaVersion,
            Guid.NewGuid().ToString("D"),
            DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            "Circle",
            0,
            "AQ==",
            1f,
            2f,
            3f,
            0f,
            "External Target",
            0,
            1234,
            "precise");

        Assert.False(FormationChatSyncCodec.TryDecode(FormationChatSyncCodec.Encode(source), out _));
    }

    [Fact]
    public void SenderFallbackSnapshot_RoundTripsAssignedAnchorIdentityWithinChatLimit() {
        var source = new FormationChatAnchorPayload(
            FormationChatSyncCodec.CurrentSchemaVersion,
            Guid.NewGuid().ToString("D"),
            DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            "Circle 4.5 (32)",
            129,
            "/////w==",
            123.456f,
            7.89f,
            -456.123f,
            2.75f,
            "Formation Leader@Long World Name",
            18014498545172021UL,
            123456789UL,
            "precise");

        var encoded = FormationChatSyncCodec.Encode(source);

        Assert.True(FormationChatSyncCodec.TryDecode(encoded, out var decoded));
        Assert.Equal(source, decoded);
        Assert.True(System.Text.Encoding.UTF8.GetByteCount($"/cwl2 {FormationChatSyncCodec.CommandName} {encoded}") <= 500);
    }

    [Fact]
    public void FormationSnapshot_IsRecognizedAsHiddenInternalTransport() {
        var source = new FormationChatAnchorPayload(
            FormationChatSyncCodec.CurrentSchemaVersion,
            Guid.NewGuid().ToString("D"),
            DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            "Circle 2.5 (32)",
            250,
            "/wEAAA==",
            134.81335f,
            3.2149503f,
            -13.046509f,
            0.0546968f,
            "External Target",
            0,
            269339510,
            "precise");
        var encoded = FormationChatSyncCodec.Encode(source);

        Assert.True(ChatWatcher.IsInternalLuaSyncEnvelope(
            [FormationChatSyncCodec.CommandName, encoded]));
    }

    [Fact]
    public void EligibleMemberBitmap_UsesStableCidRosterOrder() {
        var formation = new Formation {
            Points = [
                new FormationPoint { Cids = [300UL, 100UL] },
                new FormationPoint { Cids = [200UL] },
            ],
        };

        var encoded = FormationChatSyncCodec.EncodeEligibleMembers(
            formation,
            groups: null,
            eligibleContentIds: [100UL, 300UL]);

        Assert.True(FormationChatSyncCodec.IsEligibleMember(formation, null, 100UL, encoded));
        Assert.False(FormationChatSyncCodec.IsEligibleMember(formation, null, 200UL, encoded));
        Assert.True(FormationChatSyncCodec.IsEligibleMember(formation, null, 300UL, encoded));
    }

    [Fact]
    public void ExternalOrigin_PreservesEverySavedPointOffset() {
        var formation = new Formation {
            Points = [
                new FormationPoint { Offset = new Vector3(0f, 0f, -1.75f) },
                new FormationPoint { Offset = new Vector3(1.25f, 0f, 0.5f), Angle = 45f },
            ],
        };
        var originPosition = new Vector3(100f, 5f, 200f);
        var originRotation = MathF.PI / 2f;
        var adjustedPointOnePosition = FormationPointMovement.AdjustExternalOriginPosition(
            formation,
            FormationPointMovement.AnchorPointIndex,
            originPosition,
            originRotation);

        var actual = FormationPointMovement.BuildAnchoredWorldMove(
            formation,
            1,
            FormationPointMovement.AnchorPointIndex,
            adjustedPointOnePosition,
            originRotation);
        var expected = FormationMath.ToMopWorld(formation.Points[1], originPosition, originRotation);

        Assert.NotNull(actual);
        Assert.Equal(expected.Position.X, actual.Value.Position.X, 5);
        Assert.Equal(expected.Position.Y, actual.Value.Position.Y, 5);
        Assert.Equal(expected.Position.Z, actual.Value.Position.Z, 5);
        Assert.Equal(expected.Rotation, actual.Value.Rotation, 5);
    }
}
