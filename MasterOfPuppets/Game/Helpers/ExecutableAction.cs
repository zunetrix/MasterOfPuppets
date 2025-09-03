using System;

using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

using static FFXIVClientStructs.FFXIV.Client.UI.Misc.RaptureHotbarModule;

namespace MasterOfPuppets;

[Serializable]
public class ExecutableAction
{
    [JsonProperty("id")] public uint ActionId;
    [JsonProperty("name")] public string ActionName;
    [JsonProperty("iconId")] public uint IconId;
    [JsonProperty("textCommand")] public string TextCommand;
    [JsonProperty("category")] public string? Category;
    // [JsonProperty("sortOrder")] public int? SortOrder;
    [JsonConverter(typeof(StringEnumConverter))]
    [JsonProperty("type")] public HotbarSlotType? HotbarSlotType;
}
