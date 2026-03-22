namespace XivIpc.Internal;

internal static class TinyIpcEnvironment {
    internal const string BrokerDirectory = "TINYIPC_BROKER_DIR";
    internal const string BrokerIdleShutdownMs = "TINYIPC_BROKER_IDLE_SHUTDOWN_MS";
    internal const string BrokerSocketPath = "TINYIPC_BROKER_SOCKET_PATH";
    internal const string BusBackend = "TINYIPC_BUS_BACKEND";
    internal const string EnableLogging = "TINYIPC_ENABLE_LOGGING";
    internal const string FileNotifier = "TINYIPC_FILE_NOTIFIER";
    internal const string LaunchId = "TINYIPC_LAUNCH_ID";
    internal const string LogDirectory = "TINYIPC_LOG_DIR";
    internal const string LogFileCount = "TINYIPC_LOG_FILE_COUNT";
    internal const string LogLevel = "TINYIPC_LOG_LEVEL";
    internal const string LogMaxBytes = "TINYIPC_LOG_MAX_BYTES";
    internal const string LogPayload = "TINYIPC_LOG_PAYLOAD";
    internal const string MessageBusBackend = "TINYIPC_MESSAGE_BUS_BACKEND";
    internal const string MessageTtlMs = "TINYIPC_MESSAGE_TTL_MS";
    internal const string NativeHostPath = "TINYIPC_NATIVE_HOST_PATH";
    internal const string SharedDirectory = "TINYIPC_SHARED_DIR";
    internal const string SharedGroup = "TINYIPC_SHARED_GROUP";
    internal const string SharedPrefix = "TINYIPC_SHARED_PREFIX";
    internal const string SlotCount = "TINYIPC_SLOT_COUNT";
    internal const string UnixShell = "TINYIPC_UNIX_SHELL";

    private static readonly AsyncLocal<OverrideScope?> CurrentOverrides = new();

    internal static string? GetEnvironmentVariable(string variableName) {
        for (OverrideScope? scope = CurrentOverrides.Value; scope is not null; scope = scope.Parent) {
            if (scope.Values.TryGetValue(variableName, out string? value))
                return value;
        }

        return Environment.GetEnvironmentVariable(variableName);
    }

    internal static IDisposable Override(params (string Name, string? Value)[] values) {
        var overrides = new Dictionary<string, string?>(values.Length, StringComparer.Ordinal);
        foreach ((string name, string? value) in values)
            overrides[name] = value;

        return Override(overrides);
    }

    internal static IDisposable Override(IReadOnlyDictionary<string, string?> values) {
        OverrideScope? previous = CurrentOverrides.Value;
        var next = new OverrideScope(previous, values);
        CurrentOverrides.Value = next;
        return next;
    }

    private sealed class OverrideScope : IDisposable {
        private readonly OverrideScope? _previous;
        private bool _disposed;

        internal OverrideScope(OverrideScope? previous, IReadOnlyDictionary<string, string?> values) {
            _previous = previous;
            Values = values;
            Parent = previous;
        }

        internal OverrideScope? Parent { get; }

        internal IReadOnlyDictionary<string, string?> Values { get; }

        public void Dispose() {
            if (_disposed)
                return;

            CurrentOverrides.Value = _previous;
            _disposed = true;
        }
    }
}
