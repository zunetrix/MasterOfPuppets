using System;
using System.Linq;
using System.Collections.Generic;
using System.Text.Json.Serialization;

public class Character
{
    [JsonPropertyName("cid")]
    public ulong Cid;

    [JsonPropertyName("name")]
    public string Name;
}

public class CidGroup
{
    [JsonPropertyName("name")]
    public string Name;

    [JsonPropertyName("cids")]
    public List<ulong> Cids;
}

public class Command
{
    [JsonPropertyName("cids")]
    public List<ulong> Cids;

    [JsonPropertyName("actions")]
    public string Actions;

    public Command Clone()
    {
        return new Command
        {
            Cids = this.Cids != null
                ? this.Cids.ToList()
                : new List<ulong>(),
            Actions = this.Actions
        };
    }

    public Command CloneWithoutCharacters()
    {
        return new Command
        {
            Cids = new List<ulong>(),
            Actions = this.Actions
        };
    }
}

public class Macro
{
    [JsonPropertyName("name")]
    public string Name;

    [JsonPropertyName("commands")]
    public List<Command> Commands;

    public Macro Clone()
    {
        return new Macro
        {
            Name = this.Name,
            Commands = this.Commands.Select(cmd => cmd.Clone()).ToList(),
        };
    }

    public Macro CloneWithoutCharacters()
    {
        return new Macro
        {
            Name = this.Name,
            Commands = this.Commands.Select(cmd => cmd.CloneWithoutCharacters()).ToList(),
        };
    }

    public void SanitizeAllActions()
    {
        if (Commands == null) return;

        foreach (var command in Commands)
        {
            if (string.IsNullOrWhiteSpace(command.Actions)) continue;

            var cleaned = string.Join(
                "\n",
                command.Actions
                    .Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(line => line.Trim())
                    .Where(line => !string.IsNullOrWhiteSpace(line))
                    .Select(line => System.Text.RegularExpressions.Regex.Replace(line, @"\s+", " "))
            );

            command.Actions = cleaned;
        }
    }
}
