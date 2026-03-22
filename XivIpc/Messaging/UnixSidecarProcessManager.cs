using System.Diagnostics;
using System.Net.Sockets;

using XivIpc.Internal;

namespace XivIpc.Messaging;

internal static class UnixSidecarProcessManager {
    private const string BrokerDirectoryName = "xivipc";
    private const string BrokerSocketFileName = "tinyipc-sidecar.sock";
    private const int StartupTimeoutMs = 10_000;
    private static readonly TimeSpan BrokerTerminationTimeout = TimeSpan.FromSeconds(5);

    private static readonly object Sync = new();

    private static Process? _process;
    private static int _refCount;

    internal static Lease Acquire()
        => Acquire(CaptureSettings());

    internal static Lease Acquire(RuntimeSettings settings) {
        lock (Sync) {
            string socketPath = settings.SocketPath;
            EnsureBrokerStarted(settings, socketPath);
            _refCount++;
            return new Lease(socketPath);
        }
    }

    internal static RuntimeSettings CaptureSettings() {
        string? brokerDirectory = TinyIpcEnvironment.GetEnvironmentVariable(TinyIpcEnvironment.BrokerDirectory);
        string? sharedDirectory = TinyIpcEnvironment.GetEnvironmentVariable(TinyIpcEnvironment.SharedDirectory);
        string? logDirectory = TinyIpcEnvironment.GetEnvironmentVariable(TinyIpcEnvironment.LogDirectory);
        string? sharedGroup = TinyIpcEnvironment.GetEnvironmentVariable(TinyIpcEnvironment.SharedGroup);
        string? enableLogging = TinyIpcEnvironment.GetEnvironmentVariable(TinyIpcEnvironment.EnableLogging);
        string? logLevel = TinyIpcEnvironment.GetEnvironmentVariable(TinyIpcEnvironment.LogLevel);
        string? logPayload = TinyIpcEnvironment.GetEnvironmentVariable(TinyIpcEnvironment.LogPayload);
        string? logMaxBytes = TinyIpcEnvironment.GetEnvironmentVariable(TinyIpcEnvironment.LogMaxBytes);
        string? logFileCount = TinyIpcEnvironment.GetEnvironmentVariable(TinyIpcEnvironment.LogFileCount);
        string? fileNotifier = TinyIpcEnvironment.GetEnvironmentVariable(TinyIpcEnvironment.FileNotifier);
        string? explicitHostPath = TinyIpcEnvironment.GetEnvironmentVariable(TinyIpcEnvironment.NativeHostPath);
        string? explicitSocketPath = TinyIpcEnvironment.GetEnvironmentVariable(TinyIpcEnvironment.BrokerSocketPath);

        string resolvedBrokerDirectory = ResolveBrokerDirectory(sharedDirectory, brokerDirectory);
        string socketPath = string.IsNullOrWhiteSpace(explicitSocketPath)
            ? CombineUnixPath(resolvedBrokerDirectory, BrokerSocketFileName)
            : ResolveUnixPath(explicitSocketPath);
        string diagnosticsDirectory = ResolveDiagnosticsLogDirectory(socketPath, logDirectory);

        return new RuntimeSettings(
            socketPath,
            resolvedBrokerDirectory,
            string.IsNullOrWhiteSpace(sharedDirectory) ? string.Empty : ResolveUnixPath(sharedDirectory),
            diagnosticsDirectory,
            sharedGroup,
            enableLogging,
            logLevel,
            logPayload,
            logMaxBytes,
            logFileCount,
            fileNotifier,
            explicitHostPath);
    }

    private static void EnsureBrokerStarted(RuntimeSettings settings, string socketPath) {
        string statePath = BrokerStateSnapshot.ResolvePath(socketPath);
        if (CanConnect(socketPath))
            return;

        string brokerDirectory = Path.GetDirectoryName(socketPath) ?? throw new InvalidOperationException("Broker socket path must have a parent directory.");
        Directory.CreateDirectory(UnixSharedStorageHelpers.ConvertPathForCurrentRuntime(brokerDirectory));
        UnixSharedStorageHelpers.ApplyBrokerPermissions(brokerDirectory, isDirectory: true);

        BrokerStateSnapshot? state = BrokerStateSnapshot.TryRead(statePath);
        if (state is not null && BrokerStateSnapshot.IsLive(state)) {
            if (CanConnect(socketPath))
                return;

            if (!BrokerStateSnapshot.TryTerminate(state, BrokerTerminationTimeout))
                throw new SidecarStartupException($"Failed to terminate unreachable TinyIpc broker pid={state.Pid} instance={state.InstanceId}.");

            CleanupOwnedProcess(state.Pid);
        }

        if (!TryStartHelper(settings, socketPath, out string failureDetails))
            throw new SidecarStartupException($"Failed to start native TinyIpc broker for socket '{socketPath}'.{Environment.NewLine}{failureDetails}");

        WaitForBroker(settings, socketPath, statePath, TimeSpan.FromMilliseconds(StartupTimeoutMs));
    }

