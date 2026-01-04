using System;

using Dalamud.Interface.Windowing;

using MasterOfPuppets.Debug;

namespace MasterOfPuppets;

public class PluginUi : IDisposable {
    private Plugin Plugin { get; }

    private WindowSystem WindowSystem { get; } = new();
    public MainWindow MainWindow { get; }
    public SettingsWindow SettingsWindow { get; }
    public MacroEditorWindow MacroEditorWindow { get; }
    public MacroBatchEditorWindow MacroBatchEditorWindow { get; }
    public MacroQueueWindow MacroQueueWindow { get; }
    public CharactersWindow CharactersWindow { get; }
    public ActionsBroadcastWindow ActionsBroadcastWindow { get; }

    public MacroHelpWindow MacroHelpWindow { get; }
    public MacroImportExportWindow MacroImportExportWindow { get; }
    public IconPickerDialogWindow IconPickerDialogWindow { get; }
    public DebugWindow DebugWindow { get; }

    public PluginUi(Plugin plugin) {
        Plugin = plugin;

        MainWindow = AddWindow(new MainWindow(Plugin, this));
        SettingsWindow = AddWindow(new SettingsWindow(Plugin));
        MacroEditorWindow = AddWindow(new MacroEditorWindow(Plugin, this));
        MacroBatchEditorWindow = AddWindow(new MacroBatchEditorWindow(Plugin));
        MacroQueueWindow = AddWindow(new MacroQueueWindow(Plugin));
        CharactersWindow = AddWindow(new CharactersWindow(Plugin));
        ActionsBroadcastWindow = AddWindow(new ActionsBroadcastWindow(Plugin));

        MacroHelpWindow = AddWindow(new MacroHelpWindow(Plugin));
        MacroImportExportWindow = AddWindow(new MacroImportExportWindow(Plugin));
        IconPickerDialogWindow = AddWindow(new IconPickerDialogWindow());
        DebugWindow = AddWindow(new DebugWindow(Plugin));

        // MainWindow = new MainWindow(Plugin, this);
        // WindowSystem.AddWindow(MainWindow);
    }

    private T AddWindow<T>(T window) where T : Window {
        WindowSystem.AddWindow(window);
        return window;
    }

    public void Dispose() {
        WindowSystem.RemoveAllWindows();
    }

    public void Draw() {
        // var player = DalamudApi.ObjectTable.LocalPlayer;
        // if (player == null) return;

        WindowSystem.Draw();
    }
}
