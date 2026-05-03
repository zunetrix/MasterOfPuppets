using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

using MasterOfPuppets.Movement;

namespace MasterOfPuppets.Formations;

public enum FormationShapeType {
    Circle, Rectangle, Line, StaggeredLine, FigureEight, Spiral,
    Polygon, Star, Rose, Heart, Ellipse, Arc, SineWave,
    Zigzag, Grid, SpokedWheel, Hypotrochoid, Lissajous,
    StarPolygon, LogarithmicSpiral, Chevron, RingWithCenter, Cross
}

public enum FormationShapeFaceMode {
    Outward,
    Inward,
    North,
    Tangent,
}

public enum FormationShapeAnchorMode {
    ShapeOnly,
    AnchorAtCenter,
}

public sealed class FormationShapeSpec {
    public FormationShapeType Type { get; set; } = FormationShapeType.Circle;
    public int Count { get; set; } = 8;
    public IReadOnlyList<ulong>? AssignedCids { get; set; }
    public FormationShapeAnchorMode AnchorMode { get; set; } = FormationShapeAnchorMode.ShapeOnly;
    public float Radius { get; set; } = 5f;
    public float Radius2 { get; set; } = 3f;
    public float Width { get; set; } = 8f;
    public float Depth { get; set; } = 4f;
    public float Spacing { get; set; } = 1.5f;
    public float AngleOffsetDegrees { get; set; }
    public int IntParameter { get; set; } = 4;
    public FormationShapeFaceMode FaceMode { get; set; } = FormationShapeFaceMode.Inward;
    public bool AnchorNorthernmostPoint { get; set; } = true;
}

public static class FormationShapeGenerator {
    public static readonly string[] ShapeNames = [
        "Circle", "Rectangle", "Line", "Staggered Line", "Figure 8", "Spiral",
        "Polygon", "Star", "Rose", "Heart", "Ellipse", "Arc", "Sine Wave",
        "Zigzag", "Grid", "Spoked Wheel", "Hypotrochoid", "Lissajous",
        "Star Poly", "Log Spiral", "Chevron", "Ring Center", "Cross"
    ];

    public static readonly string[] FaceModeNames = ["Outward", "Inward", "North", "Tangent"];
    public static readonly string[] AnchorModeNames = ["Shape only", "Anchor at center"];

    public static List<FormationPoint> Generate(FormationShapeSpec spec) {
        var count = Math.Max(1, spec.Count);
        List<FormationPoint> points;

        if (spec.AnchorMode == FormationShapeAnchorMode.AnchorAtCenter) {
            points = [new FormationPoint { Offset = Vector3.Zero }];
            points.AddRange(GenerateShapePoints(spec, Math.Max(0, count - 1)));
        } else {
            points = GenerateShapePoints(spec, count);
        }

        ApplyPostProcessing(points, spec);
        ApplyAssignments(points, spec.AssignedCids);
        return points;
    }

    private static List<FormationPoint> GenerateShapePoints(FormationShapeSpec spec, int count) {
        if (count <= 0)
            return [];

        List<FormationPoint> points = spec.Type switch {
            FormationShapeType.Circle => GenCircle(count, spec.Radius),
            FormationShapeType.Rectangle => GenRectangle(count, spec.Width, spec.Depth),
            FormationShapeType.Line => GenLine(count, spec.Spacing),
            FormationShapeType.StaggeredLine => GenStaggeredLine(count, spec.Spacing, spec.Radius),
            FormationShapeType.FigureEight => GenFigureEight(count, spec.Radius),
            FormationShapeType.Spiral => GenSpiral(count, spec.Radius, spec.Radius2),
            FormationShapeType.Polygon => GenPolygon(count, spec.IntParameter, spec.Radius),
            FormationShapeType.Star => GenStar(count, spec.IntParameter, spec.Radius2, spec.Radius),
            FormationShapeType.Rose => GenRose(count, spec.IntParameter, spec.Radius),
            FormationShapeType.Heart => GenHeart(count, spec.Radius),
            FormationShapeType.Ellipse => GenEllipse(count, spec.Radius, spec.Radius2),
            FormationShapeType.Arc => GenArc(count, spec.Radius, -90, 90),
            FormationShapeType.SineWave => GenSineWave(count, spec.Radius, spec.Radius2, spec.Width),
            FormationShapeType.Zigzag => GenZigzag(count, spec.Spacing, spec.Radius),
            FormationShapeType.Grid => GenGrid(count, spec.IntParameter, spec.Spacing, spec.Width),
            FormationShapeType.SpokedWheel => GenSpokedWheel(count, spec.IntParameter, spec.Radius, spec.Radius2),
            FormationShapeType.Hypotrochoid => GenHypotrochoid(count, spec.Radius, spec.Radius2, spec.Width, spec.Depth),
            FormationShapeType.Lissajous => GenLissajous(count, 3, 2, MathF.PI / 2, spec.Radius),
            FormationShapeType.StarPolygon => GenStarPolygon(count, spec.IntParameter, 2, spec.Radius),
            FormationShapeType.LogarithmicSpiral => GenLogarithmicSpiral(count, 0.4f, 0.15f, 3.0f),
            FormationShapeType.Chevron => GenChevron(count, 40, spec.Spacing),
            FormationShapeType.RingWithCenter => GenRingWithCenter(count, spec.Radius, spec.IntParameter),
            FormationShapeType.Cross => GenCross(count, spec.Width, spec.Spacing),
            _ => [],
        };
        return points;
    }

