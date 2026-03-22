using System;

using Dalamud.Interface.Windowing;

using MasterOfPuppets.Debug;

namespace MasterOfPuppets;

public class PluginUi : IDisposable {
    private Plugin Plugin { get; }

    private WindowSystem WindowSystem { get; } = new();
    public MainWindow MainWindow { get; }
    public SettingsWindow SettingsWindow { get; }
    public ActionsBroadcastWindow ActionsBroadcastWindow { get; }
    public MacroEditorWindow MacroEditorWindow { get; }
    public MacroBatchEditorWindow MacroBatchEditorWindow { get; }
    public MacroQueueWindow MacroQueueWindow { get; }
    public CharactersWindow CharactersWindow { get; }
    public HelpWindow HelpWindow { get; }
    public MacroImportExportWindow MacroImportExportWindow { get; }
    public IconPickerDialogWindow IconPickerDialogWindow { get; }
    public DebugWindow DebugWindow { get; }
    public FormationWindow FormationWindow { get; }
    public PeerMonitorWindow PeerMonitorWindow { get; }

    public PluginUi(Plugin plugin) {
        Plugin = plugin;

        MainWindow = AddWindow(new MainWindow(Plugin, this));
        SettingsWindow = AddWindow(new SettingsWindow(Plugin));
        MacroEditorWindow = AddWindow(new MacroEditorWindow(Plugin, this));
        MacroBatchEditorWindow = AddWindow(new MacroBatchEditorWindow(Plugin));
        MacroQueueWindow = AddWindow(new MacroQueueWindow(Plugin));
        CharactersWindow = AddWindow(new CharactersWindow(Plugin));
        ActionsBroadcastWindow = AddWindow(new ActionsBroadcastWindow(Plugin));
        HelpWindow = AddWindow(new HelpWindow(Plugin));
        MacroImportExportWindow = AddWindow(new MacroImportExportWindow(Plugin));
        IconPickerDialogWindow = AddWindow(new IconPickerDialogWindow());
        DebugWindow = AddWindow(new DebugWindow(Plugin));
        FormationWindow = AddWindow(new FormationWindow(Plugin));
        PeerMonitorWindow = AddWindow(new PeerMonitorWindow(Plugin));
    }

    private T AddWindow<T>(T window) where T : Window {
        WindowSystem.AddWindow(window);
        return window;
    }

    public void Dispose() {
        WindowSystem.RemoveAllWindows();
    }

    public void Draw() {
        if (!DalamudApi.PlayerState.IsLoaded) return;
        WindowSystem.Draw();
    }
}
