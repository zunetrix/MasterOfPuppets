using System.Diagnostics;

using FFXIVClientStructs.FFXIV.Client.System.Framework;

using MasterOfPuppets.Util;

namespace MasterOfPuppets;

internal unsafe class GameWindowManager {
    private readonly Plugin _plugin;
    private readonly int _originalMinWidth; // 0x400
    private readonly int _originalMinHeight; // 0x2D0

    internal GameWindowManager(Plugin plugin) {
        _plugin = plugin;

        var gameWindow = Framework.Instance()->GameWindow;
        if (gameWindow == null) return;

        _originalMinWidth = gameWindow->MinWidth;
        _originalMinHeight = gameWindow->MinHeight;

        if (_plugin.Config.AllowFreeGameWindowResize)
            RemoveSizeRestrictions();

        if (_plugin.Config.ShowCharacterNameInWindowTitle)
            SetCharacterNameWindowsTitle(true);
    }

    /// <summary>
    /// Removes the XIV size restriction
    /// </summary>
    internal void RemoveSizeRestrictions() {
        var gameWindow = Framework.Instance()->GameWindow;
        if (gameWindow == null) return;

        gameWindow->MinWidth = 0;
        gameWindow->MinHeight = 0;
    }

    /// <summary>
    /// Set back to SE default restriction
    /// </summary>
    internal void RestoreSizeRestrictions() {
        var gameWindow = Framework.Instance()->GameWindow;
        if (gameWindow == null) return;

        gameWindow->MinWidth = _originalMinWidth;
        gameWindow->MinHeight = _originalMinHeight;
    }

    public void SetCharacterNameWindowsTitle(bool enabled) {
        DalamudApi.Framework.RunOnTick(() => {
            var title = enabled
                ? $"{DalamudApi.PlayerState.CharacterName}@{DalamudApi.PlayerState.HomeWorld.Value.Name}"
                : "FINAL FANTASY XIV";
            WindowsApi.SetWindowText(Process.GetCurrentProcess().MainWindowHandle, title);
        });
    }
}
