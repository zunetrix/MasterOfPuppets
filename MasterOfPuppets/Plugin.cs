using System;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;

using Dalamud.Plugin;
using Dalamud.Plugin.Services;

using MasterOfPuppets.Camera;
using MasterOfPuppets.Formations;
using MasterOfPuppets.Ipc;
using MasterOfPuppets.LuaScripting;
using MasterOfPuppets.LuaScripting.Synchronization;
using MasterOfPuppets.Movement;
using MasterOfPuppets.Resources;
using MasterOfPuppets.Util.ImGuiExt.AutoComplete;

namespace MasterOfPuppets;

public class Plugin : IDalamudPlugin {
    private readonly LuaFrameworkUpdateCadence _luaFrameworkUpdateCadence = new();

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
    internal CombatActionObserver CombatActionObserver { get; }
    internal EmoteObserver EmoteObserver { get; }
    internal LuaScriptManager LuaScriptManager { get; }
    internal MultiboxManager MultiboxManager { get; }
    internal GameRenderManager GameRenderManager { get; }
    internal GameWindowManager GameWindowManager { get; }
    internal KeyboardBroadcastManager KeyboardBroadcastManager { get; }
    internal AutoLoginManager AutoLoginManager { get; }
    internal ServerBarProvider ServerBarProvider { get; }
    internal DateTime LastDiskReloadTime { get; set; } = DateTime.MinValue;

    public Plugin(IDalamudPluginInterface pluginInterface) {
        pluginInterface.Create<DalamudApi>();
        Config = pluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        Config.Initialize(DalamudApi.PluginInterface);
        if (LuaScriptCatalog.EnsureMirrorCombatTarget(Config))
            DalamudApi.PluginLog.Information("[Lua] Added packaged script 'Mirror Combat Target' (no forced targeting).");
        // Load packaged defaults into memory without mutating the user's
        // configuration until they explicitly save through the UI.
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
        CombatActionObserver = new CombatActionObserver();
        EmoteObserver = new EmoteObserver();
        LuaScriptManager = new LuaScriptManager(this);
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
        DalamudApi.ClientState.TerritoryChanged += OnTerritoryChanged;
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
    }

    private void OnFrameworkUpdate(IFramework framework) {
        if (!DalamudApi.ClientState.IsLoggedIn) { return; }

        FollowPath.Update(framework);
        MovementManager.Update();
        FormationTrackingSession.Update();
        LuaScriptManager.Update();
        var nowMs = Environment.TickCount64;
        if (_luaFrameworkUpdateCadence.ShouldUpdateLaunches(nowMs))
            ChatWatcher.LuaDistributedLaunches.Update();
        if (_luaFrameworkUpdateCadence.ShouldTickSessions(nowMs))
            ChatWatcher.LuaDistributedSessions.Tick(DateTimeOffset.UtcNow);
        KeyboardBroadcastManager.Update();
        IpcProvider.UpdateCharacterDataHeartbeat();
        IpcProvider.UpdateMirrorDynamicLaunch();

        if (Config.AutoAcceptPartyInvite || Config.AutoAcceptTeleport) {
            var charConfig = Config.Characters.FirstOrDefault(c => c.Cid == DalamudApi.PlayerState.ContentId);
            GameDialogManager.AutoAcceptUpdate(
                Config.AutoAcceptPartyInvite && (charConfig?.AutoAcceptPartyInvite ?? true),
                Config.AutoAcceptTeleport && (charConfig?.AutoAcceptTeleport ?? true),
                Config.AutoAcceptPartyInviteOnlyFromCharacters,
                Config.Characters,
                IpcProvider.GetFreshPeerCharacterData());
        }
    }

    private static void OnLanguageChange(string langCode) {
        Language.Culture = new CultureInfo(langCode);
    }

    internal void StopAllMovementLocal() {
        LuaScriptManager.StopLocal();
        StopNonLuaMovementLocal();
    }

    internal void StopNonLuaMovementLocal() {
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
        LuaScriptManager.CancelForHostTransition("logged out or changed character");
        ChatWatcher.LuaDistributedLaunches.CancelAll("logged out or changed character");
        ChatWatcher.LuaDistributedSessions.StopAll("logged out or changed character");
        Ui.MainWindow.IsOpen = false;
    }

    private void OnTerritoryChanged(uint territoryId) {
        LuaScriptManager.CancelForHostTransition($"territory changed to {territoryId}");
        ChatWatcher.LuaDistributedLaunches.CancelAll($"territory changed to {territoryId}");
        ChatWatcher.LuaDistributedSessions.StopAll($"territory changed to {territoryId}");
    }

    internal void ReloadConfigFromDisk() {
        try {
            var configFile = DalamudApi.PluginInterface.ConfigFile;
            if (configFile.Exists) {
                string json;
                using (var stream = new System.IO.FileStream(configFile.FullName, System.IO.FileMode.Open, System.IO.FileAccess.Read, System.IO.FileShare.ReadWrite))
                using (var reader = new System.IO.StreamReader(stream)) {
                    json = reader.ReadToEnd();
                }
                LastDiskReloadTime = DateTime.UtcNow;
                Config.UpdateFromJson(json);
                IpcProvider.SyncConfiguration(saveLocally: false);
                DalamudApi.ShowNotification("Configuration reloaded from disk and synced", Dalamud.Interface.ImGuiNotification.NotificationType.Success, 5000);
            }
        } catch (Exception ex) {
            DalamudApi.PluginLog.Error(ex, "Failed to reload configuration from disk");
            DalamudApi.ShowNotification($"Failed to reload configuration: {ex.Message}", Dalamud.Interface.ImGuiNotification.NotificationType.Error, 5000);
        }
    }

    public void Dispose() {
        DalamudApi.PluginInterface.UiBuilder.OpenConfigUi -= Ui.SettingsWindow.Toggle;
        DalamudApi.PluginInterface.UiBuilder.OpenMainUi -= Ui.MainWindow.Toggle;
        DalamudApi.PluginInterface.UiBuilder.Draw -= Ui.Draw;
        DalamudApi.ClientState.Logout -= OnLogout;
        DalamudApi.ClientState.Login -= OnLogin;
        DalamudApi.ClientState.TerritoryChanged -= OnTerritoryChanged;
        DalamudApi.PluginInterface.LanguageChanged -= OnLanguageChange;
        DalamudApi.Framework.Update -= OnFrameworkUpdate;
        GameCameraManager.Dispose();
        CombatActionObserver.Dispose();
        EmoteObserver.Dispose();
        GameActionManager.Dispose();
        IpcProvider.Dispose();
        ChatWatcher.Dispose();
        // ChatLogMessageWatcher.Dispose();
        ItemMover.Dispose();
        MacroHandler.Dispose();
        PluginCommandManager.Dispose();
        MovementManager.Dispose();
        FollowPath.Dispose();
        LuaScriptManager.Dispose();
        FormationTrackingSession.Stop();
        SimpleInputMovement.Dispose();
        KeyboardBroadcastManager.Dispose();
        AutoLoginManager.Dispose();
        XivLauncherManager.Cancel();
        GameWindowManager.Dispose();
        GameRenderManager.Dispose();
        ServerBarProvider.Dispose();
        GameSettingsManager.Dispose();
        Ui.Dispose();
    }
}
