using System;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

using Dalamud.Plugin;
using Dalamud.Plugin.Services;

using MasterOfPuppets.Camera;
using MasterOfPuppets.Formations;
using MasterOfPuppets.Ipc;
using MasterOfPuppets.Movement;
using MasterOfPuppets.RemoteControl;
using MasterOfPuppets.Resources;
using MasterOfPuppets.Util.ImGuiExt.AutoComplete;

namespace MasterOfPuppets;

public class Plugin : IDalamudPlugin {
    internal static string Name => "Master Of Puppets";

    internal Configuration Config { get; }
    internal PluginUi Ui { get; }
    internal PluginCommandManager PluginCommandManager { get; }
    internal IpcProvider IpcProvider { get; }
    internal ChatWatcher ChatWatcher { get; }
    // internal ChatLogMessageWatcher ChatLogMessageWatcher { get; }
    internal ItemMover ItemMover { get; }
    internal MacroHandler MacroHandler { get; }
    internal MacroManager MacroManager { get; }
    internal FormationManager FormationManager { get; }
    internal CompletionIndex CompletionIndex { get; }
    internal MovementManager MovementManager { get; }
    internal FollowPath FollowPath { get; }
    internal SimpleInputMovement SimpleInputMovement { get; }
    internal FormationTrackingSession FormationTrackingSession { get; }
    internal MultiboxManager MultiboxManager { get; }
    internal GameRenderManager GameRenderManager { get; }
    internal GameWindowManager GameWindowManager { get; }
    internal KeyboardBroadcastManager KeyboardBroadcastManager { get; }
    internal AutoLoginManager AutoLoginManager { get; }
    internal ServerBarProvider ServerBarProvider { get; }

    // Remote Control
    internal RemoteControlServer? RemoteControlServer { get; private set; }
    internal TunnelService? TunnelService { get; private set; }
    internal RemoteControlClient? RemoteControlClient { get; private set; }
    public string RemoteControlStatus => RemoteControlServer?.StatusMessage ?? "Stopped";
    public string TunnelStatus => TunnelService == null ? "Stopped"
        : TunnelService.Status switch {
            RemoteControl.TunnelStatus.Starting => "Starting…",
            RemoteControl.TunnelStatus.Running  => $"Running: {TunnelService.PublicUrl}",
            RemoteControl.TunnelStatus.Error    => $"Error: {TunnelService.LastError}",
            _                                   => "Stopped",
        };
    public string RemoteControlClientStatus => RemoteControlClient?.StatusMessage ?? "Stopped";

    public Plugin(IDalamudPluginInterface pluginInterface) {
        pluginInterface.Create<DalamudApi>();
        Config = pluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        Config.Initialize(DalamudApi.PluginInterface);
        GameCameraManager.Initialize();

        Ui = new PluginUi(this);
        // Dalamud.Utility.Util.IsWine()
        var platform = Dalamud.Utility.Util.GetHostPlatform();
        IIpcTransport ipcTransport =
            platform == OSPlatform.Linux || platform == OSPlatform.FreeBSD
                ? new LinuxIpcTransport()
                : new TinyIpcTransport();

        IpcProvider = new IpcProvider(this, ipcTransport);
        ChatWatcher = new ChatWatcher(this);
        // ChatLogMessageWatcher = new ChatLogMessageWatcher(this);
        ItemMover = new ItemMover(this);
        MacroManager = new MacroManager(this);
        FormationManager = new FormationManager(this);
        MacroHandler = new MacroHandler(this);
        PluginCommandManager = new PluginCommandManager(this);
        CompletionIndex = new CompletionIndex();

        FollowPath = new FollowPath(this);
        MovementManager = new MovementManager(FollowPath);
        SimpleInputMovement = new SimpleInputMovement();
        FormationTrackingSession = new FormationTrackingSession(this);
        MultiboxManager = new MultiboxManager(this);
        GameRenderManager = new GameRenderManager(this);
        GameWindowManager = new GameWindowManager(this);
        KeyboardBroadcastManager = new KeyboardBroadcastManager(this);
        AutoLoginManager = new AutoLoginManager(this);
        // load last
        ServerBarProvider = new ServerBarProvider(this);

        OnLanguageChange(DalamudApi.PluginInterface.UiLanguage);
        DalamudApi.PluginInterface.LanguageChanged += OnLanguageChange;

        DalamudApi.ClientState.Login += OnLogin;
        DalamudApi.ClientState.Logout += OnLogout;
        DalamudApi.PluginInterface.UiBuilder.Draw += Ui.Draw;
        DalamudApi.PluginInterface.UiBuilder.OpenConfigUi += Ui.SettingsWindow.Toggle;
        DalamudApi.PluginInterface.UiBuilder.OpenMainUi += Ui.MainWindow.Toggle;
        DalamudApi.Framework.Update += OnFrameworkUpdate;

        if (Config.OpenOnStartup) {
            Ui.MainWindow.IsOpen = true;
        }

        if (AutoLoginPlanner.HasEnabledCandidates(Config.Characters) && !DalamudApi.ClientState.IsLoggedIn) {
            AutoLoginManager.Start();
        }

        if (Config.RemoteControlEnabled) {
            RefreshRemoteControlServer();
        }

        if (Config.RemoteControlClientEnabled) {
            RefreshRemoteControlClient();
        }
    }

