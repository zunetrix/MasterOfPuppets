using System;
using System.Globalization;
using System.Text.RegularExpressions;

namespace MasterOfPuppets;

/// <summary>
/// Evaluates dynamic tokens in a macro action string at dispatch time,
/// so each execution (including each loop iteration) produces fresh values.
/// </summary>
public static class MacroTokenProcessor {

    // Matches {random(a,b)} or {random(a,b,c,...)} with optional decimal values.
    // Two values   → random range  : {random(1,5)}      → int 1–5
    //                                {random(1.5,3.5)}   → float 1.50–3.50
    // Three+ values → pick from list: {random(1,3,7)}   → "1", "3", or "7"
    //                                 {random(1.5,2,2.5)} → "1.5", "2", or "2.5"
    private static readonly Regex RandomRegex =
        new(@"\{random\((\d+(?:\.\d+)?(?:,\d+(?:\.\d+)?)+)\)\}", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>
    /// Replaces all recognised tokens in <paramref name="action"/> and returns the result.
    /// </summary>
    public static string Process(string action) {
        return RandomRegex.Replace(action, match => {
            var raw = match.Groups[1].Value;
            var parts = raw.Split(',');
            bool isFloat = raw.Contains('.');

            if (parts.Length == 2) {
                if (isFloat) {
                    float a = float.Parse(parts[0], CultureInfo.InvariantCulture);
                    float b = float.Parse(parts[1], CultureInfo.InvariantCulture);
                    if (a > b) (a, b) = (b, a);
                    var result = a + (float)Random.Shared.NextDouble() * (b - a);
                    return result.ToString("F2", CultureInfo.InvariantCulture);
                } else {
                    int a = int.Parse(parts[0]);
                    int b = int.Parse(parts[1]);
                    if (a > b) (a, b) = (b, a);
                    return Random.Shared.Next(a, b + 1).ToString();
                }
            }

            // List: return one raw string value (preserves original formatting).
            return parts[Random.Shared.Next(parts.Length)].Trim();
        });
    }
}
