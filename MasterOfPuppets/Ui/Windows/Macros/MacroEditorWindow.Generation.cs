using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

using Dalamud.Bindings.ImGui;
using Dalamud.Interface.ImGuiNotification;

using MasterOfPuppets.Extensions;
using MasterOfPuppets.Formations;
using MasterOfPuppets.Util.ImGuiExt;

namespace MasterOfPuppets;

public partial class MacroEditorWindow {
    private int _macroGenMode;
    private int _macroGenSource;
    private int _macroGenFormationIndex;
    private float _macroGenTravelSecondsPerUnit = 0.2f;
    private int _macroGenStep = 1;
    private bool _macroGenReverse;
    private string _macroGenPetActionCommand = "/pac \"Place\" <t>";

    private FormationShapeType _macroGenShapeType = FormationShapeType.Circle;
    private int _macroGenShapeCount = 8;
    private int _macroGenLastShapeAssignmentFormationIndex = -1;
    private float _macroGenShapeRadius = 5f;
    private float _macroGenShapeRadius2 = 3f;
    private float _macroGenShapeWidth = 8f;
    private float _macroGenShapeDepth = 4f;
    private float _macroGenShapeSpacing = 1.5f;
    private float _macroGenShapeAngleOffset;
    private int _macroGenShapeParamInt = 4;
    private int _macroGenShapeFaceMode = (int)FormationShapeFaceMode.Inward;

    private static readonly string[] MacroGeneratorModeNames = ["Movement", "Pet Placement", "Movement + Pet Placement"];
    private static readonly string[] MacroGeneratorSourceNames = ["Existing Formation", "Generated Shape"];
    private static readonly string[] MacroGeneratorDirectionNames = ["Forward through point order", "Backward through point order"];

    private void DrawMacroCommandGeneratorModal() {
        ImGui.SetNextWindowSize(new Vector2(520f, 0f), ImGuiCond.FirstUseEver);
        if (!ImGui.BeginPopupModal("Generate Commands##MacroCommandGenerator", ImGuiWindowFlags.AlwaysAutoResize))
            return;

        var formations = Plugin.Config.Formations;
        if (formations.Count == 0) {
            ImGui.TextDisabled("Create or import a formation before generating macro commands.");
            DrawMacroGeneratorCancelButton();
            ImGui.EndPopup();
            return;
        }

        _macroGenFormationIndex = Math.Clamp(_macroGenFormationIndex, 0, formations.Count - 1);
        var selectedFormation = formations[_macroGenFormationIndex];

        ImGui.TextWrapped("This inserts commands for characters assigned to the selected formation. Offsets are measured from the point assigned to your current character.");
        ImGui.Separator();

        var localCid = DalamudApi.PlayerState.ContentId;
        var originResolved = FormationMacroGenerator.TryResolveAssignedPointIndex(selectedFormation, localCid, Plugin.Config.CidsGroups, out var originPointIndex);
        var originLabel = originResolved
            ? FormatMacroGeneratorPointLabel(selectedFormation, originPointIndex)
            : "Current character is not assigned to this formation.";

        DrawMacroGeneratorSectionTitle("Source");

        DrawMacroGeneratorLabel("Mode", "What kind of commands to insert. Existing formation movement inserts one origin command containing one movement line per step. Generated shape movement inserts per-character /mopmoverelativeto commands. Pet Placement targets each destination and runs the pet action.");
        ImGui.SetNextItemWidth(220);
        ImGui.Combo("##macroGenMode", ref _macroGenMode, MacroGeneratorModeNames, MacroGeneratorModeNames.Length);

        DrawMacroGeneratorLabel("Generate from", "Use an existing formation as-is, or generate a new shape while copying character assignments from a formation.");
        ImGui.SetNextItemWidth(220);
        ImGui.Combo("##macroGenSource", ref _macroGenSource, MacroGeneratorSourceNames, MacroGeneratorSourceNames.Length);

        DrawMacroGeneratorLabel(
            _macroGenSource == 0 ? "Formation" : "Character assignments",
            _macroGenSource == 0
                ? "The formation whose points and assigned characters/groups will be used."
                : "The formation used only to decide which characters/groups receive generated shape commands. Assignments are copied by point number.");
        ImGui.SetNextItemWidth(220);
        var formationNames = formations.Select(f => f.Name).ToArray();
        ImGui.Combo("##macroGenFormation", ref _macroGenFormationIndex, formationNames, formationNames.Length);
        selectedFormation = formations[_macroGenFormationIndex];

        originResolved = FormationMacroGenerator.TryResolveAssignedPointIndex(selectedFormation, localCid, Plugin.Config.CidsGroups, out originPointIndex);
        originLabel = originResolved
            ? FormatMacroGeneratorPointLabel(selectedFormation, originPointIndex)
            : "Current character is not assigned to this formation.";

        if (_macroGenSource == 1) {
            ImGui.Separator();
            DrawMacroGeneratorSectionTitle("Shape");
            DrawMacroGeneratorShapeControls(selectedFormation.Points.Count);
        }

        ImGui.Separator();
        if (_macroGenMode != (int)FormationMacroGeneratorMode.PetPlacement) {
            DrawMacroGeneratorSectionTitle("Movement");
            DrawMacroGeneratorMovementControls();
        }

        if (_macroGenMode != (int)FormationMacroGeneratorMode.Movement) {
            DrawMacroGeneratorSectionTitle("Pet Placement");
            DrawMacroGeneratorPetControls();
        }

        DrawMacroGeneratorAdvancedControls();

        ImGui.Separator();

        var preview = PreviewGeneratedMacroCommands(selectedFormation, originResolved, originPointIndex);
        ImGui.TextWrapped($"Origin: {originLabel}");
        ImGui.TextWrapped(preview.Message);
        if (preview.Warnings.Count > 0)
            ImGui.TextDisabled($"{preview.Warnings.Count} warning(s). Insert will show details.");

        var disableInsert = !preview.CanInsert;
        if (disableInsert) ImGui.BeginDisabled();
        if (ImGui.Button("Insert Commands##macroGenInsert", new Vector2(140, 0))) {
            InsertGeneratedMacroCommands(selectedFormation, originPointIndex);
            ImGui.CloseCurrentPopup();
        }
        if (disableInsert) ImGui.EndDisabled();

        ImGui.SameLine();
        DrawMacroGeneratorCancelButton();

        ImGui.EndPopup();
    }

