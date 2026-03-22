using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace MasterOfPuppets.Util;

public static class WindowsApi {
    public const int WM_CLOSE = 0x10;
    public const uint MAPVK_VK_TO_VSC = 0;
    public const uint WM_KEYDOWN = 0x0100;
    public const uint WM_KEYUP = 0x0101;

    public static void ExecuteCmd(string fileName, string args = null) {
        ProcessStartInfo processStartInfo;
        processStartInfo = args is null
            ? new ProcessStartInfo(fileName)
            : new ProcessStartInfo(fileName, args);
        processStartInfo.UseShellExecute = true;

        Process.Start(processStartInfo);
    }

    public static void OpenFolder(string folderPath) {
        try {
            if (!Directory.Exists(folderPath)) return;

            ExecuteCmd(folderPath);
        } catch (Exception e) {
            DalamudApi.PluginLog.Error(e.Message);
        }
    }

    public static void OpenFile(string filePath) {
        try {
            if (!File.Exists(filePath)) return;

            ExecuteCmd(filePath);
        } catch (Exception e) {
            DalamudApi.PluginLog.Error(e.Message);
        }
    }

    public static void OpenFileLocation(string filePath) {
        try {
            if (!File.Exists(filePath)) return;

            var args = $"/select,\"{filePath}\"";
            ExecuteCmd("explorer.exe", args);
        } catch (Exception e) {
            DalamudApi.PluginLog.Error($"Failed to open file location: {e.Message}");
        }
    }

    public static void OpenUrl(string url) {
        try {
            ExecuteCmd(url);
        } catch (Exception e) {
            DalamudApi.PluginLog.Error(e.Message);
        }
    }

    [DllImport("user32.dll")] public static extern int SetWindowText(IntPtr hWnd, string text);

    [DllImport("user32.dll")] public static extern int SendMessage(IntPtr hWnd, int wMsg, IntPtr wParam, IntPtr lParam);

    // key broadcast
    [DllImport("user32.dll")]
    public static extern bool GetKeyboardState(byte[] lpKeyState);
    // [DllImport("user32.dll")]
    // public static extern short GetAsyncKeyState(int vKey);

    [DllImport("user32.dll")]
    public static extern uint MapVirtualKey(uint uCode, uint uMapType);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool PostMessage(nint hWnd, uint Msg, nint wParam, nint lParam);

    public delegate bool EnumWindowsProc(nint hwnd, nint lParam);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, nint lParam);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern int GetWindowThreadProcessId(nint hWnd, out int lpdwProcessId);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern int GetClassName(nint hWnd, StringBuilder lpClassName, int nMaxCount);

    [DllImport("user32.dll")]
    public static extern nint GetForegroundWindow();
}
