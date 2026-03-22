using System.Globalization;
using System.Text;

namespace XivIpc.Internal;

internal enum TinyIpcLogLevel {
    Error = 0,
    Warning = 1,
    Info = 2,
    Debug = 3,
    Trace = 4
}

internal interface ITinyIpcLogSink : IDisposable {
    void WriteLine(string line);
}

internal static class TinyIpcLogger {
    private static readonly object Sync = new();
    private static readonly ITinyIpcLogSink DisabledSink = new NullTinyIpcLogSink();
    private static readonly string EmergencyLogPath = Path.Combine(Path.GetTempPath(), "TinyIpc", "tinyipc-emergency.log");
    private static ITinyIpcLogSink _sink = DisabledSink;
    private static TinyIpcLogLevel _enabledThrough = TinyIpcLogLevel.Error;
    private static string _runtimeKind = "unknown";
    private static bool _initialized;
    private static bool _enabled;
    private static bool _failedClosed;
    private static bool _globalHandlersRegistered;

    public static void EnsureInitialized(RuntimeEnvironmentInfo runtime) {
        EnsureGlobalExceptionHandlersRegistered();

        if (_initialized)
            return;

        lock (Sync) {
            if (_initialized)
                return;

            _runtimeKind = runtime.Kind.ToString();
            _initialized = true;

            if (ReadOptionalBooleanEnvironment(TinyIpcEnvironment.EnableLogging) == false)
                return;

            try {
                _enabledThrough = ParseLogLevel(TinyIpcEnvironment.GetEnvironmentVariable(TinyIpcEnvironment.LogLevel)) ?? TinyIpcLogLevel.Info;
                _sink = CreateConfiguredSink();
                _enabled = true;
            } catch (Exception ex) {
                TryWriteEmergencyLine(
                    TinyIpcLogLevel.Error,
                    "Logging",
                    "InitializationFailed",
                    "TinyIpc logging initialization failed; attempting emergency fallback.",
                    ex);

                try {
                    _sink = CreateEmergencyFallbackSink();
                    _enabled = true;
                    _failedClosed = false;
                } catch (Exception fallbackEx) {
                    _sink = DisabledSink;
                    _enabled = false;
                    _failedClosed = true;
                    TryWriteEmergencyLine(
                        TinyIpcLogLevel.Error,
                        "Logging",
                        "FallbackInitializationFailed",
                        "TinyIpc emergency logging fallback also failed.",
                        fallbackEx);
                    return;
                }
            }
        }

        Info(
            "Logging",
            "Initialized",
            "TinyIpc logging enabled.",
            ("runtime", runtime.Kind),
            ("sharedDir", TinyIpcEnvironment.GetEnvironmentVariable(TinyIpcEnvironment.SharedDirectory)),
            ("backend", TinyIpcEnvironment.GetEnvironmentVariable(TinyIpcEnvironment.BusBackend)),
            ("notifier", TinyIpcEnvironment.GetEnvironmentVariable(TinyIpcEnvironment.FileNotifier)));
    }

    internal static void ResetForTests() {
        lock (Sync) {
            if (_globalHandlersRegistered) {
                AppDomain.CurrentDomain.UnhandledException -= OnUnhandledException;
                TaskScheduler.UnobservedTaskException -= OnUnobservedTaskException;
                _globalHandlersRegistered = false;
            }

            _sink.Dispose();
            _sink = DisabledSink;
            _enabledThrough = TinyIpcLogLevel.Error;
            _runtimeKind = "unknown";
            _initialized = false;
            _enabled = false;
            _failedClosed = false;
        }
    }

    public static bool IsEnabled(TinyIpcLogLevel level)
        => _enabled && !_failedClosed && level <= _enabledThrough;

    public static void EnsureGlobalExceptionHandlersRegistered() {
        if (_globalHandlersRegistered)
            return;

        lock (Sync) {
            if (_globalHandlersRegistered)
                return;

            AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
            TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
            _globalHandlersRegistered = true;
        }
    }

