using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

using Dalamud.Plugin;

using ExposedObject;

namespace MasterOfPuppets.Util;

public record PluginCollectionEntry(string InternalName, bool IsEnabled);

public record PluginCollectionInfo(string Name, bool IsEnabled, List<PluginCollectionEntry> Plugins);

public static class DalamudPluginHelper {
    private static readonly string[] LoadErrorStates = ["UnloadError", "LoadError", "DependencyResolutionFailed"];

    public static List<IExposedPlugin> GetInstalledPlugins() {
        return DalamudApi.PluginInterface.InstalledPlugins
            .OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static bool IsPluginLoaded(string internalName) {
        return DalamudApi.PluginInterface.InstalledPlugins
            .Any(p => p.IsLoaded && string.Equals(p.InternalName, internalName, StringComparison.OrdinalIgnoreCase));
    }

    // Dalamud has no public API to toggle plugins/collections, so use its built-in chat commands (match by manifest Name)
    public static string GetPluginCommand(IExposedPlugin plugin, bool enable) =>
        $"/{(enable ? "xlenableplugin" : "xldisableplugin")} \"{plugin.Name}\"";

    public static string GetCollectionCommand(string collectionName, bool enable) =>
        $"/{(enable ? "xlenablecollection" : "xldisablecollection")} \"{collectionName}\"";

    public static void SetPluginState(IExposedPlugin plugin, bool enable) {
        DalamudApi.CommandManager.ProcessCommand(GetPluginCommand(plugin, enable));
    }

    public static void SetCollectionState(string collectionName, bool enable) {
        DalamudApi.CommandManager.ProcessCommand(GetCollectionCommand(collectionName, enable));
    }

    /// <summary>
    /// Reads Dalamud plugin collections (profiles) via reflection. The default profile is not included.
    /// </summary>
    public static List<PluginCollectionInfo> GetCollections() {
        var collections = new List<PluginCollectionInfo>();
        try {
            var profileManager = GetDalamudService("Dalamud.Plugin.Internal.Profiles.ProfileManager");
            if (GetProperty(profileManager, "Profiles") is not IEnumerable<object> profiles)
                return collections;

            foreach (var profile in profiles) {
                if (GetProperty(profile, "IsDefaultProfile") is true)
                    continue;

                var plugins = (GetProperty(profile, "Plugins") as IEnumerable<object> ?? [])
                    .Select(entry => new PluginCollectionEntry(
                        GetProperty(entry, "InternalName")?.ToString() ?? string.Empty,
                        GetProperty(entry, "IsEnabled") is true))
                    .ToList();

                collections.Add(new PluginCollectionInfo(
                    GetProperty(profile, "Name")?.ToString() ?? string.Empty,
                    GetProperty(profile, "IsEnabled") is true,
                    plugins));
            }
        } catch (Exception e) {
            DalamudApi.PluginLog.Error($"{e.Message}\n{e.StackTrace}");
        }

        return collections.OrderBy(c => c.Name, StringComparer.OrdinalIgnoreCase).ToList();
    }

    /// <summary>
    /// Resets plugins stuck in a load error state back to the initial state and marks them as disabled,
    /// so they can be enabled again without restarting the game.
    /// </summary>
    /// <returns>Number of plugins fixed.</returns>
    public static int FixPluginLoadError() {
        var fixedCount = 0;
        try {
            var pluginManager = GetDalamudService("Dalamud.Plugin.Internal.PluginManager");
            var installedPlugins = Exposed.From(pluginManager).InstalledPlugins;
            var stateEnum = DalamudAssembly.GetType("Dalamud.Plugin.Internal.Types.PluginState", true)!;

            foreach (var t in installedPlugins) {
                var localPlugin = (object)t;
                var name = GetProperty(localPlugin, "Name")?.ToString();
                var state = GetProperty(localPlugin, "State")?.ToString();

                if (!LoadErrorStates.Contains(state))
                    continue;

                DalamudApi.PluginLog.Warning($"Plugin {name} detected error state {state}");
                SetProperty(localPlugin, "State", stateEnum.GetEnumValues().GetValue(0));
                var manifest = GetProperty(localPlugin, "Manifest");
                if (manifest != null)
                    SetProperty(manifest, "Disabled", true);

                fixedCount++;
                DalamudApi.PluginLog.Information($"Plugin {name}, state {GetProperty(localPlugin, "State")}");
            }
        } catch (Exception e) {
            DalamudApi.PluginLog.Error($"{e.Message}\n{e.StackTrace}");
        }

        return fixedCount;
    }

    private static Assembly DalamudAssembly => DalamudApi.PluginInterface.GetType().Assembly;

    // Dalamud.Service<T>.Get()
    private static object GetDalamudService(string typeName) {
        return Exposed.From(DalamudAssembly
            .GetType("Dalamud.Service`1", true)!
            .MakeGenericType(DalamudAssembly.GetType(typeName, true)!))
            .Get();
    }

    private static object? GetProperty(object obj, string name) {
        return obj.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.Instance)?.GetValue(obj);
    }

    private static void SetProperty(object obj, string name, object? value) {
        obj.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.Instance)?.SetValue(obj, value);
    }
}
