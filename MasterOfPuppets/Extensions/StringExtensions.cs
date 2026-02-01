using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;

namespace MasterOfPuppets.Extensions;

public static class StringExtensions {
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

    internal static bool ContainsIgnoreCase(this string haystack, string needle) {
        return CultureInfo.InvariantCulture.CompareInfo.IndexOf(haystack, needle, CompareOptions.IgnoreCase) >= 0;
    }

    public static string Compress(this string input) {
        var bytes = Encoding.UTF8.GetBytes(input);
        using var ms = new MemoryStream();
        using (var gs = new GZipStream(ms, CompressionMode.Compress))
            gs.Write(bytes, 0, bytes.Length);
        return Convert.ToBase64String(ms.ToArray());
    }

    public static string Decompress(this string input) {
        var data = Convert.FromBase64String(input);
        using var ms = new MemoryStream(data);
        using var gs = new GZipStream(ms, CompressionMode.Decompress);
        using var r = new StreamReader(gs);
        return r.ReadToEnd();
    }
}
