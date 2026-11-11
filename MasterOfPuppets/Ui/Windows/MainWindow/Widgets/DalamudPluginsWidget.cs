using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.ImGuiNotification;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Plugin;

using MasterOfPuppets.Resources;
using MasterOfPuppets.Util;
using MasterOfPuppets.Util.ImGuiExt;

namespace MasterOfPuppets;

public sealed class DalamudPluginsWidget : Widget
{
    public override string Title => "Plugins";
    public override FontAwesomeIcon Icon => FontAwesomeIcon.PuzzlePiece;

    // plugin state changes asynchronously after enable/disable, so refresh the list periodically
    private const long RefreshIntervalMs = 1000;

    private readonly List<IExposedPlugin> _plugins = new();
    private readonly List<PluginCollectionInfo> _collections = new();
    private Dictionary<string, string> _availableUpdates = new(StringComparer.OrdinalIgnoreCase);
    private string _searchString = string.Empty;
    private long _lastRefresh;
    private float _pluginBroadcastDelaySeconds = 0f;
    private bool _broadcastMode = false;

    public DalamudPluginsWidget(WidgetContext ctx) : base(ctx)
    {
    }

    public override void OnShow()
    {
        ReloadData();
        base.OnShow();
    }

    public override void OnHide()
    {
        _plugins.Clear();
        _collections.Clear();
        _availableUpdates.Clear();
        _searchString = string.Empty;
        base.OnHide();
    }

    public override void Draw()
    {
        if (Environment.TickCount64 - _lastRefresh >= RefreshIntervalMs)
            ReloadData();

        using (ImRaii.Group())
        {
            DrawHeader();
        }

        using var tabBar = ImRaii.TabBar("##PluginsWidgetTabs");
        if (!tabBar) return;

        using (var tab = ImRaii.TabItem($"Plugins ({_plugins.Count})###PluginsTab"))
        {
            if (tab)
            {
                using var scroll = ImRaii.Child("##PluginsScrollArea", new Vector2(-1, -1), false);
                if (scroll) DrawPluginsTable();
            }
        }

        using (var tab = ImRaii.TabItem($"Collections ({_collections.Count})###PluginCollectionsTab"))
        {
            if (tab)
            {
                using var scroll = ImRaii.Child("##PluginCollectionsScrollArea", new Vector2(-1, -1), false);
                if (scroll) DrawCollectionsTable();
            }
        }
    }

    private void ReloadData()
    {
        _plugins.Clear();
        _plugins.AddRange(DalamudPluginHelper.GetInstalledPlugins());
        _collections.Clear();
        _collections.AddRange(DalamudPluginHelper.GetCollections());
        _availableUpdates = DalamudPluginHelper.GetAvailableUpdates();
        _lastRefresh = Environment.TickCount64;
    }

    private int BroadcastDelayMs => (int)(_pluginBroadcastDelaySeconds * 1000);

    private void DrawHeader()
    {
        var loadedCount = _plugins.Count(p => p.IsLoaded);
        ImGui.Text($"Dalamud Plugins ({loadedCount}/{_plugins.Count} enabled)");
        if (_availableUpdates.Count > 0)
        {
            ImGui.SameLine();
            ImGui.TextColored(Style.Colors.Green, $"{_availableUpdates.Count} update(s) available");
        }
        ImGui.SameLine();
        ImGuiUtil.HelpMarker("""
        Update, enable or disable installed Dalamud plugins and collections
        Broadcast Mode off: Enable/Disable applies only to this client
        Broadcast Mode on: applies to all clients, one client at a time, waiting the delay between each client
        """);

        ImGui.Spacing();

        ImGui.SetNextItemWidth(350);
        ImGui.InputTextWithHint("##PluginsSearchInput", Language.SearchInputLabel, ref _searchString, 255, ImGuiInputTextFlags.AutoSelectAll);

        ImGui.SameLine();
        if (ImGuiUtil.IconButton(FontAwesomeIcon.Sync, "##PluginsReloadDataBtn", "Reload"))
        {
            ReloadData();
        }

        ImGui.SameLine();
        DrawCheckForUpdatesButton();

        ImGui.SameLine();
        if (ImGuiUtil.IconButtonStyled(FontAwesomeIcon.Wrench, ImGuiUtil.IconButtonStyle.Warning, "##PluginsFixLoadErrorBtn", "Fix Plugin Load Error"))
        {
            var fixedCount = DalamudPluginHelper.FixPluginLoadError();
            DalamudApi.ShowNotification(
                fixedCount > 0 ? $"Fixed {fixedCount} plugin(s) with load error" : "No plugins with load error found",
                fixedCount > 0 ? NotificationType.Success : NotificationType.Info,
                5000);
            ReloadData();
        }

        ImGui.SameLine();
        if (ImGuiUtil.IconButtonStyled(FontAwesomeIcon.SatelliteDish, ImGuiUtil.IconButtonStyle.Warning, "##PluginsFixLoadErrorBroadcastBtn", "Fix Plugin Load Error on all clients"))
        {
            Context.Plugin.IpcProvider.ExecuteTextCommand("/mop plugin fixload");
        }

        ImGui.SameLine();
        using (ImRaii.PushColor(ImGuiCol.Button, Style.Components.ButtonSuccessNormal, _broadcastMode))
        {
            if (ImGuiUtil.IconButton(FontAwesomeIcon.BroadcastTower, "##PluginsBroadcastModeBtn"))
            {
                _broadcastMode = !_broadcastMode;
            }
        }
        ImGuiUtil.ToolTip($"Broadcast Mode: {(_broadcastMode ? "ON" : "OFF")}\nToggle Enable/Disable between this client and all clients");

        using (ImRaii.Disabled(!_broadcastMode))
        {
            ImGui.SameLine();
            ImGui.Text("Delay:");
            ImGui.SameLine();
            ImGui.SetNextItemWidth(70 * ImGuiHelpers.GlobalScale);
            if (ImGui.DragFloat("##PluginBroadcastDelay", ref _pluginBroadcastDelaySeconds, 0.1f, 0f, 60f, "%.1fs"))
            {
                _pluginBroadcastDelaySeconds = Math.Clamp(_pluginBroadcastDelaySeconds, 0f, 60f);
            }
            if (ImGui.IsItemClicked(ImGuiMouseButton.Right))
            {
                _pluginBroadcastDelaySeconds = 0f;
            }
        }
        ImGuiUtil.ToolTip("Delay between each client when broadcasting (drag or double-click to type, right-click to reset)");

        if (_broadcastMode)
        {
            ImGui.SameLine();
            ImGui.TextColored(Style.Colors.Green, "[Broadcast Mode Active]");
        }

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();
    }