    public static void Error(string component, string eventName, string message, Exception? exception = null, params (string Key, object? Value)[] fields)
        => Log(TinyIpcLogLevel.Error, component, eventName, message, exception, fields);

    public static void Warning(string component, string eventName, string message, Exception? exception = null, params (string Key, object? Value)[] fields)
        => Log(TinyIpcLogLevel.Warning, component, eventName, message, exception, fields);

    public static void Info(string component, string eventName, string message, params (string Key, object? Value)[] fields)
        => Log(TinyIpcLogLevel.Info, component, eventName, message, null, fields);

    public static void Debug(string component, string eventName, string message, params (string Key, object? Value)[] fields)
        => Log(TinyIpcLogLevel.Debug, component, eventName, message, null, fields);

    public static void Trace(string component, string eventName, string message, params (string Key, object? Value)[] fields)
        => Log(TinyIpcLogLevel.Trace, component, eventName, message, null, fields);

    public static string? CreatePayloadPreview(byte[] payload) {
        if (!ReadOptionalBooleanEnvironment(TinyIpcEnvironment.LogPayload).GetValueOrDefault())
            return null;

        const int previewBytes = 32;
        int count = Math.Min(payload.Length, previewBytes);
        return Convert.ToHexString(payload, 0, count);
    }

    private static void Log(TinyIpcLogLevel level, string component, string eventName, string message, Exception? exception, params (string Key, object? Value)[] fields) {
        if (!IsEnabled(level))
            return;

        try {
            var sb = new StringBuilder(256);
            sb.Append(DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture));
            sb.Append(" level=").Append(level.ToString().ToUpperInvariant());
            sb.Append(" pid=").Append(Environment.ProcessId);
            sb.Append(" tid=").Append(Environment.CurrentManagedThreadId);
            sb.Append(" runtime=").Append(_runtimeKind);
            sb.Append(" component=").Append(Sanitize(component));
            sb.Append(" event=").Append(Sanitize(eventName));
            sb.Append(" message=\"").Append(Sanitize(message)).Append('"');

            for (int i = 0; i < fields.Length; i++) {
                sb.Append(' ');
                sb.Append(Sanitize(fields[i].Key));
                sb.Append('=');
                sb.Append('"');
                sb.Append(Sanitize(FormatValue(fields[i].Value)));
                sb.Append('"');
            }

            if (exception != null) {
                sb.Append(" exceptionType=\"").Append(Sanitize(exception.GetType().FullName ?? exception.GetType().Name)).Append('"');
                sb.Append(" exception=\"").Append(Sanitize(exception.ToString())).Append('"');
            }

            _sink.WriteLine(sb.ToString());
        } catch (Exception ex) {
            TryWriteEmergencyLine(level, component, eventName, message, ex, fields);
            lock (Sync) {
                _sink.Dispose();
                _sink = DisabledSink;
                _enabled = false;
                _failedClosed = true;
            }
        }
    }

    private static void OnUnhandledException(object sender, UnhandledExceptionEventArgs e) {
        Exception? exception = e.ExceptionObject as Exception;
        TryWriteEmergencyLine(
            TinyIpcLogLevel.Error,
            "GlobalException",
            "UnhandledException",
            "AppDomain.CurrentDomain.UnhandledException fired.",
            exception,
            ("isTerminating", e.IsTerminating),
            ("exceptionObjectType", e.ExceptionObject?.GetType().FullName));
        Error(
            "GlobalException",
            "UnhandledException",
            "AppDomain.CurrentDomain.UnhandledException fired.",
            exception,
            ("isTerminating", e.IsTerminating),
            ("exceptionObjectType", e.ExceptionObject?.GetType().FullName));
    }

    private static void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e) {
        TryWriteEmergencyLine(
            TinyIpcLogLevel.Error,
            "GlobalException",
            "UnobservedTaskException",
            "TaskScheduler.UnobservedTaskException fired.",
            e.Exception,
            ("observed", e.Observed));
        Error(
            "GlobalException",
            "UnobservedTaskException",
            "TaskScheduler.UnobservedTaskException fired.",
            e.Exception,
            ("observed", e.Observed));
    }

    private static string ResolveLogDirectory() {
        string? configured = TinyIpcEnvironment.GetEnvironmentVariable(TinyIpcEnvironment.LogDirectory);
        if (!string.IsNullOrWhiteSpace(configured))
            return UnixSharedStorageHelpers.ConvertPathForCurrentRuntime(configured);

        return Path.Combine(Path.GetTempPath(), "TinyIpc");
    }

    private static ITinyIpcLogSink CreateConfiguredSink() {
        string directory = ResolveLogDirectory();
        return CreateFileSink(directory);
    }

    private static ITinyIpcLogSink CreateEmergencyFallbackSink() {
        string directory = Path.Combine(Path.GetTempPath(), "TinyIpc");
        return CreateFileSink(directory);
    }

    private static ITinyIpcLogSink CreateFileSink(string directory) {
        Directory.CreateDirectory(directory);
        try {
            UnixSharedStorageHelpers.ApplyPermissions(directory, isDirectory: true);
        } catch {
        }

        return new FileTinyIpcLogSink(
            directory,
            ParsePositiveInt64(TinyIpcEnvironment.GetEnvironmentVariable(TinyIpcEnvironment.LogMaxBytes)) ?? 5 * 1024 * 1024,
            ParsePositiveInt32(TinyIpcEnvironment.GetEnvironmentVariable(TinyIpcEnvironment.LogFileCount)) ?? 3);
    }

    private static void TryWriteEmergencyLine(
        TinyIpcLogLevel level,
        string component,
        string eventName,
        string message,
        Exception? exception = null,
        params (string Key, object? Value)[] fields) {
        try {
            string? directory = Path.GetDirectoryName(EmergencyLogPath);
            if (!string.IsNullOrWhiteSpace(directory))
                Directory.CreateDirectory(directory);

            var sb = new StringBuilder(256);
            sb.Append(DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture));
            sb.Append(" level=").Append(level.ToString().ToUpperInvariant());
            sb.Append(" pid=").Append(Environment.ProcessId);
            sb.Append(" tid=").Append(Environment.CurrentManagedThreadId);
            sb.Append(" runtime=").Append(_runtimeKind);
            sb.Append(" component=").Append(Sanitize(component));
            sb.Append(" event=").Append(Sanitize(eventName));
            sb.Append(" message=\"").Append(Sanitize(message)).Append('"');

            for (int i = 0; i < fields.Length; i++) {
                sb.Append(' ');
                sb.Append(Sanitize(fields[i].Key));
                sb.Append('=');
                sb.Append('"');
                sb.Append(Sanitize(FormatValue(fields[i].Value)));
                sb.Append('"');
            }

            if (exception != null) {
                sb.Append(" exceptionType=\"").Append(Sanitize(exception.GetType().FullName ?? exception.GetType().Name)).Append('"');
                sb.Append(" exception=\"").Append(Sanitize(exception.ToString())).Append('"');
            }

            File.AppendAllText(EmergencyLogPath, sb.AppendLine().ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        } catch {
        }
    }

    private static TinyIpcLogLevel? ParseLogLevel(string? value) {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        return value.Trim().ToLowerInvariant() switch {
            "error" => TinyIpcLogLevel.Error,
            "warn" => TinyIpcLogLevel.Warning,
            "warning" => TinyIpcLogLevel.Warning,
            "info" => TinyIpcLogLevel.Info,
            "debug" => TinyIpcLogLevel.Debug,
            "trace" => TinyIpcLogLevel.Trace,
            _ => null
        };
    }

    private static bool? ReadOptionalBooleanEnvironment(string name) {
        string? value = TinyIpcEnvironment.GetEnvironmentVariable(name);
        if (string.IsNullOrWhiteSpace(value))
            return null;

        return value.Trim() switch {
            "1" => true,
            "true" => true,
            "TRUE" => true,
            "0" => false,
            "false" => false,
            "FALSE" => false,
            _ => null
        };
    }

    private static int? ParsePositiveInt32(string? value) {
        if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed) || parsed <= 0)
            return null;

        return parsed;
    }

    private static long? ParsePositiveInt64(string? value) {
        if (!long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out long parsed) || parsed <= 0)
            return null;

        return parsed;
    }

    private static string FormatValue(object? value) {
        return value switch {
            null => string.Empty,
            bool b => b ? "true" : "false",
            byte[] bytes => Convert.ToHexString(bytes),
            _ => Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty
        };
    }

    private static string Sanitize(string value)
        => value.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\r", "\\r", StringComparison.Ordinal)
            .Replace("\n", "\\n", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal);

    private sealed class NullTinyIpcLogSink : ITinyIpcLogSink {
        public void WriteLine(string line) {
        }

        public void Dispose() {
        }
    }

    private sealed class FileTinyIpcLogSink : ITinyIpcLogSink {
        private readonly object _gate = new();
        private readonly string _directory;
        private readonly string _baseName;
        private readonly long _maxBytes;
        private readonly int _fileCount;
        private StreamWriter _writer;
        private bool _disposed;
        private Guid _instanceId = Guid.NewGuid();

        public FileTinyIpcLogSink(string directory, long maxBytes, int fileCount) {
            _directory = directory;
            _maxBytes = Math.Max(1_024, maxBytes);
            _fileCount = Math.Max(1, fileCount);
            _baseName = $"tinyipc-{_instanceId}.log";
            _writer = OpenWriter(CurrentPath);
            CleanupRetainedFiles();
        }

        public void WriteLine(string line) {
            lock (_gate) {
                ThrowIfDisposed();

                if (NeedsRotation(line.Length))
                    Rotate();

                _writer.WriteLine(line);
                _writer.Flush();
            }
        }

        public void Dispose() {
            lock (_gate) {
                if (_disposed)
                    return;

                _disposed = true;
                _writer.Dispose();
            }
        }

        private string CurrentPath => Path.Combine(_directory, _baseName);

        private bool NeedsRotation(int additionalCharacters) {
            try {
                FileInfo info = new(CurrentPath);
                return info.Exists && info.Length + additionalCharacters + Environment.NewLine.Length > _maxBytes;
            } catch {
                return false;
            }
        }

        private void Rotate() {
            _writer.Dispose();

            if (_fileCount == 1) {
                if (File.Exists(CurrentPath))
                    File.Delete(CurrentPath);

                _writer = OpenWriter(CurrentPath);
                return;
            }

            for (int index = _fileCount - 1; index >= 1; index--) {
                string source = index == 1 ? CurrentPath : GetArchivePath(index - 1);
                string destination = GetArchivePath(index);

                if (File.Exists(destination))
                    File.Delete(destination);

                if (File.Exists(source))
                    File.Move(source, destination);
            }

            _writer = OpenWriter(CurrentPath);
            CleanupRetainedFiles();
        }

        private string GetArchivePath(int index) => Path.Combine(_directory, $"{_baseName}.{index}");

        private void CleanupRetainedFiles() {
            string[] files = Directory.GetFiles(_directory, $"{_baseName}*");
            if (files.Length <= _fileCount)
                return;

            Array.Sort(files, StringComparer.Ordinal);
            for (int i = 0; i < files.Length - _fileCount; i++) {
                if (!string.Equals(files[i], CurrentPath, StringComparison.Ordinal))
                    File.Delete(files[i]);
            }
        }

        private static StreamWriter OpenWriter(string path) {
            var stream = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);

            try {
                UnixSharedStorageHelpers.ApplyPermissions(path, isDirectory: false);
            } catch {
            }

            return new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)) {
                AutoFlush = true
            };
        }

        private void ThrowIfDisposed() {
            if (_disposed)
                throw new ObjectDisposedException(nameof(FileTinyIpcLogSink));
        }
    }
}

