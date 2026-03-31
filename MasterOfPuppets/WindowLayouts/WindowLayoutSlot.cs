using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace MasterOfPuppets.WindowLayouts;

public class WindowLayoutSlot {
    /// <summary>Position and size of the window in screen coordinates (absolute pixels).</summary>
    [JsonPropertyName("x")]
    public int X { get; set; }

    [JsonPropertyName("y")]
    public int Y { get; set; }

    [JsonPropertyName("width")]
    public int Width { get; set; } = 1920;

    [JsonPropertyName("height")]
    public int Height { get; set; } = 1080;

    [JsonPropertyName("cids")]
    public List<ulong> Cids { get; set; } = new();

    [JsonPropertyName("groupIds")]
    public List<string> GroupIds { get; set; } = new();

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

    public WindowLayoutSlot Clone() => new() {
        X = X,
        Y = Y,
        Width = Width,
        Height = Height,
        Cids = [.. Cids],
        GroupIds = [.. GroupIds],
    };
}
