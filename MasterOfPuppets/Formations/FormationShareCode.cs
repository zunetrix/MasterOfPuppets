using System;
using System.Collections.Generic;
using System.Linq;

using MasterOfPuppets.Extensions;

namespace MasterOfPuppets.Formations;

public static class FormationShareCode {
    public const string Prefix = "MOPF1:";

    public static string Export(Formation formation, bool includeAssignments) {
        var clone = formation.Clone();
        if (!includeAssignments) {
            foreach (var point in clone.Points) {
                point.Cids.Clear();
                point.GroupIds.Clear();
            }
        }

        return Prefix + clone.JsonSerialize().Compress();
    }

    public static bool TryImport(string code, out Formation formation, out string error) {
        formation = new Formation();
        error = string.Empty;

        if (string.IsNullOrWhiteSpace(code)) {
            error = "Formation code is empty.";
            return false;
        }

        var trimmed = code.Trim();
        if (!trimmed.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase)) {
            error = $"Formation code must start with {Prefix}.";
            return false;
        }

        try {
            var json = trimmed[Prefix.Length..].Decompress();
            var imported = json.JsonDeserialize<Formation>();
            if (imported == null) {
                error = "Formation code did not contain a formation.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(imported.Name))
                imported.Name = "Imported Formation";
            imported.Points ??= [];
            foreach (var point in imported.Points) {
                point.Cids ??= [];
                point.GroupIds ??= [];
            }

            formation = imported;
            return true;
        } catch (Exception ex) {
            error = $"Could not parse formation code: {ex.Message}";
            return false;
        }
    }

    public static string GetUniqueName(string requestedName, IEnumerable<Formation> existingFormations) {
        var baseName = string.IsNullOrWhiteSpace(requestedName) ? "Imported Formation" : requestedName.Trim();
        if (!existingFormations.Any(f => f.Name.Equals(baseName, StringComparison.OrdinalIgnoreCase)))
            return baseName;

        var index = 2;
        while (existingFormations.Any(f => f.Name.Equals($"{baseName} ({index})", StringComparison.OrdinalIgnoreCase)))
            index++;
        return $"{baseName} ({index})";
    }
}