    private static void DrawMacroGeneratorCancelButton() {
        if (ImGui.Button("Cancel##macroGenCancel", new Vector2(100, 0)))
            ImGui.CloseCurrentPopup();
    }

    private void DrawMacroGeneratorMovementControls() {
        var originName = GetLocalPlayerNameWorld();
        DrawMacroGeneratorLabel("Movement origin", "$me and $target are resolved separately by every client that receives a macro. Generated static movement commands use the current local character name directly.");
        ImGui.TextDisabled(string.IsNullOrWhiteSpace(originName) ? "Could not resolve current character." : originName);

        if (_macroGenSource == 0) {
            ImGui.TextDisabled("Existing formations insert one origin command with one movement line per step.");
            ImGuiUtil.HelpMarker("Each /mopformationmove line broadcasts one saved-formation target. Waits are calculated from saved point-to-point path distance using the same cadence as mop_scripts.");
        }

        DrawMacroGenFloat("Seconds per unit", "Movement wait time per yalms/meters of path distance. Higher values move more slowly.", ref _macroGenTravelSecondsPerUnit, 0.01f, 0.01f, 10f, "%.2f");
        DrawMacroGeneratorLabel("Global delay", "Read from Settings > Global delay between actions. It is subtracted from generated /mopwait values because the macro engine applies it after each movement command.");
        ImGui.TextDisabled($"{Plugin.Config.DelayBetweenActions:F2}s");
    }

    private void DrawMacroGeneratorPetControls() {
        DrawMacroGeneratorLabel("Pet action", "Command run after targeting each destination character. Examples: /pac \"Place\" <t> or /moppetbarslot 1.");
        ImGui.SetNextItemWidth(220);
        ImGui.InputText("##macroGenPetAction", ref _macroGenPetActionCommand, 256);
    }

