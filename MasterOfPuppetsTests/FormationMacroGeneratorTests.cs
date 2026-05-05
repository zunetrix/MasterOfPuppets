using System;
using System.Globalization;
using System.Linq;
using System.Numerics;

using MasterOfPuppets;
using MasterOfPuppets.Formations;
using MasterOfPuppets.Movement;

using Xunit;

public class FormationMacroGeneratorTests {
    [Fact]
    public void GenerateLoopMacro_UsesOffsetsRelativeToAnchorAndVariableOrigin() {
        var formation = new Formation {
            Points = [
                new FormationPoint { Offset = Vector3.Zero, Cids = [100] },
                new FormationPoint { Offset = new Vector3(0f, 0f, -2f), Cids = [101] },
                new FormationPoint { Offset = new Vector3(2f, 0f, 0f), Cids = [102] },
                new FormationPoint { Offset = new Vector3(0f, 0f, 2f), Cids = [103] },
            ],
        };

        var macro = FormationMacroGenerator.GenerateLoopMacro(formation, new FormationMacroGeneratorOptions {
            MacroName = "Circle Mop",
            OriginReference = "Origin Character@World",
            TravelSecondsPerUnit = 1f,
            GlobalDelaySeconds = 0f,
        });

        Assert.Equal("Circle Mop", macro.Name);
        Assert.Equal(3, macro.Commands.Count);
        Assert.Equal([101UL], macro.Commands[0].Cids);

        var lines = macro.Commands[0].Actions.Split('\n');
        Assert.Equal("/mopmoverelativeto 0.00 0.00 -2.00 \"Origin Character@World\"", lines[0]);
        Assert.Equal("/mopwait 2.83", lines[1]);
        Assert.Equal("/mopmoverelativeto 2.00 0.00 0.00 \"Origin Character@World\"", lines[2]);
        Assert.Equal("/mopwait 2.83", lines[3]);
        Assert.Equal("/mopmoverelativeto 0.00 0.00 2.00 \"Origin Character@World\"", lines[4]);
        Assert.Equal("/mopwait 4.00", lines[5]);
        Assert.Equal("/moploop", lines[6]);
    }

    [Fact]
    public void GenerateLoopMacro_CanBakeOriginRotationIntoRelativeMoveCoordinatesAndFacing() {
        var formation = new Formation {
            Points = [
                new FormationPoint { Offset = Vector3.Zero, Angle = 0f, Cids = [100] },
                new FormationPoint { Offset = new Vector3(0f, 0f, 2f), Angle = 90f, Cids = [101] },
            ],
        };

        var macro = FormationMacroGenerator.GenerateLoopMacro(formation, new FormationMacroGeneratorOptions {
            OriginReference = "Origin Character@World",
            TransformRelativeMovesByOriginRotation = true,
            EmitRelativeMoveFacing = true,
            OriginRotationRadians = MathF.PI / 2f,
            TravelSecondsPerUnit = 1f,
            GlobalDelaySeconds = 0f,
        });

        var lines = macro.Commands[0].Actions.Split('\n');
        Assert.Equal("/mopmoverelativeto 2.00 0.00 0.00 \"Origin Character@World\" 180.00", lines[0]);
        Assert.DoesNotContain("/mopformationmove", macro.Commands[0].Actions);
    }

    [Fact]
    public void GenerateLoopMacro_GeneratedShapeStyleMovement_UsesStaticRelativeMoveCommands() {
        var formation = new Formation {
            Points = FormationShapeGenerator.Generate(new FormationShapeSpec {
                Type = FormationShapeType.Circle,
                Count = 4,
                Radius = 2f,
                AnchorMode = FormationShapeAnchorMode.AnchorAtCenter,
                AssignedCids = [100, 101, 102, 103],
            }),
        };

        var macro = FormationMacroGenerator.GenerateLoopMacro(formation, new FormationMacroGeneratorOptions {
            OriginReference = "Origin Character@World",
            TransformRelativeMovesByOriginRotation = true,
            EmitRelativeMoveFacing = true,
            OriginRotationRadians = 0f,
        });

        Assert.Equal(3, macro.Commands.Count);
        var lines = macro.Commands[0].Actions.Split('\n');
        Assert.Equal("/mopmoverelativeto 0.00 0.00 -2.00 \"Origin Character@World\" 0.00", lines[0]);
        Assert.DoesNotContain("/mopformationmove", macro.Commands[0].Actions);
    }

