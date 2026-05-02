using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Text;

using MasterOfPuppets;
using MasterOfPuppets.Formations;
using MasterOfPuppets.Movement;

using Xunit;

public class FormationImportTests {
    [Fact]
    public void Upstream_Mop_Circle_Uses_Main_Plot_Orientation() {
        var circle = BuildUpstreamCircleFormation();

        Assert.Equal(8, circle.Points.Count);

        var plots = circle.Points.Select(p => ToPlot(p.Offset)).ToArray();
        AssertClose(0f, plots[0].X);
        AssertClose(0f, plots[0].Y);
        AssertClose(0f, circle.Points[0].Angle);

        Assert.All(plots.Skip(1), p => Assert.True(p.Y > 0f));
        AssertClose(-3.535534f, plots[1].X);
        AssertClose(1.4644661f, plots[1].Y);
        AssertClose(-5f, plots[2].X);
        AssertClose(5f, plots[2].Y);
        AssertClose(0f, plots[4].X);
        AssertClose(10f, plots[4].Y);
        AssertClose(5f, plots[6].X);
        AssertClose(5.000001f, plots[6].Y);

        float[] expectedAngles = [0f, -45f, -90f, -135f, 180f, 135f, 90f, 45f];
        for (var i = 0; i < expectedAngles.Length; i++)
            AssertClose(expectedAngles[i], circle.Points[i].Angle, 0.001f);
    }

    [Fact]
    public void Upstream_Mop_Circle_Plot_Coordinates_Round_Trip_To_Stored_Offsets() {
        var circle = BuildUpstreamCircleFormation();

        foreach (var point in circle.Points) {
            var plot = ToPlot(point.Offset);
            var offset = FromPlot(plot);

            AssertClose(point.Offset.X, offset.X);
            AssertClose(point.Offset.Y, offset.Y);
            AssertClose(point.Offset.Z, offset.Z);
        }
    }

    [Fact]
    public void Mop_World_Math_Round_Trips_Position_And_Angle() {
        var point = new FormationPoint {
            Offset = new Vector3(3f, 0f, -2f),
            Angle = -45f,
        };
        var origin = new Vector3(10f, 0f, 10f);
        var originRotation = 0.5f;

        var (position, rotation) = FormationMath.ToMopWorld(point, origin, originRotation);
        var (offset, angle) = FormationMath.ToMopRelative(position, rotation, origin, originRotation);

        AssertClose(point.Offset.X, offset.X);
        AssertClose(point.Offset.Y, offset.Y);
        AssertClose(point.Offset.Z, offset.Z);
        AssertClose(point.Angle, angle);
    }

    [Fact]
    public void Mop_World_Math_Uses_Main_Cardinal_Rotations() {
        var offset = new Vector3(0f, 0f, 1f);
        var origin = Vector3.Zero;

        AssertClose(1f, FormationMath.ToMopWorld(offset, 0f, origin, 0f).Position.Z);
        AssertClose(-1f, FormationMath.ToMopWorld(offset, 0f, origin, MathF.PI).Position.Z);
        AssertClose(1f, FormationMath.ToMopWorld(offset, 0f, origin, MathF.PI / 2f).Position.X);
        AssertClose(-1f, FormationMath.ToMopWorld(offset, 0f, origin, -MathF.PI / 2f).Position.X);
    }

    [Fact]
    public void Anchor_Relative_World_Math_Keeps_Anchor_Fixed() {
        var anchor = new FormationPoint {
            Offset = new Vector3(2f, 0f, 3f),
            Angle = 45f,
        };
        var member = new FormationPoint {
            Offset = new Vector3(1f, 0f, 5f),
            Angle = -45f,
        };
        var anchorWorldPosition = new Vector3(10f, 0f, 20f);
        var anchorWorldRotation = 0.5f;

        var (anchorPosition, anchorRotation) = FormationMath.GetMopRelativeWorld(
            anchor,
            anchor,
            anchorWorldPosition,
            anchorWorldRotation);
        AssertClose(anchorWorldPosition.X, anchorPosition.X);
        AssertClose(anchorWorldPosition.Z, anchorPosition.Z);
        AssertClose(anchorWorldRotation, anchorRotation);

        var (memberPosition, memberRotation) = FormationMath.GetMopRelativeWorld(
            anchor,
            member,
            anchorWorldPosition,
            anchorWorldRotation);
        var expectedMemberPosition = ApplyLeaderRotation(member.Offset - anchor.Offset, anchorWorldRotation, anchorWorldPosition);
        AssertClose(expectedMemberPosition.X, memberPosition.X);
        AssertClose(expectedMemberPosition.Z, memberPosition.Z);
        AssertClose(anchorWorldRotation - 90f * Angle.DegToRad, memberRotation);
    }

