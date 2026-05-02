using System;
using System.Collections.Generic;
using System.Numerics;

using MasterOfPuppets;
using MasterOfPuppets.Formations;
using MasterOfPuppets.Movement;

using Xunit;

public class FormationImportTests {
    [Fact]
    public void Parses_BardToolbox_Config_Formations_And_Characters() {
        var import = BardToolboxFormationImporter.ParseConfigJson(SampleBardToolboxConfig);

        Assert.Single(import.Formations);
        Assert.Equal("Line", import.Formations[0].Name);
        Assert.Equal(FormationExecutionMode.RelativeToLocalAssignedPoint, import.Formations[0].ExecutionMode);
        Assert.Equal(2, import.Formations[0].Points.Count);
        Assert.Equal((ulong)222, import.Formations[0].Points[0].Cids[0]);
        Assert.Equal((ulong)111, import.Formations[0].Points[1].Cids[0]);
        Assert.Equal(-90f, import.Formations[0].Points[1].Angle, 0.001f);
        Assert.Equal("Alpha@Gilgamesh", import.CharacterNames[111]);
    }

    [Fact]
    public void Imports_With_AppendAll_Unique_Names() {
        var formations = new List<Formation> {
            new() { Name = "Line" },
        };
        var characters = new List<Character>();
        var import = BardToolboxFormationImporter.ParseConfigJson(SampleBardToolboxConfig);

        var result = BardToolboxFormationImporter.ImportInto(
            formations,
            characters,
            import,
            MacroImportMode.AppendAll);

        Assert.Equal(1, result.FormationsImported);
        Assert.Equal("Line (2)", formations[1].Name);
        Assert.Equal(2, result.CharactersImported);
        Assert.Contains(characters, c => c.Cid == 111 && c.Name == "Alpha@Gilgamesh");
    }

    [Fact]
    public void Mop_World_Math_Round_Trips_Position_And_Angle() {
        var point = new FormationPoint {
            Offset = new Vector3(3f, 0f, -2f),
            Angle = 45f,
        };
        var origin = new Vector3(10f, 0f, 10f);
        var originRotation = 0.5f;

        var (position, rotation) = FormationMath.ToMopWorld(point, origin, originRotation);
        var (offset, angle) = FormationMath.ToMopRelative(position, rotation, origin, originRotation);

        Assert.Equal(point.Offset.X, offset.X, 0.001f);
        Assert.Equal(point.Offset.Y, offset.Y, 0.001f);
        Assert.Equal(point.Offset.Z, offset.Z, 0.001f);
        Assert.Equal(point.Angle, angle, 0.001f);
    }

    [Fact]
    public void Mop_Relative_Math_Uses_Assigned_Anchor_Point() {
        var anchor = new FormationPoint {
            Offset = new Vector3(2, 0, 0),
            Angle = 0,
        };
        var member = new FormationPoint {
            Offset = new Vector3(3, 0, 0),
            Angle = -90,
        };

        var (position, rotation) = FormationMath.GetMopRelativeWorld(
            anchor,
            member,
            new Vector3(10, 0, 10),
            0f);

        Assert.Equal(9f, position.X, 0.001f);
        Assert.Equal(10f, position.Z, 0.001f);
        Assert.Equal(90f * Angle.DegToRad, rotation, 0.001f);
    }

    private const string SampleBardToolboxConfig = """
    {
      "$type": "BardToolboxNamespace.Config, BardToolbox",
      "SavedFormationList": [
        {
          "$type": "BardToolboxNamespace.Config+NamedFormation, BardToolbox",
          "11": "Line",
          "22": {
            "$type": "System.Collections.Generic.Dictionary`2",
            "111": {
              "$type": "BardToolboxNamespace.Config+FormationEntry, BardToolbox",
              "i": 1,
              "Pepsi1": 111,
              "Pepsi2": { "$type": "System.Numerics.Vector3", "X": 1.0, "Y": 0.0, "Z": 2.0 },
              "Pepsi3": 1.5707964
            },
            "222": {
              "$type": "BardToolboxNamespace.Config+FormationEntry, BardToolbox",
              "i": 0,
              "Pepsi1": 222,
              "Pepsi2": { "$type": "System.Numerics.Vector3", "X": 0.0, "Y": 0.0, "Z": 0.0 },
              "Pepsi3": 0.0
            }
          }
        }
      ],
      "CidToNameWorld": {
        "111": { "Item1": "Alpha", "Item2": "Gilgamesh" },
        "222": { "Item1": "Beta", "Item2": "Gilgamesh" }
      }
    }
    """;
}
