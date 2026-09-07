using System.Numerics;

using MasterOfPuppets.LuaScripting.Choreography;

using Xunit;

public sealed class LuaTrajectoryFollowerTests {
    [Fact]
    public void ThirtyTwoActorRing_ConvergesAroundMovingAnchor_WithoutReversalOrCenterCrossing() {
        const int count = 32;
        const float radius = 3.1f;
        const float pace = 0.9f;
        const float delta = 0.02f;
        const float duration = 75f;
        var followers = Enumerable.Range(0, count).Select(_ => new LuaTrajectoryFollower()).ToArray();
        var positions = new Vector3[count];
        var facings = new float[count];
        var previousSpeeds = new float[count];
        var maximumAcceleration = 0f;
        var minimumRadius = float.MaxValue;

        for (var slot = 0; slot < count; slot++) {
            var phase = Tau * slot / count + (slot % 2 == 0 ? 0.24f : -0.18f);
            var displacedRadius = radius + (slot % 3 - 1) * 0.65f;
            positions[slot] = Anchor(0f) + Unit(phase) * displacedRadius;
            facings[slot] = phase + MathF.PI / 2f;
        }

        for (var time = 0f; time < duration; time += delta) {
            var anchor = Anchor(time);
            var anchorVelocity = AnchorVelocity(time);
            for (var slot = 0; slot < count; slot++) {
                var desiredPhase = Tau * slot / count + pace / radius * time;
                var desired = anchor + Unit(desiredPhase) * radius;
                var output = followers[slot].Step(new LuaTrajectoryFollowerInput(
                    anchor,
                    anchorVelocity,
                    positions[slot],
                    facings[slot],
                    desired,
                    desiredPhase,
                    radius,
                    pace,
                    Direction: 1,
                    DeltaSeconds: delta));

                Assert.True(output.IsFinite);
                Assert.InRange(output.RequestedSpeed, 0f, 6.4f);
                maximumAcceleration = MathF.Max(
                    maximumAcceleration,
                    MathF.Abs(output.RequestedSpeed - previousSpeeds[slot]) / delta);
                previousSpeeds[slot] = output.RequestedSpeed;
                var radialOut = Vector3.Normalize(new Vector3(
                    positions[slot].X - anchor.X,
                    0f,
                    positions[slot].Z - anchor.Z));
                var forwardTangent = new Vector3(radialOut.Z, 0f, -radialOut.X);
                var relativeVelocity = output.Velocity - anchorVelocity;
                Assert.True(Vector3.Dot(relativeVelocity, forwardTangent) >= -0.0001f, "Follower requested reverse travel.");

                facings[slot] = output.RequestedFacing;
                positions[slot] += output.Velocity * delta;
                minimumRadius = MathF.Min(minimumRadius, HorizontalDistance(positions[slot], anchor));
            }
        }

        Assert.InRange(maximumAcceleration, 0f, 2.5002f);
        Assert.True(minimumRadius >= radius * 0.65f - 0.02f, $"minimum radius: {minimumRadius}");
        var finalAnchor = Anchor(duration);
        for (var slot = 0; slot < count; slot++) {
            var desiredPhase = Tau * slot / count + pace / radius * duration;
            var relative = positions[slot] - finalAnchor;
            var actualPhase = MathF.Atan2(relative.X, relative.Z);
            var phaseError = Wrap(desiredPhase - actualPhase);
            var radialError = radius - HorizontalDistance(positions[slot], finalAnchor);
            Assert.InRange(MathF.Abs(phaseError), 0f, 0.035f);
            Assert.InRange(MathF.Abs(radialError), 0f, 0.035f);
        }
    }

    [Fact]
    public void ActorAheadOfSlot_SlowsOrHolds_ButNeverReverses() {
        var follower = new LuaTrajectoryFollower();
        var desiredPhase = 0f;
        var actorPhase = 1f;

        var output = follower.Step(new LuaTrajectoryFollowerInput(
            Vector3.Zero,
            Vector3.Zero,
            Unit(actorPhase) * 3f,
            actorPhase + MathF.PI / 2f,
            Unit(desiredPhase) * 3f,
            desiredPhase,
            3f,
            0.9f,
            Direction: 1,
            DeltaSeconds: 0.02f));

        var actorRadial = Unit(actorPhase);
        var forwardTangent = new Vector3(actorRadial.Z, 0f, -actorRadial.X);
        Assert.True(Vector3.Dot(output.Velocity, forwardTangent) >= 0f);
        Assert.Equal(LuaTrajectoryLocomotion.Hold, output.Locomotion);
    }

    private const float Tau = MathF.PI * 2f;
    private static Vector3 Unit(float phase) => new(MathF.Sin(phase), 0f, MathF.Cos(phase));
    private static Vector3 Anchor(float time) => new(1.2f * MathF.Sin(time * 0.11f), 0f, 0.8f * MathF.Cos(time * 0.07f));
    private static Vector3 AnchorVelocity(float time) => new(1.2f * 0.11f * MathF.Cos(time * 0.11f), 0f, -0.8f * 0.07f * MathF.Sin(time * 0.07f));
    private static float HorizontalDistance(Vector3 left, Vector3 right) =>
        new Vector2(left.X - right.X, left.Z - right.Z).Length();
    private static float Wrap(float value) => MathF.Atan2(MathF.Sin(value), MathF.Cos(value));
}
