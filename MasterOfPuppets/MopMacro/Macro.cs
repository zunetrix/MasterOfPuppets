using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

public class Character {
    [JsonPropertyName("cid")]
    public ulong Cid;

    [JsonPropertyName("name")]
    public string Name;
}

public class CidGroup {
    [JsonPropertyName("name")]
    public string Name;

    [JsonPropertyName("cids")]
    public List<ulong> Cids;
}

public class Command {
    [JsonPropertyName("cids")]
    public List<ulong> Cids;

    [JsonPropertyName("actions")]
    public string Actions;

    public Command Clone() {
        return new Command {
            Cids = this.Cids != null
                ? this.Cids.ToList()
                : new List<ulong>(),
            Actions = this.Actions
        };
    }

    public Command CloneWithoutCharacters() {
        return new Command {
            Cids = new List<ulong>(),
            Actions = this.Actions
        };
    }

    public void SanitizeActionsText() {
        if (string.IsNullOrWhiteSpace(this.Actions)) return;

        var lines = this.Actions
            .Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Trim())
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .Select(line => System.Text.RegularExpressions.Regex.Replace(line, @"\s+", " "))
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


    public string[] GetActionList() {
        string[] actionList = this.Actions.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries)
        .Where(line => line.Length > 0 && !line.StartsWith("#")).ToArray();

        return actionList;
    }
}

public class Macro {
    [JsonPropertyName("name")]
    public string Name;

    [JsonPropertyName("commands")]
    public List<Command> Commands;

    public Macro Clone() {
        return new Macro {
            Name = this.Name,
            Commands = this.Commands.Select(cmd => cmd.Clone()).ToList(),
        };
    }

    public Macro CloneWithoutCharacters() {
        return new Macro {
            Name = this.Name,
            Commands = this.Commands.Select(cmd => cmd.CloneWithoutCharacters()).ToList(),
        };
    }

    public string[] GetCidActions(ulong cid) =>
    Commands?.FirstOrDefault(c => c.Cids.Contains(cid))?.GetActionList() ?? [];

    public void SanitizeActions() {
        Commands?.ForEach(command => command.SanitizeActionsText());
    }
}
