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

public class CidGroup
{
    [JsonPropertyName("name")]
    public string Name;

    [JsonPropertyName("cids")]
    public List<ulong> Cids;
}

public class Command
{
    // [JsonPropertyName("characters")]
    // public List<Character> Characters;
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
