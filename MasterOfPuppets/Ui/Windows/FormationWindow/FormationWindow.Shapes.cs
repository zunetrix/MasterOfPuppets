using System;
using System.Collections.Generic;
using System.Numerics;

using MasterOfPuppets.Formations;
using MasterOfPuppets.Movement;

namespace MasterOfPuppets;

public partial class FormationWindow {
    private void GenerateShape(Formation formation) {
        if (!_appendMode) formation.Points.Clear();

        List<FormationPoint> newPoints = _shapeType switch {
            ShapeType.Circle => GenCircle(_shapeN, _shapeRadius),
            ShapeType.Rectangle => GenRectangle(_shapeN, _shapeWidth, _shapeDepth),
            ShapeType.Line => GenLine(_shapeN, _shapeSpacing),
            ShapeType.StaggeredLine => GenStaggeredLine(_shapeN, _shapeSpacing, _shapeRadius),
            ShapeType.FigureEight => GenFigureEight(_shapeN, _shapeRadius),
            ShapeType.Spiral => GenSpiral(_shapeN, _shapeRadius, _shapeRadius2),
            ShapeType.Polygon => GenPolygon(_shapeN, _shapeParamInt, _shapeRadius),
            ShapeType.Star => GenStar(_shapeN, _shapeParamInt, _shapeRadius2, _shapeRadius),
            ShapeType.Rose => GenRose(_shapeN, _shapeParamInt, _shapeRadius),
            ShapeType.Heart => GenHeart(_shapeN, _shapeRadius),
            ShapeType.Ellipse => GenEllipse(_shapeN, _shapeRadius, _shapeRadius2),
            ShapeType.Arc => GenArc(_shapeN, _shapeRadius, -90, 90),
            ShapeType.SineWave => GenSineWave(_shapeN, _shapeRadius, _shapeRadius2, _shapeWidth),
            ShapeType.Zigzag => GenZigzag(_shapeN, _shapeSpacing, _shapeRadius),
            ShapeType.Grid => GenGrid(_shapeN, _shapeParamInt, _shapeSpacing, _shapeWidth),
            ShapeType.SpokedWheel => GenSpokedWheel(_shapeN, _shapeParamInt, _shapeRadius, _shapeRadius2),
            ShapeType.Hypotrochoid => GenHypotrochoid(_shapeN, _shapeRadius, _shapeRadius2, _shapeWidth, _shapeDepth),
            ShapeType.Lissajous => GenLissajous(_shapeN, 3, 2, MathF.PI / 2, _shapeRadius),
            ShapeType.StarPolygon => GenStarPolygon(_shapeN, _shapeParamInt, 2, _shapeRadius),
            ShapeType.LogarithmicSpiral => GenLogarithmicSpiral(_shapeN, 0.4f, 0.15f, 3.0f),
            ShapeType.Chevron => GenChevron(_shapeN, 40, _shapeSpacing),
            ShapeType.RingWithCenter => GenRingWithCenter(_shapeN, _shapeRadius, _shapeParamInt),
            ShapeType.Cross => GenCross(_shapeN, _shapeWidth, _shapeSpacing),
            _ => new List<FormationPoint>()
        };

        float baseR = _shapeAngleOff * Angle.DegToRad;
        foreach (var p in newPoints) {
            // Apply global rotation offset
            if (baseR != 0) {
                float cos = MathF.Cos(baseR);
                float sin = MathF.Sin(baseR);
                float nx = p.Offset.X * cos - p.Offset.Z * sin;
                float nz = p.Offset.X * sin + p.Offset.Z * cos;
                p.Offset = new Vector3(nx, 0, nz);
                p.Angle += _shapeAngleOff;
            }

            // Apply facing mode if not Tangent (which is 3)
            if (_faceMode != 3) {
                float a = MathF.Atan2(p.Offset.X, p.Offset.Z);
                p.Angle = _faceMode switch {
                    1 => (360f - a * Angle.RadToDeg) % 360f, // Inward
                    2 => 0f,                                  // North
                    _ => (180f - a * Angle.RadToDeg) % 360f, // Outward
                };
            }
        }

        // Apply tangent facing if requested
        if (_faceMode == 3 && newPoints.Count > 1) {
            for (int i = 0; i < newPoints.Count; i++) {
                Vector3 a, b;
                if (i < newPoints.Count - 1) {
                    a = newPoints[i].Offset;
                    b = newPoints[i + 1].Offset;
                } else {
                    a = newPoints[i - 1].Offset;
                    b = newPoints[i].Offset;
                }
                float dx = b.X - a.X;
                float dz = b.Z - a.Z;
                if (MathF.Abs(dx) > 0.001f || MathF.Abs(dz) > 0.001f) {
                    // Bearing CW from south (+Z). Atan2(x, z) gives radians CW from south.
                    // Converting to our convention (0=north, CW+)
                    float bearing = MathF.Atan2(dx, dz) * Angle.RadToDeg;
                    newPoints[i].Angle = (180f - bearing) % 360f;
                }
            }
        }

        foreach (var p in newPoints) {
            formation.Points.Add(p);
        }

        _selPoint = -1;
        Plugin.Config.Save();
        Plugin.IpcProvider.SyncConfiguration();
    }

