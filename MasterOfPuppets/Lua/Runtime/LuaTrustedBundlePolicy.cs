using System;
using System.Collections.Generic;

using MasterOfPuppets.LuaScripting.Runs;

namespace MasterOfPuppets.LuaScripting.Runtime;

/// <summary>
/// Selects only an installed, locally validated bundle for IPC execution. Wire
/// payloads are evidence of conductor intent, never an executable trust root.
/// </summary>
public static class LuaTrustedBundlePolicy {
    public static bool TrySelectInstalled(
        LuaScriptDefinition? installed,
        string transmittedSourceHash,
        IReadOnlyDictionary<string, string>? transmittedModules,
        LuaResourceKind? transmittedResources,
        string transmittedBundleHash,
        out LuaScriptDefinition trusted,
        out string error) {
        trusted = null!;
        if (installed == null) {
            error = "the script is not installed locally";
            return false;
        }

        try {
            installed.Validate();
        } catch (Exception ex) {
            error = $"the installed script is invalid: {ex.Message}";
            return false;
        }

        if (!string.Equals(installed.Hash, transmittedSourceHash, StringComparison.OrdinalIgnoreCase)) {
            error = "the installed source hash differs from the conductor";
            return false;
        }

        string transmittedManifestHash;
        try {
            transmittedManifestHash = LuaModuleManifest.ComputeHash(transmittedModules);
        } catch (Exception ex) {
            error = $"the transmitted dependency manifest is invalid: {ex.Message}";
            return false;
        }
        if (!string.Equals(
                installed.DependencyManifestHash,
                transmittedManifestHash,
                StringComparison.OrdinalIgnoreCase)) {
            error = "the installed dependency manifest differs from the conductor";
            return false;
        }

        if (installed.RequiredResources != transmittedResources) {
            error = "the installed resource declaration differs from the conductor";
            return false;
        }

        if (!string.Equals(installed.BundleHash, transmittedBundleHash, StringComparison.OrdinalIgnoreCase)) {
            error = "the installed capability contract differs from the conductor";
            return false;
        }

        trusted = installed;
        error = string.Empty;
        return true;
    }
}
