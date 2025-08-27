using System.Collections.Generic;
using System.Text.Json.Serialization;

public class Character
{
    [JsonPropertyName("cid")]
    public ulong Cid;

    [JsonPropertyName("name")]
    public string Name;
}

// public enum ActionType
// {
//     Emote,
//     Delay,
//     Item,
//     Movement,
//     Macro
// }

// public class Action
// {
//     [JsonPropertyName("type")]
//     public ActionType Type;

//     [JsonPropertyName("content")]
//     public string Content;
// }

public class Command
{
    [JsonPropertyName("characters")]
    public List<Character> Characters;

    // [JsonPropertyName("actions")]
    // public List<Action> Actions;
    [JsonPropertyName("actions")]
    public string Actions;
}

public class Macro
{
    [JsonPropertyName("name")]
    public string Name;

    [JsonPropertyName("commands")]
    public List<Command> Commands;
}
