using System.Collections.Generic;
using System.Text.Json.Serialization;

public class Character {
    [JsonPropertyName("cid")]
    public ulong Cid;

    [JsonPropertyName("name")]
    public string Name = string.Empty;

    [JsonPropertyName("keyboardBroadcast")]
    public bool KeyboardBroadcastEnabled = true;

    [JsonPropertyName("autoAcceptPartyInvite")]
    public bool AutoAcceptPartyInvite = true;

    [JsonPropertyName("autoAcceptTeleport")]
    public bool AutoAcceptTeleport = true;

    [JsonPropertyName("autoLogin")]
    public bool AutoLoginEnabled = false;
}

public class CidGroup {
    [JsonPropertyName("name")]
    public string Name = string.Empty;

    [JsonPropertyName("cids")]
    public List<ulong> Cids = new List<ulong>();
}