    private static void WaitForBroker(RuntimeSettings settings, string socketPath, string statePath, TimeSpan timeout) {
        Stopwatch sw = Stopwatch.StartNew();
        Exception? last = null;

        while (sw.Elapsed < timeout) {
            try {
                if (CanConnect(socketPath) && BrokerStateSnapshot.TryRead(statePath) is not null)
                    return;

            } catch (Exception ex) {
                if (ex is SidecarStartupException)
                    throw;

                last = ex;
            }

            Thread.Sleep(50);
        }

        string diagnostics = BuildBrokerStartupDiagnostics(settings, socketPath, statePath);
        throw new SidecarStartupException(
            $"Timed out waiting for broker socket '{socketPath}' to become reachable.{Environment.NewLine}{diagnostics}",
            last ?? new TimeoutException());
    }

    private static bool TryStartHelper(RuntimeSettings settings, string socketPath, out string failureDetails) {
        ProcessStartInfo psi = BuildStartInfo(
            settings,
            socketPath,
            out string launchMode,
            out string launchCommand,
            out string hostPathUnix,
            out string hostPathWindows,
            out string launchId,
            out string stdoutLogPath,
            out string stderrLogPath);

        try {
            _process = Process.Start(psi) ?? throw new InvalidOperationException("Process.Start returned null.");
            failureDetails = string.Empty;
            return true;
        } catch (Exception ex) {
            failureDetails =
                $"mode={launchMode}"
                + $" launchId={launchId}"
                + $" launchCommand={launchCommand}"
                + $" hostPathWindows={hostPathWindows}"
                + $" hostPathUnix={hostPathUnix}"
                + $" stdoutLogPath={stdoutLogPath}"
                + $" stderrLogPath={stderrLogPath}"
                + Environment.NewLine
                + ex;
            return false;
        }
    }

