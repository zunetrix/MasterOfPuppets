using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MasterOfPuppets;

internal static class XivLauncherManager {
    private static readonly object StateLock = new();

    private static CancellationTokenSource? _queueCancellation;
    private static bool _isLaunching;
    private static string _status = "Idle";
    private static int _currentIndex;
    private static int _totalCount;

    internal static bool IsLaunching {
        get {
            lock (StateLock) {
                return _isLaunching;
            }
        }
    }

    internal static string Status {
        get {
            lock (StateLock) {
                return _status;
            }
        }
    }

    internal static int CurrentIndex {
        get {
            lock (StateLock) {
                return _currentIndex;
            }
        }
    }

    internal static int TotalCount {
        get {
            lock (StateLock) {
                return _totalCount;
            }
        }
    }

    /// <summary>
    /// Starts the supplied accounts in order. Entries are copied before the background task starts,
    /// so editing the UI while a queue is running cannot alter the active queue.
    /// </summary>
    internal static bool StartQueue(Configuration config, IEnumerable<XivLaunchEntry> entries) {
        var queue = entries
            .Where(entry => entry != null)
            .Select(entry => new XivLaunchEntry {
                Name = entry.Name?.Trim() ?? string.Empty,
                UserName = entry.UserName?.Trim() ?? string.Empty,
                AutoLogin = entry.AutoLogin,
                UseSteamServiceAccount = entry.UseSteamServiceAccount,
                UseOtp = entry.UseOtp,
                Enabled = entry.Enabled,
                RoamingPath = entry.RoamingPath?.Trim() ?? string.Empty,
                XivLauncherPath = entry.XivLauncherPath?.Trim() ?? string.Empty,
            })
            .Where(entry => !string.IsNullOrWhiteSpace(entry.UserName))
            .ToList();

        if (queue.Count == 0) {
            lock (StateLock) {
                if (!_isLaunching) {
                    _status = "No launchable character usernames are configured.";
                    _currentIndex = 0;
                    _totalCount = 0;
                }
            }
            return false;
        }

        var launcherPath = config.XivLauncherPath?.Trim() ?? string.Empty;
        var delaySeconds = Math.Clamp(config.XivLaunchDelaySeconds, 0, 300);

        if (config.MultiboxEnabled) {
            MultiboxManager.RemoveMutexes();
        }

        CancellationTokenSource cancellation;
        lock (StateLock) {
            if (_isLaunching) {
                _status = "A launch queue is already running.";
                return false;
            }

            cancellation = new CancellationTokenSource();
            _queueCancellation = cancellation;
            _isLaunching = true;
            _currentIndex = 0;
            _totalCount = queue.Count;
            _status = $"Preparing to launch {queue.Count} account {(queue.Count == 1 ? string.Empty : "s")}...";
        }

        _ = Task.Run(() => RunQueueAsync(queue, launcherPath, delaySeconds, cancellation));
        return true;
    }

    internal static void Cancel() {
        CancellationTokenSource? cancellation;
        lock (StateLock) {
            cancellation = _queueCancellation;
            if (_isLaunching) {
                _status = "Cancelling launch queue...";
            }
        }

        cancellation?.Cancel();
    }

