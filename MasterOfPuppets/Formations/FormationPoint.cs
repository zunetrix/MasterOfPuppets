using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text.Json.Serialization;

namespace MasterOfPuppets.Formations;

public class FormationPoint {
    /// <summary>World-space offset from the formation leader's position. Y is typically 0.</summary>
    [JsonPropertyName("offset")]
    public Vector3 Offset;

    /// <summary>MoP editor facing angle in degrees. Convention: 0 = north, increases clockwise.</summary>
    [JsonPropertyName("angle")]
    public float Angle;

    [JsonPropertyName("cids")]
    public List<ulong> Cids = new();

    [JsonPropertyName("groupIds")]
    public List<string> GroupIds = new();

    /// <summary>Returns the union of directly assigned CIDs and CIDs from referenced groups.</summary>
    public HashSet<ulong> GetEffectiveCids(IReadOnlyList<CidGroup>? groups = null) {
        var result = new HashSet<ulong>(Cids);

        if (groups != null && GroupIds.Count > 0) {
            foreach (var groupName in GroupIds) {
                var group = groups.FirstOrDefault(g => g.Name == groupName);
                if (group != null)
                    foreach (var cid in group.Cids)
                        result.Add(cid);
            }
        }

        return result;
    }

    public FormationPoint Clone() => new() {
        Offset = Offset,
        Angle = Angle,
        Cids = [.. Cids],
        GroupIds = [.. GroupIds],
    };
}