    private void DrawMacroGeneratorShapeControls(int assignmentPointCount) {
        if (_macroGenLastShapeAssignmentFormationIndex != _macroGenFormationIndex) {
            _macroGenShapeCount = Math.Max(1, assignmentPointCount);
            _macroGenLastShapeAssignmentFormationIndex = _macroGenFormationIndex;
        }

        DrawMacroGeneratorLabel("Shape", "The shape to generate. Character assignments are copied from the selected formation by point number.");
        ImGui.SetNextItemWidth(220);
        int shapeTypeInt = (int)_macroGenShapeType;
        if (ImGui.Combo("##macroGenShape", ref shapeTypeInt, FormationShapeGenerator.ShapeNames, FormationShapeGenerator.ShapeNames.Length))
            _macroGenShapeType = (FormationShapeType)shapeTypeInt;

        _macroGenShapeCount = Math.Max(1, _macroGenShapeCount);
        DrawMacroGenInt("Point count", "Number of generated shape points. If this differs from the assignment formation, unmatched points are left unassigned.", ref _macroGenShapeCount, 1, 64);
        if (_macroGenShapeCount != assignmentPointCount)
            ImGui.TextDisabled("Assignments are copied by point index; unmatched shape points are unassigned.");

        DrawMacroGeneratorShapeParameterControls();

        DrawMacroGenFloat("Rotation offset", "Rotates the generated shape before commands are created.", ref _macroGenShapeAngleOffset, 1f, -180f, 180f, "%.0f deg");
        DrawMacroGeneratorLabel("Facing", "How generated points face in the shape. This is preserved if the generated shape is later saved as a formation.");
        ImGui.SetNextItemWidth(160);
        ImGui.Combo("##macroGenShapeFace", ref _macroGenShapeFaceMode, FormationShapeGenerator.FaceModeNames, FormationShapeGenerator.FaceModeNames.Length);
    }

