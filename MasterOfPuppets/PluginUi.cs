using System;

using Dalamud.Interface.Windowing;

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
    public EmotesWindow EmotesWindow { get; }
    public MountWindow MountWindow { get; }
    public MinionWindow MinionWindow { get; }
    public ItemWindow ItemWindow { get; }
    public FacewearWindow FacewearWindow { get; }
    public FashionAccessoriesWindow FashionAccessoriesWindow { get; }
    public MacroHelpWindow MacroHelpWindow { get; }
    public MacroImportExportWindow MacroImportExportWindow { get; }
    public IconPickerDialogWindow IconPickerDialogWindow { get; }
    public DebugWindow DebugWindow { get; }

    public PluginUi(Plugin plugin) {
        Plugin = plugin;

        MainWindow = new MainWindow(Plugin, this);
        SettingsWindow = new SettingsWindow(Plugin);
        MacroEditorWindow = new MacroEditorWindow(Plugin, this);
        MacroBatchEditorWindow = new MacroBatchEditorWindow(Plugin);
        MacroQueueWindow = new MacroQueueWindow(Plugin);
        CharactersWindow = new CharactersWindow(Plugin);
        EmotesWindow = new EmotesWindow(Plugin);
        MountWindow = new MountWindow(Plugin);
        MinionWindow = new MinionWindow(Plugin);
        ItemWindow = new ItemWindow(Plugin);
        FacewearWindow = new FacewearWindow(Plugin);
        FashionAccessoriesWindow = new FashionAccessoriesWindow(Plugin);
        MacroHelpWindow = new MacroHelpWindow(Plugin);
        MacroImportExportWindow = new MacroImportExportWindow(Plugin);
        IconPickerDialogWindow = new IconPickerDialogWindow();
        DebugWindow = new DebugWindow(Plugin, this);

        WindowSystem.AddWindow(MainWindow);
        WindowSystem.AddWindow(SettingsWindow);
        WindowSystem.AddWindow(MacroEditorWindow);
        WindowSystem.AddWindow(MacroBatchEditorWindow);
        WindowSystem.AddWindow(MacroQueueWindow);
        WindowSystem.AddWindow(CharactersWindow);
        WindowSystem.AddWindow(EmotesWindow);
        WindowSystem.AddWindow(MountWindow);
        WindowSystem.AddWindow(MinionWindow);
        WindowSystem.AddWindow(ItemWindow);
        WindowSystem.AddWindow(FacewearWindow);
        WindowSystem.AddWindow(FashionAccessoriesWindow);
        WindowSystem.AddWindow(MacroHelpWindow);
        WindowSystem.AddWindow(MacroImportExportWindow);
        WindowSystem.AddWindow(IconPickerDialogWindow);
        WindowSystem.AddWindow(DebugWindow);
    }

    public void Dispose() {
        WindowSystem.RemoveAllWindows();
    }

    public void Draw() {
        // var player = DalamudApi.Objects.LocalPlayer;
        // if (player == null) return;

        WindowSystem.Draw();
    }
}

