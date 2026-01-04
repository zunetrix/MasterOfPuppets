using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

public class Character {
    [JsonPropertyName("cid")]
    public ulong Cid;

    [JsonPropertyName("name")]
    public string Name = string.Empty;
}

public class CidGroup {
    [JsonPropertyName("name")]
    public string Name = string.Empty;

    [JsonPropertyName("cids")]
    public List<ulong> Cids = new List<ulong>();
}

public class Command {
    [JsonPropertyName("cids")]
    public List<ulong> Cids = new List<ulong>();

    [JsonPropertyName("actions")]
    public string Actions = string.Empty;

    public Command Clone(bool includeCids = true) {
        return new Command {
            Cids = includeCids
                ? (this.Cids?.ToList() ?? new List<ulong>())
                : new List<ulong>(),
            Actions = this.Actions
        };
    }

    public void SanitizeActionsText() {
        if (string.IsNullOrWhiteSpace(this.Actions)) return;

        var lines = this.Actions
            .Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Trim())
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .Select(line => Regex.Replace(line, @"\s+", " "))
            .ToList();

        lines = lines
       .Where(line => !line.StartsWith("/mop run", StringComparison.OrdinalIgnoreCase))
       .ToList();

        // keep only the last loop action
        int lastLoopIndex = lines.FindLastIndex(l => l.StartsWith("/moploop", StringComparison.OrdinalIgnoreCase));
        if (lastLoopIndex != -1) {
            lines = lines
                .Where((line, idx) => !line.StartsWith("/moploop", StringComparison.OrdinalIgnoreCase) || idx == lastLoopIndex)
                .ToList();
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

    private static List<string> RemoveVariableDefinitions(IEnumerable<string> lines) {
        return lines
            .Where(l => !Regex.IsMatch(l, @"^\$[A-Za-z_]\w*\s*="))
            .ToList();
    }

    private static string[] SubstituteVariables(
        IEnumerable<string> lines,
        Dictionary<string, string> variables
    ) {
        var result = new List<string>();

        foreach (var line in lines) {
            var resolved = line;

            foreach (var (key, value) in variables) {
                resolved = Regex.Replace(
                    resolved,
                    $@"\${Regex.Escape(key)}\b",
                    value
                );
            }

            result.Add(resolved);
        }

        return result.ToArray();
    }

    private static Dictionary<string, string> MergeVariables(
        Dictionary<string, string>? macroVars,
        Dictionary<string, string> commandVars
    ) {
        var result = new Dictionary<string, string>();

        if (macroVars != null) {
            foreach (var (k, v) in macroVars)
                result[k] = v;
        }

        // local var overwrite macro var
        foreach (var (k, v) in commandVars)
            result[k] = v;

        return result;
    }

    public string[] GetActionList(Dictionary<string, string>? macroVariables = null) {
        if (string.IsNullOrWhiteSpace(Actions))
            return Array.Empty<string>();

        var lines = PreprocessLines(Actions);

        var commandVars = ExtractVariables(lines);

        var mergedVars = MergeVariables(macroVariables, commandVars);

        var actionLines = RemoveVariableDefinitions(lines);

        return SubstituteVariables(actionLines, mergedVars);
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

    public string[] GetCidActions(ulong cid) {
        var macroVars = GetMacroVariables();

        return Commands
            .FirstOrDefault(c => c.Cids.Contains(cid))
            ?.GetActionList(macroVars)
            ?? Array.Empty<string>();
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