    private void OnFrameworkUpdate(IFramework framework) {
        if (!DalamudApi.ClientState.IsLoggedIn) { return; }

        FollowPath.Update(framework);
        MovementManager.Update();
        FormationTrackingSession.Update();
        KeyboardBroadcastManager.Update();
        IpcProvider.UpdateCharacterDataHeartbeat();

        if (Config.AutoAcceptPartyInvite || Config.AutoAcceptTeleport) {
            var charConfig = Config.Characters.FirstOrDefault(c => c.Cid == DalamudApi.PlayerState.ContentId);
            GameDialogManager.AutoAcceptUpdate(
                Config.AutoAcceptPartyInvite && (charConfig?.AutoAcceptPartyInvite ?? true),
                Config.AutoAcceptTeleport && (charConfig?.AutoAcceptTeleport ?? true),
                Config.AutoAcceptPartyInviteOnlyFromCharacters,
                Config.Characters);
        }
    }

    private static void OnLanguageChange(string langCode) {
        Language.Culture = new CultureInfo(langCode);
    }

    internal void StopAllMovementLocal() {
        FormationTrackingSession.Stop();
        SimpleInputMovement.StopMove();
        MovementManager.StopMove();
    }

    private void OnLogin() {
        AutoLoginManager.Stop();

        if (Config.OpenOnLogin) {
            Ui.MainWindow.IsOpen = true;
        }

        // if (Config.ApplyGameSettingsProfileOnLogin && !string.IsNullOrWhiteSpace(Config.LoginGameSettingsProfile)) {
        //     var profile = Config.GameSettingsProfiles.FirstOrDefault(p =>
        //         p.Name.Equals(Config.LoginGameSettingsProfile, StringComparison.OrdinalIgnoreCase));
        //     if (profile != null) {
        //         GameSettingsManager.ApplyProfile(profile, Config.GameSettingsProfileKeys);
        //     } else {
        //         DalamudApi.PluginLog.Warning($"Could not find Game Settings Profile to apply on login: {Config.LoginGameSettingsProfile}");
        //     }
        // }

        if (Config.RunLoginMacro) {
            int macroIndex = MacroManager.FindMacroIndex(Config.LoginMacro);
            if (macroIndex >= 0) {
                MacroHandler.ExecuteMacro(macroIndex);
            }
        }
    }

    private void OnLogout(int type, int code) {
        Ui.MainWindow.IsOpen = false;
    }