    private static ProcessStartInfo BuildStartInfo(
        RuntimeSettings settings,
        string socketPath,
        out string launchMode,
        out string launchCommand,
        out string hostPathUnix,
        out string hostPathWindows,
        out string launchId,
        out string stdoutLogPath,
        out string stderrLogPath) {
        hostPathWindows = ResolveNativeHostPath(settings);
        hostPathUnix = ConvertWindowsPathToUnix(hostPathWindows);
        launchId = Guid.NewGuid().ToString("N");

        string diagnosticsDirectory = settings.DiagnosticsDirectory;
        Directory.CreateDirectory(diagnosticsDirectory);
        stdoutLogPath = Path.Combine(diagnosticsDirectory, $"tinyipc-sidecar-{launchId}.stdout.log");
        stderrLogPath = Path.Combine(diagnosticsDirectory, $"tinyipc-sidecar-{launchId}.stderr.log");

        bool isDll = string.Equals(Path.GetExtension(hostPathUnix), ".dll", StringComparison.OrdinalIgnoreCase);
        ProcessStartInfo psi;
        if (isDll) {
            launchMode = "dotnet-dll";
            launchCommand = $"/usr/bin/env dotnet {QuoteForShell(hostPathUnix)}";
            psi = new ProcessStartInfo("/usr/bin/env") {
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = AppContext.BaseDirectory
            };
            psi.ArgumentList.Add("dotnet");
            psi.ArgumentList.Add(hostPathUnix);
        } else {
            launchMode = "direct-exec";
            launchCommand = hostPathUnix;
            psi = new ProcessStartInfo(hostPathUnix) {
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = AppContext.BaseDirectory
            };
        }

        CopyFixedPathEnvironment(psi, "TINYIPC_SHARED_DIR", settings.SharedDirectory);
        CopyFixedPathEnvironment(psi, "TINYIPC_BROKER_DIR", settings.BrokerDirectory);
        CopyFixedVerbatimEnvironment(psi, "TINYIPC_ENABLE_LOGGING", settings.EnableLogging);
        CopyFixedPathEnvironment(psi, "TINYIPC_LOG_DIR", settings.DiagnosticsDirectory);
        CopyFixedVerbatimEnvironment(psi, "TINYIPC_LOG_LEVEL", settings.LogLevel);
        CopyFixedVerbatimEnvironment(psi, "TINYIPC_LOG_PAYLOAD", settings.LogPayload);
        CopyFixedVerbatimEnvironment(psi, "TINYIPC_LOG_MAX_BYTES", settings.LogMaxBytes);
        CopyFixedVerbatimEnvironment(psi, "TINYIPC_LOG_FILE_COUNT", settings.LogFileCount);
        CopyFixedVerbatimEnvironment(psi, "TINYIPC_SHARED_GROUP", settings.SharedGroup);
        CopyFixedVerbatimEnvironment(psi, "TINYIPC_FILE_NOTIFIER", settings.FileNotifier);
        CopyVerbatimEnvironment(psi, TinyIpcEnvironment.MessageTtlMs);
        CopyVerbatimEnvironment(psi, TinyIpcEnvironment.BrokerIdleShutdownMs);

        psi.Environment["TINYIPC_BROKER_SOCKET_PATH"] = ConvertUnixPathForChild(socketPath);
        psi.Environment["TINYIPC_BUS_BACKEND"] = "sidecar-brokered";
        psi.Environment["TINYIPC_LAUNCH_ID"] = launchId;

        TinyIpcProcessStamp stamp = TinyIpcProcessStamp.Create(typeof(UnixSidecarProcessManager));
        string hostSha256 = TinyIpcProcessStamp.ComputeSha256(ResolveNativePath(hostPathWindows));
        TinyIpcLogger.Info(
            nameof(UnixSidecarProcessManager),
            "BrokerLaunchPrepared",
            "Prepared native TinyIpc broker start info.",
            ("mode", launchMode),
            ("launchId", launchId),
            ("hostPathWindows", hostPathWindows),
            ("hostPathUnix", hostPathUnix),
            ("hostSha256", hostSha256),
            ("launchCommand", launchCommand),
            ("stdoutLogPath", stdoutLogPath),
            ("stderrLogPath", stderrLogPath),
            ("childSharedDir", psi.Environment.TryGetValue("TINYIPC_SHARED_DIR", out string? childSharedDir) ? childSharedDir : string.Empty),
            ("childBrokerDir", psi.Environment.TryGetValue("TINYIPC_BROKER_DIR", out string? childBrokerDir) ? childBrokerDir : string.Empty),
            ("childLogDir", psi.Environment.TryGetValue("TINYIPC_LOG_DIR", out string? childLogDir) ? childLogDir : string.Empty),
            ("childSharedGroup", psi.Environment.TryGetValue("TINYIPC_SHARED_GROUP", out string? childSharedGroup) ? childSharedGroup : string.Empty),
            ("childBrokerSocketPath", psi.Environment["TINYIPC_BROKER_SOCKET_PATH"]),
            ("childBackend", psi.Environment["TINYIPC_BUS_BACKEND"]),
            ("childLaunchId", psi.Environment["TINYIPC_LAUNCH_ID"]),
            ("assemblyPath", stamp.AssemblyPath),
            ("sha256", stamp.Sha256),
            ("processPath", stamp.ProcessPath),
            ("processSha256", stamp.ProcessSha256));

        return psi;
    }

    private static bool CanConnect(string socketPath) {
        if (string.IsNullOrWhiteSpace(socketPath))
            return false;

        try {
            using var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
            socket.ReceiveTimeout = 250;
            socket.SendTimeout = 250;
            socket.Connect(new UnixDomainSocketEndPoint(socketPath));
            return true;
        } catch {
            return false;
        }
    }

    private static string ResolveBrokerSocketPath() {
        return CaptureSettings().SocketPath;
    }

    private static string ResolveBrokerDirectory() {
        return ResolveBrokerDirectory(
            TinyIpcEnvironment.GetEnvironmentVariable(TinyIpcEnvironment.SharedDirectory),
            TinyIpcEnvironment.GetEnvironmentVariable(TinyIpcEnvironment.BrokerDirectory));
    }

