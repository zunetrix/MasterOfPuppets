using System;
using System.Collections.Generic;
using System.Linq;

namespace MasterOfPuppets.LuaScripting.Runtime;

public sealed record LuaCapabilityDescriptor(
    string Name,
    string Version,
    string Description,
    IReadOnlyList<string> Permissions) {

    public LuaCapabilityDescriptor Validate() {
        if (string.IsNullOrWhiteSpace(Name))
            throw new ArgumentException("Lua capability name is required.", nameof(Name));
        if (!System.Version.TryParse(Version, out _))
            throw new ArgumentException($"Lua capability '{Name}' has invalid version '{Version}'.", nameof(Version));
        if (string.IsNullOrWhiteSpace(Description))
            throw new ArgumentException($"Lua capability '{Name}' requires a description.", nameof(Description));
        if (Permissions == null || Permissions.Any(string.IsNullOrWhiteSpace))
            throw new ArgumentException($"Lua capability '{Name}' contains an invalid permission.", nameof(Permissions));
        return this with {
            Name = Name.Trim(),
            Description = Description.Trim(),
            Permissions = Permissions.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(value => value, StringComparer.Ordinal).ToArray(),
        };
    }

    public bool MeetsMinimumVersion(string? minimumVersion) {
        if (string.IsNullOrWhiteSpace(minimumVersion))
            return true;
        if (!System.Version.TryParse(minimumVersion.Trim(), out var minimum))
            throw new ArgumentException($"Invalid minimum capability version '{minimumVersion}'.", nameof(minimumVersion));
        return System.Version.Parse(Version) >= minimum;
    }
}