    private static void ApplyAssignments(List<FormationPoint> points, IReadOnlyList<ulong>? assignedCids) {
        if (assignedCids == null)
            return;

        for (var i = 0; i < points.Count && i < assignedCids.Count; i++) {
            points[i].Cids = assignedCids[i] == 0 ? [] : [assignedCids[i]];
            points[i].GroupIds = [];
        }
    }

    private static void ApplyPostProcessing(List<FormationPoint> points, FormationShapeSpec spec) {
        float angleOffRad = spec.AngleOffsetDegrees * Angle.DegToRad;
        if (angleOffRad != 0) {
            foreach (var p in points)
                p.Offset = FormationMath.RotateOffset(p.Offset, angleOffRad);
        }

        if (spec.AnchorNorthernmostPoint && spec.AnchorMode != FormationShapeAnchorMode.AnchorAtCenter && points.Count > 0) {
            float minZ = points.Min(p => p.Offset.Z);
            foreach (var p in points)
                p.Offset.Z -= minZ;
        }

        if (points.Count == 0)
            return;

        float centerX = spec.AnchorMode == FormationShapeAnchorMode.AnchorAtCenter ? 0f : points.Average(p => p.Offset.X);
        float centerZ = spec.AnchorMode == FormationShapeAnchorMode.AnchorAtCenter ? 0f : points.Average(p => p.Offset.Z);

        if (spec.FaceMode != FormationShapeFaceMode.Tangent) {
            foreach (var p in points) {
                float dx = p.Offset.X - centerX;
                float dz = p.Offset.Z - centerZ;
                float outward = MathF.Abs(dx) < 0.001f && MathF.Abs(dz) < 0.001f
                    ? 0f
                    : StoredDeltaToPlotAngleDegrees(dx, dz);
                p.Angle = spec.FaceMode switch {
                    FormationShapeFaceMode.Outward => outward,
                    FormationShapeFaceMode.Inward => outward + 180f,
                    FormationShapeFaceMode.North => 0f,
                    _ => 0f,
                };
                p.Angle = FormationMath.NormalizeDegrees(p.Angle);
            }
            return;
        }

        if (points.Count <= 1)
            return;

        for (int i = 0; i < points.Count; i++) {
            if (spec.AnchorMode == FormationShapeAnchorMode.AnchorAtCenter && i == 0) {
                points[i].Angle = 0f;
                continue;
            }

            Vector3 a = i < points.Count - 1 ? points[i].Offset : points[i - 1].Offset;
            Vector3 b = i < points.Count - 1 ? points[i + 1].Offset : points[i].Offset;
            float dx = b.X - a.X;
            float dz = b.Z - a.Z;
            if (MathF.Abs(dx) > 0.001f || MathF.Abs(dz) > 0.001f)
                points[i].Angle = StoredDeltaToPlotAngleDegrees(dx, dz);
        }
    }

    private static float StoredDeltaToPlotAngleDegrees(float dx, float dz) =>
        FormationMath.NormalizeDegrees(MathF.Atan2(dx, dz) * Angle.RadToDeg);

    private static List<FormationPoint> GenCircle(int count, float radius) {
        var pts = new List<FormationPoint>();
        for (int i = 0; i < count; i++) {
            float a = (2 * MathF.PI * i) / count - MathF.PI / 2;
            pts.Add(new FormationPoint { Offset = new Vector3(radius * MathF.Cos(a), 0, radius * MathF.Sin(a)) });
        }
        return pts;
    }