    private List<FormationPoint> GenCircle(int count, float radius) {
        var pts = new List<FormationPoint>();
        for (int i = 0; i < count; i++) {
            float a = (2 * MathF.PI * i) / count - MathF.PI / 2;
            pts.Add(new FormationPoint {
                Offset = new Vector3(radius * MathF.Cos(a), 0, radius * MathF.Sin(a))
            });
        }
        return pts;
    }

    private List<FormationPoint> GenRectangle(int count, float width, float depth) {
        var pts = new List<FormationPoint>();
        if (count < 4) return pts;
        float perimeter = 2 * (width + depth);
        float spacing = perimeter / count;
        Vector3 current = new Vector3(-width / 2, 0, -depth / 2);
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

    private List<FormationPoint> GenLine(int count, float spacing) {
        var pts = new List<FormationPoint>();
        float startX = -spacing * (count - 1) / 2;
        for (int i = 0; i < count; i++) {
            pts.Add(new FormationPoint { Offset = new Vector3(startX + i * spacing, 0, 0) });
        }
        return pts;
    }

    private List<FormationPoint> GenStaggeredLine(int count, float spacing, float depthOff) {
        var pts = new List<FormationPoint>();
        float startX = -spacing * (count - 1) / 2;
        for (int i = 0; i < count; i++) {
            float z = (i % 2 == 0) ? depthOff : -depthOff;
            pts.Add(new FormationPoint { Offset = new Vector3(startX + i * spacing, 0, z) });
        }
        return pts;
    }

    private List<FormationPoint> GenFigureEight(int count, float radius) {
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

    private List<FormationPoint> GenSpiral(int count, float radialStep, float rotations) {
        var pts = new List<FormationPoint>();
        for (int i = 0; i < count; i++) {
            float prog = (float)i / (count - 1);
            float angle = 2 * MathF.PI * rotations * prog;
            float r = radialStep * (i + 1);
            pts.Add(new FormationPoint { Offset = new Vector3(r * MathF.Cos(angle), 0, r * MathF.Sin(angle)) });
        }
        return pts;
    }

    private List<FormationPoint> GenPolygon(int count, int sides, float radius) {
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
            Vector3 start = verts[idx];
            Vector3 end = verts[(idx + 1) % sides];
            pts.Add(new FormationPoint { Offset = Vector3.Lerp(start, end, local) });
        }
        return pts;
    }

    private List<FormationPoint> GenStar(int count, int points, float innerR, float outerR) {
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

    private List<FormationPoint> GenRose(int count, int petals, float radius) {
        var pts = new List<FormationPoint>();
        for (int i = 0; i < count; i++) {
            float t = (2 * MathF.PI * i) / count;
            float r = radius * MathF.Cos(petals * t);
            pts.Add(new FormationPoint { Offset = new Vector3(r * MathF.Cos(t), 0, r * MathF.Sin(t)) });
        }
        return pts;
    }

    private List<FormationPoint> GenHeart(int count, float scale) {
        var pts = new List<FormationPoint>();
        for (int i = 0; i < count; i++) {
            float t = (2 * MathF.PI * i) / count;
            float x = (scale / 16f) * (16 * MathF.Pow(MathF.Sin(t), 3));
            float z = (scale / 16f) * (13 * MathF.Cos(t) - 5 * MathF.Cos(2 * t) - 2 * MathF.Cos(3 * t) - MathF.Cos(4 * t));
            pts.Add(new FormationPoint { Offset = new Vector3(x, 0, -z) }); // Invert Z for upright heart
        }
        return pts;
    }

    private List<FormationPoint> GenEllipse(int count, float rx, float rz) {
        var pts = new List<FormationPoint>();
        for (int i = 0; i < count; i++) {
            float a = (2 * MathF.PI * i) / count - MathF.PI / 2;
            pts.Add(new FormationPoint { Offset = new Vector3(rx * MathF.Cos(a), 0, rz * MathF.Sin(a)) });
        }
        return pts;
    }

    private List<FormationPoint> GenArc(int count, float radius, float startDeg, float endDeg) {
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

    private List<FormationPoint> GenSineWave(int count, float amp, float wave, float len) {
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

    private List<FormationPoint> GenZigzag(int count, float step, float amp) {
        var pts = new List<FormationPoint>();
        for (int i = 0; i < count; i++) {
            float x = step * i - (step * (count - 1) / 2);
            float z = (i % 2 == 1) ? amp : -amp;
            pts.Add(new FormationPoint { Offset = new Vector3(x, 0, z) });
        }
        return pts;
    }

    private List<FormationPoint> GenGrid(int count, int cols, float spX, float spZ) {
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

    private List<FormationPoint> GenSpokedWheel(int count, int spokes, float radius, float innerR) {
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

    private List<FormationPoint> GenHypotrochoid(int count, float R, float r, float d, float rotations) {
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

    private List<FormationPoint> GenLissajous(int count, float ax, float ay, float delta, float radius) {
        var pts = new List<FormationPoint>();
        for (int i = 0; i < count; i++) {
            float t = (2 * MathF.PI * i) / count;
            float x = radius * MathF.Sin(ax * t + delta);
            float z = radius * MathF.Sin(ay * t);
            pts.Add(new FormationPoint { Offset = new Vector3(x, 0, z) });
        }
        return pts;
    }

    private List<FormationPoint> GenStarPolygon(int count, int sides, int step, float radius) {
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
            int next = (path[path.Count - 1] + step) % sides;
            if (next == path[0] || path.Contains(next)) break;
            path.Add(next);
        }
        if (path.Count < 2) for (int i = 1; i < sides; i++) path.Add(i);

        int totalEdges = path.Count;
        for (int i = 0; i < count; i++) {
            float prog = ((float)i / count) * totalEdges;
            int idx = (int)MathF.Floor(prog) % totalEdges;
            float local = prog - MathF.Floor(prog);
            pts.Add(new FormationPoint { Offset = Vector3.Lerp(verts[path[idx]], verts[path[(idx + 1) % totalEdges]], local) });
        }
        return pts;
    }

    private List<FormationPoint> GenLogarithmicSpiral(int count, float a, float b, float rotations) {
        var pts = new List<FormationPoint>();
        if (count < 2) return pts;
        float maxTheta = 2 * MathF.PI * rotations;
        for (int i = 0; i < count; i++) {
            float theta = (maxTheta * i) / (count - 1);
            float r = a * MathF.Exp(b * theta);
            pts.Add(new FormationPoint { Offset = new Vector3(r * MathF.Cos(theta), 0, r * MathF.Sin(theta)) });
        }
        return pts;
    }

    private List<FormationPoint> GenChevron(int count, float deg, float spacing) {
        var pts = new List<FormationPoint>();
        pts.Add(new FormationPoint { Offset = Vector3.Zero });
        float rad = deg * Angle.DegToRad;
        for (int i = 1; i < count; i++) {
            int s = (i + 1) / 2;
            float dir = (i % 2 == 1) ? -1 : 1;
            float x = dir * MathF.Sin(rad) * s * spacing;
            float z = -MathF.Cos(rad) * s * spacing;
            pts.Add(new FormationPoint { Offset = new Vector3(x, 0, z) });
        }
        return pts;
    }

    private List<FormationPoint> GenRingWithCenter(int count, float radius, int centerCount) {
        var pts = new List<FormationPoint>();
        centerCount = Math.Min(centerCount, count);
        for (int i = 0; i < centerCount; i++) pts.Add(new FormationPoint { Offset = Vector3.Zero });
        int remain = count - centerCount;
        if (remain > 0) {
            pts.AddRange(GenCircle(remain, radius));
        }
        return pts;
    }

    private List<FormationPoint> GenCross(int count, float armLen, float spacing) {
        var pts = new List<FormationPoint> { new FormationPoint { Offset = Vector3.Zero } };
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
