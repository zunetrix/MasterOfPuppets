using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;

using MasterOfPuppets.Formations;
using MasterOfPuppets.Movement;
using MasterOfPuppets.Util;

namespace MasterOfPuppets;

public sealed class FormationMacroGeneratorOptions {
    public string MacroName { get; set; } = "Generated Formation";
    public FormationMacroGeneratorMode Mode { get; set; } = FormationMacroGeneratorMode.Movement;
    public int AnchorPointIndex { get; set; }
    public string OriginReference { get; set; } = string.Empty;
    public bool UseFormationMoveCommand { get; set; }
    public string FormationMoveName { get; set; } = string.Empty;
    public ulong OriginContentId { get; set; }
    public float TravelSecondsPerUnit { get; set; } = 0.2f;
    public float GlobalDelaySeconds { get; set; } = 0.25f;
    public bool ClosedLoop { get; set; } = true;
    public int Step { get; set; } = 1;
    public bool Reverse { get; set; }
    public MovementArrivalMode FormationMoveArrivalMode { get; set; } = MovementArrivalMode.Continuous;
    public FormationMoveAnchorMode FormationMoveAnchorMode { get; set; } = FormationMoveAnchorMode.Self;
    public int Precision { get; set; } = 2;
    public bool UseMatchingGroups { get; set; }
    public string PetActionCommand { get; set; } = "/pac \"Place\" <t>";
    public bool LinkPetTraversalToMovement { get; set; } = true;
    public int PetStep { get; set; } = 1;
    public bool PetReverse { get; set; }
    public bool TransformRelativeMovesByOriginRotation { get; set; }
    public bool EmitRelativeMoveFacing { get; set; }
    public float OriginRotationRadians { get; set; }
}

public enum FormationMacroGeneratorMode {
    Movement,
    PetPlacement,
    MovementAndPetPlacement,
}

public sealed class FormationMacroGenerationResult {
    public Macro Macro { get; init; } = new();
    public List<string> Warnings { get; init; } = [];
}

public static class FormationMacroGenerator {
    public static Macro GenerateLoopMacro(Formation formation, FormationMacroGeneratorOptions options) {
        return GenerateLoopMacroWithDiagnostics(formation, options).Macro;
    }

    public static bool TryResolveAssignedPointIndex(
        Formation formation,
        ulong contentId,
        IReadOnlyList<CidGroup>? groups,
        out int pointIndex) {
        pointIndex = formation.Points.FindIndex(point => point.GetEffectiveCids(groups).Contains(contentId));
        return pointIndex >= 0;
    }

    public static FormationMacroGenerationResult GenerateLoopMacroWithDiagnostics(
        Formation formation,
        FormationMacroGeneratorOptions options,
        IReadOnlyList<CidGroup>? groups = null,
        IReadOnlyList<Character>? characters = null) {
        if (formation.Points.Count == 0)
            return new FormationMacroGenerationResult { Macro = new Macro { Name = options.MacroName } };

        var anchorIndex = Math.Clamp(options.AnchorPointIndex, 0, formation.Points.Count - 1);
        var anchor = formation.Points[anchorIndex];
        var destinations = formation.Points
            .Select((point, index) => new IndexedPoint(index, point))
            .Where(item => item.Index != anchorIndex)
            .ToList();

        var macro = new Macro {
            Name = options.MacroName,
            Commands = [],
        };
        var warnings = new List<string>();

        if (destinations.Count == 0)
            return new FormationMacroGenerationResult { Macro = macro, Warnings = warnings };

        if (options.Mode is FormationMacroGeneratorMode.Movement or FormationMacroGeneratorMode.MovementAndPetPlacement) {
            if (options.UseFormationMoveCommand)
                AddFormationMoveCommand(macro, destinations, anchor, formation, options, warnings);
            else
                AddMovementCommands(macro, destinations, anchor, options, groups);
        }

        if (options.Mode is FormationMacroGeneratorMode.PetPlacement or FormationMacroGeneratorMode.MovementAndPetPlacement)
            AddPetPlacementCommands(macro, destinations, anchor, options, groups, characters, warnings);

        return new FormationMacroGenerationResult { Macro = macro, Warnings = warnings };
    }

