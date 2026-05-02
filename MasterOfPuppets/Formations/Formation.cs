using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace MasterOfPuppets.Formations;

public class Formation {
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("points")]
    public List<FormationPoint> Points { get; set; } = new();

    [JsonPropertyName("executionMode")]
    public FormationExecutionMode ExecutionMode { get; set; } = FormationExecutionMode.LeaderOrigin;

    public Formation Clone() => new() {
        Name = Name,
        ExecutionMode = ExecutionMode,
        Points = Points.ConvertAll(p => p.Clone()),
    };
}

public enum FormationExecutionMode {
    LeaderOrigin,
    RelativeToLocalAssignedPoint,
    ClientOrder,
}
