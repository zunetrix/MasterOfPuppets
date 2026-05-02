using System;
using System.Numerics;

using MasterOfPuppets.Movement;

namespace MasterOfPuppets.Formations;

public static class FormationMath {
    public static (Vector3 Position, float Rotation) ToMopWorld(FormationPoint point, Vector3 originPosition, float originRotation) =>
        ToMopWorld(point.Offset, point.Angle, originPosition, originRotation);

    public static (Vector3 Position, float Rotation) ToMopWorld(Vector3 offset, float angleDegrees, Vector3 originPosition, float originRotation) {
        var matrix = Matrix4x4.CreateRotationY(originRotation + MathF.PI);
        var position = Vector3.Transform(offset, matrix) + originPosition;
        var rotation = originRotation - angleDegrees * Angle.DegToRad;
        return (position, rotation);
    }

    public static (Vector3 Offset, float AngleDegrees) ToMopRelative(Vector3 position, float rotation, Vector3 originPosition, float originRotation) {
        var relativePosition = position - originPosition;
        var matrix = Matrix4x4.CreateRotationY(-originRotation - MathF.PI);
        var offset = Vector3.Transform(relativePosition, matrix);
        var angleDegrees = NormalizeDegrees((originRotation - rotation) * Angle.RadToDeg);
        return (offset, angleDegrees);
    }

    public static (Vector3 Position, float Rotation) GetMopRelativeWorld(
        FormationPoint anchorPoint,
        FormationPoint memberPoint,
        Vector3 anchorWorldPosition,
        float anchorWorldRotation) {
        var anchorRotation = -anchorPoint.Angle * Angle.DegToRad;
        Matrix4x4.Invert(
            Matrix4x4.CreateRotationY(anchorRotation) * Matrix4x4.CreateTranslation(anchorPoint.Offset),
            out var inverseAnchor);

        var relativePosition = Vector3.Transform(memberPoint.Offset, inverseAnchor);
        var relativeRotation = (-memberPoint.Angle + anchorPoint.Angle) * Angle.DegToRad;
        return ToAbsolute(relativePosition, relativeRotation, anchorWorldPosition, anchorWorldRotation);
    }

    public static float NormalizeDegrees(float degrees) {
        while (degrees < -180f)
            degrees += 360f;
        while (degrees > 180f)
            degrees -= 360f;
        return degrees;
    }

    private static (Vector3 Position, float Rotation) ToAbsolute(Vector3 relativePosition, float relativeRotation, Vector3 pivotPosition, float pivotRotation) {
        var matrix = Matrix4x4.CreateRotationY(pivotRotation + MathF.PI);
        var position = Vector3.Transform(relativePosition, matrix) + pivotPosition;
        return (position, relativeRotation + pivotRotation);
    }
}