    private static void AddFormationMoveCommand(
        Macro macro,
        IReadOnlyList<IndexedPoint> destinations,
        FormationPoint anchor,
        Formation formation,
        FormationMacroGeneratorOptions options,
        List<string> warnings) {
        if (options.OriginContentId == 0) {
            warnings.Add("Skipped formation movement command: current character content ID is unavailable.");
            return;
        }

        var formationName = string.IsNullOrWhiteSpace(options.FormationMoveName)
            ? formation.Name
            : options.FormationMoveName.Trim();
        if (string.IsNullOrWhiteSpace(formationName)) {
            warnings.Add("Skipped formation movement command: formation name is unavailable.");
            return;
        }

        var direction = options.Reverse ? "backward" : "forward";
        var step = Math.Max(1, options.Step);
        var waitOrder = SequenceFrom(destinations[0].Index, destinations, step, options.Reverse);
        var waits = SegmentDelays(waitOrder, anchor, options);
        var arrivalMode = options.FormationMoveArrivalMode == MovementArrivalMode.Precise ? "precise" : "continuous";
        var anchorMode = options.FormationMoveAnchorMode == FormationMoveAnchorMode.Target ? " target" : string.Empty;
        var lines = new List<string>();
        for (var i = 0; i < waits.Count; i++) {
            lines.Add($"/mopformationmove \"{ArgumentParser.EscapeQuotedArgument(formationName)}\" {direction} {step} {i} {arrivalMode}{anchorMode}");
            lines.Add($"/mopwait {waits[i].ToString("F2", CultureInfo.InvariantCulture)}");
        }

        lines.Add("/moploop");
        macro.Commands.Add(new Command {
            Cids = [options.OriginContentId],
            GroupIds = [],
            Actions = string.Join("\n", lines),
        });
    }

    private static void AddMovementCommands(
        Macro macro,
        IReadOnlyList<IndexedPoint> destinations,
        FormationPoint anchor,
        FormationMacroGeneratorOptions options,
        IReadOnlyList<CidGroup>? groups) {
        var step = Math.Max(1, options.Step);
        foreach (var start in destinations) {
            var assignment = BuildAssignment(start.Point, groups, options.UseMatchingGroups);
            if (assignment.IsEmpty)
                continue;

            var order = SequenceFrom(start.Index, destinations, step, options.Reverse);
            var waits = SegmentDelays(order, anchor, options);
            var lines = new List<string>();

            for (int i = 0; i < order.Count; i++) {
                var point = order[i].Point;
                var (relative, facing) = BuildRelativeMove(anchor, point, options);
                var line =
                    "/mopmoverelativeto "
                    + $"{Format(relative.X, options.Precision)} "
                    + $"{Format(relative.Y, options.Precision)} "
                    + $"{Format(relative.Z, options.Precision)} "
                    + $"\"{options.OriginReference}\"";
                if (facing.HasValue)
                    line += $" {Format(facing.Value, options.Precision)}";

                lines.Add(line);
                lines.Add($"/mopwait {waits[i].ToString("F2", CultureInfo.InvariantCulture)}");
            }

            lines.Add("/moploop");
            macro.Commands.Add(new Command {
                Cids = assignment.Cids,
                GroupIds = assignment.GroupIds,
                Actions = string.Join("\n", lines),
            });
        }
    }

    private static (Vector3 Offset, float? FacingDegrees) BuildRelativeMove(
        FormationPoint anchor,
        FormationPoint point,
        FormationMacroGeneratorOptions options) {
        if (!options.TransformRelativeMovesByOriginRotation)
            return (point.Offset - anchor.Offset, null);

        var (position, rotation) = FormationMath.GetMopRelativeWorld(
            anchor,
            point,
            Vector3.Zero,
            options.OriginRotationRadians);
        var facing = options.EmitRelativeMoveFacing
            ? FormationMath.NormalizeDegrees(rotation * 180f / MathF.PI)
            : (float?)null;
        return (position, facing);
    }

    private static void AddPetPlacementCommands(
        Macro macro,
        IReadOnlyList<IndexedPoint> destinations,
        FormationPoint anchor,
        FormationMacroGeneratorOptions options,
        IReadOnlyList<CidGroup>? groups,
        IReadOnlyList<Character>? characters,
        List<string> warnings) {
        var step = Math.Max(1, options.LinkPetTraversalToMovement ? options.Step : options.PetStep);
        var reverse = options.LinkPetTraversalToMovement ? options.Reverse : options.PetReverse;
        foreach (var start in destinations) {
            var assignment = BuildAssignment(start.Point, groups, options.UseMatchingGroups);
            if (assignment.IsEmpty)
                continue;

            var order = SequenceFrom(start.Index, destinations, step, reverse);
            var waits = SegmentDelays(order, anchor, options);
            var lines = new List<string>();

            for (int i = 0; i < order.Count; i++) {
                var target = order[i];
                if (!TryGetSinglePointTargetName(target, groups, characters, out var targetName, out var warning)) {
                    warnings.Add(warning);
                    continue;
                }

                lines.Add($"/moptarget \"{ArgumentParser.EscapeQuotedArgument(targetName)}\"");
                lines.Add(options.PetActionCommand);
                lines.Add($"/mopwait {waits[i].ToString("F2", CultureInfo.InvariantCulture)}");
            }

            if (lines.Count == 0)
                continue;

            lines.Add("/moploop");
            macro.Commands.Add(new Command {
                Cids = assignment.Cids,
                GroupIds = assignment.GroupIds,
                Actions = string.Join("\n", lines),
            });
        }
    }

