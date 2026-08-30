using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace MasterOfPuppets;

public class Command {
    [JsonPropertyName("cids")]
    public List<ulong> Cids = new List<ulong>();

    [JsonPropertyName("groupIds")]
    public List<string> GroupIds = new List<string>();

    [JsonPropertyName("actions")]
    public string Actions = string.Empty;

    public Command Clone(bool includeCids = true) {
        return new Command {
            Cids = includeCids
                ? (this.Cids?.ToList() ?? new List<ulong>())
                : new List<ulong>(),
            GroupIds = includeCids
                ? (this.GroupIds?.ToList() ?? new List<string>())
                : new List<string>(),
            Actions = this.Actions
        };
    }

    public HashSet<ulong> GetEffectiveCids(IReadOnlyList<CidGroup>? groups = null) {
        var result = new HashSet<ulong>(Cids);

        if (groups != null && GroupIds.Count > 0) {
            foreach (var groupName in GroupIds) {
                var group = groups.FirstOrDefault(g => g.Name == groupName);
                if (group != null) {
                    foreach (var cid in group.Cids)
                        result.Add(cid);
                }
            }
        }

        return result;
    }

    public void SanitizeActionsText() {
        if (string.IsNullOrWhiteSpace(this.Actions)) return;

        var lines = this.Actions
            .Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Trim())
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .Select(line => Regex.Replace(line, @"\s+", " "))
            .ToList();

        // remove /mop run
        lines = lines
            .Where(line => !line.StartsWith("/mop run", StringComparison.OrdinalIgnoreCase))
            .ToList();

        // -----------------------------
        // LOOP LOGIC
        // -----------------------------
        int startIndex = lines.FindLastIndex(l =>
            l.StartsWith("/moploopstart", StringComparison.OrdinalIgnoreCase));

        int endIndex = lines.FindLastIndex(l =>
            l.StartsWith("/moploopend", StringComparison.OrdinalIgnoreCase));

        if (startIndex != -1 && endIndex != -1 && endIndex > startIndex) {
            // keep last valid block
            lines = lines
                .Where((line, idx) =>
                    (!line.StartsWith("/moploopstart", StringComparison.OrdinalIgnoreCase) &&
                     !line.StartsWith("/moploopend", StringComparison.OrdinalIgnoreCase))
                    ||
                    (idx >= startIndex && idx <= endIndex))
                .ToList();

            // remove standalone /moploop if block exists
            lines = lines
                .Where(line => !line.StartsWith("/moploop", StringComparison.OrdinalIgnoreCase)
                               || line.StartsWith("/moploopstart", StringComparison.OrdinalIgnoreCase)
                               || line.StartsWith("/moploopend", StringComparison.OrdinalIgnoreCase))
                .ToList();
        } else {
            // fallback - keep only last /moploop
            int lastLoopIndex = lines.FindLastIndex(l =>
                l.StartsWith("/moploop", StringComparison.OrdinalIgnoreCase));

            if (lastLoopIndex != -1) {
                lines = lines
                    .Where((line, idx) =>
                        !line.StartsWith("/moploop", StringComparison.OrdinalIgnoreCase)
                        || idx == lastLoopIndex)
                    .ToList();
            }
        }

