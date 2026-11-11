using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

using Dalamud.Interface.ImGuiNotification;
using Dalamud.Plugin;

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
            if (GetProperty(pluginManager, "InstalledPlugins") is not IEnumerable<object> installedPlugins)
                return fixedCount;
            var stateEnum = DalamudAssembly.GetType("Dalamud.Plugin.Internal.Types.PluginState", true)!;

            foreach (var localPlugin in installedPlugins) {
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

    // plugins (InternalName) with an update currently running on this client
    private static readonly ConcurrentDictionary<string, byte> UpdatingPlugins = new(StringComparer.OrdinalIgnoreCase);

    public static string GetUpdatePluginCommand(IExposedPlugin plugin) => $"/mop plugin update \"{plugin.InternalName}\"";

    public static bool IsPluginUpdating(string internalName) => UpdatingPlugins.ContainsKey(internalName);

    /// <summary>
    /// Plugins with an update available on this client (InternalName -> new version), read from
    /// PluginManager.UpdatablePlugins (the same list the plugin installer uses). Dev plugins are excluded.
    /// </summary>
    public static Dictionary<string, string> GetAvailableUpdates() {
        var updates = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        try {
            foreach (var update in GetUpdatablePlugins()) {
                var localPlugin = GetProperty(update, "InstalledPlugin");
                if (localPlugin == null || GetProperty(localPlugin, "IsDev") is true)
                    continue;
                var internalName = GetProperty(localPlugin, "InternalName")?.ToString();
                if (!string.IsNullOrEmpty(internalName))
                    updates[internalName] = GetProperty(update, "EffectiveVersion")?.ToString() ?? "?";
            }
        } catch (Exception e) {
            DalamudApi.PluginLog.Error($"{e.Message}\n{e.StackTrace}");
        }

        return updates;
    }

    /// <summary>
    /// Updates a single plugin (matched by InternalName or Name) the same way the plugin installer's
    /// update button does: PluginManager.UpdateSinglePluginAsync(update, notify: true, dryRun: false).
    /// Runs in the background and shows a notification with the result.
    /// </summary>
    /// <returns>False if no update is available for the plugin or it is already updating.</returns>
    public static bool UpdatePlugin(string name) {
        try {
            var pluginManager = GetDalamudService("Dalamud.Plugin.Internal.PluginManager");
            var update = GetUpdatablePlugins(pluginManager).FirstOrDefault(u =>
                GetProperty(u, "InstalledPlugin") is { } p &&
                GetProperty(p, "IsDev") is not true &&
                (string.Equals(GetProperty(p, "InternalName")?.ToString(), name, StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(GetProperty(p, "Name")?.ToString(), name, StringComparison.OrdinalIgnoreCase)));

            if (update == null) {
                DalamudApi.ShowNotification($"No update available for {name}", NotificationType.Info, 5000);
                return false;
            }

            var localPlugin = GetProperty(update, "InstalledPlugin")!;
            var internalName = GetProperty(localPlugin, "InternalName")?.ToString() ?? name;
            var displayName = GetProperty(localPlugin, "Name")?.ToString() ?? name;
            var version = GetProperty(update, "EffectiveVersion")?.ToString();

            if (!UpdatingPlugins.TryAdd(internalName, 0))
                return false;

            var updateMethod = pluginManager.GetType().GetMethod("UpdateSinglePluginAsync", BindingFlags.Public | BindingFlags.Instance)
                ?? throw new MissingMethodException(pluginManager.GetType().FullName, "UpdateSinglePluginAsync");

            DalamudApi.ShowNotification($"Updating {displayName} to v{version}...", NotificationType.Info, 3000);

            Task.Run(async () => {
                string? status = null;
                try {
                    var task = (Task)updateMethod.Invoke(pluginManager, [update, true, false])!;
                    await task;
                    var result = task.GetType().GetProperty("Result")?.GetValue(task);
                    status = result == null ? null : GetProperty(result, "Status")?.ToString();
                } catch (Exception e) {
                    DalamudApi.PluginLog.Error($"Update {displayName} failed: {e.Message}\n{e.StackTrace}");
                    status = "Exception";
                } finally {
                    UpdatingPlugins.TryRemove(internalName, out _);
                }

                var success = status is "Success" or "AlreadyUpToDate";
                DalamudApi.ShowNotification(
                    success ? $"{displayName} updated to v{version}" : $"{displayName} update failed ({status})",
                    success ? NotificationType.Success : NotificationType.Error,
                    5000);
            });

            return true;
        } catch (Exception e) {
            DalamudApi.PluginLog.Error($"{e.Message}\n{e.StackTrace}");
            return false;
        }
    }

    private static volatile bool _isCheckingForUpdates;

    public static bool IsCheckingForUpdates => _isCheckingForUpdates;

    /// <summary>
    /// Reloads all plugin repositories (PluginManager.ReloadAllReposAsync), which also re-detects
    /// available plugin updates. Runs in the background and shows a notification with the result.
    /// </summary>
    /// <returns>False if a check is already running or it could not be started.</returns>
    public static bool CheckForUpdates() {
        if (_isCheckingForUpdates)
            return false;

        try {
            var pluginManager = GetDalamudService("Dalamud.Plugin.Internal.PluginManager");
            var reloadMethod = pluginManager.GetType().GetMethod("ReloadAllReposAsync", BindingFlags.Public | BindingFlags.Instance, Type.EmptyTypes)
                ?? throw new MissingMethodException(pluginManager.GetType().FullName, "ReloadAllReposAsync");

            _isCheckingForUpdates = true;
            DalamudApi.ShowNotification("Checking for plugin updates...", NotificationType.Info, 3000);

            Task.Run(async () => {
                try {
                    await (Task)reloadMethod.Invoke(pluginManager, null)!;
                    var count = GetAvailableUpdates().Count;
                    DalamudApi.ShowNotification(
                        count > 0 ? $"{count} plugin update(s) available" : "All plugins are up to date",
                        count > 0 ? NotificationType.Success : NotificationType.Info,
                        5000);
                } catch (Exception e) {
                    DalamudApi.PluginLog.Error($"Check for updates failed: {e.Message}\n{e.StackTrace}");
                    DalamudApi.ShowNotification("Check for plugin updates failed", NotificationType.Error, 5000);
                } finally {
                    _isCheckingForUpdates = false;
                }
            });

            return true;
        } catch (Exception e) {
            _isCheckingForUpdates = false;
            DalamudApi.PluginLog.Error($"{e.Message}\n{e.StackTrace}");
            return false;
        }
    }

    private static IEnumerable<object> GetUpdatablePlugins(object? pluginManager = null) {
        pluginManager ??= GetDalamudService("Dalamud.Plugin.Internal.PluginManager");
        return GetProperty(pluginManager, "UpdatablePlugins") as IEnumerable<object> ?? [];
    }

    private static Assembly DalamudAssembly => DalamudApi.PluginInterface.GetType().Assembly;

    // Dalamud.Service<T>.Get()
    private static object GetDalamudService(string typeName) {
        var serviceType = DalamudAssembly
            .GetType("Dalamud.Service`1", true)!
            .MakeGenericType(DalamudAssembly.GetType(typeName, true)!);
        var getMethod = serviceType.GetMethod("Get", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static, Type.EmptyTypes)
            ?? throw new MissingMethodException(serviceType.FullName, "Get");
        return getMethod.Invoke(null, null)
            ?? throw new InvalidOperationException($"Service {typeName} is not available");
    }

    private static object? GetProperty(object obj, string name) {
        return obj.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.Instance)?.GetValue(obj);
    }

    private static void SetProperty(object obj, string name, object? value) {
        obj.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.Instance)?.SetValue(obj, value);
    }
}