    private static List<FormationPoint> GenRectangle(int count, float width, float depth) {
        var pts = new List<FormationPoint>();
        if (count < 4) return pts;
        float perimeter = 2 * (width + depth);
        float spacing = perimeter / count;
        Vector3 current = new(-width / 2, 0, -depth / 2);
        pts.Add(new FormationPoint { Offset = current });

        (float dx, float dz)[] dirs = [(1, 0), (0, 1), (-1, 0), (0, -1)];
        float[] lens = [width, depth, width, depth];

        int dirIdx = 0;
        float distOnEdge = 0;
        while (pts.Count < count) {
            float remain = spacing;
            while (remain > 0.001f) {
                float canGo = lens[dirIdx] - distOnEdge;
                float advance = MathF.Min(remain, canGo);
                current.X += dirs[dirIdx].dx * advance;
                current.Z += dirs[dirIdx].dz * advance;
                distOnEdge += advance;
                remain -= advance;
                if (distOnEdge >= lens[dirIdx] - 0.001f) {
                    dirIdx = (dirIdx + 1) % 4;
                    distOnEdge = 0;
                }
            }
            pts.Add(new FormationPoint { Offset = current });
        }
        return pts;
    }

    private static List<FormationPoint> GenLine(int count, float spacing) {
        var pts = new List<FormationPoint>();
        float startX = -spacing * (count - 1) / 2;
        for (int i = 0; i < count; i++)
            pts.Add(new FormationPoint { Offset = new Vector3(startX + i * spacing, 0, 0) });
        return pts;
    }

    private static List<FormationPoint> GenStaggeredLine(int count, float spacing, float depthOff) {
        var pts = new List<FormationPoint>();
        float startX = -spacing * (count - 1) / 2;
        for (int i = 0; i < count; i++) {
            float z = i % 2 == 0 ? depthOff : -depthOff;
            pts.Add(new FormationPoint { Offset = new Vector3(startX + i * spacing, 0, z) });
        }
        return pts;
    }

    private static List<FormationPoint> GenFigureEight(int count, float radius) {
        var pts = new List<FormationPoint>();
        for (int i = 0; i < count; i++) {
            float t = (2 * MathF.PI * i) / count;
            float denom = 1 + MathF.Pow(MathF.Sin(t), 2);
            float x = (radius * MathF.Sin(t)) / denom;
            float z = (radius * MathF.Sin(t) * MathF.Cos(t)) / denom;
            pts.Add(new FormationPoint { Offset = new Vector3(x, 0, z) });
        }
        return pts;
    }

    private static List<FormationPoint> GenSpiral(int count, float radialStep, float rotations) {
        var pts = new List<FormationPoint>();
        if (count < 2) return pts;
        for (int i = 0; i < count; i++) {
            float prog = (float)i / (count - 1);
            float angle = 2 * MathF.PI * rotations * prog;
            float r = radialStep * (i + 1);
            pts.Add(new FormationPoint { Offset = new Vector3(r * MathF.Cos(angle), 0, r * MathF.Sin(angle)) });
        }
        return pts;
    }

    private static List<FormationPoint> GenPolygon(int count, int sides, float radius) {
        var pts = new List<FormationPoint>();
        if (sides < 3) return pts;
        Vector3[] verts = new Vector3[sides];
        for (int i = 0; i < sides; i++) {
            float a = (2 * MathF.PI * i) / sides - MathF.PI / 2;
            verts[i] = new Vector3(radius * MathF.Cos(a), 0, radius * MathF.Sin(a));
        }
        for (int i = 0; i < count; i++) {
            float prog = ((float)i / count) * sides;
            int idx = (int)MathF.Floor(prog) % sides;
            float local = prog - MathF.Floor(prog);
            pts.Add(new FormationPoint { Offset = Vector3.Lerp(verts[idx], verts[(idx + 1) % sides], local) });
        }
        return pts;
    }

    private static List<FormationPoint> GenStar(int count, int points, float innerR, float outerR) {
        var pts = new List<FormationPoint>();
        int total = points * 2;
        Vector3[] verts = new Vector3[total];
        for (int i = 0; i < points; i++) {
            float a = (2 * MathF.PI * i) / points - MathF.PI / 2;
            verts[i * 2] = new Vector3(outerR * MathF.Cos(a), 0, outerR * MathF.Sin(a));
            float ia = a + (MathF.PI / points);
            verts[i * 2 + 1] = new Vector3(innerR * MathF.Cos(ia), 0, innerR * MathF.Sin(ia));
        }
        for (int i = 0; i < count; i++) {
            float prog = ((float)i / count) * total;
            int idx = (int)MathF.Floor(prog) % total;
            float local = prog - MathF.Floor(prog);
            pts.Add(new FormationPoint { Offset = Vector3.Lerp(verts[idx], verts[(idx + 1) % total], local) });
        }
        return pts;
    }