        this.Actions = string.Join("\n", lines);
    }

    public static List<string> PreprocessLines(string text) {
        // remove /* ... */
        text = Regex.Replace(text, @"/\*.*?\*/", string.Empty, RegexOptions.Singleline);

        return text
            .Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries)
            .Select(l => l.Trim())
            .Select(l => {
                int idx = l.IndexOf('#');
                return idx >= 0 ? l[..idx].Trim() : l;
            })
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .ToList();
    }

    public static Dictionary<string, string> ExtractVariables(IEnumerable<string> lines) {
        // var vars = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var vars = new Dictionary<string, string>();

        var regex = new Regex(
            @"^\$(?<name>[A-Za-z_]\w*)\s*=\s*(?<value>""[^""]*""|'[^']*'|.+?)\s*$",
            RegexOptions.Compiled
        );

        foreach (var line in lines) {
            var match = regex.Match(line);
            if (!match.Success)
                continue;

            var name = match.Groups["name"].Value;
            var value = match.Groups["value"].Value.Trim();

            // remove quotes if present
            if ((value.StartsWith("\"") && value.EndsWith("\"")) ||
                (value.StartsWith("'") && value.EndsWith("'")))
                value = value[1..^1];

            vars[name] = value;
        }

        return vars;
    }

    internal static List<string> RemoveVariableDefinitions(IEnumerable<string> lines) {
        return lines
            .Where(l => !Regex.IsMatch(l, @"^\$[A-Za-z_]\w*\s*="))
            .ToList();
    }

    internal static string[] SubstituteVariables(
        IEnumerable<string> lines,
        Dictionary<string, string> variables
    ) {
        var result = new List<string>();

        foreach (var line in lines) {
            var resolved = line;

            for (int pass = 0; pass < 5; pass++) {
                var prev = resolved;
                foreach (var (key, value) in variables) {
                    resolved = Regex.Replace(
                        resolved,
                        $@"\${Regex.Escape(key)}\b",
                        value
                    );
                }

                if (string.Equals(resolved, prev, StringComparison.Ordinal))
                    break;
            }

            result.Add(resolved);
        }

        return result.ToArray();
    }

    /// <summary>
    /// Resolves arithmetic expressions inside variable definitions. A variable whose
    /// value is (or references other variables that make it) a finite arithmetic
    /// expression is replaced with its computed result, e.g.
    /// <c>$totalWait = 7 * $interval</c> becomes <c>$totalWait = 5.6</c> once
    /// <c>$interval = 0.8</c> is substituted and evaluated. Non-numeric values such as
    /// <c>/clap</c> or <c>some name</c> are left untouched.
    /// </summary>
    internal static Dictionary<string, string> ResolveVariableExpressions(Dictionary<string, string> variables) {
        if (variables == null || variables.Count == 0)
            return variables;

        // Iterate so forward references ($a = $b where $b is defined later) settle too.
        for (int pass = 0; pass < 5; pass++) {
            bool changed = false;
            foreach (var key in variables.Keys.ToList()) {
                var resolved = variables[key];

                // Substitute sibling variable references into this value (multi-pass).
                for (int inner = 0; inner < 5; inner++) {
                    var prev = resolved;
                    foreach (var (k, v) in variables) {
                        resolved = Regex.Replace(resolved, $@"\${Regex.Escape(k)}\b", v);
                    }
                    if (string.Equals(resolved, prev, StringComparison.Ordinal))
                        break;
                }

                if (MathExpressionEvaluator.TryEvaluate(resolved, out string evaluated)) {
                    if (!string.Equals(evaluated, variables[key], StringComparison.Ordinal)) {
                        variables[key] = evaluated;
                        changed = true;
                    }
                }
            }
            if (!changed)
                break;
        }

        return variables;
    }

    private static Dictionary<string, string> MergeVariables(
        Dictionary<string, string>? runtimeVars,
        Dictionary<string, string>? macroVars,
        Dictionary<string, string> commandVars
    ) {
        var result = new Dictionary<string, string>();

        if (runtimeVars != null) {
            foreach (var (k, v) in runtimeVars)
                result[k] = v;
        }

        if (macroVars != null) {
            foreach (var (k, v) in macroVars)
                result[k] = v;
        }

        // local var overwrite macro var
        foreach (var (k, v) in commandVars)
            result[k] = v;

        return result;
    }

    public string[] GetActionList(
        Dictionary<string, string>? macroVariables = null,
        Dictionary<string, string>? runtimeVariables = null,
        Dictionary<string, string>? inlineOverrides = null) {
        if (string.IsNullOrWhiteSpace(Actions))
            return Array.Empty<string>();

        var lines = PreprocessLines(Actions);

        var commandVars = ExtractVariables(lines);

        var mergedVars = MergeVariables(runtimeVariables, macroVariables, commandVars);

        if (inlineOverrides != null)
            foreach (var (k, v) in inlineOverrides) mergedVars[k] = v;

        ResolveVariableExpressions(mergedVars);

        var actionLines = RemoveVariableDefinitions(lines);

        return SubstituteVariables(actionLines, mergedVars);
    }

    internal MacroExecutionPlan CreateExecutionPlan(
        Dictionary<string, string>? macroVariables = null,
        Dictionary<string, string>? runtimeVariables = null,
        Dictionary<string, string>? inlineOverrides = null) {
        if (string.IsNullOrWhiteSpace(Actions))
            return new MacroExecutionPlan(Array.Empty<string>());

        var lines = PreprocessLines(Actions);
        var commandVars = ExtractVariables(lines);
        var mergedVars = MergeVariables(runtimeVariables, macroVariables, commandVars);

        if (inlineOverrides != null)
            foreach (var (key, value) in inlineOverrides)
                mergedVars[key] = value;

        return new MacroExecutionPlan(RemoveVariableDefinitions(lines).ToArray(), mergedVars);
    }
}

public class Macro {
    [JsonPropertyName("name")]
    public string Name = string.Empty;

    [JsonPropertyName("tags")]
    public List<string> Tags = new List<string>();

    [JsonPropertyName("color")]
    public Vector4 Color = new Vector4(1f, 1f, 1f, 1f);

    [JsonPropertyName("iconId")]
    public uint IconId = 0;

