using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;

using MasterOfPuppets.LuaScripting.Runtime;
using MasterOfPuppets.LuaScripting.Runs;

namespace MasterOfPuppets.LuaScripting;

public sealed class LuaScriptDefinition {
    public const int MaximumSourceLength = 256 * 1024;
    public const int CurrentSchemaVersion = 2;

    public int SchemaVersion { get; set; } = CurrentSchemaVersion;
    public string Id { get; set; } = string.Empty;
    public int Revision { get; set; } = 1;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Variables { get; set; } = string.Empty;
    public string ParticipantFormation { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public Dictionary<string, string> Modules { get; set; } = new(StringComparer.Ordinal);
    public List<string> DeclaredCapabilities { get; set; } = new();
    public List<LuaScriptParameterDefinition> Parameters { get; set; } = new();
    public LuaResourceKind? RequiredResources { get; set; }
    public List<string> Tags { get; set; } = new();
    public Vector4 Color { get; set; } = Vector4.One;
    public uint IconId { get; set; }

    public string Hash => ComputeHash(Source);

    public LuaScriptDefinition Clone() => new() {
        SchemaVersion = SchemaVersion,
        Id = Id,
        Revision = Revision,
        Name = Name,
        Description = Description,
        Variables = Variables,
        ParticipantFormation = ParticipantFormation,
        Source = Source,
        Modules = new Dictionary<string, string>(Modules ?? new Dictionary<string, string>(), StringComparer.Ordinal),
        DeclaredCapabilities = DeclaredCapabilities?.ToList() ?? new List<string>(),
        Parameters = Parameters?.Select(parameter => parameter.Clone()).ToList() ?? new List<LuaScriptParameterDefinition>(),
        RequiredResources = RequiredResources,
        Tags = Tags?.ToList() ?? new List<string>(),
        Color = Color,
        IconId = IconId,
    };

    public void Validate() {
        MigrateMetadata();
        Name = Name?.Trim() ?? string.Empty;
        if (Name.Length == 0)
            throw new ArgumentException("Lua script name is required.");
        if (Name.Length > 100)
            throw new ArgumentException("Lua script name cannot exceed 100 characters.");
        if (Name.Contains('"'))
            throw new ArgumentException("Lua script names cannot contain quotation marks.");
        Source ??= string.Empty;
        if (string.IsNullOrWhiteSpace(Source))
            throw new ArgumentException("Lua script source is required.");
        if (Source.Length > MaximumSourceLength)
            throw new ArgumentException($"Lua script source cannot exceed {MaximumSourceLength:N0} characters.");
        LuaScriptValidator.ThrowIfInvalid(Source, $"@{Name}");
        Modules = new Dictionary<string, string>(
            LuaModuleManifest.NormalizeAndValidate(Modules),
            StringComparer.Ordinal);
        DeclaredCapabilities = (DeclaredCapabilities ?? new List<string>())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToList();
        Parameters ??= new List<LuaScriptParameterDefinition>();
        if (Parameters.Count > 64)
            throw new ArgumentException("A Lua bundle cannot declare more than 64 parameters.");
        foreach (var parameter in Parameters)
            parameter.Validate();
        var duplicateParameter = Parameters.GroupBy(parameter => parameter.Name, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicateParameter != null)
            throw new ArgumentException($"Lua parameter '{duplicateParameter.Key}' is declared more than once.");
        if (RequiredResources.HasValue)
            RequiredResources = LuaResourceKinds.ValidateMask(RequiredResources.Value);

        Description = Description?.Trim() ?? string.Empty;
        Variables ??= string.Empty;
        ParticipantFormation = ParticipantFormation?.Trim() ?? string.Empty;
        Tags = (Tags ?? new List<string>())
            .Where(tag => !string.IsNullOrWhiteSpace(tag))
            .Select(tag => tag.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static string ComputeHash(string source) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(source ?? string.Empty))).ToLowerInvariant();

    public string DependencyManifestHash => LuaModuleManifest.ComputeHash(Modules);

    public string BundleHash {
        get {
            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            // A bundle hash identifies executable contract content shared between clients.
            // Id and Revision identify one local catalog entry and are deliberately excluded:
            // importing a shared script assigns a fresh local identity.
            AppendHash(hash, "mop.lua.bundle.v3");
            AppendHash(hash, SchemaVersion.ToString(System.Globalization.CultureInfo.InvariantCulture));
            AppendHash(hash, Hash);
            AppendHash(hash, DependencyManifestHash);
            AppendHash(hash, RequiredResources.HasValue
                ? ((int)LuaResourceKinds.ValidateMask(RequiredResources.Value)).ToString(System.Globalization.CultureInfo.InvariantCulture)
                : "legacy");
            foreach (var capability in (DeclaredCapabilities ?? new List<string>())
                         .Where(value => !string.IsNullOrWhiteSpace(value))
                         .Select(value => value.Trim())
                         .Distinct(StringComparer.OrdinalIgnoreCase)
                         .OrderBy(value => value, StringComparer.OrdinalIgnoreCase))
                AppendHash(hash, capability.ToLowerInvariant());
            foreach (var parameter in (Parameters ?? new List<LuaScriptParameterDefinition>())
                         .OrderBy(value => value.Name, StringComparer.OrdinalIgnoreCase)) {
                AppendHash(hash, parameter.Name?.Trim().ToLowerInvariant() ?? string.Empty);
                AppendHash(hash, parameter.Type?.Trim().ToLowerInvariant() ?? string.Empty);
                AppendHash(hash, parameter.DefaultValue ?? string.Empty);
                AppendHash(hash, parameter.Minimum?.ToString("R", System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty);
                AppendHash(hash, parameter.Maximum?.ToString("R", System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty);
                AppendHash(hash, parameter.Required ? "1" : "0");
            }
            return Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
        }
    }

    public LuaResourceKind ResolveRequiredResources() =>
        RequiredResources ?? LuaResourceKinds.LegacyExclusive;

    public bool MigrateMetadata() {
        var changed = false;
        if (SchemaVersion <= 0) {
            SchemaVersion = CurrentSchemaVersion;
            changed = true;
        }
        if (SchemaVersion > CurrentSchemaVersion)
            throw new ArgumentException($"Lua script schema {SchemaVersion} is newer than supported schema {CurrentSchemaVersion}.");
        if (SchemaVersion < CurrentSchemaVersion) {
            SchemaVersion = CurrentSchemaVersion;
            changed = true;
        }
        if (Revision < 1) {
            Revision = 1;
            changed = true;
        }
        if (string.IsNullOrWhiteSpace(Id) || !Guid.TryParse(Id, out _)) {
            Id = CreateMigrationId(Name, Source);
            changed = true;
        }
        Parameters ??= new List<LuaScriptParameterDefinition>();
        DeclaredCapabilities ??= new List<string>();
        Modules ??= new Dictionary<string, string>(StringComparer.Ordinal);
        return changed;
    }

    public void RegenerateIdentity() {
        Id = Guid.NewGuid().ToString("D");
        Revision = 1;
        SchemaVersion = CurrentSchemaVersion;
    }

    private static string CreateMigrationId(string? name, string? source) {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"mop.lua.migration.v2\0{name?.Trim()}\0{ComputeHash(source ?? string.Empty)}"));
        return new Guid(bytes.AsSpan(0, 16)).ToString("D");
    }

    private static void AppendHash(IncrementalHash hash, string value) {
        var bytes = Encoding.UTF8.GetBytes(value);
        hash.AppendData(BitConverter.GetBytes(bytes.Length));
        hash.AppendData(bytes);
    }
}
