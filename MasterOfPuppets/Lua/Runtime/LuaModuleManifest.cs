using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace MasterOfPuppets.LuaScripting.Runtime;

public static partial class LuaModuleManifest {
    public const int MaximumModuleCount = 64;
    public const int MaximumModuleSourceLength = 64 * 1024;
    public const int MaximumTotalSourceLength = 256 * 1024;

    [GeneratedRegex("^[A-Za-z_][A-Za-z0-9_]*(?:\\.[A-Za-z_][A-Za-z0-9_]*)*$", RegexOptions.CultureInvariant)]
    private static partial Regex ModuleNamePattern();

    public static IReadOnlyDictionary<string, string> NormalizeAndValidate(
        IReadOnlyDictionary<string, string>? modules,
        bool validateSyntax = true) {
        if (modules == null || modules.Count == 0)
            return new Dictionary<string, string>(StringComparer.Ordinal);
        if (modules.Count > MaximumModuleCount)
            throw new ArgumentException($"A Lua bundle may contain at most {MaximumModuleCount} modules.", nameof(modules));

        var result = new SortedDictionary<string, string>(StringComparer.Ordinal);
        var totalLength = 0;
        foreach (var (rawName, rawSource) in modules) {
            var name = NormalizeName(rawName);
            var source = rawSource ?? string.Empty;
            if (string.IsNullOrWhiteSpace(source))
                throw new ArgumentException($"Lua module '{name}' has no source.", nameof(modules));
            if (source.Length > MaximumModuleSourceLength)
                throw new ArgumentException(
                    $"Lua module '{name}' cannot exceed {MaximumModuleSourceLength:N0} characters.",
                    nameof(modules));
            totalLength = checked(totalLength + source.Length);
            if (totalLength > MaximumTotalSourceLength)
                throw new ArgumentException(
                    $"Lua module source cannot exceed {MaximumTotalSourceLength:N0} characters in total.",
                    nameof(modules));
            if (!result.TryAdd(name, source))
                throw new ArgumentException($"Duplicate normalized Lua module name '{name}'.", nameof(modules));
            if (validateSyntax)
                LuaScriptValidator.ThrowIfInvalid(source, $"@module:{name}");
        }
        return result;
    }

    public static string NormalizeName(string? name) {
        var normalized = name?.Trim() ?? string.Empty;
        if (normalized.Length is 0 or > 100 || !ModuleNamePattern().IsMatch(normalized))
            throw new ArgumentException(
                "Lua module names must be dot-separated identifiers (for example, theatre.dialogue).",
                nameof(name));
        return normalized;
    }

    public static string ComputeHash(IReadOnlyDictionary<string, string>? modules) {
        var normalized = NormalizeAndValidate(modules, validateSyntax: false);
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (var (name, source) in normalized) {
            Append(hash, name);
            Append(hash, source);
        }
        return Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
    }

    public static string ComputeBundleHash(string source, IReadOnlyDictionary<string, string>? modules) {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        Append(hash, source ?? string.Empty);
        Append(hash, ComputeHash(modules));
        return Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
    }

    private static void Append(IncrementalHash hash, string value) {
        var bytes = Encoding.UTF8.GetBytes(value);
        hash.AppendData(BitConverter.GetBytes(bytes.Length));
        hash.AppendData(bytes);
    }
}