    private void DrawMacroGeneratorShapeParameterControls() {
        switch (_macroGenShapeType) {
            case FormationShapeType.Circle:
            case FormationShapeType.FigureEight:
            case FormationShapeType.Heart:
            case FormationShapeType.Arc:
            case FormationShapeType.Lissajous:
                DrawMacroGenFloat("Radius / scale", "Overall size of the generated shape.", ref _macroGenShapeRadius);
                break;
            case FormationShapeType.Rectangle:
                DrawMacroGenFloat("Width", "Horizontal size of the generated rectangle.", ref _macroGenShapeWidth);
                DrawMacroGenFloat("Depth", "Vertical size of the generated rectangle.", ref _macroGenShapeDepth);
                break;
            case FormationShapeType.Line:
            case FormationShapeType.Chevron:
                DrawMacroGenFloat("Spacing", "Distance between neighboring generated points.", ref _macroGenShapeSpacing);
                break;
            case FormationShapeType.Cross:
                DrawMacroGenFloat("Spacing", "Distance between neighboring generated points.", ref _macroGenShapeSpacing);
                DrawMacroGenFloat("Arm length", "Length of each cross arm.", ref _macroGenShapeWidth);
                break;
            case FormationShapeType.StaggeredLine:
            case FormationShapeType.Zigzag:
                DrawMacroGenFloat("Step spacing", "Distance between neighboring generated points along the path.", ref _macroGenShapeSpacing);
                DrawMacroGenFloat("Amplitude / depth", "Side-to-side or front-to-back offset of the generated points.", ref _macroGenShapeRadius);
                break;
            case FormationShapeType.Spiral:
                DrawMacroGenFloat("Radial step", "How quickly the spiral moves away from the center.", ref _macroGenShapeRadius);
                DrawMacroGenFloat("Rotations", "Number of turns in the spiral.", ref _macroGenShapeRadius2);
                break;
            case FormationShapeType.LogarithmicSpiral:
                DrawMacroGenFloat("A parameter", "Base scale of the logarithmic spiral.", ref _macroGenShapeRadius);
                DrawMacroGenFloat("B parameter", "Growth rate of the logarithmic spiral.", ref _macroGenShapeRadius2);
                break;
            case FormationShapeType.Polygon:
            case FormationShapeType.StarPolygon:
                DrawMacroGenFloat("Radius", "Distance from the center to the generated polygon points.", ref _macroGenShapeRadius);
                DrawMacroGenInt("Sides", "Number of polygon sides used to generate the path.", ref _macroGenShapeParamInt, 3, 32);
                break;
            case FormationShapeType.Rose:
                DrawMacroGenFloat("Radius", "Overall size of the rose curve.", ref _macroGenShapeRadius);
                DrawMacroGenInt("Petals", "Number of rose petals in the generated path.", ref _macroGenShapeParamInt, 1, 32);
                break;
            case FormationShapeType.Star:
                DrawMacroGenFloat("Outer radius", "Distance from center to outer star points.", ref _macroGenShapeRadius);
                DrawMacroGenFloat("Inner radius", "Distance from center to inner star points.", ref _macroGenShapeRadius2);
                DrawMacroGenInt("Points", "Number of star tips.", ref _macroGenShapeParamInt, 2, 32);
                break;
            case FormationShapeType.SpokedWheel:
                DrawMacroGenFloat("Outer radius", "Distance from center to outer wheel points.", ref _macroGenShapeRadius);
                DrawMacroGenFloat("Inner radius", "Distance from center to inner wheel points.", ref _macroGenShapeRadius2);
                DrawMacroGenInt("Spokes", "Number of wheel spokes.", ref _macroGenShapeParamInt, 1, 32);
                break;
            case FormationShapeType.Ellipse:
                DrawMacroGenFloat("Radius X", "Horizontal radius of the ellipse.", ref _macroGenShapeRadius);
                DrawMacroGenFloat("Radius Z", "Vertical radius of the ellipse.", ref _macroGenShapeRadius2);
                break;
            case FormationShapeType.SineWave:
                DrawMacroGenFloat("Amplitude", "Height of the sine wave.", ref _macroGenShapeRadius);
                DrawMacroGenFloat("Wavelength", "Distance between wave repeats.", ref _macroGenShapeRadius2);
                DrawMacroGenFloat("Total length", "Total horizontal length of the generated wave.", ref _macroGenShapeWidth);
                break;
            case FormationShapeType.Grid:
                DrawMacroGenFloat("X spacing", "Horizontal spacing between grid points.", ref _macroGenShapeSpacing);
                DrawMacroGenFloat("Z spacing", "Vertical spacing between grid points.", ref _macroGenShapeWidth);
                DrawMacroGenInt("Columns", "Number of grid columns.", ref _macroGenShapeParamInt, 1, 32);
                break;
            case FormationShapeType.Hypotrochoid:
                DrawMacroGenFloat("R", "Outer radius parameter for the hypotrochoid.", ref _macroGenShapeRadius);
                DrawMacroGenFloat("r", "Inner radius parameter for the hypotrochoid.", ref _macroGenShapeRadius2);
                DrawMacroGenFloat("d", "Pen distance parameter for the hypotrochoid.", ref _macroGenShapeWidth);
                DrawMacroGenFloat("Rotations", "Number of rotations used to trace the curve.", ref _macroGenShapeDepth);
                break;
            case FormationShapeType.RingWithCenter:
                DrawMacroGenFloat("Radius", "Radius of the outer ring.", ref _macroGenShapeRadius);
                DrawMacroGenInt("Center points", "Number of generated points placed in the center before ring points.", ref _macroGenShapeParamInt, 1, 16);
                break;
        }
    }

    private void DrawMacroGeneratorAdvancedControls() {
        if (!ImGui.CollapsingHeader("Advanced##macroGenAdvanced"))
            return;

        DrawMacroGenInt("Point stride", "Skip amount through formation point order. The default of 1 visits every point in saved order; 2 visits every second point.", ref _macroGenStep, 1, 64);

        DrawMacroGeneratorLabel("Path direction", "Order each character visits points. Forward follows saved point order; backward follows the reverse saved point order.");
        var direction = _macroGenReverse ? 1 : 0;
        ImGui.SetNextItemWidth(220);
        if (ImGui.Combo("##macroGenDirection", ref direction, MacroGeneratorDirectionNames, MacroGeneratorDirectionNames.Length))
            _macroGenReverse = direction == 1;

        ImGui.TextDisabled("Generated commands always loop. The final wait is timed back to the first destination before /moploop.");
    }

    private static void DrawMacroGeneratorSectionTitle(string label) {
        ImGui.Text(label);
        ImGui.Separator();
    }

    private static void DrawMacroGeneratorLabel(string label, string help) {
        ImGui.Text(label);
        ImGuiUtil.HelpMarker(help);
        ImGui.SameLine(180);
    }