    [Fact]
    public void GenerateLoopMacro_GeneratedShapeStyleMovement_CanFaceAnchorNorth() {
        var formation = new Formation {
            Points = FormationShapeGenerator.Generate(new FormationShapeSpec {
                Type = FormationShapeType.Circle,
                Count = 4,
                Radius = 2f,
                AnchorMode = FormationShapeAnchorMode.AnchorAtCenter,
                AssignedCids = [100, 101, 102, 103],
            }),
        };

        var macro = FormationMacroGenerator.GenerateLoopMacro(formation, new FormationMacroGeneratorOptions {
            OriginReference = "Origin Character@World",
            TransformRelativeMovesByOriginRotation = true,
            EmitRelativeMoveFacing = true,
            EmitAnchorFacingNorth = true,
            OriginRotationRadians = 0f,
        });

        Assert.Equal(4, macro.Commands.Count);
        Assert.Equal([100UL], macro.Commands[0].Cids);
        Assert.Equal("/mopfaceabs 0\n/moploop", macro.Commands[0].Actions);
        Assert.Equal([101UL], macro.Commands[1].Cids);
        Assert.Contains("/mopmoverelativeto", macro.Commands[1].Actions);
    }

    [Fact]
    public void GenerateLoopMacro_GeneratedShapeStyleMovement_InwardFacesTowardCenter() {
        AssertGeneratedCircleFacing(FormationShapeFaceMode.Inward, shouldFaceTowardCenter: true);
    }

    [Fact]
    public void GenerateLoopMacro_GeneratedShapeStyleMovement_OutwardFacesAwayFromCenter() {
        AssertGeneratedCircleFacing(FormationShapeFaceMode.Outward, shouldFaceTowardCenter: false);
    }

    [Fact]
    public void GenerateLoopMacro_CopiesGroupAssignments() {
        var formation = new Formation {
            Points = [
                new FormationPoint { Offset = Vector3.Zero, Cids = [100] },
                new FormationPoint { Offset = new Vector3(1f, 0f, 0f), GroupIds = ["Group A"] },
            ],
        };

        var macro = FormationMacroGenerator.GenerateLoopMacro(formation, new FormationMacroGeneratorOptions());

        var command = Assert.Single(macro.Commands);
        Assert.Empty(command.Cids);
        Assert.Equal(["Group A"], command.GroupIds);
    }

    [Fact]
    public void GenerateLoopMacro_DefaultTimingMatchesMopScriptsCode() {
        var formation = new Formation {
            Points = [
                new FormationPoint { Offset = Vector3.Zero, Cids = [100] },
                new FormationPoint { Offset = Vector3.Zero, Cids = [101] },
                new FormationPoint { Offset = new Vector3(1.75f, 0f, 0f), Cids = [102] },
            ],
        };

        var macro = FormationMacroGenerator.GenerateLoopMacro(formation, new FormationMacroGeneratorOptions {
            OriginReference = "Origin Character@World",
        });

        var lines = macro.Commands[0].Actions.Split('\n');
        Assert.Equal("/mopwait 0.10", lines[1]);
        Assert.Equal("/mopwait 0.10", lines[3]);
    }

    [Fact]
    public void GenerateLoopMacro_ConfiguredGlobalDelayIsSubtractedFromTravelWaits() {
        var formation = new Formation {
            Points = [
                new FormationPoint { Offset = Vector3.Zero, Cids = [100] },
                new FormationPoint { Offset = Vector3.Zero, Cids = [101] },
                new FormationPoint { Offset = new Vector3(1.75f, 0f, 0f), Cids = [102] },
            ],
        };

        var macro = FormationMacroGenerator.GenerateLoopMacro(formation, new FormationMacroGeneratorOptions {
            OriginReference = "Origin Character@World",
            GlobalDelaySeconds = 0f,
        });

        var lines = macro.Commands[0].Actions.Split('\n');
        Assert.Equal("/mopwait 0.35", lines[1]);
        Assert.Equal("/mopwait 0.35", lines[3]);
    }