    private void DrawCheckForUpdatesButton()
    {
        var isChecking = DalamudPluginHelper.IsCheckingForUpdates;
        using (ImRaii.Disabled(isChecking))
        {
            if (ImGuiUtil.IconButtonStyled(FontAwesomeIcon.Download, ImGuiUtil.IconButtonStyle.Info, "##PluginsCheckForUpdatesBtn"))
            {
                if (_broadcastMode)
                {
                    Context.Plugin.IpcProvider.ExecuteTextCommand("/mop plugin checkupdate");
                }
                else
                {
                    DalamudPluginHelper.CheckForUpdates();
                }
            }
        }
        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
        {
            using (ImRaii.PushColor(ImGuiCol.Border, Style.Components.TooltipBorderColor))
            using (ImRaii.PushStyle(ImGuiStyleVar.PopupBorderSize, 1))
            using (ImRaii.Tooltip())
            {
                ImGui.Text(isChecking ? "Checking for updates..."
                    : _broadcastMode ? "[Broadcast] Check for plugin updates on all clients"
                    : "Check for plugin updates on this client");
                ImGui.TextColored(Style.Colors.Gray, "Reloads the plugin repositories, same as the Dalamud plugin installer");
            }
        }
    }

    private bool MatchesSearch(string text) =>
        string.IsNullOrEmpty(_searchString) || text.Contains(_searchString, StringComparison.OrdinalIgnoreCase);

    private static bool BeginTable(string id, int columns)
    {
        return ImGui.BeginTable(id, columns,
            ImGuiTableFlags.RowBg | ImGuiTableFlags.PadOuterX |
            ImGuiTableFlags.NoSavedSettings | ImGuiTableFlags.BordersInnerV);
    }

    private static void SetupStatusAndActionsColumns(bool includeUpdate = false)
    {
        var framePadding = ImGui.GetStyle().FramePadding.X * 2;
        var spacing = ImGui.GetStyle().ItemSpacing.X;
        var actionsColWidth = ImGui.CalcTextSize("Enable").X + framePadding + spacing + ImGui.CalcTextSize("Disable").X + framePadding;
        if (includeUpdate)
            actionsColWidth += ImGui.CalcTextSize("Update").X + framePadding + spacing;

        ImGui.TableSetupColumn("Status", ImGuiTableColumnFlags.WidthFixed, ImGui.CalcTextSize("Status").X);
        ImGui.TableSetupColumn("Actions", ImGuiTableColumnFlags.WidthFixed, actionsColWidth);
    }

    private static void DrawStatus(bool enabled)
    {
        ImGuiUtil.TextIcon(FontAwesomeIcon.Circle, enabled ? Style.Colors.Green : Style.Colors.Red);
        ImGuiUtil.ToolTip(enabled ? "Enabled" : "Disabled");
    }