    [JsonPropertyName("commands")]
    public List<Command> Commands = new List<Command>();

    [JsonPropertyName("variables")]
    public string Variables = string.Empty;

    public Macro Clone(bool includeCids = true) {
        return new Macro {
            Name = this.Name,
            Tags = this.Tags?.ToList() ?? new List<string>(),
            Color = this.Color,
            IconId = this.IconId,
            Variables = this.Variables,
            Commands = this.Commands
                .Select(cmd => cmd.Clone(includeCids))
                .ToList(),
        };
    }

    private Dictionary<string, string> GetMacroVariables() {
        if (string.IsNullOrWhiteSpace(Variables))
            return new Dictionary<string, string>();

        var lines = Command.PreprocessLines(Variables);
        return Command.ExtractVariables(lines);
    }

    public string[] GetCidActions(
        ulong cid,
        IReadOnlyList<CidGroup>? groups = null,
        Dictionary<string, string>? inlineVars = null,
        MacroRuntimeVariables? runtimeVariables = null) {
        return CreateCidExecutionPlan(cid, groups, inlineVars, runtimeVariables).ResolveAllActions();
    }

    public MacroExecutionPlan CreateCidExecutionPlan(
        ulong cid,
        IReadOnlyList<CidGroup>? groups = null,
        Dictionary<string, string>? inlineVars = null,
        MacroRuntimeVariables? runtimeVariables = null) {
        var macroVars = GetMacroVariables();
        var runtimeVars = runtimeVariables?.ToDictionary();
        var resolvedInlineVars = runtimeVariables?.ResolveInlinePlaceholders(inlineVars) ?? inlineVars;

        // Resolve which command this characteristic targets and find its position in the
        // macro's ordered command list. Two families of auto-variables are exposed:
        //   $commandIndex/$commandCount      — macro level: which command am I, how many commands.
        //   $assignmentIndex/$assignmentCount — command level: this character's stagger lane among
        //                                       everyone this command targets (direct cids first,
        //                                       then each group's cids, in author listing order,
        //                                       deduplicated by first-seen position). This lets one
        //                                       command with identical text drive a per-character
        //                                       stagger, $offset = $assignmentIndex * $step, with no
        //                                       hardcoded count or index. All four are injected as
        //                                       authoritative overrides so author-declared values
        //                                       cannot break the stagger.
        int commandIndex = -1;
        Command? command = null;
        for (int i = 0; i < Commands.Count; i++) {
            if (Commands[i].GetEffectiveCids(groups).Contains(cid)) {
                commandIndex = i;
                command = Commands[i];
                break;
            }
        }

        if (command == null)
            return new MacroExecutionPlan(Array.Empty<string>());

        // Assignment lane = union of the command's targets in author-listing order.
        var orderedTargets = new List<ulong>();
        var seen = new HashSet<ulong>(); // first-seen position wins when a cid appears more than once
        foreach (var directCid in command.Cids) {
            if (seen.Add(directCid))
                orderedTargets.Add(directCid);
        }
        if (groups != null && command.GroupIds.Count > 0) {
            foreach (var groupName in command.GroupIds) {
                var group = groups.FirstOrDefault(g => g.Name == groupName);
                if (group == null)
                    continue;
                foreach (var groupCid in group.Cids) {
                    if (seen.Add(groupCid))
                        orderedTargets.Add(groupCid);
                }
            }
        }
        var assignmentIndex = orderedTargets.IndexOf(cid);

        // Preserve caller precedence: resolvedInlineVars first (as before), then overlay the
        // auto variables so they are authoritative and cannot be shadowed.
        var mergedInline = new Dictionary<string, string>();
        if (resolvedInlineVars != null)
            foreach (var (k, v) in resolvedInlineVars) mergedInline[k] = v;
        mergedInline["commandIndex"] = commandIndex.ToString();
        mergedInline["commandCount"] = Commands.Count.ToString();
        mergedInline["assignmentIndex"] = assignmentIndex.ToString();
        mergedInline["assignmentCount"] = orderedTargets.Count.ToString();

        return command.CreateExecutionPlan(macroVars, runtimeVars, mergedInline);
    }

    public void SanitizeActions() {
        Commands.ForEach(command => command.SanitizeActionsText());
    }

    public void SanitizeMacroVariablesText() {
        if (string.IsNullOrWhiteSpace(this.Variables))
            return;

        var lines = this.Variables
            .Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Trim())
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .Select(line => Regex.Replace(line, @"\s+", " "))
            .Where(line =>
                line.StartsWith("#") ||
                // var pattern
                Regex.IsMatch(
                    line,
                    @"^\$[A-Za-z_]\w*\s*=\s*.+"
                )
            )
            .ToList();

        this.Variables = string.Join("\n", lines);
    }
}