    [Fact]
    public void GenerateLoopMacro_ReusesExactMatchingGroupWhenEnabled() {
        var formation = new Formation {
            Points = [
                new FormationPoint { Offset = Vector3.Zero, Cids = [100] },
                new FormationPoint { Offset = new Vector3(1f, 0f, 0f), Cids = [101, 102] },
            ],
        };
        var groups = new[] {
            new CidGroup { Name = "Duo", Cids = [102, 101] },
            new CidGroup { Name = "Other", Cids = [101] },
        };

        var result = FormationMacroGenerator.GenerateLoopMacroWithDiagnostics(
            formation,
            new FormationMacroGeneratorOptions { UseMatchingGroups = true },
            groups);

        var command = Assert.Single(result.Macro.Commands);
        Assert.Empty(command.Cids);
        Assert.Equal(["Duo"], command.GroupIds);
    }

    [Fact]
    public void GenerateLoopMacro_KeepsDirectCidsWhenGroupReuseDisabled() {
        var formation = new Formation {
            Points = [
                new FormationPoint { Offset = Vector3.Zero, Cids = [100] },
                new FormationPoint { Offset = new Vector3(1f, 0f, 0f), Cids = [101, 102] },
            ],
        };
        var groups = new[] {
            new CidGroup { Name = "Duo", Cids = [102, 101] },
        };

        var result = FormationMacroGenerator.GenerateLoopMacroWithDiagnostics(
            formation,
            new FormationMacroGeneratorOptions(),
            groups);

        var command = Assert.Single(result.Macro.Commands);
        Assert.Equal([101UL, 102UL], command.Cids);
        Assert.Empty(command.GroupIds);
    }

    [Fact]
    public void TryResolveAssignedPointIndex_FindsDirectAssignedCharacter() {
        var formation = new Formation {
            Points = [
                new FormationPoint { Offset = Vector3.Zero, Cids = [100] },
                new FormationPoint { Offset = Vector3.One, Cids = [101] },
            ],
        };

        var found = FormationMacroGenerator.TryResolveAssignedPointIndex(formation, 101, null, out var pointIndex);

        Assert.True(found);
        Assert.Equal(1, pointIndex);
    }

    [Fact]
    public void TryResolveAssignedPointIndex_FindsCharacterThroughGroup() {
        var formation = new Formation {
            Points = [
                new FormationPoint { Offset = Vector3.Zero, GroupIds = ["Group A"] },
            ],
        };
        var groups = new[] {
            new CidGroup { Name = "Group A", Cids = [100, 101] },
        };

        var found = FormationMacroGenerator.TryResolveAssignedPointIndex(formation, 101, groups, out var pointIndex);

        Assert.True(found);
        Assert.Equal(0, pointIndex);
    }

    [Fact]
    public void TryResolveAssignedPointIndex_ReturnsFalseWhenCharacterIsUnassigned() {
        var formation = new Formation {
            Points = [
                new FormationPoint { Offset = Vector3.Zero, Cids = [100] },
            ],
        };

        var found = FormationMacroGenerator.TryResolveAssignedPointIndex(formation, 101, null, out var pointIndex);

        Assert.False(found);
        Assert.Equal(-1, pointIndex);
    }

