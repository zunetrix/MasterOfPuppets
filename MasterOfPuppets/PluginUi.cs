using System;

using Dalamud.Interface.Windowing;

namespace MasterOfPuppets
{
    internal class PluginUi : IDisposable
    {
        private Plugin Plugin { get; }

        private WindowSystem WindowSystem { get; } = new();
        public MainWindow MainWindow { get; }
        public SettingsWindow SettingsWindow { get; }
        public MacroEditorWindow MacroEditorWindow { get; }
        public MacroExecutionQueueWindow MacroExecutionQueueWindow { get; }
        public GameCatalogWindow GameCatalogWindow { get; }

        public PluginUi(Plugin plugin)
        {
            Plugin = plugin;

            MainWindow = new MainWindow(Plugin, this);
            SettingsWindow = new SettingsWindow(Plugin);
            MacroEditorWindow = new MacroEditorWindow(Plugin);
            MacroExecutionQueueWindow = new MacroExecutionQueueWindow(Plugin);
            GameCatalogWindow = new GameCatalogWindow(Plugin);

            WindowSystem.AddWindow(MainWindow);
            WindowSystem.AddWindow(SettingsWindow);
            WindowSystem.AddWindow(MacroEditorWindow);
            WindowSystem.AddWindow(MacroExecutionQueueWindow);
            WindowSystem.AddWindow(GameCatalogWindow);
        }

        public void Dispose()
        {
            WindowSystem.RemoveAllWindows();
        }

        public void Draw()
        {
            WindowSystem.Draw();

            var player = DalamudApi.ClientState.LocalPlayer;
            if (player == null) return;
        }
    }
}