    [Fact]
    public void BardToolbox_TightCircle_Imports_To_Mop_Plot_Orientation() {
        var import = ReadBardToolboxImport();
        var formation = import.Formations.Single(f => f.Name == "Tight Circle");

        Assert.Equal(8, formation.Points.Count);

        var anchor = formation.Points[0];
        Assert.Equal(TestCharacterCatalog.CidForIndex(1), anchor.Cids[0]);
        AssertClose(0f, ToPlot(anchor.Offset).X);
        AssertClose(0.25f, ToPlot(anchor.Offset).Y);
        AssertClose(0f, anchor.Angle);

        var kicking = formation.Points[4];
        Assert.Equal(TestCharacterCatalog.CidForIndex(5), kicking.Cids[0]);
        AssertClose(0.75f, ToPlot(kicking.Offset).X);
        AssertClose(1f, ToPlot(kicking.Offset).Y);
        AssertClose(89.18872f, kicking.Angle, 0.001f);
    }

    [Fact]
    public void BardToolbox_Import_Matches_Corrected_Output_For_Representative_Formations() {
        var import = ReadBardToolboxImport();

        foreach (var formationRows in BardToolboxRows.GroupBy(r => r.FormationName)) {
            var formation = import.Formations.Single(f => f.Name == formationRows.Key);
            var expectedRows = formationRows.OrderBy(r => r.Index).ToArray();

            Assert.Equal(expectedRows.Length, formation.Points.Count);
            for (var i = 0; i < expectedRows.Length; i++) {
                var expected = expectedRows[i];
                var actual = formation.Points[i];

                Assert.Equal(TestCharacterCatalog.CidForIndex(expected.PerformerIndex), actual.Cids.Single());
                AssertClose(-expected.X, actual.Offset.X, 0.001f);
                AssertClose(expected.Y, actual.Offset.Y, 0.001f);
                AssertClose(-expected.Z, actual.Offset.Z, 0.001f);
                AssertClose(
                    FormationMath.NormalizeDegrees(expected.RotationRadians * Angle.RadToDeg),
                    actual.Angle,
                    0.001f);
            }
        }
    }

    [Fact]
    public void BardToolbox_Import_Sorts_By_BardToolbox_Index() {
        var import = ReadBardToolboxImport();
        var formation = import.Formations.Single(f => f.Name == "Tight Circle");

        Assert.Equal(
            Enumerable.Range(1, 8).Select(TestCharacterCatalog.CidForIndex),
            formation.Points.Select(p => p.Cids.Single()));
    }

    [Fact]
    public void BardToolbox_Import_Adds_Anonymous_Characters_And_Renames_Duplicates() {
        var import = ReadBardToolboxImport();

        Assert.Equal(4, import.Formations.Count);
        Assert.Contains(import.Formations, f => f.Name == "Circle");
        Assert.Contains(import.Formations, f => f.Name == "Tight Circle");
        Assert.Contains(import.Formations, f => f.Name == "Big Circle");
        Assert.Contains(import.Formations, f => f.Name == "Circle Rodeo");
        Assert.All(import.CharacterNames, kvp => {
            Assert.True(kvp.Key >= TestCharacterCatalog.BaseCid);
            Assert.StartsWith("Performer ", kvp.Value);
            Assert.EndsWith("@Test", kvp.Value);
        });

        var formations = new List<Formation> {
            BuildUpstreamCircleFormation(),
        };
        var characters = new List<Character>();
        var result = BardToolboxFormationImporter.ImportInto(
            formations,
            characters,
            import,
            MacroImportMode.AppendAll);

        Assert.Equal(4, result.FormationsImported);
        Assert.Equal(BardToolboxRows.Length, result.PointsImported);
        Assert.Contains(formations, f => f.Name == "Circle");
        Assert.Contains(formations, f => f.Name == "Circle (2)");
        Assert.Contains(formations, f => f.Name == "Tight Circle");
        Assert.Contains(formations, f => f.Name == "Big Circle");
        Assert.Contains(formations, f => f.Name == "Circle Rodeo");
        Assert.Equal(import.CharacterNames.Count, result.CharactersImported);
        Assert.All(characters, c => {
            Assert.True(c.Cid >= TestCharacterCatalog.BaseCid);
            Assert.StartsWith("Performer ", c.Name);
        });
    }

    private static BardToolboxFormationImport ReadBardToolboxImport() =>
        BardToolboxFormationImporter.ParseConfigJson(BardToolboxConfigJson);