    /// <summary>
    /// Enable + Disable buttons, always available regardless of the current state.
    /// Applies locally or to all clients (staggered) depending on Broadcast Mode.
    /// </summary>
    private void DrawActions(Action<bool> setLocal, Func<bool, string> getCommand)
    {
        DrawActionButton(true, setLocal, getCommand);
        ImGui.SameLine();
        DrawActionButton(false, setLocal, getCommand);
    }

    private void DrawActionButton(bool enable, Action<bool> setLocal, Func<bool, string> getCommand)
    {
        DrawCommandButton(
            enable ? "Enable" : "Disable",
            enable ? ImGuiUtil.ButtonStyle.Success : ImGuiUtil.ButtonStyle.Danger,
            getCommand(enable),
            () => setLocal(enable));
    }

    /// <summary>
    /// Update button: locally only enabled when this client has an update for the plugin;
    /// in Broadcast Mode always enabled, since other clients may have the update even if this one doesn't.
    /// </summary>
    private void DrawUpdateButton(IExposedPlugin plugin)
    {
        var hasUpdate = _availableUpdates.TryGetValue(plugin.InternalName, out var newVersion);
        var isUpdating = DalamudPluginHelper.IsPluginUpdating(plugin.InternalName);

        string? localInfo = isUpdating ? "Updating..."
            : hasUpdate ? $"v{plugin.Version} -> v{newVersion}"
            : "No update available on this client";

        DrawCommandButton(
            "Update",
            ImGuiUtil.ButtonStyle.Info,
            DalamudPluginHelper.GetUpdatePluginCommand(plugin),
            () => DalamudPluginHelper.UpdatePlugin(plugin.InternalName),
            localEnabled: hasUpdate && !isUpdating,
            extraTooltip: localInfo);
    }

    private void DrawCommandButton(string label, ImGuiUtil.ButtonStyle style, string command, Action runLocal, bool localEnabled = true, string? extraTooltip = null)
    {
        using (ImRaii.Disabled(!_broadcastMode && !localEnabled))
        {
            if (ImGuiUtil.ButtonStyled($"{label}##{label}Btn", style))
            {
                if (_broadcastMode)
                {
                    Context.Plugin.IpcProvider.ExecutePluginCommandStaggered(command, BroadcastDelayMs);
                    DalamudApi.ShowNotification($"{label} broadcast to all clients", NotificationType.Info, 3000);
                }
                else
                {
                    runLocal();
                }
            }
        }
        // AllowWhenDisabled so the tooltip still shows why the local action is unavailable
        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
        {
            using (ImRaii.PushColor(ImGuiCol.Border, Style.Components.TooltipBorderColor))
            using (ImRaii.PushStyle(ImGuiStyleVar.PopupBorderSize, 1))
            using (ImRaii.Tooltip())
            {
                ImGui.Text(_broadcastMode ? $"[Broadcast] {label} on all clients" : $"{label} on this client");
                if (!string.IsNullOrEmpty(extraTooltip))
                    ImGui.TextColored(Style.Colors.Gray, extraTooltip);
                ImGui.Text("Right click for more options");
            }
        }

        var popupId = $"ContextMenu{label}Command";
        ImGui.OpenPopupOnItemClick(popupId, ImGuiPopupFlags.MouseButtonRight);

        using (ImRaii.PushColor(ImGuiCol.Border, Style.Components.TooltipBorderColor))
        using (ImRaii.PushStyle(ImGuiStyleVar.PopupBorderSize, 1))
        {
            using var popup = ImRaii.Popup(popupId);
            if (popup)
            {
                if (ImGui.MenuItem("Copy Command"))
                {
                    ImGui.SetClipboardText(command);
                    DalamudApi.ShowNotification(Language.ClipboardCopyMessage, NotificationType.Info, 5000);
                }

                if (ImGui.MenuItem("Copy Broadcast Command"))
                {
                    ImGui.SetClipboardText($"/mopbr {command}");
                    DalamudApi.ShowNotification(Language.ClipboardCopyMessage, NotificationType.Info, 5000);
                }
                ImGuiUtil.ToolTip("Runs on all clients at the same time (no delay between clients)");
            }
        }
    }