    [Fact]
    public void GenerateLoopMacro_PetPlacement_TargetsSingleResolvedCharacters() {
        var formation = new Formation {
            Points = [
                new FormationPoint { Offset = Vector3.Zero, Cids = [100] },
                new FormationPoint { Offset = new Vector3(1f, 0f, 0f), Cids = [101] },
                new FormationPoint { Offset = new Vector3(2f, 0f, 0f), Cids = [102] },
            ],
        };
        var characters = new[] {
            new Character { Cid = 101, Name = "Alpha@World" },
            new Character { Cid = 102, Name = "Beta@World" },
        };

        var result = FormationMacroGenerator.GenerateLoopMacroWithDiagnostics(
            formation,
            new FormationMacroGeneratorOptions {
                Mode = FormationMacroGeneratorMode.PetPlacement,
                PetActionCommand = "/moppetbarslot 1",
            },
            characters: characters);

        Assert.Empty(result.Warnings);
        Assert.Equal(2, result.Macro.Commands.Count);
        Assert.Contains("/moptarget \"Alpha@World\"", result.Macro.Commands[0].Actions);
        Assert.Contains("/moppetbarslot 1", result.Macro.Commands[0].Actions);
        Assert.Contains("/mopwait", result.Macro.Commands[0].Actions);
        Assert.Contains("/moptarget \"Beta@World\"", result.Macro.Commands[0].Actions);
        Assert.EndsWith("/moploop", result.Macro.Commands[0].Actions);
    }

    [Fact]
    public void GenerateLoopMacro_PetPlacement_WarnsAndSkipsAmbiguousTargets() {
        var formation = new Formation {
            Points = [
                new FormationPoint { Offset = Vector3.Zero, Cids = [100] },
                new FormationPoint { Offset = new Vector3(1f, 0f, 0f), Cids = [101, 102] },
                new FormationPoint { Offset = new Vector3(2f, 0f, 0f), Cids = [103] },
            ],
        };
        var characters = new[] {
            new Character { Cid = 103, Name = "Solo@World" },
        };

        var result = FormationMacroGenerator.GenerateLoopMacroWithDiagnostics(
            formation,
            new FormationMacroGeneratorOptions { Mode = FormationMacroGeneratorMode.PetPlacement },
            characters: characters);

        Assert.NotEmpty(result.Warnings);
        Assert.All(result.Macro.Commands, command => Assert.DoesNotContain("101", command.Actions));
        Assert.All(result.Macro.Commands, command => Assert.Contains("/moptarget \"Solo@World\"", command.Actions));
    }

    [Fact]
    public void GenerateLoopMacro_SupportsStepAndReverseTraversal() {
        var formation = new Formation {
            Points = [
                new FormationPoint { Offset = Vector3.Zero, Cids = [100] },
                new FormationPoint { Offset = new Vector3(1f, 0f, 0f), Cids = [101] },
                new FormationPoint { Offset = new Vector3(2f, 0f, 0f), Cids = [102] },
                new FormationPoint { Offset = new Vector3(3f, 0f, 0f), Cids = [103] },
                new FormationPoint { Offset = new Vector3(4f, 0f, 0f), Cids = [104] },
            ],
        };

        var macro = FormationMacroGenerator.GenerateLoopMacro(formation, new FormationMacroGeneratorOptions {
            OriginReference = "Origin Character@World",
            Step = 2,
            Reverse = true,
            TravelSecondsPerUnit = 1f,
            GlobalDelaySeconds = 0f,
        });

        var moves = macro.Commands[0].Actions
            .Split('\n')
            .Where(line => line.StartsWith("/mopmoverelativeto"))
            .ToArray();

        Assert.Equal("/mopmoverelativeto 1.00 0.00 0.00 \"Origin Character@World\"", moves[0]);
        Assert.Equal("/mopmoverelativeto 3.00 0.00 0.00 \"Origin Character@World\"", moves[1]);
        Assert.Equal("/mopmoverelativeto 1.00 0.00 0.00 \"Origin Character@World\"", moves[2]);
        Assert.Equal("/mopmoverelativeto 3.00 0.00 0.00 \"Origin Character@World\"", moves[3]);
    }

