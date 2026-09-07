using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using Lua;

namespace MasterOfPuppets.LuaScripting.Runtime;

/// <summary>Run-local loader that can only return prevalidated bundle modules.</summary>
public sealed class LuaInMemoryModuleLoader : ILuaModuleLoader {
    private readonly IReadOnlyDictionary<string, string> _modules;

    public LuaInMemoryModuleLoader(IReadOnlyDictionary<string, string>? modules) {
        _modules = LuaModuleManifest.NormalizeAndValidate(modules);
    }

    public bool Exists(string moduleName) {
        try {
            return _modules.ContainsKey(LuaModuleManifest.NormalizeName(moduleName));
        } catch (ArgumentException) {
            return false;
        }
    }

    public ValueTask<LuaModule> LoadAsync(string moduleName, CancellationToken cancellationToken = default) {
        cancellationToken.ThrowIfCancellationRequested();
        var normalized = LuaModuleManifest.NormalizeName(moduleName);
        if (!_modules.TryGetValue(normalized, out var source))
            throw new FileNotFoundException(
                $"Lua module '{normalized}' is not present in this script bundle.",
                normalized);
        return new ValueTask<LuaModule>(new LuaModule(normalized, source));
    }
}