    private static List<FormationPoint> GenRose(int count, int petals, float radius) {
        var pts = new List<FormationPoint>();
        for (int i = 0; i < count; i++) {
            float t = (2 * MathF.PI * i) / count;
            float r = radius * MathF.Cos(petals * t);
            pts.Add(new FormationPoint { Offset = new Vector3(r * MathF.Cos(t), 0, r * MathF.Sin(t)) });
        }
        return pts;
    }

    private static List<FormationPoint> GenHeart(int count, float scale) {
        var pts = new List<FormationPoint>();
        for (int i = 0; i < count; i++) {
            float t = (2 * MathF.PI * i) / count;
            float x = (scale / 16f) * (16 * MathF.Pow(MathF.Sin(t), 3));
            float z = (scale / 16f) * (13 * MathF.Cos(t) - 5 * MathF.Cos(2 * t) - 2 * MathF.Cos(3 * t) - MathF.Cos(4 * t));
            pts.Add(new FormationPoint { Offset = new Vector3(x, 0, -z) });
        }
        return pts;
    }

    private static List<FormationPoint> GenEllipse(int count, float rx, float rz) {
        var pts = new List<FormationPoint>();
        for (int i = 0; i < count; i++) {
            float a = (2 * MathF.PI * i) / count - MathF.PI / 2;
            pts.Add(new FormationPoint { Offset = new Vector3(rx * MathF.Cos(a), 0, rz * MathF.Sin(a)) });
        }
        return pts;
    }

    private static List<FormationPoint> GenArc(int count, float radius, float startDeg, float endDeg) {
        var pts = new List<FormationPoint>();
        if (count < 2) return pts;
        float start = startDeg * Angle.DegToRad;
        float end = endDeg * Angle.DegToRad;
        for (int i = 0; i < count; i++) {
            float t = start + (end - start) * ((float)i / (count - 1));
            pts.Add(new FormationPoint { Offset = new Vector3(radius * MathF.Cos(t), 0, radius * MathF.Sin(t)) });
        }
        return pts;
    }

    private static List<FormationPoint> GenSineWave(int count, float amp, float wave, float len) {
        var pts = new List<FormationPoint>();
        if (count < 2) return pts;
        float startX = -len / 2;
        float step = len / (count - 1);
        for (int i = 0; i < count; i++) {
            float x = startX + i * step;
            float z = amp * MathF.Sin((2 * MathF.PI * x) / wave);
            pts.Add(new FormationPoint { Offset = new Vector3(x, 0, z) });
        }
        return pts;
    }

    private static List<FormationPoint> GenZigzag(int count, float step, float amp) {
        var pts = new List<FormationPoint>();
        for (int i = 0; i < count; i++) {
            float x = step * i - step * (count - 1) / 2;
            float z = i % 2 == 1 ? amp : -amp;
            pts.Add(new FormationPoint { Offset = new Vector3(x, 0, z) });
        }
        return pts;
    }

    private static List<FormationPoint> GenGrid(int count, int cols, float spX, float spZ) {
        var pts = new List<FormationPoint>();
        if (cols < 1) cols = 1;
        int rows = (int)MathF.Ceiling((float)count / cols);
        float startX = -(cols - 1) * spX / 2;
        float startZ = -(rows - 1) * spZ / 2;
        for (int r = 0; r < rows; r++) {
            for (int c = 0; c < cols; c++) {
                if (pts.Count >= count) break;
                pts.Add(new FormationPoint { Offset = new Vector3(startX + c * spX, 0, startZ + r * spZ) });
            }
        }
        return pts;
    }

    private static List<FormationPoint> GenSpokedWheel(int count, int spokes, float radius, float innerR) {
        var pts = new List<FormationPoint>();
        if (spokes < 1) spokes = 1;
        int layers = (int)MathF.Ceiling((float)count / spokes);
        for (int l = 0; l < layers; l++) {
            float r = innerR + (radius - innerR) * ((float)(l + 1) / layers);
            for (int s = 0; s < spokes; s++) {
                if (pts.Count >= count) break;
                float a = (2 * MathF.PI * s) / spokes - MathF.PI / 2;
                pts.Add(new FormationPoint { Offset = new Vector3(r * MathF.Cos(a), 0, r * MathF.Sin(a)) });
            }
        }
        return pts;
    }

