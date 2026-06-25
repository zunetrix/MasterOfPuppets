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

    /// <summary>
    /// Shift the Cids and GroupIds assignments across all points
    /// forward=true shifts assignments to the next point, false shifts to the previous point
    /// </summary>
    public void ShiftAssignments(bool forward) {
        if (Points.Count < 2) return;

        if (forward) {
            // Save last point's assignments
            var lastCids = Points[^1].Cids;
            var lastGroupIds = Points[^1].GroupIds;

            // Shift each point's assignments to the next
            for (int i = Points.Count - 1; i > 0; i--) {
                Points[i].Cids = Points[i - 1].Cids;
                Points[i].GroupIds = Points[i - 1].GroupIds;
            }

            // Wrap: first point gets last point's old assignments
            Points[0].Cids = lastCids;
            Points[0].GroupIds = lastGroupIds;
        } else {
            // Save first point's assignments
            var firstCids = Points[0].Cids;
            var firstGroupIds = Points[0].GroupIds;

            // Shift each point's assignments to the previous
            for (int i = 0; i < Points.Count - 1; i++) {
                Points[i].Cids = Points[i + 1].Cids;
                Points[i].GroupIds = Points[i + 1].GroupIds;
            }

            // Wrap: last point gets first point's old assignments
            Points[^1].Cids = firstCids;
            Points[^1].GroupIds = firstGroupIds;
        }
    }
}
