using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;

using FFXIVClientStructs.FFXIV.Client.System.Framework;

using MasterOfPuppets.Util;

namespace MasterOfPuppets;

internal unsafe class GameWindowManager : IDisposable {
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

        DalamudApi.ClientState.Login += OnLogin;
        DalamudApi.ClientState.Logout += OnLogout;
    }
    public void Dispose() {
        DalamudApi.ClientState.Login -= OnLogin;
        DalamudApi.ClientState.Logout -= OnLogout;
    }

    private void OnLogin() {
        if (_plugin.Config.ShowCharacterNameInWindowTitle)
            SetCharacterNameWindowsTitle(true);
    }

    private void OnLogout(int type, int code) {
        SetCharacterNameWindowsTitle(false);
    }

    public void SetCharacterNameWindowsTitle(bool enabled) {
        if (!DalamudApi.PlayerState.IsLoaded) return;

        var playerName = DalamudApi.PlayerState.CharacterName.ToString();
        var homeWorld = DalamudApi.PlayerState.HomeWorld.Value.Name.ToString();

        var title = (enabled && !string.IsNullOrWhiteSpace(playerName))
            ? $"{playerName}@{homeWorld}"
            : "FINAL FANTASY XIV";

        DalamudApi.Framework.RunOnTick(() => {
            WindowsApi.SetWindowText(Process.GetCurrentProcess().MainWindowHandle, title);
        });
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

    public void ApplyWindowLayout(string layoutName) {
        var layout = _plugin.Config.WindowLayouts
            .FirstOrDefault(l => string.Equals(l.Name, layoutName, StringComparison.OrdinalIgnoreCase));
        if (layout == null) {
            DalamudApi.PluginLog.Warning($"[WindowLayout] Layout '{layoutName}' not found.");
            return;
        }

        var myCid = DalamudApi.PlayerState.ContentId;
        var slot = layout.Slots.FirstOrDefault(s => s.GetEffectiveCids(_plugin.Config.CidsGroups).Contains(myCid));
        if (slot == null) return;

        MoveAndResizeWindow(slot.X, slot.Y, slot.Width, slot.Height);
        DalamudApi.PluginLog.Debug($"[WindowLayout] Applied slot visually X={slot.X} Y={slot.Y} W={slot.Width} H={slot.Height}");
    }

    public void ApplyAutoTiledLayoutInternal(bool keepAspectRatio) {
        var peers = _plugin.IpcProvider.GetConnectedPeers().ToList();
        int count = peers.Count;
        if (count == 0) return;

        var myCid = DalamudApi.PlayerState.ContentId;
        int myIndex = peers.FindIndex(p => p.ContentId == myCid);
        if (myIndex == -1) return;

        if (!WindowsApi.SystemParametersInfo(WindowsApi.SPI_GETWORKAREA, 0, out var workArea, 0)) {
            workArea.Left = 0;
            workArea.Top = 0;
            workArea.Right = WindowsApi.GetSystemMetrics(WindowsApi.SM_CXSCREEN);
            workArea.Bottom = WindowsApi.GetSystemMetrics(WindowsApi.SM_CYSCREEN);
        }

        int workLeft = workArea.Left;
        int workTop = workArea.Top;
        int screenW = workArea.Width;
        int screenH = workArea.Height;
        if (screenW <= 0) screenW = 1920;
        if (screenH <= 0) screenH = 1080;

        int cols = (int)Math.Ceiling(Math.Sqrt(count));
        int rows = (int)Math.Ceiling((double)count / cols);
        if (count == 2) {
            cols = 2;
            rows = 1;
        }

        int cellW = screenW / cols;
        int cellH = screenH / rows;

        int col = myIndex % cols;
        int row = myIndex / cols;

        int slotW = cellW;
        int slotH = cellH;

        if (keepAspectRatio) {
            int targetH = (int)(cellW * (9f / 16f));
            if (targetH > cellH) {
                targetH = cellH;
                slotW = (int)(targetH * (16f / 9f));
            } else {
                slotH = targetH;
            }
        }

        int offsetX = workLeft;
        int offsetY = workTop;

        int slotX = offsetX + col * slotW;
        int slotY = offsetY + row * slotH;

        MoveAndResizeWindow(slotX, slotY, slotW, slotH);
        DalamudApi.PluginLog.Debug($"[WindowLayout] Applied Auto Tiled visually X={slotX} Y={slotY} W={slotW} H={slotH}");
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