    [Fact]
    public void GenerateLoopMacro_SkipsUnassignedStartPoints() {
        var formation = new Formation {
            Points = [
                new FormationPoint { Offset = Vector3.Zero, Cids = [100] },
                new FormationPoint { Offset = new Vector3(1f, 0f, 0f) },
                new FormationPoint { Offset = new Vector3(2f, 0f, 0f), Cids = [102] },
            ],
        };

        var macro = FormationMacroGenerator.GenerateLoopMacro(formation, new FormationMacroGeneratorOptions());

        var command = Assert.Single(macro.Commands);
        Assert.Equal([102UL], command.Cids);
    }

    [Fact]
    public void GenerateLoopMacro_ExistingFormationMovement_UsesSingleOriginMoveCommandWithSteps() {
        var formation = BuildEightPointFormation();

        var result = FormationMacroGenerator.GenerateLoopMacroWithDiagnostics(
            formation,
            new FormationMacroGeneratorOptions {
                Mode = FormationMacroGeneratorMode.Movement,
                AnchorPointIndex = 0,
                UseFormationMoveCommand = true,
                FormationMoveName = "Circle",
                OriginContentId = 100,
            });

        Assert.Empty(result.Warnings);
        var command = Assert.Single(result.Macro.Commands);
        Assert.Equal([100UL], command.Cids);
        Assert.Empty(command.GroupIds);
        Assert.DoesNotContain("/mopformationpath", command.Actions);
        var moves = command.Actions
            .Split('\n')
            .Where(line => line.StartsWith("/mopformationmove"))
            .ToArray();
        Assert.Equal(7, moves.Length);
        Assert.Equal("/mopformationmove \"Circle\" forward 1 0 continuous", moves[0]);
        Assert.Equal("/mopformationmove \"Circle\" forward 1 6 continuous", moves[6]);
        Assert.EndsWith("/moploop", command.Actions);
    }

    [Fact]
    public void GenerateLoopMacro_ExistingFormationMovement_EmitsArrivalAndTargetAnchor() {
        var formation = BuildEightPointFormation();

        var result = FormationMacroGenerator.GenerateLoopMacroWithDiagnostics(
            formation,
            new FormationMacroGeneratorOptions {
                Mode = FormationMacroGeneratorMode.Movement,
                AnchorPointIndex = 0,
                UseFormationMoveCommand = true,
                FormationMoveName = "Circle",
                OriginContentId = 100,
                FormationMoveArrivalMode = MovementArrivalMode.Precise,
                FormationMoveAnchorMode = FormationMoveAnchorMode.Target,
            });

        var command = Assert.Single(result.Macro.Commands);
        var moves = command.Actions
            .Split('\n')
            .Where(line => line.StartsWith("/mopformationmove"))
            .ToArray();

        Assert.Equal("/mopformationmove \"Circle\" forward 1 0 precise target", moves[0]);
    }

    [Fact]
    public void GenerateLoopMacro_ExistingFormationMovement_UsesMopScriptsTimingDefaults() {
        var formation = new Formation {
            Name = "Circle",
            Points = [
                new FormationPoint { Offset = Vector3.Zero, Cids = [100] },
                new FormationPoint { Offset = Vector3.Zero, Cids = [101] },
                new FormationPoint { Offset = new Vector3(1.75f, 0f, 0f), Cids = [102] },
            ],
        };

        var result = FormationMacroGenerator.GenerateLoopMacroWithDiagnostics(
            formation,
            new FormationMacroGeneratorOptions {
                Mode = FormationMacroGeneratorMode.Movement,
                UseFormationMoveCommand = true,
                FormationMoveName = "Circle",
                OriginContentId = 100,
            });

        var command = Assert.Single(result.Macro.Commands);
        var lines = command.Actions.Split('\n');
        Assert.Equal("/mopformationmove \"Circle\" forward 1 0 continuous", lines[0]);
        Assert.Equal("/mopwait 0.10", lines[1]);
        Assert.Equal("/mopformationmove \"Circle\" forward 1 1 continuous", lines[2]);
        Assert.Equal("/mopwait 0.10", lines[3]);
    }

