using System;
using System.Numerics;

using MasterOfPuppets.Extensions;
using MasterOfPuppets.Movement;

namespace MasterOfPuppets.Formations;

public static class FormationMath {
    public static (Vector3 Position, float Rotation) ToMopWorld(FormationPoint point, Vector3 originPosition, float originRotation) =>
        ToMopWorld(point.Offset, point.Angle, originPosition, originRotation);

    public static (Vector3 Position, float Rotation) ToMopWorld(Vector3 offset, float angleDegrees, Vector3 originPosition, float originRotation) {
        var position = offset.ApplyLeaderRotation(originRotation, originPosition);
        var rotation = originRotation + angleDegrees * Angle.DegToRad;
        return (position, rotation);
    }

    public static (Vector3 Offset, float AngleDegrees) ToMopRelative(Vector3 position, float rotation, Vector3 originPosition, float originRotation) {
        var relativePosition = position - originPosition;
        var offset = RotateOffset(relativePosition, -originRotation);
        var angleDegrees = NormalizeDegrees((rotation - originRotation) * Angle.RadToDeg);
        return (offset, angleDegrees);
    }

    public static (Vector3 Position, float Rotation) GetMopRelativeWorld(
        FormationPoint anchorPoint,
        FormationPoint memberPoint,
        Vector3 anchorWorldPosition,
        float anchorWorldRotation) {
        var memberRelativeToAnchor = memberPoint.Offset - anchorPoint.Offset;
        var memberRotationRelativeToAnchor = NormalizeDegrees(memberPoint.Angle - anchorPoint.Angle);
        var worldPosition = memberRelativeToAnchor.ApplyLeaderRotation(anchorWorldRotation, anchorWorldPosition);
        var worldRotation = anchorWorldRotation + memberRotationRelativeToAnchor * Angle.DegToRad;

        return (worldPosition, worldRotation);
    }

    public static float NormalizeDegrees(float degrees) {
        while (degrees < -180f)
            degrees += 360f;
        while (degrees > 180f)
            degrees -= 360f;
        return degrees;
    }

    public static float DirectionToDegrees(float x, float z) =>
        NormalizeDegrees(MathF.Atan2(x, -z) * Angle.RadToDeg);

    public static Vector3 RotateOffset(Vector3 offset, float rotationRadians) {
        float cos = MathF.Cos(rotationRadians);
        float sin = MathF.Sin(rotationRadians);

        return new Vector3(
            offset.X * cos + offset.Z * sin,
            offset.Y,
            -offset.X * sin + offset.Z * cos);
    }
}
