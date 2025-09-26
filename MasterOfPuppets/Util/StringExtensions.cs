using System.IO;
using System.Linq;
using System.Collections.Generic;

public static class StringExtensions
{
    public static string EllipsisPath(this string path, int maxLength = 30, char delimiter = '\\')
    {
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
        while (idx > 0)
        {
            endParts.Insert(0, parts[idx]);
            string candidate = string.Join(delimiter.ToString(), resultParts.Concat(new[] { "..." }).Concat(endParts));
            if (candidate.Length > maxLength)
            {
                endParts.RemoveAt(0);
                break;
            }
            idx--;
        }

        resultParts.Add("...");
        resultParts.AddRange(endParts);

        return string.Join(delimiter.ToString(), resultParts);
    }

}
