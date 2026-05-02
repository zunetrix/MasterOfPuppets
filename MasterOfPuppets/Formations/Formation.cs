using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace MasterOfPuppets.Formations;

public class Formation {
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("points")]
    public List<FormationPoint> Points { get; set; } = new();

    public Formation Clone() => new() {
        Name = Name,
        Points = Points.ConvertAll(p => p.Clone()),
    };
}