    private static Formation BuildUpstreamCircleFormation() => new() {
        Name = "Circle",
        Points = [
            new FormationPoint { Offset = new Vector3(-2.1855695E-7f, 0f, 0f), Angle = 0.0000032443963f },
            new FormationPoint { Offset = new Vector3(3.535534f, 0f, 1.4644661f), Angle = -45f },
            new FormationPoint { Offset = new Vector3(5f, 0f, 5f), Angle = -90f },
            new FormationPoint { Offset = new Vector3(3.5355341f, 0f, 8.535534f), Angle = -135f },
            new FormationPoint { Offset = new Vector3(-2.1855695E-7f, 0f, 10f), Angle = 179.99998f },
            new FormationPoint { Offset = new Vector3(-3.535534f, 0f, 8.535534f), Angle = 135f },
            new FormationPoint { Offset = new Vector3(-5f, 0f, 5.000001f), Angle = 90.00001f },
            new FormationPoint { Offset = new Vector3(-3.5355332f, 0f, 1.4644656f), Angle = 44.99999f },
        ],
    };

    private static Vector2 ToPlot(Vector3 offset) => new(-offset.X, offset.Z);

    private static Vector3 FromPlot(Vector2 plot) => new(-plot.X, 0f, plot.Y);

    private static Vector3 ApplyLeaderRotation(Vector3 offset, float leaderRotRad, Vector3 leaderPos) {
        var angle = -leaderRotRad;
        var cos = MathF.Cos(angle);
        var sin = MathF.Sin(angle);

        return leaderPos + new Vector3(
            offset.X * cos - offset.Z * sin,
            0f,
            offset.X * sin + offset.Z * cos);
    }

    private static void AssertClose(float expected, float actual, float tolerance = 0.0001f) =>
        Assert.InRange(MathF.Abs(expected - actual), 0f, tolerance);

    private static class TestCharacterCatalog {
        public const ulong BaseCid = 900_000_000_000_000UL;

        public static ulong CidForIndex(int index) => BaseCid + (ulong)index;

        public static string NameForIndex(int index) => $"Performer {index:00}";
    }

    private readonly record struct BtbRow(
        string FormationName,
        int Index,
        int PerformerIndex,
        float X,
        float Y,
        float Z,
        float RotationRadians);

    private static readonly BtbRow[] BardToolboxRows = [
        new("Circle", 0, 1, 0f, 0f, -1.5f, 3.1415927f),
        new("Circle", 1, 2, 1f, 0f, -1f, 2.3599586f),

        new("Tight Circle", 0, 1, -0.0f, 0f, -0.25f, 0f),
        new("Tight Circle", 4, 5, 0.75f, 0f, -1f, -4.7265487f),
        new("Tight Circle", 6, 7, 0.5f, -0.0000019073486f, -1.5f, -3.9466035f),
        new("Tight Circle", 3, 4, -0.75f, 0f, -1f, -1.5865381f),
        new("Tight Circle", 7, 8, 0f, 0f, -1.75f, -3.1643572f),
        new("Tight Circle", 1, 2, 0.5f, 0f, -0.5f, 0.76662445f),
        new("Tight Circle", 5, 6, -0.5f, 0f, -1.5f, -2.3665793f),
        new("Tight Circle", 2, 3, -0.5f, 0f, -0.5f, -0.7966218f),

        new("Big Circle", 7, 8, -3.9444897f, -0.0091228485f, 3.9698389f, 5.5014563f),
        new("Big Circle", 4, 5, 4.04102f, -0.008942604f, 3.4879546f, 0.78151655f),
        new("Big Circle", 0, 1, 0f, -0.009066582f, -5f, 3.1337202f),
        new("Big Circle", 6, 7, -4.0133305f, -0.009148598f, -3.8320959f, 3.9314985f),
        new("Big Circle", 1, 2, 5f, -0.0088739395f, -0.0f, 1.5715288f),
        new("Big Circle", 5, 6, 4.2245946f, -0.00895977f, -3.4878929f, 2.351474f),
        new("Big Circle", 2, 3, -5f, -0.009205818f, -0.0f, 4.7115393f),
        new("Big Circle", 3, 4, 0f, 0f, 5f, 0f),

        new("Circle Rodeo", 0, 1, 3.061617E-16f, 0f, -5f, 3.1415927f),
        new("Circle Rodeo", 1, 2, 1.2940953f, 0f, -4.829629f, 2.8797932f),
        new("Circle Rodeo", 2, 3, 2.5f, 0f, -4.3301272f, 2.6179938f),
        new("Circle Rodeo", 3, 4, 3.535534f, 0f, -3.535534f, 2.3561945f),
        new("Circle Rodeo", 4, 5, 4.3301272f, 0f, -2.5f, 2.0943952f),
        new("Circle Rodeo", 5, 6, 4.829629f, 0f, -1.2940953f, 1.8325957f),
        new("Circle Rodeo", 6, 7, 5f, 0f, 0f, 1.5707964f),
        new("Circle Rodeo", 7, 8, 4.829629f, 0f, 1.2940953f, 1.3089969f),
        new("Circle Rodeo", 8, 9, 4.3301272f, 0f, 2.5f, 1.0471976f),
        new("Circle Rodeo", 9, 10, 3.535534f, 0f, 3.535534f, 0.7853982f),
        new("Circle Rodeo", 10, 11, 2.5f, 0f, 4.3301272f, 0.5235988f),
        new("Circle Rodeo", 11, 12, 1.2940953f, 0f, 4.829629f, 0.2617994f),
        new("Circle Rodeo", 12, 13, 3.061617E-16f, 0f, 5f, 6.123234E-17f),
        new("Circle Rodeo", 13, 14, -1.2940953f, 0f, 4.829629f, -0.2617994f),
        new("Circle Rodeo", 14, 15, -2.5f, 0f, 4.3301272f, -0.5235988f),
        new("Circle Rodeo", 15, 16, -3.535534f, 0f, 3.535534f, -0.7853982f),
        new("Circle Rodeo", 16, 17, -4.3301272f, 0f, 2.5f, -1.0471976f),
        new("Circle Rodeo", 17, 18, -4.829629f, 0f, 1.2940953f, -1.3089969f),
        new("Circle Rodeo", 18, 19, -5f, 0f, 6.123234E-16f, -1.5707964f),
        new("Circle Rodeo", 19, 20, -4.829629f, 0f, -1.2940953f, -1.8325957f),
        new("Circle Rodeo", 20, 21, -4.3301272f, 0f, -2.5f, -2.0943952f),
        new("Circle Rodeo", 21, 22, -3.535534f, 0f, -3.535534f, -2.3561945f),
        new("Circle Rodeo", 22, 23, -2.5f, 0f, -4.3301272f, -2.6179938f),
        new("Circle Rodeo", 23, 24, -1.2940953f, 0f, -4.829629f, -2.8797932f),
    ];

