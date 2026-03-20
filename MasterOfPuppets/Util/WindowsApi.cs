using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace MasterOfPuppets.Util;

public static class WindowsApi {
    public const int WM_CLOSE = 0x10;

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
}
