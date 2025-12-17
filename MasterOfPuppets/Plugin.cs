using System.Globalization;

using Dalamud.Plugin;
using Dalamud.Plugin.Services;

using MasterOfPuppets.Ipc;
using MasterOfPuppets.Resources;
using MasterOfPuppets.Util.ImGuiExt.AutoComplete;
using MasterOfPuppets.Movement;

namespace MasterOfPuppets;

public class Plugin : IDalamudPlugin {
    internal static string Name => "Master Of Puppets";

    internal Configuration Config { get; }
    internal PluginUi Ui { get; }
    internal PluginCommandManager PluginCommandManager { get; }
    internal IpcProvider IpcProvider { get; }
    internal ChatWatcher ChatWatcher { get; }
    internal MacroHandler MacroHandler { get; }
    internal MacroManager MacroManager { get; }
    internal CompletionIndex CompletionIndex { get; }
    internal MovementManager MovementManager { get; }
    internal FollowPath FollowPath { get; }

    public Plugin(IDalamudPluginInterface pluginInterface) {
        pluginInterface.Create<DalamudApi>();
        Config = pluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        Config.Initialize(DalamudApi.PluginInterface);

        Ui = new PluginUi(this);
        IpcProvider = new IpcProvider(this);
        ChatWatcher = new ChatWatcher(this);
        MacroManager = new MacroManager(this);
        MacroHandler = new MacroHandler(this);
        PluginCommandManager = new PluginCommandManager(this);
        CompletionIndex = new CompletionIndex();

        FollowPath = new FollowPath(this);
        MovementManager = new MovementManager(FollowPath);

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

    public void Dispose() {
        DalamudApi.PluginInterface.UiBuilder.OpenConfigUi -= Ui.SettingsWindow.Toggle;
        DalamudApi.PluginInterface.UiBuilder.OpenMainUi -= Ui.MainWindow.Toggle;
        DalamudApi.PluginInterface.UiBuilder.Draw -= Ui.Draw;
        DalamudApi.ClientState.Logout -= OnLogout;
        DalamudApi.ClientState.Login -= OnLogin;
        DalamudApi.PluginInterface.LanguageChanged -= OnLanguageChange;
        DalamudApi.Framework.Update -= OnFrameworkUpdate;

        IpcProvider.Dispose();
        ChatWatcher.Dispose();
        MacroHandler.Dispose();
        PluginCommandManager.Dispose();
        MovementManager.Dispose();
        FollowPath.Dispose();

        Ui.Dispose();
    }

    private void OnFrameworkUpdate(IFramework fwk) {
        FollowPath.Update(fwk);
        MovementManager.Update();
    }

    private static void OnLanguageChange(string langCode) {
        Language.Culture = new CultureInfo(langCode);
    }

    private void OnLogin() {
        if (Config.OpenOnLogin) {
            Ui.MainWindow.IsOpen = true;
        }
    }

    private void OnLogout(int type, int code) {
        Ui.MainWindow.IsOpen = false;
    }
}