    private static CommandAssignment BuildAssignment(
        FormationPoint point,
        IReadOnlyList<CidGroup>? groups,
        bool useMatchingGroups) {
        var cids = point.Cids.ToList();
        var groupIds = point.GroupIds.ToList();

        if (useMatchingGroups && cids.Count > 0 && groupIds.Count == 0 && groups != null) {
            var matchingGroup = FindExactMatchingGroup(cids, groups);
            if (matchingGroup != null) {
                cids.Clear();
                groupIds.Add(matchingGroup.Name);
            }
        }

        return new CommandAssignment(cids, groupIds);
    }

    private static CidGroup? FindExactMatchingGroup(IReadOnlyCollection<ulong> cids, IReadOnlyList<CidGroup> groups) {
        var cidSet = cids.ToHashSet();
        return groups.FirstOrDefault(group => group.Cids.ToHashSet().SetEquals(cidSet));
    }

    private static bool TryGetSinglePointTargetName(
        IndexedPoint point,
        IReadOnlyList<CidGroup>? groups,
        IReadOnlyList<Character>? characters,
        out string targetName,
        out string warning) {
        targetName = string.Empty;
        warning = string.Empty;

        var effectiveCids = point.Point.GetEffectiveCids(groups).ToList();
        if (effectiveCids.Count != 1) {
            warning = $"Skipped pet target for point {point.Index + 1}: expected exactly one assigned character, found {effectiveCids.Count}.";
            return false;
        }

        var cid = effectiveCids[0];
        var characterName = characters?.FirstOrDefault(character => character.Cid == cid)?.Name;
        if (string.IsNullOrWhiteSpace(characterName)) {
            warning = $"Skipped pet target for point {point.Index + 1}: character {cid} is not in the character list.";
            return false;
        }

        targetName = characterName;
        return true;
    }

    private static List<IndexedPoint> SequenceFrom(
        int startFormationIndex,
        IReadOnlyList<IndexedPoint> destinations,
        int step,
        bool reverse) {
        var startListIndex = destinations.ToList().FindIndex(item => item.Index == startFormationIndex);
        if (startListIndex < 0)
            startListIndex = 0;

        var sequence = new List<IndexedPoint>();
        var seen = new HashSet<int>();
        var idx = startListIndex;
        var direction = reverse ? -1 : 1;

        while (seen.Add(idx)) {
            sequence.Add(destinations[idx]);
            idx = PositiveMod(idx + direction * step, destinations.Count);
        }

        while (sequence.Count < destinations.Count)
            sequence.AddRange(sequence);

        return sequence.Take(destinations.Count).ToList();
    }

    private static List<float> SegmentDelays(
        IReadOnlyList<IndexedPoint> order,
        FormationPoint anchor,
        FormationMacroGeneratorOptions options) {
        var delays = new List<float>();
        for (int i = 0; i < order.Count; i++) {
            if (!options.ClosedLoop && i == order.Count - 1) {
                delays.Add(delays.Count > 0 ? delays[^1] : 0.1f);
                continue;
            }

            var start = order[i].Point.Offset - anchor.Offset;
            var end = options.ClosedLoop
                ? order[(i + 1) % order.Count].Point.Offset - anchor.Offset
                : order[i + 1].Point.Offset - anchor.Offset;
            var distance = Distance2D(start, end);
            var delay = options.TravelSecondsPerUnit * distance - options.GlobalDelaySeconds;
            delays.Add(MathF.Round(MathF.Max(0.1f, delay), 2, MidpointRounding.ToEven));
        }
        return delays;
    }

    private static float Distance2D(Vector3 a, Vector3 b) =>
        new Vector2(a.X - b.X, a.Z - b.Z).Length();

    private static int PositiveMod(int value, int mod) =>
        (value % mod + mod) % mod;

    private static string Format(float value, int precision) {
        if (MathF.Abs(value) < 0.000001f)
            value = 0f;
        return value.ToString($"F{Math.Clamp(precision, 0, 6)}", CultureInfo.InvariantCulture);
    }

    private sealed record IndexedPoint(int Index, FormationPoint Point);
    private sealed record CommandAssignment(List<ulong> Cids, List<string> GroupIds) {
        public bool IsEmpty => Cids.Count == 0 && GroupIds.Count == 0;
    }
}
