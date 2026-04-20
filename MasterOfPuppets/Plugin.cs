using System.Globalization;
using System.Linq;

using Dalamud.Plugin;
using Dalamud.Plugin.Services;

using MasterOfPuppets.Camera;
using MasterOfPuppets.Ipc;
using MasterOfPuppets.Movement;
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
    internal ItemMover ItemMover { get; }
    internal MacroHandler MacroHandler { get; }
    internal MacroManager MacroManager { get; }
    internal CompletionIndex CompletionIndex { get; }
    internal MovementManager MovementManager { get; }
    internal FollowPath FollowPath { get; }
    internal SimpleInputMovement SimpleInputMovement { get; }
    internal MultiboxManager MultiboxManager { get; }
    internal GameWindowManager GameWindowManager { get; }
    internal KeyboardBroadcastManager KeyboardBroadcastManager { get; }

    public Plugin(IDalamudPluginInterface pluginInterface) {
        pluginInterface.Create<DalamudApi>();
        Config = pluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        Config.Initialize(DalamudApi.PluginInterface);
        GameCameraManager.Initialize();

        Ui = new PluginUi(this);
        // Dalamud.Utility.Util.GetHostPlatform();
        IpcProvider = new IpcProvider(this, Dalamud.Utility.Util.IsWine() ? new LinuxIpcTransport() : new TinyIpcTransport());
        ChatWatcher = new ChatWatcher(this);
        ItemMover = new ItemMover(this);
        MacroManager = new MacroManager(this);
        MacroHandler = new MacroHandler(this);
        PluginCommandManager = new PluginCommandManager(this);
        CompletionIndex = new CompletionIndex();

        FollowPath = new FollowPath(this);
        MovementManager = new MovementManager(FollowPath);
        SimpleInputMovement = new SimpleInputMovement();
        MultiboxManager = new MultiboxManager(this);
        GameWindowManager = new GameWindowManager(this);
        KeyboardBroadcastManager = new KeyboardBroadcastManager(this);

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
    }

    private void OnFrameworkUpdate(IFramework framework) {
        if (!DalamudApi.ClientState.IsLoggedIn) { return; }

        FollowPath.Update(framework);
        MovementManager.Update();
        KeyboardBroadcastManager.Update();

        if (Config.AutoAcceptPartyInvite || Config.AutoAcceptTeleport) {
            var charConfig = Config.Characters.FirstOrDefault(c => c.Cid == DalamudApi.PlayerState.ContentId);
            GameDialogManager.AutoAcceptUpdate(
                Config.AutoAcceptPartyInvite && (charConfig?.AutoAcceptPartyInvite ?? true),
                Config.AutoAcceptTeleport && (charConfig?.AutoAcceptTeleport ?? true));
        }
    }

    private static void OnLanguageChange(string langCode) {
        Language.Culture = new CultureInfo(langCode);
    }

    private void OnLogin() {
        if (Config.OpenOnLogin) {
            Ui.MainWindow.IsOpen = true;
        }

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
        ItemMover.Dispose();
        MacroHandler.Dispose();
        PluginCommandManager.Dispose();
        MovementManager.Dispose();
        FollowPath.Dispose();
        SimpleInputMovement.Dispose();
        KeyboardBroadcastManager.Dispose();
        GameWindowManager.Dispose();
        Ui.Dispose();
    }
}

