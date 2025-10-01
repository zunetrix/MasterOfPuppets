using System.Globalization;

public static class MacroArgParser {
    public static string Normalize(string arg) =>
        arg?.Trim().Trim('"') ?? string.Empty;

    public static bool TryParseInt(string arg, out int value) =>
        int.TryParse(Normalize(arg), out value);

    public static bool TryParseUint(string arg, out uint value) =>
        uint.TryParse(Normalize(arg), out value);

    public static bool TryParseDouble(string arg, out double value) =>
        double.TryParse(Normalize(arg), NumberStyles.Float, CultureInfo.InvariantCulture, out value);
}