    private static readonly string BardToolboxConfigJson = BuildBardToolboxConfigJson();

    private static string BuildBardToolboxConfigJson() {
        var json = new StringBuilder();
        json.AppendLine("{");
        json.AppendLine("""  "$type": "BardToolboxNamespace.Config, BardToolbox",""");
        json.AppendLine("""  "SavedFormationList": [""");

        var formations = BardToolboxRows.GroupBy(r => r.FormationName).ToArray();
        for (var formationIndex = 0; formationIndex < formations.Length; formationIndex++) {
            var formation = formations[formationIndex];
            json.AppendLine("    {");
            json.AppendLine("""      "$type": "BardToolboxNamespace.Config+NamedFormation, BardToolbox",""");
            json.AppendLine($"""      "11": "{formation.Key}",""");
            json.AppendLine("""      "22": {""");

            var rows = formation.ToArray();
            for (var rowIndex = 0; rowIndex < rows.Length; rowIndex++) {
                var row = rows[rowIndex];
                var cid = TestCharacterCatalog.CidForIndex(row.PerformerIndex);
                json.AppendLine($"        \"{cid}\": {{");
                json.AppendLine("""          "$type": "BardToolboxNamespace.Config+FormationEntry, BardToolbox",""");
                json.AppendLine(FormattableString.Invariant($"          \"i\": {row.Index},"));
                json.AppendLine(FormattableString.Invariant($"          \"Pepsi1\": {cid},"));
                json.AppendLine($"          \"Pepsi2\": {{ \"$type\": \"System.Numerics.Vector3, System.Private.CoreLib\", \"X\": {Float(row.X)}, \"Y\": {Float(row.Y)}, \"Z\": {Float(row.Z)} }},");
                json.AppendLine($"          \"Pepsi3\": {Float(row.RotationRadians)}");
                json.Append(rowIndex == rows.Length - 1 ? "        }" : "        },");
                json.AppendLine();
            }

            json.AppendLine("      }");
            json.Append(formationIndex == formations.Length - 1 ? "    }" : "    },");
            json.AppendLine();
        }

        json.AppendLine("  ],");
        json.AppendLine("""  "CidToNameWorld": {""");

        var performerIndexes = BardToolboxRows.Select(r => r.PerformerIndex).Distinct().Order().ToArray();
        for (var i = 0; i < performerIndexes.Length; i++) {
            var performerIndex = performerIndexes[i];
            var cid = TestCharacterCatalog.CidForIndex(performerIndex);
            var suffix = i == performerIndexes.Length - 1 ? string.Empty : ",";
            json.AppendLine($"    \"{cid}\": {{ \"Item1\": \"{TestCharacterCatalog.NameForIndex(performerIndex)}\", \"Item2\": \"Test\" }}{suffix}");
        }

        json.AppendLine("  }");
        json.AppendLine("}");
        return json.ToString();
    }

    private static string Float(float value) =>
        value.ToString("G9", CultureInfo.InvariantCulture);
}