    [Fact]
    public void GenerateLoopMacro_ExistingFormationMovement_CenterAnchorCircleUsesMopScriptsCadence() {
        var formation = new Formation {
            Name = "Circle",
            Points = [
                new FormationPoint { Offset = Vector3.Zero, Cids = [100] },
            ],
        };
        const float radius = 5f;
        for (var i = 0; i < 7; i++) {
            var angle = 2f * MathF.PI * i / 7f - MathF.PI / 2f;
            formation.Points.Add(new FormationPoint {
                Offset = new Vector3(radius * MathF.Cos(angle), 0f, radius * MathF.Sin(angle)),
                Cids = [(ulong)(101 + i)],
            });
        }

        var result = FormationMacroGenerator.GenerateLoopMacroWithDiagnostics(
            formation,
            new FormationMacroGeneratorOptions {
                Mode = FormationMacroGeneratorMode.Movement,
                AnchorPointIndex = 0,
                UseFormationMoveCommand = true,
                FormationMoveName = "Circle",
                OriginContentId = 100,
            });

        var waits = result.Macro.Commands[0].Actions
            .Split('\n')
            .Where(line => line.StartsWith("/mopwait"))
            .ToArray();

        Assert.Equal(7, waits.Length);
        Assert.All(waits, wait => Assert.Equal("/mopwait 0.62", wait));
    }

    [Fact]
    public void GenerateLoopMacro_ExistingFormationMovement_DoesNotUseSkippedAnchorChordForEveryWait() {
        var formation = new Formation {
            Name = "Circel",
            Points = [
                new FormationPoint { Offset = Vector3.Zero, Cids = [100] },
                new FormationPoint { Offset = new Vector3(3.535534f, 0f, 1.4644661f), Cids = [101] },
                new FormationPoint { Offset = new Vector3(5f, 0f, 5f), Cids = [102] },
                new FormationPoint { Offset = new Vector3(3.5355341f, 0f, 8.535534f), Cids = [103] },
                new FormationPoint { Offset = new Vector3(0f, 0f, 10f), Cids = [104] },
                new FormationPoint { Offset = new Vector3(-3.535534f, 0f, 8.535534f), Cids = [105] },
                new FormationPoint { Offset = new Vector3(-5f, 0f, 5.000001f), Cids = [106] },
                new FormationPoint { Offset = new Vector3(-3.5355332f, 0f, 1.4644656f), Cids = [107] },
            ],
        };

        var result = FormationMacroGenerator.GenerateLoopMacroWithDiagnostics(
            formation,
            new FormationMacroGeneratorOptions {
                Mode = FormationMacroGeneratorMode.Movement,
                AnchorPointIndex = 0,
                UseFormationMoveCommand = true,
                FormationMoveName = "Circel",
                OriginContentId = 100,
            });

        var waits = result.Macro.Commands[0].Actions
            .Split('\n')
            .Where(line => line.StartsWith("/mopwait"))
            .ToArray();

        Assert.Equal(7, waits.Length);
        Assert.Equal("/mopwait 0.52", waits[0]);
        Assert.Equal("/mopwait 1.16", waits[^1]);
        Assert.NotEqual(waits[0], waits[^1]);
    }

    [Fact]
    public void GenerateLoopMacro_ExistingFormationCombined_UsesOneMoveCommandAndPetCommands() {
        var formation = BuildEightPointFormation();
        var characters = Enumerable.Range(1, 7)
            .Select(i => new Character { Cid = (ulong)(100 + i), Name = $"Character {i}@World" })
            .ToArray();

        var result = FormationMacroGenerator.GenerateLoopMacroWithDiagnostics(
            formation,
            new FormationMacroGeneratorOptions {
                Mode = FormationMacroGeneratorMode.MovementAndPetPlacement,
                AnchorPointIndex = 0,
                UseFormationMoveCommand = true,
                FormationMoveName = "Circle",
                OriginContentId = 100,
                PetActionCommand = "/moppetbarslot 1",
            },
            characters: characters);

        Assert.Empty(result.Warnings);
        Assert.Equal(8, result.Macro.Commands.Count);
        Assert.Equal([100UL], result.Macro.Commands[0].Cids);
        Assert.Contains("/mopformationmove \"Circle\" forward 1 0 continuous", result.Macro.Commands[0].Actions);
        Assert.DoesNotContain("/mopformationpath", result.Macro.Commands[0].Actions);
        Assert.All(result.Macro.Commands.Skip(1), command => {
            Assert.DoesNotContain(100UL, command.Cids);
            Assert.Contains("/moppetbarslot 1", command.Actions);
        });
    }

