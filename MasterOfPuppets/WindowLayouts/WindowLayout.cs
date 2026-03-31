using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace MasterOfPuppets.WindowLayouts;

public class WindowLayout {
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("slots")]
    public List<WindowLayoutSlot> Slots { get; set; } = new();

    public WindowLayout Clone() => new() {
        Name = Name,
        Slots = Slots.ConvertAll(s => s.Clone()),
    };
}