    private static void DrawMacroGenFloat(string label, string help, ref float value, float speed = 0.1f, float min = 0f, float max = 100f, string format = "%.1f") {
        DrawMacroGeneratorLabel(label, help);
        ImGui.SetNextItemWidth(140);
        ImGui.DragFloat($"##macroGen_{label}", ref value, speed, min, max, format);
    }

    private static void DrawMacroGenInt(string label, string help, ref int value, int min, int max) {
        DrawMacroGeneratorLabel(label, help);
        ImGui.SetNextItemWidth(110);
        ImGui.DragInt($"##macroGen_{label}", ref value, 0.2f, min, max);
    }

    private MacroCommandGeneratorPreview PreviewGeneratedMacroCommands(
        Formation assignmentFormation,
        bool originResolved,
        int originPointIndex) {
        if (!originResolved)
            return new MacroCommandGeneratorPreview(false, "Current character must be assigned to the selected formation before commands can be generated.", []);

        var sourceFormation = BuildMacroGeneratorSourceFormation(assignmentFormation);
        if (!sourceFormation.Points.IndexExists(originPointIndex))
            return new MacroCommandGeneratorPreview(false, "The generated shape does not have the point assigned to your current character.", []);

        var movementEnabled = _macroGenMode != (int)FormationMacroGeneratorMode.PetPlacement;
        if (_macroGenSource == 1 && movementEnabled && string.IsNullOrWhiteSpace(GetLocalPlayerNameWorld()))
            return new MacroCommandGeneratorPreview(false, "Current character name/world must be resolved before generated shape movement commands can be created.", []);

        var result = GenerateMacroCommands(sourceFormation, originPointIndex);
        if (result.Macro.Commands.Count == 0)
            return new MacroCommandGeneratorPreview(false, "No commands can be generated from the currently assigned formation points.", result.Warnings);

        var insertIndex = MacroItem.Commands.IndexExists(SelectedCommandIndex)
            ? SelectedCommandIndex + 1
            : MacroItem.Commands.Count;
        var insertionText = MacroItem.Commands.Count == 0
            ? "at the start of this macro"
            : $"after command {insertIndex}";

        var message = $"Will insert {result.Macro.Commands.Count} command(s) {insertionText}.";
        if (_macroGenSource == 0 && movementEnabled) {
            var petCommands = Math.Max(0, result.Macro.Commands.Count - 1);
            var movementSteps = Math.Max(0, sourceFormation.Points.Count - 1);
            message = _macroGenMode == (int)FormationMacroGeneratorMode.Movement
                ? $"Will insert 1 origin movement command containing {movementSteps} movement step(s) {insertionText}."
                : $"Will insert 1 origin movement command containing {movementSteps} movement step(s) + {petCommands} pet command(s) {insertionText}.";
        } else if (_macroGenSource == 1 && movementEnabled) {
            message = $"Will insert {result.Macro.Commands.Count} generated shape command(s) {insertionText}.";
        }

        return new MacroCommandGeneratorPreview(true, message, result.Warnings);
    }

    private FormationMacroGenerationResult GenerateMacroCommands(Formation sourceFormation, int originPointIndex) {
        var useFormationMove = _macroGenSource == 0
            && _macroGenMode != (int)FormationMacroGeneratorMode.PetPlacement;
        var originName = GetLocalPlayerNameWorld();
        var result = FormationMacroGenerator.GenerateLoopMacroWithDiagnostics(
            sourceFormation,
            new FormationMacroGeneratorOptions {
                MacroName = MacroItem.Name,
                Mode = (FormationMacroGeneratorMode)_macroGenMode,
                AnchorPointIndex = originPointIndex,
                OriginReference = originName,
                UseFormationMoveCommand = useFormationMove,
                FormationMoveName = sourceFormation.Name,
                OriginContentId = DalamudApi.PlayerState.ContentId,
                TravelSecondsPerUnit = _macroGenTravelSecondsPerUnit,
                GlobalDelaySeconds = Math.Max(0f, (float)Plugin.Config.DelayBetweenActions),
                Step = _macroGenStep,
                Reverse = _macroGenReverse,
                ClosedLoop = true,
                UseMatchingGroups = false,
                PetActionCommand = string.IsNullOrWhiteSpace(_macroGenPetActionCommand) ? "/pac \"Place\" <t>" : _macroGenPetActionCommand.Trim(),
            },
            Plugin.Config.CidsGroups,
            Plugin.Config.Characters);

        return result;
    }