    [Fact]
    public void GenerateLoopMacro_PetPlacement_CanUseIndependentStrideAndDirection() {
        var formation = BuildEightPointFormation();
        var characters = Enumerable.Range(1, 7)
            .Select(i => new Character { Cid = (ulong)(100 + i), Name = $"Character {i}@World" })
            .ToArray();

        var result = FormationMacroGenerator.GenerateLoopMacroWithDiagnostics(
            formation,
            new FormationMacroGeneratorOptions {
                Mode = FormationMacroGeneratorMode.PetPlacement,
                AnchorPointIndex = 0,
                OriginContentId = 100,
                LinkPetTraversalToMovement = false,
                PetStep = 2,
                PetReverse = true,
            },
            characters: characters);

        var command = Assert.Single(result.Macro.Commands, command => command.Cids.SequenceEqual([101UL]));
        var lines = command.Actions.Split('\n');
        Assert.Equal("/moptarget \"Character 1@World\"", lines[0]);
        Assert.Equal("/moptarget \"Character 6@World\"", lines[3]);
        Assert.Equal("/moptarget \"Character 4@World\"", lines[6]);
    }

    private static void AssertGeneratedCircleFacing(FormationShapeFaceMode faceMode, bool shouldFaceTowardCenter) {
        foreach (var originRotation in new[] { 0f, MathF.PI / 2f, MathF.PI, -MathF.PI / 2f }) {
            var formation = new Formation {
                Points = FormationShapeGenerator.Generate(new FormationShapeSpec {
                    Type = FormationShapeType.Circle,
                    Count = 5,
                    Radius = 2f,
                    AnchorMode = FormationShapeAnchorMode.AnchorAtCenter,
                    FaceMode = faceMode,
                    AssignedCids = [100, 101, 102, 103, 104],
                }),
            };

            var macro = FormationMacroGenerator.GenerateLoopMacro(formation, new FormationMacroGeneratorOptions {
                OriginReference = "Origin",
                TransformRelativeMovesByOriginRotation = true,
                EmitRelativeMoveFacing = true,
                OriginRotationRadians = originRotation,
            });

            foreach (var command in macro.Commands) {
                var line = command.Actions.Split('\n')[0];
                var (offset, facingDegrees) = ParseRelativeMove(line);
                var desiredDirection = Vector3.Normalize(shouldFaceTowardCenter ? -offset : offset);
                var facingDirection = facingDegrees.Degrees().ToDirectionXZ();
                var dot = Vector3.Dot(desiredDirection, facingDirection);

                Assert.True(
                    dot > 0.999f,
                    $"Expected {faceMode} command '{line}' at origin rotation {originRotation} to face {(shouldFaceTowardCenter ? "toward" : "away from")} center; dot={dot}.");
            }
        }
    }

    private static (Vector3 Offset, float FacingDegrees) ParseRelativeMove(string line) {
        var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal("/mopmoverelativeto", parts[0]);

        return (
            new Vector3(
                float.Parse(parts[1], CultureInfo.InvariantCulture),
                float.Parse(parts[2], CultureInfo.InvariantCulture),
                float.Parse(parts[3], CultureInfo.InvariantCulture)),
            float.Parse(parts[^1], CultureInfo.InvariantCulture));
    }

    private static Formation BuildEightPointFormation() {
        var formation = new Formation { Name = "Circle" };
        for (var i = 0; i < 8; i++) {
            formation.Points.Add(new FormationPoint {
                Offset = new Vector3(i, 0f, 0f),
                Cids = [(ulong)(100 + i)],
            });
        }

        return formation;
    }
}