    private static List<FormationPoint> GenHypotrochoid(int count, float R, float r, float d, float rotations) {
        var pts = new List<FormationPoint>();
        float maxT = 2 * MathF.PI * rotations;
        for (int i = 0; i < count; i++) {
            float t = (maxT * i) / count;
            float x = (R - r) * MathF.Cos(t) + d * MathF.Cos(((R - r) / r) * t);
            float z = (R - r) * MathF.Sin(t) - d * MathF.Sin(((R - r) / r) * t);
            pts.Add(new FormationPoint { Offset = new Vector3(x, 0, z) });
        }
        return pts;
    }

    private static List<FormationPoint> GenLissajous(int count, float ax, float ay, float delta, float radius) {
        var pts = new List<FormationPoint>();
        for (int i = 0; i < count; i++) {
            float t = (2 * MathF.PI * i) / count;
            float x = radius * MathF.Sin(ax * t + delta);
            float z = radius * MathF.Sin(ay * t);
            pts.Add(new FormationPoint { Offset = new Vector3(x, 0, z) });
        }
        return pts;
    }

    private static List<FormationPoint> GenStarPolygon(int count, int sides, int step, float radius) {
        var pts = new List<FormationPoint>();
        if (sides < 3) return pts;
        step = Math.Max(1, Math.Min(step, sides - 1));
        Vector3[] verts = new Vector3[sides];
        for (int i = 0; i < sides; i++) {
            float a = (2 * MathF.PI * i) / sides - MathF.PI / 2;
            verts[i] = new Vector3(radius * MathF.Cos(a), 0, radius * MathF.Sin(a));
        }
        var path = new List<int> { 0 };
        while (true) {
            int next = (path[^1] + step) % sides;
            if (next == path[0] || path.Contains(next)) break;
            path.Add(next);
        }
        if (path.Count < 2)
            for (int i = 1; i < sides; i++)
                path.Add(i);

        int totalEdges = path.Count;
        for (int i = 0; i < count; i++) {
            float prog = ((float)i / count) * totalEdges;
            int idx = (int)MathF.Floor(prog) % totalEdges;
            float local = prog - MathF.Floor(prog);
            pts.Add(new FormationPoint { Offset = Vector3.Lerp(verts[path[idx]], verts[path[(idx + 1) % totalEdges]], local) });
        }
        return pts;
    }

    private static List<FormationPoint> GenLogarithmicSpiral(int count, float a, float b, float rotations) {
        var pts = new List<FormationPoint>();
        if (count < 2) return pts;
        float maxTheta = 2 * MathF.PI * rotations;
        for (int i = 0; i < count; i++) {
            float theta = maxTheta * i / (count - 1);
            float r = a * MathF.Exp(b * theta);
            pts.Add(new FormationPoint { Offset = new Vector3(r * MathF.Cos(theta), 0, r * MathF.Sin(theta)) });
        }
        return pts;
    }

    private static List<FormationPoint> GenChevron(int count, float deg, float spacing) {
        var pts = new List<FormationPoint> { new() { Offset = Vector3.Zero } };
        float rad = deg * Angle.DegToRad;
        for (int i = 1; i < count; i++) {
            int s = (i + 1) / 2;
            float dir = i % 2 == 1 ? -1 : 1;
            float x = dir * MathF.Sin(rad) * s * spacing;
            float z = -MathF.Cos(rad) * s * spacing;
            pts.Add(new FormationPoint { Offset = new Vector3(x, 0, z) });
        }
        return pts;
    }

    private static List<FormationPoint> GenRingWithCenter(int count, float radius, int centerCount) {
        var pts = new List<FormationPoint>();
        centerCount = Math.Min(centerCount, count);
        for (int i = 0; i < centerCount; i++)
            pts.Add(new FormationPoint { Offset = Vector3.Zero });
        int remain = count - centerCount;
        if (remain > 0)
            pts.AddRange(GenCircle(remain, radius));
        return pts;
    }

    private static List<FormationPoint> GenCross(int count, float armLen, float spacing) {
        var pts = new List<FormationPoint> { new() { Offset = Vector3.Zero } };
        int armPts = Math.Max(1, (int)(armLen / spacing));
        (int dx, int dz)[] dirs = [(1, 0), (-1, 0), (0, 1), (0, -1)];
        foreach (var d in dirs) {
            for (int s = 1; s <= armPts; s++) {
                if (pts.Count >= count) break;
                pts.Add(new FormationPoint { Offset = new Vector3(d.dx * s * spacing, 0, d.dz * s * spacing) });
            }
            if (pts.Count >= count) break;
        }
        return pts;
    }
}
