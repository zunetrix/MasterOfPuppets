using System;
using System.Collections.Generic;

using MasterOfPuppets.Movement;

namespace MasterOfPuppets.Formations;

public enum FormationAnchorKind {
    Default,
    Self,
    Sender,
    Target,
    FocusTarget,
    Named,
}

public enum FormationAnchorFailureKind {
    None,
    NoTargetSelected,
    NoFocusTargetSelected,
    AnchorNameEmpty,
    AnchorNotVisible,
    ConfigurationError,
    Unsupported,
}

public sealed record FormationAnchorReference(FormationAnchorKind Kind, string? Name = null) {
    public static readonly FormationAnchorReference Default = new(FormationAnchorKind.Default);
    public static readonly FormationAnchorReference Self = new(FormationAnchorKind.Self);
    public static readonly FormationAnchorReference Sender = new(FormationAnchorKind.Sender);
    public static readonly FormationAnchorReference Target = new(FormationAnchorKind.Target);
    public static readonly FormationAnchorReference FocusTarget = new(FormationAnchorKind.FocusTarget);

    public static FormationAnchorReference Named(string name) => new(FormationAnchorKind.Named, name);

    public override string ToString() =>
        Kind == FormationAnchorKind.Named ? $"\"{Name}\"" : Kind.ToString().ToLowerInvariant();
}

public sealed record FormationAnchorParseResult(
    FormationAnchorReference Anchor,
    SimpleMovementMode MovementMode,
    string? InvalidArgument,
    FormationAnchorReference? Fallback = null);

public static class FormationAnchorArgumentParser {
    public static FormationAnchorParseResult ParseAnchorAndArrival(
        IEnumerable<string> arguments,
        FormationAnchorReference? defaultAnchor = null,
        SimpleMovementMode defaultMovementMode = SimpleMovementMode.Precise) {
        var anchor = defaultAnchor ?? FormationAnchorReference.Default;
        FormationAnchorReference? fallback = null;
        var movementMode = defaultMovementMode;
        string? invalidArgument = null;

        foreach (var rawArgument in arguments) {
            var argument = rawArgument.Trim();
            if (argument.Length == 0)
                continue;

            if (SimpleInputMovement.TryParseMode(argument, out var parsedMovementMode)) {
                movementMode = parsedMovementMode;
                continue;
            }

            if (argument.Equals("hybrid", StringComparison.OrdinalIgnoreCase)
                || argument.Equals("steered", StringComparison.OrdinalIgnoreCase)) {
                invalidArgument ??= argument;
                continue;
            }

            if (argument.StartsWith("fallback=", StringComparison.OrdinalIgnoreCase)) {
                var fbCandidate = argument["fallback=".Length..].Trim();
                if (fbCandidate.Length == 0 || fbCandidate.Equals("\"\"", StringComparison.Ordinal) || fbCandidate.Equals("''", StringComparison.Ordinal)) {
                    fallback = null;
                } else if (fbCandidate.Equals("default", StringComparison.OrdinalIgnoreCase)) {
                    fallback = FormationAnchorReference.Default;
                } else if (fbCandidate.Equals("self", StringComparison.OrdinalIgnoreCase)) {
                    fallback = FormationAnchorReference.Self;
                } else if (fbCandidate.Equals("sender", StringComparison.OrdinalIgnoreCase)) {
                    fallback = FormationAnchorReference.Sender;
                } else if (fbCandidate.Equals("target", StringComparison.OrdinalIgnoreCase)
                    || fbCandidate.Equals("<t>", StringComparison.OrdinalIgnoreCase)
                    || fbCandidate.Equals("[t]", StringComparison.OrdinalIgnoreCase)) {
                    fallback = FormationAnchorReference.Target;
                } else if (fbCandidate.Equals("ftarget", StringComparison.OrdinalIgnoreCase)
                    || fbCandidate.Equals("focustarget", StringComparison.OrdinalIgnoreCase)
                    || fbCandidate.Equals("<f>", StringComparison.OrdinalIgnoreCase)
                    || fbCandidate.Equals("[f]", StringComparison.OrdinalIgnoreCase)
                    || fbCandidate.Equals("<focus>", StringComparison.OrdinalIgnoreCase)
                    || fbCandidate.Equals("[focus]", StringComparison.OrdinalIgnoreCase)) {
                    fallback = FormationAnchorReference.FocusTarget;
                } else {
                    fallback = FormationAnchorReference.Named(fbCandidate);
                }
                continue;
            }

            var candidate = argument.StartsWith("anchor=", StringComparison.OrdinalIgnoreCase)
                ? argument["anchor=".Length..].Trim()
                : argument;
            if (candidate.Length == 0 || candidate.Equals("\"\"", StringComparison.Ordinal) || candidate.Equals("''", StringComparison.Ordinal)) {
                anchor = defaultAnchor ?? FormationAnchorReference.Default;
                continue;
            }

            if (candidate.Equals("default", StringComparison.OrdinalIgnoreCase)) {
                anchor = FormationAnchorReference.Default;
            } else if (candidate.Equals("self", StringComparison.OrdinalIgnoreCase)) {
                anchor = FormationAnchorReference.Self;
            } else if (candidate.Equals("sender", StringComparison.OrdinalIgnoreCase)) {
                anchor = FormationAnchorReference.Sender;
            } else if (candidate.Equals("target", StringComparison.OrdinalIgnoreCase)
                || candidate.Equals("<t>", StringComparison.OrdinalIgnoreCase)
                || candidate.Equals("[t]", StringComparison.OrdinalIgnoreCase)) {
                anchor = FormationAnchorReference.Target;
            } else if (candidate.Equals("ftarget", StringComparison.OrdinalIgnoreCase)
                || candidate.Equals("focustarget", StringComparison.OrdinalIgnoreCase)
                || candidate.Equals("<f>", StringComparison.OrdinalIgnoreCase)
                || candidate.Equals("[f]", StringComparison.OrdinalIgnoreCase)
                || candidate.Equals("<focus>", StringComparison.OrdinalIgnoreCase)
                || candidate.Equals("[focus]", StringComparison.OrdinalIgnoreCase)) {
                anchor = FormationAnchorReference.FocusTarget;
            } else {
                anchor = FormationAnchorReference.Named(candidate);
            }
        }

        return new FormationAnchorParseResult(anchor, movementMode, invalidArgument, fallback);
    }

    public static string FormatForMacro(FormationAnchorReference anchor) =>
        anchor.Kind switch {
            FormationAnchorKind.Default => string.Empty,
            FormationAnchorKind.Self => "self",
            FormationAnchorKind.Sender => "sender",
            FormationAnchorKind.Target => "target",
            FormationAnchorKind.FocusTarget => "ftarget",
            FormationAnchorKind.Named => $"\"{Util.ArgumentParser.EscapeQuotedArgument(anchor.Name ?? string.Empty)}\"",
            _ => string.Empty,
        };
}
