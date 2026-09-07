using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;

using MasterOfPuppets.Util;

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
        // Use BlobUtil to produce line‑wrapped Base64 with optional GZip compression.
        var bytes = Encoding.UTF8.GetBytes(input);
        return BlobUtil.Encode(bytes, compress: true);
    }

    public static string Decompress(this string input) {
        // BlobUtil handles whitespace, line breaks, and auto‑detects GZip compression.
        if (string.IsNullOrWhiteSpace(input))
            return string.Empty;
        return BlobUtil.Decode(input);
    }

    internal static byte[] ToTerminatedBytes(this string s) {
        var utf8 = Encoding.UTF8;
        var bytes = new byte[utf8.GetByteCount(s) + 1];
        utf8.GetBytes(s, 0, s.Length, bytes, 0);
        bytes[^1] = 0;
        return bytes;
    }
}
