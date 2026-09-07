using System;
using System.Numerics;

namespace MasterOfPuppets.LuaScripting;

public readonly record struct LuaTrajectorySample(
    Vector3 RelativeOffset,
    float FacingRadians,
    double ElapsedSeconds) {

    // A 16-column row at one-yalm spacing with a three-yalm opposite-row
    // offset reaches roughly 15.3 yalms from its left-most anchor.
    public const float MaximumRadius = 20f;

    public static bool TryCreate(
        double x,
        double z,
        double facingRadians,
        double elapsedSeconds,
        out LuaTrajectorySample sample) {
        sample = default;
        if (!double.IsFinite(x)
            || !double.IsFinite(z)
            || !double.IsFinite(facingRadians)
            || !double.IsFinite(elapsedSeconds))
            return false;

        var offset = new Vector3((float)x, 0f, (float)z);
        var radius = MathF.Sqrt(offset.X * offset.X + offset.Z * offset.Z);
        if (radius > MaximumRadius)
            offset *= MaximumRadius / radius;

        sample = new LuaTrajectorySample(offset, NormalizeRadians((float)facingRadians), elapsedSeconds);
        return true;
    }

    private static float NormalizeRadians(float radians) =>
        MathF.Atan2(MathF.Sin(radians), MathF.Cos(radians));
}