    private static string ResolveBrokerDirectory(string? sharedDirectory, string? explicitDirectory) {
        if (!string.IsNullOrWhiteSpace(explicitDirectory))
            return ResolveUnixPath(explicitDirectory);

        if (!string.IsNullOrWhiteSpace(sharedDirectory))
            return ResolveUnixPath(sharedDirectory);

        return Path.Combine("/run", BrokerDirectoryName);
    }

    private static string BuildBrokerStartupDiagnostics(RuntimeSettings settings, string socketPath, string statePath) {
        string logDirectory = settings.DiagnosticsDirectory;
        string latestNativeLog = FindLatestNativeBrokerLog(logDirectory);
        string nativeLogTail = string.IsNullOrWhiteSpace(latestNativeLog)
            ? "<none>"
            : ReadLogTail(latestNativeLog, 20);

        string stateSummary;
        try {
            BrokerStateSnapshot? state = BrokerStateSnapshot.TryRead(statePath);
            stateSummary = state is null
                ? "<none>"
                : $"pid={state.Pid} instanceId={state.InstanceId} sessions={state.SessionCount} channels={state.ChannelCount}";
        } catch (Exception ex) {
            stateSummary = $"<error: {ex.GetType().Name}: {ex.Message}>";
        }

        return string.Join(
            Environment.NewLine,
            "Broker startup diagnostics:",
            $"socketExists={File.Exists(socketPath)}",
            $"stateExists={File.Exists(statePath)}",
            $"state={stateSummary}",
            $"logDirectory={logDirectory}",
            $"latestNativeLog={latestNativeLog}",
            "latestNativeLogTail:",
            nativeLogTail);
    }

    internal static string ResolveNativeHostPath()
        => ResolveNativeHostPath(CaptureSettings());

    internal static string ResolveNativeHostPath(RuntimeSettings settings) {
        string? explicitPath = settings.ExplicitNativeHostPath;
        if (!string.IsNullOrWhiteSpace(explicitPath)) {
            string resolved = ResolveNativePath(explicitPath);
            if (File.Exists(resolved))
                return resolved;
        }

        string[] candidates = GetNativeHostCandidatePathsForDiagnostics(settings);

        string? existing = candidates.FirstOrDefault(File.Exists);
        return existing ?? throw new SidecarStartupException("XivIpc.NativeHost executable or dll was not found.");
    }

    internal static bool TryResolveNativeHostPath(out string path) {
        try {
            path = ResolveNativeHostPath();
            return true;
        } catch {
            path = string.Empty;
            return false;
        }
    }

    internal static string[] GetNativeHostCandidatePathsForDiagnostics()
        => GetNativeHostCandidatePathsForDiagnostics(CaptureSettings());

