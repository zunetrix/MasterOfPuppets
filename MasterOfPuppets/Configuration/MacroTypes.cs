using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

public class Character
{
    [JsonPropertyName("cid")]
    public ulong Cid;

    [JsonPropertyName("name")]
    public string Name;
}

public class Command
{
    [JsonPropertyName("characters")]
    public List<Character> Characters;

    // [JsonPropertyName("actions")]
    // public List<Action> Actions;
    [JsonPropertyName("actions")]
    public string Actions;

    public Command Clone()
    {
        return new Command
        {
            Characters = this.Characters != null
                ? this.Characters.Select(c => new Character { Cid = c.Cid, Name = c.Name }).ToList()
                : new List<Character>(),
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
}
