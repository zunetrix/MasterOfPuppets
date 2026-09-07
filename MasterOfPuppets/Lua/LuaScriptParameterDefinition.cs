using System;
using System.Globalization;

namespace MasterOfPuppets.LuaScripting;

public sealed class LuaScriptParameterDefinition {
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = "string";
    public string DefaultValue { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public double? Minimum { get; set; }
    public double? Maximum { get; set; }
    public bool Required { get; set; }

    public LuaScriptParameterDefinition Clone() => new() {
        Name = Name,
        Type = Type,
        DefaultValue = DefaultValue,
        Description = Description,
        Minimum = Minimum,
        Maximum = Maximum,
        Required = Required,
    };

    public void Validate() {
        Name = Name?.Trim() ?? string.Empty;
        if (Name.Length is 0 or > 64 || !IsIdentifier(Name))
            throw new ArgumentException("Lua parameter names must be identifiers of at most 64 characters.");
        Type = Type?.Trim().ToLowerInvariant() ?? string.Empty;
        if (Type is not ("string" or "number" or "integer" or "boolean"))
            throw new ArgumentException($"Lua parameter '{Name}' has unsupported type '{Type}'.");
        Description = Description?.Trim() ?? string.Empty;
        DefaultValue ??= string.Empty;
        if (Minimum is { } minimum && !double.IsFinite(minimum)
            || Maximum is { } maximum && !double.IsFinite(maximum)
            || Minimum.HasValue && Maximum.HasValue && Minimum > Maximum)
            throw new ArgumentException($"Lua parameter '{Name}' has invalid numeric bounds.");
        if (Type is "string" or "boolean" && (Minimum.HasValue || Maximum.HasValue))
            throw new ArgumentException($"Lua parameter '{Name}' can use bounds only for number or integer types.");
        if (DefaultValue.Length > 1000)
            throw new ArgumentException($"Lua parameter '{Name}' default cannot exceed 1,000 characters.");
        if (DefaultValue.Length > 0)
            ValidateValue(DefaultValue);
    }

    public void ValidateValue(string value) {
        value ??= string.Empty;
        if (Required && value.Length == 0)
            throw new ArgumentException($"Lua parameter '{Name}' is required.");
        if (value.Length == 0)
            return;
        if (Type == "boolean" && !bool.TryParse(value, out _))
            throw new ArgumentException($"Lua parameter '{Name}' must be true or false.");
        if (Type is not ("number" or "integer"))
            return;
        if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) || !double.IsFinite(number))
            throw new ArgumentException($"Lua parameter '{Name}' must be a finite {Type}.");
        if (Type == "integer" && number != Math.Truncate(number))
            throw new ArgumentException($"Lua parameter '{Name}' must be an integer.");
        if (Minimum.HasValue && number < Minimum || Maximum.HasValue && number > Maximum)
            throw new ArgumentException($"Lua parameter '{Name}' must be between {Minimum?.ToString(CultureInfo.InvariantCulture) ?? "-infinity"} and {Maximum?.ToString(CultureInfo.InvariantCulture) ?? "infinity"}.");
    }

    private static bool IsIdentifier(string value) {
        if (!(char.IsLetter(value[0]) || value[0] == '_'))
            return false;
        for (var index = 1; index < value.Length; index++)
            if (!(char.IsLetterOrDigit(value[index]) || value[index] == '_'))
                return false;
        return true;
    }
}