    private void InsertGeneratedMacroCommands(Formation assignmentFormation, int originPointIndex) {
        var sourceFormation = BuildMacroGeneratorSourceFormation(assignmentFormation);
        var result = GenerateMacroCommands(sourceFormation, originPointIndex);

        if (result.Macro.Commands.Count == 0) {
            ImGuiModalDialog.Show("No Commands Generated", "No assigned formation points produced commands.", ("OK", () => { }));
            return;
        }

        var insertIndex = MacroItem.Commands.IndexExists(SelectedCommandIndex)
            ? SelectedCommandIndex + 1
            : MacroItem.Commands.Count;
        MacroItem.Commands.InsertRange(insertIndex, result.Macro.Commands.Select(command => command.Clone()).ToList());
        SelectedCommandIndex = insertIndex;

        if (result.Warnings.Count > 0) {
            var warningText = string.Join("\n", result.Warnings.Distinct().Take(8));
            if (result.Warnings.Count > 8)
                warningText += $"\n...and {result.Warnings.Count - 8} more.";
            ImGuiModalDialog.Show("Commands Generated With Warnings", warningText, ("OK", () => { }));
        } else {
            DalamudApi.ShowNotification($"Inserted {result.Macro.Commands.Count} generated commands", NotificationType.Success, 5000);
        }
    }

    private Formation BuildMacroGeneratorSourceFormation(Formation assignmentFormation) {
        if (_macroGenSource == 0)
            return assignmentFormation.Clone();

        var generated = new Formation {
            Name = $"{FormationShapeGenerator.ShapeNames[(int)_macroGenShapeType]} Commands",
            Points = FormationShapeGenerator.Generate(new FormationShapeSpec {
                Type = _macroGenShapeType,
                Count = _macroGenShapeCount,
                Radius = _macroGenShapeRadius,
                Radius2 = _macroGenShapeRadius2,
                Width = _macroGenShapeWidth,
                Depth = _macroGenShapeDepth,
                Spacing = _macroGenShapeSpacing,
                AngleOffsetDegrees = _macroGenShapeAngleOffset,
                IntParameter = _macroGenShapeParamInt,
                FaceMode = (FormationShapeFaceMode)_macroGenShapeFaceMode,
            }),
        };

        for (var i = 0; i < generated.Points.Count && i < assignmentFormation.Points.Count; i++) {
            generated.Points[i].Cids = [.. assignmentFormation.Points[i].Cids];
            generated.Points[i].GroupIds = [.. assignmentFormation.Points[i].GroupIds];
        }

        return generated;
    }

    private string FormatMacroGeneratorPointLabel(Formation formation, int pointIndex) {
        if (!formation.Points.IndexExists(pointIndex))
            return $"Point {pointIndex + 1}";

        var point = formation.Points[pointIndex];
        var names = new List<string>();
        names.AddRange(point.Cids
            .Select(cid => Plugin.Config.Characters.FirstOrDefault(character => character.Cid == cid)?.Name ?? cid.ToString())
            .Where(name => !string.IsNullOrWhiteSpace(name)));
        names.AddRange(point.GroupIds.Select(groupId => $"Group: {groupId}"));

        return names.Count == 0
            ? $"Point {pointIndex + 1}"
            : $"Point {pointIndex + 1}, {string.Join(", ", names)}";
    }

    private static string GetLocalPlayerNameWorld() {
        try {
            if (string.IsNullOrWhiteSpace(DalamudApi.PlayerState.CharacterName))
                return string.Empty;

            var world = DalamudApi.PlayerState.HomeWorld.Value.Name.ToString();
            return string.IsNullOrWhiteSpace(world)
                ? DalamudApi.PlayerState.CharacterName
                : $"{DalamudApi.PlayerState.CharacterName}@{world}";
        } catch {
            return string.Empty;
        }
    }

    private sealed record MacroCommandGeneratorPreview(bool CanInsert, string Message, List<string> Warnings);
}
