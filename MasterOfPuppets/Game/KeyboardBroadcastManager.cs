using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

using MasterOfPuppets.Util;

namespace MasterOfPuppets;

/// <summary>
/// Captures keyboard input on the local client via GetKeyboardState polling and
/// broadcasts each key-down / key-up transition to peers via IPC.
/// Peers call <see cref="ForwardKey"/> to replay the event to their FFXIV window.
/// </summary>
public sealed class KeyboardBroadcastManager : IDisposable {

    // VK codes for keys that carry the "extended key" flag in lParam.
    private static readonly HashSet<int> ExtendedKeys = new() {
        0x21, 0x22, 0x23, 0x24,  // Page Up/Down, End, Home
        0x25, 0x26, 0x27, 0x28,  // Arrow keys
        0x2D, 0x2E,               // Insert, Delete
        0x5B, 0x5C, 0x5D,         // Left Win, Right Win, Apps
        0x6F,                     // Numpad /
        0xA3,                     // Right Ctrl
        0xA5,                     // Right Alt
    };

    private readonly Plugin _plugin;
    private readonly byte[] _prev = new byte[256];
    private readonly byte[] _curr = new byte[256];
    private nint _gameHwnd;
    private bool _hadFocus;

    /// <summary>True when this client is capturing keys and broadcasting them to peers.</summary>
    public bool IsCapturing { get; private set; }

    /// <summary>True when this client accepts incoming <c>KeyboardInput</c> IPC messages and
    /// forwards them to the local FFXIV window.</summary>
    public bool IsReceiving { get; set; }

    public KeyboardBroadcastManager(Plugin plugin) {
        _plugin = plugin;
    }

    public void StartCapture() => IsCapturing = true;
    public void StopCapture() => IsCapturing = false;

    /// <summary>
    /// Called every framework-update tick. Polls keyboard state and broadcasts transitions.
    /// Must be called from a thread where <c>GetKeyboardState</c> reflects game-window input
    /// (i.e. the Dalamud Framework.Update thread).
    /// </summary>
    public void Update() {
        if (!IsCapturing) return;

        // prevent send keys while windows lost focus
        if (_gameHwnd == nint.Zero)
            _gameHwnd = FindGameWindow();

        bool hasFocus = _gameHwnd != nint.Zero && WindowsApi.GetForegroundWindow() == _gameHwnd;

        if (!hasFocus) {
            if (_hadFocus) {
                // Just lost focus - release all held keys on puppets to prevent stuck keys.
                for (int vk = 1; vk < 255; vk++) {
                    if ((_prev[vk] & 0x80) == 0) continue;
                    if (_plugin.Config.KeyboardBroadcastIgnoredKeys.Contains(vk)) continue;
                    uint scan = WindowsApi.MapVirtualKey((uint)vk, WindowsApi.MAPVK_VK_TO_VSC);
                    uint ext = ExtendedKeys.Contains(vk) ? 1u : 0u;
                    _plugin.IpcProvider.BroadcastKeyInput(WindowsApi.WM_KEYUP, (uint)vk, scan, ext);
                }
                Array.Clear(_prev, 0, 256);
            }
            _hadFocus = false;
            return;
        }

        _hadFocus = true;
        WindowsApi.GetKeyboardState(_curr);

        for (int vk = 1; vk < 255; vk++) {
            bool wasDown = (_prev[vk] & 0x80) != 0;
            bool isDown = (_curr[vk] & 0x80) != 0;
            if (isDown == wasDown) continue;
            if (_plugin.Config.KeyboardBroadcastIgnoredKeys.Contains(vk)) continue;

            uint scan = WindowsApi.MapVirtualKey((uint)vk, WindowsApi.MAPVK_VK_TO_VSC);
            uint ext = ExtendedKeys.Contains(vk) ? 1u : 0u;
            _plugin.IpcProvider.BroadcastKeyInput(isDown ? WindowsApi.WM_KEYDOWN : WindowsApi.WM_KEYUP, (uint)vk, scan, ext);
        }

        Array.Copy(_curr, _prev, 256);
    }

    /// <summary>
    /// Replays a received key event to the local FFXIV window via PostMessage.
    /// Called by <see cref="IpcProvider"/> when a <c>KeyboardInput</c> message arrives.
    /// </summary>
    public void ForwardKey(uint msg, uint vkCode, uint scanCode, uint flags) {
        if (!IsReceiving) return;

        if (_gameHwnd == nint.Zero)
            _gameHwnd = FindGameWindow();
        if (_gameHwnd == nint.Zero) return;

        bool isExtended = (flags & 0x01) != 0;
        bool isKeyUp = msg == WindowsApi.WM_KEYUP;

        nint lParam = 1;
        lParam |= (nint)(scanCode << 16);
        if (isExtended) lParam |= (nint)(1 << 24);
        if (isKeyUp) lParam |= unchecked((nint)0xC0000000L); // prev-state + transition bits

        WindowsApi.PostMessage(_gameHwnd, msg, (nint)vkCode, lParam);
    }

    /// <summary>
    /// Finds the FFXIV game window (class "FFXIVGAME") that belongs to this process.
    /// </summary>
    private static nint FindGameWindow() {
        int currentPid = Process.GetCurrentProcess().Id;
        nint found = nint.Zero;
        var className = new StringBuilder(256);

        WindowsApi.EnumWindows((hwnd, _) => {
            WindowsApi.GetWindowThreadProcessId(hwnd, out int pid);
            if (pid != currentPid) return true;

            className.Clear();
            WindowsApi.GetClassName(hwnd, className, className.Capacity);
            if (className.ToString() == "FFXIVGAME") {
                found = hwnd;
                return false; // stop enumeration
            }
            return true;
        }, nint.Zero);

        return found;
    }

    public void Dispose() {
        IsCapturing = false;
        IsReceiving = false;
    }
}
