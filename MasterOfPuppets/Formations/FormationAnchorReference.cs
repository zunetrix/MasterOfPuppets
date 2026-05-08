using System;
using System.Collections.Generic;
using System.Linq;

using MasterOfPuppets.Movement;

namespace MasterOfPuppets.Formations;

public enum FormationAnchorKind {
    Default,
    Self,
    Target,
    Named,
}

public sealed record FormationAnchorReference(FormationAnchorKind Kind, string? Name = null) {
    public static readonly FormationAnchorReference Default = new(FormationAnchorKind.Default);
    public static readonly FormationAnchorReference Self = new(FormationAnchorKind.Self);
    public static readonly FormationAnchorReference Target = new(FormationAnchorKind.Target);

    public static FormationAnchorReference Named(string name) => new(FormationAnchorKind.Named, name);

    public override string ToString() =>
        Kind == FormationAnchorKind.Named ? $"\"{Name}\"" : Kind.ToString().ToLowerInvariant();
}

public sealed record FormationAnchorParseResult(
    FormationAnchorReference Anchor,
    MovementArrivalMode ArrivalMode,
    string? InvalidArgument);

public static class FormationAnchorArgumentParser {
    public static FormationAnchorParseResult ParseAnchorAndArrival(
        IEnumerable<string> arguments,
        FormationAnchorReference? defaultAnchor = null,
        MovementArrivalMode defaultArrivalMode = MovementArrivalMode.Continuous) {
        var anchor = defaultAnchor ?? FormationAnchorReference.Default;
        var arrivalMode = defaultArrivalMode;
        string? invalidArgument = null;

        foreach (var rawArgument in arguments) {
            var argument = rawArgument.Trim();
            if (argument.Length == 0)
                continue;

            if (argument.Equals("precise", StringComparison.OrdinalIgnoreCase)) {
                arrivalMode = MovementArrivalMode.Precise;
                continue;
            }

            if (argument.Equals("continuous", StringComparison.OrdinalIgnoreCase)) {
                arrivalMode = MovementArrivalMode.Continuous;
                continue;
            }

            var candidate = argument.StartsWith("anchor=", StringComparison.OrdinalIgnoreCase)
                ? argument["anchor=".Length..].Trim()
                : argument;
            if (candidate.Length == 0) {
                invalidArgument ??= argument;
                continue;
            }

            if (candidate.Equals("default", StringComparison.OrdinalIgnoreCase)) {
                anchor = FormationAnchorReference.Default;
            } else if (candidate.Equals("self", StringComparison.OrdinalIgnoreCase)) {
                anchor = FormationAnchorReference.Self;
            } else if (candidate.Equals("target", StringComparison.OrdinalIgnoreCase)) {
                anchor = FormationAnchorReference.Target;
            } else {
                anchor = FormationAnchorReference.Named(candidate);
            }
        }

        return new FormationAnchorParseResult(anchor, arrivalMode, invalidArgument);
    }

    public static string FormatForMacro(FormationAnchorReference anchor) =>
        anchor.Kind switch {
            FormationAnchorKind.Default => string.Empty,
            FormationAnchorKind.Self => "self",
            FormationAnchorKind.Target => "target",
            FormationAnchorKind.Named => $"\"{MasterOfPuppets.Util.ArgumentParser.EscapeQuotedArgument(anchor.Name ?? string.Empty)}\"",
            _ => string.Empty,
        };
}
