using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;

using MasterOfPuppets;
using MasterOfPuppets.Movement;

using Newtonsoft.Json.Linq;

namespace MasterOfPuppets.Formations;

public sealed record BardToolboxFormationImportResult(
    int FormationsRead,
    int FormationsImported,
    int PointsImported,
    int CharactersImported
);

public sealed record BardToolboxFormationImport(
    List<Formation> Formations,
    Dictionary<ulong, string> CharacterNames
) {
    public int PointCount => Formations.Sum(f => f.Points.Count);
}

public static class BardToolboxFormationImporter {
    public static BardToolboxFormationImport ParseConfigJson(string json) {
        var root = JObject.Parse(json);
        var characterNames = ParseCharacterNames(root["CidToNameWorld"]);
        var formations = new List<Formation>();

        if (root["SavedFormationList"] is not JArray savedFormationList)
            return new BardToolboxFormationImport(formations, characterNames);

        foreach (var item in savedFormationList.OfType<JObject>()) {
            var name = item.Value<string>("11")?.Trim();
            if (string.IsNullOrWhiteSpace(name))
                name = "Imported Formation";

            var formation = new Formation {
                Name = name,
            };

            foreach (var entry in EnumerateEntries(item["22"])) {
                var point = ParseEntry(entry);
                if (point != null)
                    formation.Points.Add(point);
            }

            formation.Points = formation.Points
                .OrderBy(p => EntrySortIndex(p))
                .ThenBy(p => p.Cids.FirstOrDefault())
                .Select(p => {
                    p.GroupIds.RemoveAll(g => g.StartsWith(BardToolboxEntryIndexPrefix, StringComparison.Ordinal));
                    return p;
                })
                .ToList();

            formations.Add(formation);
        }

        return new BardToolboxFormationImport(formations, characterNames);
    }

    public static BardToolboxFormationImportResult ImportInto(
        IList<Formation> targetFormations,
        IList<global::Character> targetCharacters,
        BardToolboxFormationImport import,
        MacroImportMode importMode,
        bool includeCharacters = true) {
        int importedFormations = 0;
        int importedPoints = 0;
        int importedCharacters = 0;

        var formationsToImport = import.Formations.Select(f => f.Clone()).ToList();
        var formationIndexMap = targetFormations
            .Select((f, i) => new { f.Name, Index = i })
            .GroupBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().Index, StringComparer.OrdinalIgnoreCase);

        void AddFormation(Formation formation) {
            var newIndex = targetFormations.Count;
            targetFormations.Add(formation);
            formationIndexMap[formation.Name] = newIndex;
            importedFormations++;
            importedPoints += formation.Points.Count;
        }

        switch (importMode) {
            case MacroImportMode.AppendAll:
                foreach (var formation in formationsToImport) {
                    formation.Name = MakeUniqueName(formation.Name, formationIndexMap.Keys);
                    AddFormation(formation);
                }
                break;

            case MacroImportMode.AppendNew:
                foreach (var formation in formationsToImport) {
                    if (!formationIndexMap.ContainsKey(formation.Name))
                        AddFormation(formation);
                }
                break;

            case MacroImportMode.Merge:
                foreach (var formation in formationsToImport) {
                    if (formationIndexMap.TryGetValue(formation.Name, out var idx)) {
                        targetFormations[idx] = formation;
                        importedFormations++;
                        importedPoints += formation.Points.Count;
                    } else {
                        AddFormation(formation);
                    }
                }
                break;

            case MacroImportMode.ReplaceExisting:
                foreach (var formation in formationsToImport) {
                    if (formationIndexMap.TryGetValue(formation.Name, out var idx)) {
                        targetFormations[idx] = formation;
                        importedFormations++;
                        importedPoints += formation.Points.Count;
                    }
                }
                break;

            case MacroImportMode.OverwriteAll:
                targetFormations.Clear();
                foreach (var formation in formationsToImport)
                    AddFormation(formation);
                break;
        }

