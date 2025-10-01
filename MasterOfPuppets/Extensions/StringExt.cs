using System.Collections.Generic;
using System.Linq;

namespace MasterOfPuppets.Extensions;

public static class StringExt {
    public static string EllipsisPath(this string path, int maxLength = 30, char delimiter = '\\') {
        if (string.IsNullOrEmpty(path) || path.Length <= maxLength)
            return path;

        var parts = path.Split(delimiter);
        if (parts.Length <= 2)
            return path;

        string first = parts[0];
        List<string> resultParts = new() { first };

        // prio last folders
        List<string> endParts = new();
        int idx = parts.Length - 1;
        while (idx > 0) {
            endParts.Insert(0, parts[idx]);
            string candidate = string.Join(delimiter.ToString(), resultParts.Concat(new[] { "..." }).Concat(endParts));
            if (candidate.Length > maxLength) {
                endParts.RemoveAt(0);
                break;
            }
            idx--;
        }

        resultParts.Add("...");
        resultParts.AddRange(endParts);

        return string.Join(delimiter.ToString(), resultParts);
    }

    public static string? NullIfEmpty(this string self) => self != "" ? self : null;

    public static string IfEmpty(this string self, string replacement) => self != "" ? self : replacement;

    public static string Truncate(this string self, int maxLength) {
        if (self.Length > maxLength) {
            return self.Substring(0, maxLength);
        }

        return self;
    }

    public static int MaxLineLength(this string self) {
        return Enumerable.Max(self.Split("\n").Select(s => s.Count()));
    }
}