    private static string[] GetNativeHostCandidatePathsForDiagnostics(RuntimeSettings settings) {
        var candidates = new List<string>();

        void AddCandidates(string baseDirectory) {
            if (string.IsNullOrWhiteSpace(baseDirectory))
                return;

            string resolvedBase = ResolveNativePath(baseDirectory);
            candidates.Add(Path.Combine(resolvedBase, "XivIpc.NativeHost"));
            candidates.Add(Path.Combine(resolvedBase, "XivIpc.NativeHost.dll"));
            candidates.Add(Path.Combine(resolvedBase, "XivIpc.NativeHost.exe"));
        }

        string? sharedDirectory = settings.SharedDirectory;
        if (!string.IsNullOrWhiteSpace(sharedDirectory))
            AddCandidates(Path.Combine(sharedDirectory, "tinyipc-native-host"));

        string? brokerDirectory = settings.BrokerDirectory;
        if (!string.IsNullOrWhiteSpace(brokerDirectory))
            AddCandidates(Path.Combine(brokerDirectory, "tinyipc-native-host"));

        string baseDir = AppContext.BaseDirectory;
        candidates.Add(Path.Combine(baseDir, "XivIpc.NativeHost"));
        candidates.Add(Path.Combine(baseDir, "XivIpc.NativeHost.dll"));
        candidates.Add(Path.Combine(baseDir, "XivIpc.NativeHost.exe"));

        candidates.Add(Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "..", "XivIpc.NativeHost", "bin", "Debug", "net10.0", "XivIpc.NativeHost")));
        candidates.Add(Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "..", "XivIpc.NativeHost", "bin", "Debug", "net10.0", "XivIpc.NativeHost.dll")));
        candidates.Add(Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "..", "XivIpc.NativeHost", "bin", "Debug", "net9.0", "XivIpc.NativeHost")));
        candidates.Add(Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "..", "XivIpc.NativeHost", "bin", "Debug", "net9.0", "XivIpc.NativeHost.dll")));
        candidates.Add(Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "..", "XivIpc.NativeHost", "bin", "Release", "net10.0", "XivIpc.NativeHost")));
        candidates.Add(Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "..", "XivIpc.NativeHost", "bin", "Release", "net10.0", "XivIpc.NativeHost.dll")));
        candidates.Add(Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "..", "XivIpc.NativeHost", "bin", "Release", "net9.0", "XivIpc.NativeHost")));
        candidates.Add(Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "..", "XivIpc.NativeHost", "bin", "Release", "net9.0", "XivIpc.NativeHost.dll")));

        return candidates.Distinct(StringComparer.Ordinal).ToArray();
    }

    internal static int GetOwnedProcessIdForDiagnostics() {
        lock (Sync) {
            try {
                return _process is { HasExited: false } ? _process.Id : 0;
            } catch {
                return 0;
            }
        }
    }

    internal static bool WaitForOwnedProcessExitForDiagnostics(TimeSpan timeout) {
        Process? process;

        lock (Sync)
            process = _process;

        if (process is null)
            return true;

        try {
            bool exited = process.WaitForExit((int)timeout.TotalMilliseconds);
            if (exited) {
                lock (Sync) {
                    if (ReferenceEquals(_process, process)) {
                        try { _process.Dispose(); } catch { }
                        _process = null;
                    }
                }
            }

            return exited;
        } catch {
            return false;
        }
    }

    private static void Release() {
        lock (Sync) {
            if (_refCount > 0)
                _refCount--;

            if (_process is null)
                return;

            bool hasExited;
            try {
                hasExited = _process.HasExited;
            } catch {
                hasExited = true;
            }

            if (!hasExited)
                return;

            try {
                _process.Dispose();
            } catch {
            }

            _process = null;
        }
    }

    internal static string ResolveBrokerStatePathForDiagnostics()
        => BrokerStateSnapshot.ResolvePath(ResolveBrokerSocketPath());

    internal static int GetBrokerProcessIdForDiagnostics()
        => BrokerStateSnapshot.TryRead(ResolveBrokerStatePathForDiagnostics())?.Pid ?? 0;

    private static void CleanupOwnedProcess(int pid) {
        lock (Sync) {
            if (_process is null)
                return;

            try {
                if (!_process.HasExited && _process.Id != pid)
                    return;
            } catch {
            }

            try { _process.Dispose(); } catch { }
            _process = null;
        }
    }

    private static void CopyVerbatimEnvironment(ProcessStartInfo psi, string variableName) {
        string? value = TinyIpcEnvironment.GetEnvironmentVariable(variableName);
        if (!string.IsNullOrWhiteSpace(value))
            psi.Environment[variableName] = value;
    }

    private static void CopyFixedVerbatimEnvironment(ProcessStartInfo psi, string variableName, string? value) {
        if (!string.IsNullOrWhiteSpace(value))
            psi.Environment[variableName] = value;
    }

    private static void CopyPathEnvironment(ProcessStartInfo psi, string variableName) {
        string? value = TinyIpcEnvironment.GetEnvironmentVariable(variableName);
        if (!string.IsNullOrWhiteSpace(value))
            psi.Environment[variableName] = ResolveUnixPath(value);
    }

    private static void CopyFixedPathEnvironment(ProcessStartInfo psi, string variableName, string? value) {
        if (!string.IsNullOrWhiteSpace(value))
            psi.Environment[variableName] = ResolveUnixPath(value);
    }

    private static string ResolveNativePath(string rawPath) {
        RuntimeEnvironmentInfo runtime = RuntimeEnvironmentDetector.Detect();
        if (runtime.IsWindowsProcess) {
            string windowsPath = ConvertUnixPathToWindows(rawPath);
            return Path.IsPathRooted(windowsPath) ? windowsPath : Path.GetFullPath(windowsPath);
        }

        string unixPath = ConvertWindowsPathToUnix(rawPath);
        return Path.IsPathRooted(unixPath) ? unixPath : Path.GetFullPath(unixPath);
    }

    private static string ResolveUnixPath(string rawPath) {
        string unixPath = ConvertWindowsPathToUnix(rawPath);
        return Path.IsPathRooted(unixPath) ? unixPath : Path.GetFullPath(unixPath);
    }

    private static string ResolveDiagnosticsLogDirectory(string socketPath)
        => ResolveDiagnosticsLogDirectory(socketPath, TinyIpcEnvironment.GetEnvironmentVariable(TinyIpcEnvironment.LogDirectory));

    private static string ResolveDiagnosticsLogDirectory(string socketPath, string? configured) {
        if (!string.IsNullOrWhiteSpace(configured))
            return ResolveUnixPath(configured);

        return Path.GetDirectoryName(socketPath) ?? "/tmp";
    }

    private static string FindLatestNativeBrokerLog(string logDirectory) {
        if (!Directory.Exists(logDirectory))
            return string.Empty;

        foreach (string path in Directory.EnumerateFiles(logDirectory, "tinyipc-*.log", SearchOption.TopDirectoryOnly)
            .OrderByDescending(File.GetLastWriteTimeUtc)) {
            try {
                string contents = File.ReadAllText(path);
                if (contents.Contains("runtime=UnixProcess", StringComparison.Ordinal))
                    return path;
            } catch {
            }
        }

        return string.Empty;
    }

    private static string ReadLogTail(string path, int lineCount) {
        try {
            string[] lines = File.ReadAllLines(path);
            return string.Join(Environment.NewLine, lines.Skip(Math.Max(0, lines.Length - lineCount)));
        } catch (Exception ex) {
            return $"<error reading log tail: {ex.GetType().Name}: {ex.Message}>";
        }
    }

    private static string ConvertUnixPathForChild(string path) {
        return ResolveUnixPath(path);
    }

    private static string ConvertWindowsPathToUnix(string path) {
        if (string.IsNullOrWhiteSpace(path))
            return path;

        string normalized = path.Replace('\\', '/');
        if (normalized.Length >= 3 &&
            char.IsLetter(normalized[0]) &&
            normalized[1] == ':' &&
            normalized[2] == '/') {
            char drive = char.ToUpperInvariant(normalized[0]);
            string remainder = normalized[3..];
            return drive == 'Z' ? "/" + remainder : $"/mnt/{char.ToLowerInvariant(drive)}/{remainder}";
        }

        return normalized;
    }

    private static string ConvertUnixPathToWindows(string path) {
        if (string.IsNullOrWhiteSpace(path))
            return path;

        if (IsWindowsStylePath(path))
            return path.Replace('/', '\\');

        string normalized = path.Replace('\\', '/');
        if (normalized[0] == '/')
            return "Z:" + normalized.Replace('/', '\\');

        return path.Replace('/', '\\');
    }

    private static string ConvertUnixPathToWine(string path)
        => "Z:" + path.Replace('/', '\\');

    private static string CombineUnixPath(string directory, string fileName) {
        string normalizedDirectory = ResolveUnixPath(directory).TrimEnd('/');
        return normalizedDirectory.Length == 0 ? "/" + fileName : normalizedDirectory + "/" + fileName;
    }

    private static bool IsWindowsStylePath(string path)
        => !string.IsNullOrWhiteSpace(path)
           && path.Length >= 3
           && char.IsLetter(path[0])
           && path[1] == ':'
           && (path[2] == '\\' || path[2] == '/');

    private static string QuoteForShell(string value)
        => "'" + value.Replace("'", "'\"'\"'") + "'";

    internal readonly struct Lease : IDisposable {
        internal Lease(string socketPath) => SocketPath = socketPath;
        internal string SocketPath { get; }
        public void Dispose() => Release();
    }

    internal readonly record struct RuntimeSettings(
        string SocketPath,
        string BrokerDirectory,
        string SharedDirectory,
        string DiagnosticsDirectory,
        string? SharedGroup,
        string? EnableLogging,
        string? LogLevel,
        string? LogPayload,
        string? LogMaxBytes,
        string? LogFileCount,
        string? FileNotifier,
        string? ExplicitNativeHostPath);
}