        if (includeCharacters) {
            var existingCids = targetCharacters.Select(c => c.Cid).ToHashSet();
            foreach (var (cid, name) in import.CharacterNames) {
                if (cid == 0 || existingCids.Contains(cid) || string.IsNullOrWhiteSpace(name))
                    continue;

                targetCharacters.Add(new global::Character { Cid = cid, Name = name });
                existingCids.Add(cid);
                importedCharacters++;
            }
        }

        return new BardToolboxFormationImportResult(
            import.Formations.Count,
            importedFormations,
            importedPoints,
            importedCharacters);
    }

    private const string BardToolboxEntryIndexPrefix = "__btb_index__:";

    private static IEnumerable<JObject> EnumerateEntries(JToken? entriesToken) {
        if (entriesToken is not JObject entriesObject)
            yield break;

        foreach (var property in entriesObject.Properties()) {
            if (property.Name == "$type")
                continue;
            if (property.Value is JObject entry)
                yield return entry;
        }
    }

    private static FormationPoint? ParseEntry(JObject entry) {
        if (!TryGetUlong(entry["Pepsi1"], out var cid))
            return null;

        var position = ParseVector3(entry["Pepsi2"]);
        var rotation = entry.Value<float?>("Pepsi3") ?? 0f;
        var index = entry.Value<int?>("i") ?? int.MaxValue;

        return new FormationPoint {
            Offset = new Vector3(-position.X, position.Y, -position.Z),
            Angle = FormationMath.NormalizeDegrees(rotation * Angle.RadToDeg),
            Cids = cid == 0 ? [] : [cid],
            GroupIds = [$"{BardToolboxEntryIndexPrefix}{index.ToString(CultureInfo.InvariantCulture)}"],
        };
    }

    private static int EntrySortIndex(FormationPoint point) {
        var tag = point.GroupIds.FirstOrDefault(g => g.StartsWith(BardToolboxEntryIndexPrefix, StringComparison.Ordinal));
        if (tag == null)
            return int.MaxValue;
        return int.TryParse(tag[BardToolboxEntryIndexPrefix.Length..], NumberStyles.Integer, CultureInfo.InvariantCulture, out var index)
            ? index
            : int.MaxValue;
    }

    private static Vector3 ParseVector3(JToken? token) {
        if (token is not JObject obj)
            return Vector3.Zero;

        return new Vector3(
            obj.Value<float?>("X") ?? 0f,
            obj.Value<float?>("Y") ?? 0f,
            obj.Value<float?>("Z") ?? 0f);
    }

    private static Dictionary<ulong, string> ParseCharacterNames(JToken? token) {
        var result = new Dictionary<ulong, string>();
        if (token is not JObject obj)
            return result;

        foreach (var property in obj.Properties()) {
            if (!ulong.TryParse(property.Name, NumberStyles.Integer, CultureInfo.InvariantCulture, out var cid))
                continue;

            var payload = property.Value as JObject;
            var name = payload?.Value<string>("Item1") ?? payload?.Value<string>("name") ?? payload?.Value<string>("Name");
            var world = payload?.Value<string>("Item2") ?? payload?.Value<string>("world") ?? payload?.Value<string>("World");

            if (string.IsNullOrWhiteSpace(name))
                continue;

            result[cid] = string.IsNullOrWhiteSpace(world) ? name : $"{name}@{world}";
        }

        return result;
    }

    private static bool TryGetUlong(JToken? token, out ulong value) {
        value = 0;
        if (token == null)
            return false;
        if (token.Type == JTokenType.Integer) {
            var signed = token.Value<long>();
            if (signed < 0)
                return false;
            value = (ulong)signed;
            return true;
        }
        return ulong.TryParse(token.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
    }

    private static string MakeUniqueName(string name, IEnumerable<string> existingNames) {
        var existing = existingNames.ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (!existing.Contains(name))
            return name;

        int n = 2;
        while (existing.Contains($"{name} ({n})"))
            n++;
        return $"{name} ({n})";
    }
}