    internal void ReloadConfigFromDisk() {
        try {
            var configFile = DalamudApi.PluginInterface.ConfigFile;
            if (configFile.Exists) {
                var json = System.IO.File.ReadAllText(configFile.FullName);
                Config.UpdateFromJson(json);
                IpcProvider.SyncConfiguration();
                DalamudApi.ShowNotification("Configuration reloaded from disk and synced", Dalamud.Interface.ImGuiNotification.NotificationType.Success, 5000);
            }
        } catch (Exception ex) {
            DalamudApi.PluginLog.Error(ex, "Failed to reload configuration from disk");
            DalamudApi.ShowNotification($"Failed to reload configuration: {ex.Message}", Dalamud.Interface.ImGuiNotification.NotificationType.Error, 5000);
        }
    }

    public void RefreshRemoteControlServer() {
        RemoteControlServer?.Dispose();
        RemoteControlServer = null;

        if (!Config.RemoteControlEnabled) {
            if (TunnelService != null) RefreshTunnelService();
            return;
        }

        if (string.IsNullOrWhiteSpace(Config.RemoteControlToken))
            RegenerateRemoteControlToken();

        var server = new RemoteControlServer(this);
        server.Start(Config.RemoteControlPort, Config.RemoteControlToken);
        RemoteControlServer = server;

        if (!server.IsListening)
            DalamudApi.ShowNotification(server.StatusMessage, Dalamud.Interface.ImGuiNotification.NotificationType.Error, 5000);

        RefreshTunnelService();
    }

    public void RefreshTunnelService() {
        TunnelService?.Dispose();
        TunnelService = null;

        if (!Config.TunnelEnabled || !Config.RemoteControlEnabled || string.IsNullOrWhiteSpace(Config.TunnelCommand))
            return;

        try {
            TunnelService = TunnelService.TryStart(Config.TunnelCommand, Config.RemoteControlPort);
        } catch (Exception ex) {
            DalamudApi.PluginLog.Warning(ex, "[Tunnel] Failed to start.");
            DalamudApi.ShowNotification($"Tunnel failed to start: {ex.Message}", Dalamud.Interface.ImGuiNotification.NotificationType.Error, 5000);
        }
    }

    public void RegenerateRemoteControlToken() {
        Config.RemoteControlToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant();
        Config.Save();
    }

    public void RefreshRemoteControlClient() {
        RemoteControlClient?.Dispose();
        RemoteControlClient = null;

        if (!Config.RemoteControlClientEnabled
            || string.IsNullOrWhiteSpace(Config.RemoteControlClientUrl))
            return;

        var client = new RemoteControlClient(this);
        client.Start(Config.RemoteControlClientUrl, Config.RemoteControlClientToken);
        RemoteControlClient = client;
    }

    public void Dispose() {
        DalamudApi.PluginInterface.UiBuilder.OpenConfigUi -= Ui.SettingsWindow.Toggle;
        DalamudApi.PluginInterface.UiBuilder.OpenMainUi -= Ui.MainWindow.Toggle;
        DalamudApi.PluginInterface.UiBuilder.Draw -= Ui.Draw;
        DalamudApi.ClientState.Logout -= OnLogout;
        DalamudApi.ClientState.Login -= OnLogin;
        DalamudApi.PluginInterface.LanguageChanged -= OnLanguageChange;
        DalamudApi.Framework.Update -= OnFrameworkUpdate;
        GameCameraManager.Dispose();
        IpcProvider.Dispose();
        ChatWatcher.Dispose();
        // ChatLogMessageWatcher.Dispose();
        ItemMover.Dispose();
        MacroHandler.Dispose();
        PluginCommandManager.Dispose();
        MovementManager.Dispose();
        FollowPath.Dispose();
        FormationTrackingSession.Stop();
        SimpleInputMovement.Dispose();
        KeyboardBroadcastManager.Dispose();
        AutoLoginManager.Dispose();
        XivLauncherManager.Cancel();
        GameWindowManager.Dispose();
        GameRenderManager.Dispose();
        ServerBarProvider.Dispose();
        GameSettingsManager.Dispose();
        TunnelService?.Dispose();
        RemoteControlServer?.Dispose();
        RemoteControlClient?.Dispose();
        Ui.Dispose();
    }
}