    internal static string GetDefaultLauncherDirecotry() {
        if (Dalamud.Utility.Util.IsWine()) return string.Empty;

        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(localAppData)) return string.Empty;
        return Path.Combine(localAppData, "XIVLauncher");
    }

    internal static string GetDefaultLauncherPath() {
        if (Dalamud.Utility.Util.IsWine()) return string.Empty;

        var localAppData = GetDefaultLauncherDirecotry();
        if (string.IsNullOrWhiteSpace(localAppData)) return string.Empty;

        return Path.Combine(localAppData, "XIVLauncher.exe");
    }

    private static async Task RunQueueAsync(
        IReadOnlyList<XivLaunchEntry> queue,
        string launcherPath,
        int delaySeconds,
        CancellationTokenSource cancellation) {
        try {
            ValidateLauncherPath(launcherPath);

            for (var index = 0; index < queue.Count; index++) {
                cancellation.Token.ThrowIfCancellationRequested();

                var entry = queue[index];

                var displayName = string.IsNullOrWhiteSpace(entry.Name) ? entry.UserName : entry.Name;
                SetProgress(index + 1, queue.Count, $"Launching {displayName} ({index + 1}/{queue.Count})...");

                LaunchAccount(launcherPath, entry);
                DalamudApi.PluginLog.Information($"[XivLauncher] Asked Windows to start XIVLauncher for '{displayName}'");

                if (index < queue.Count - 1 && delaySeconds > 0) {
                    var remainingTime = delaySeconds;
                    while (remainingTime > 0) {
                        cancellation.Token.ThrowIfCancellationRequested();
                        SetProgress(
                            index + 1,
                            queue.Count,
                            $"Launched {displayName}. Waiting {remainingTime}s for next launch...");
                        await Task.Delay(TimeSpan.FromSeconds(1), cancellation.Token).ConfigureAwait(false);
                        remainingTime--;
                    }
                }
            }

            SetIdleStatus($"Launch queue completed. Started {queue.Count} account{(queue.Count == 1 ? string.Empty : "s")}.");
        } catch (OperationCanceledException) {
            SetIdleStatus("Launch queue cancelled.");
        } catch (Exception ex) {
            DalamudApi.PluginLog.Error(ex, "[XivLauncher] Launch queue failed.");
            SetIdleStatus($"Launch failed: {ex.Message}");
        } finally {
            lock (StateLock) {
                if (ReferenceEquals(_queueCancellation, cancellation)) {
                    _queueCancellation = null;
                    _isLaunching = false;
                }
            }

            cancellation.Dispose();
        }
    }

    private static void ValidateLauncherPath(string launcherPath) {
        if (string.IsNullOrWhiteSpace(launcherPath)) {
            throw new InvalidOperationException("Set the path to XIVLauncher.exe first.");
        }

        if (!File.Exists(launcherPath)) {
            throw new FileNotFoundException("XIVLauncher.exe was not found at the configured path.", launcherPath);
        }
    }

    /// <summary>
    /// Starts XIVLauncher through the Windows shell.
    /// </summary>
    private static void LaunchAccount(string launcherPath, XivLaunchEntry entry) {
        string effectiveLauncherPath = !string.IsNullOrWhiteSpace(entry.XivLauncherPath) ? entry.XivLauncherPath : launcherPath;

        var workingDirectory = Path.GetDirectoryName(effectiveLauncherPath);

        var args = new System.Text.StringBuilder();
        var accountArgs = $"{entry.UserName}-{entry.UseOtp}-{entry.UseSteamServiceAccount}";

        args.Append($"--account={accountArgs}");

        if (!entry.AutoLogin)
            args.Append(" --noautologin");

        if (!string.IsNullOrWhiteSpace(entry.RoamingPath))
            args.Append($" --roamingPath=\"{entry.RoamingPath}\"");

        var startInfo = new ProcessStartInfo {
            FileName = effectiveLauncherPath,
            WorkingDirectory = string.IsNullOrWhiteSpace(workingDirectory)
                ? Environment.CurrentDirectory
                : workingDirectory,
            UseShellExecute = true,
            WindowStyle = ProcessWindowStyle.Normal,
            Arguments = args.ToString(),
        };

        var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Windows could not start XIVLauncher.");

        process.Dispose();
    }

    public static string GenerateLaunchCommand(string launcherPath, XivLaunchEntry entry) {
        string effectiveLauncherPath = !string.IsNullOrWhiteSpace(entry.XivLauncherPath) ? entry.XivLauncherPath : launcherPath;

        var args = new System.Text.StringBuilder();
        var accountArgs = $"{entry.UserName}-{entry.UseOtp}-{entry.UseSteamServiceAccount}";

        args.Append($"--account={accountArgs}");

        if (!entry.AutoLogin)
            args.Append(" --noautologin");

        if (!string.IsNullOrWhiteSpace(entry.RoamingPath))
            args.Append($" --roamingPath=\"{entry.RoamingPath}\"");

        return $"{effectiveLauncherPath} {args}";
    }

    private static void SetProgress(int currentIndex, int totalCount, string status) {
        lock (StateLock) {
            _currentIndex = currentIndex;
            _totalCount = totalCount;
            _status = status;
        }
    }

    private static void SetIdleStatus(string status) {
        lock (StateLock) {
            _status = status;
            _currentIndex = 0;
            _totalCount = 0;
            _isLaunching = false;
        }
    }
}