    private void DrawPluginsTable()
    {
        var visiblePlugins = _plugins
            .Where(p => MatchesSearch(p.Name) || MatchesSearch(p.InternalName))
            .ToList();

        if (visiblePlugins.Count == 0 && !string.IsNullOrEmpty(_searchString))
        {
            ImGuiUtil.DrawColoredBanner("Your search did not match any result", Style.Colors.Red);
            return;
        }

        if (!BeginTable("##PluginsTable", 5))
            return;

        ImGui.TableSetupColumn("#", ImGuiTableColumnFlags.WidthFixed, 28 * ImGuiHelpers.GlobalScale);
        ImGui.TableSetupColumn("Name", ImGuiTableColumnFlags.WidthStretch);
        ImGui.TableSetupColumn("Version", ImGuiTableColumnFlags.WidthFixed, 80 * ImGuiHelpers.GlobalScale);
        SetupStatusAndActionsColumns(includeUpdate: true);
        ImGui.TableHeadersRow();

        for (int i = 0; i < visiblePlugins.Count; i++)
        {
            var plugin = visiblePlugins[i];
            ImGui.PushID(plugin.InternalName);
            ImGui.TableNextRow();

            // col 0: index
            ImGui.TableNextColumn();
            ImGui.Text($"{i + 1:00}");

            // col 1: name
            ImGui.TableNextColumn();
            ImGui.Text(plugin.Name);
            var pluginCollections = _collections
                .Where(c => c.Plugins.Any(e => string.Equals(e.InternalName, plugin.InternalName, StringComparison.OrdinalIgnoreCase)))
                .Select(c => c.Name)
                .ToList();
            ImGuiUtil.ToolTip($"""
            {plugin.Name}
            Internal Name: {plugin.InternalName}
            {(plugin.IsDev ? "\nDev Plugin" : "")}{(plugin.IsTesting ? "\nTesting" : "")}{(plugin.IsThirdParty ? "\nThird Party" : "")}{(pluginCollections.Count > 0 ? $"\nCollections: {string.Join(", ", pluginCollections)}" : "")}
            """);

            // col 2: version (green when an update is available on this client)
            ImGui.TableNextColumn();
            if (_availableUpdates.TryGetValue(plugin.InternalName, out var newVersion))
            {
                ImGui.TextColored(Style.Colors.Green, $"{plugin.Version}");
                ImGuiUtil.ToolTip($"Update available: v{newVersion}");
            }
            else
            {
                ImGui.TextColored(Style.Colors.Gray, $"{plugin.Version}");
            }

            // col 3: status indicator
            ImGui.TableNextColumn();
            DrawStatus(plugin.IsLoaded);

            // col 4: actions
            ImGui.TableNextColumn();
            DrawUpdateButton(plugin);
            ImGui.SameLine();
            DrawActions(
                enable => DalamudPluginHelper.SetPluginState(plugin, enable),
                enable => DalamudPluginHelper.GetPluginCommand(plugin, enable));

            ImGui.PopID();
        }

        ImGui.EndTable();
    }

    private void DrawCollectionsTable()
    {
        var visibleCollections = _collections.Where(c => MatchesSearch(c.Name)).ToList();

        if (visibleCollections.Count == 0)
        {
            if (!string.IsNullOrEmpty(_searchString))
                ImGuiUtil.DrawColoredBanner("Your search did not match any result", Style.Colors.Red);
            else
                ImGui.TextColored(Style.Colors.Gray, "No collections found");
            return;
        }

        if (!BeginTable("##PluginCollectionsTable", 5))
            return;

        ImGui.TableSetupColumn("#", ImGuiTableColumnFlags.WidthFixed, 28 * ImGuiHelpers.GlobalScale);
        ImGui.TableSetupColumn("Name", ImGuiTableColumnFlags.WidthStretch);
        ImGui.TableSetupColumn("Plugins", ImGuiTableColumnFlags.WidthFixed, ImGui.CalcTextSize("Plugins").X);
        SetupStatusAndActionsColumns();
        ImGui.TableHeadersRow();

        for (int i = 0; i < visibleCollections.Count; i++)
        {
            var collection = visibleCollections[i];
            ImGui.PushID($"Collection_{i}_{collection.Name}");
            ImGui.TableNextRow();

            // col 0: index
            ImGui.TableNextColumn();
            ImGui.Text($"{i + 1:00}");

            // col 1: name + plugin list tooltip
            ImGui.TableNextColumn();
            ImGui.Text(collection.Name);
            var pluginNames = collection.Plugins
                .Select(e => _plugins.FirstOrDefault(p => string.Equals(p.InternalName, e.InternalName, StringComparison.OrdinalIgnoreCase))?.Name ?? e.InternalName)
                .OrderBy(n => n, StringComparer.OrdinalIgnoreCase);
            ImGuiUtil.ToolTip(collection.Plugins.Count > 0 ? string.Join("\n", pluginNames) : "No plugins");

            // col 2: plugin count
            ImGui.TableNextColumn();
            ImGui.Text($"{collection.Plugins.Count}");

            // col 3: status indicator
            ImGui.TableNextColumn();
            DrawStatus(collection.IsEnabled);

            // col 4: actions
            ImGui.TableNextColumn();
            DrawActions(
                enable => DalamudPluginHelper.SetCollectionState(collection.Name, enable),
                enable => DalamudPluginHelper.GetCollectionCommand(collection.Name, enable));

            ImGui.PopID();
        }

        ImGui.EndTable();
    }
}
