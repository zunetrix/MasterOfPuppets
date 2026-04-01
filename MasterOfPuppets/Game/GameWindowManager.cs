using System.Diagnostics;
using System.Runtime.InteropServices;

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

    /// <summary>
    /// Moves and resizes the main game window, adjusting for DWM invisible drop-shadow borders.
    /// This ensures the visual placement matches the requested coordinates perfectly.
    /// </summary>
    internal void MoveAndResizeWindow(int x, int y, int width, int height) {
        var hwnd = Process.GetCurrentProcess().MainWindowHandle;

        if (WindowsApi.GetWindowRect(hwnd, out var windowRect) &&
            WindowsApi.DwmGetWindowAttribute(hwnd, WindowsApi.DWMWA_EXTENDED_FRAME_BOUNDS, out var frameRect, Marshal.SizeOf<WindowsApi.RECT>()) == 0) {
            
            var leftMargin = windowRect.Left - frameRect.Left;
            var topMargin = windowRect.Top - frameRect.Top;
            var rightMargin = windowRect.Right - frameRect.Right;
            var bottomMargin = windowRect.Bottom - frameRect.Bottom;

            x += leftMargin;
            y += topMargin;
            width += (rightMargin - leftMargin);
            height += (bottomMargin - topMargin);
        }

        WindowsApi.MoveWindow(hwnd, x, y, width, height, true);
    }

    /// <summary>
    /// Gets the visual bounds of the main game window, excluding DWM invisible drop-shadow borders.
    /// </summary>
    internal bool GetWindowVisualBounds(out WindowsApi.RECT rect) {
        var hwnd = Process.GetCurrentProcess().MainWindowHandle;
        
        if (!WindowsApi.GetWindowRect(hwnd, out rect)) {
            return false;
        }

        if (WindowsApi.DwmGetWindowAttribute(hwnd, WindowsApi.DWMWA_EXTENDED_FRAME_BOUNDS, out var frameRect, Marshal.SizeOf<WindowsApi.RECT>()) == 0) {
            rect = frameRect;
        }

        return true;
    }
}
