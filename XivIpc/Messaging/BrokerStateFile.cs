using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;

using XivIpc.Internal;

namespace XivIpc.Messaging;

internal sealed record BrokerStateSnapshot(
    string InstanceId,
    int Pid,
    long ProcessStartTicks,
    string SocketPath,
    string SharedDirectory,
    long CreatedUtcTicks,
    int SessionCount,
    int ChannelCount,
    long LastUpdatedUtcTicks) {
    private static readonly JsonSerializerOptions JsonOptions = new() {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    internal static string ResolvePath(string socketPath) {
        string? directory = Path.GetDirectoryName(socketPath);
        if (string.IsNullOrWhiteSpace(directory))
            throw new InvalidOperationException("Broker socket path must have a parent directory.");

        return Path.Combine(directory, "tinyipc-sidecar.state.json");
    }

    internal static BrokerStateSnapshot CreateNew(string socketPath) {
        string unixSocketPath = UnixSharedStorageHelpers.ConvertPathForCurrentRuntime(socketPath);
        string sharedDirectory = Path.GetDirectoryName(unixSocketPath)
            ?? throw new InvalidOperationException("Broker socket path must have a parent directory.");

        return new BrokerStateSnapshot(
            Guid.NewGuid().ToString("N"),
            Environment.ProcessId,
            TryReadProcessStartTicks(Environment.ProcessId) ?? 0,
            unixSocketPath,
            sharedDirectory,
            DateTimeOffset.UtcNow.UtcTicks,
            SessionCount: 0,
            ChannelCount: 0,
            LastUpdatedUtcTicks: DateTimeOffset.UtcNow.UtcTicks);
    }

    internal static BrokerStateSnapshot? TryRead(string path) {
        try {
            if (!File.Exists(path))
                return null;

            string json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<BrokerStateSnapshot>(json, JsonOptions);
        } catch {
            return null;
        }
    }

    internal static BrokerStateSnapshot ReadRequired(string path)
        => TryRead(path) ?? throw new InvalidOperationException($"Broker state file '{path}' was not readable.");

    internal static void Write(string path, BrokerStateSnapshot snapshot) {
        string? directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory)) {
            Directory.CreateDirectory(directory);
            UnixSharedStorageHelpers.ApplyBrokerPermissions(directory, isDirectory: true);
        }

        string tempPath = path + "." + snapshot.InstanceId + ".tmp";
        string json = JsonSerializer.Serialize(snapshot, JsonOptions);

        File.WriteAllText(tempPath, json);
        UnixSharedStorageHelpers.ApplyBrokerPermissions(tempPath, isDirectory: false);
        File.Move(tempPath, path, overwrite: true);
        UnixSharedStorageHelpers.ApplyBrokerPermissions(path, isDirectory: false);
    }

    internal static void DeleteIfOwned(string path, string instanceId) {
        BrokerStateSnapshot? current = TryRead(path);
        if (current is null || !string.Equals(current.InstanceId, instanceId, StringComparison.Ordinal))
            return;

        try {
            File.Delete(path);
        } catch {
        }
    }

    internal static bool IsLive(BrokerStateSnapshot snapshot) {
        long? startTicks = TryReadProcessStartTicks(snapshot.Pid);
        return startTicks.HasValue && startTicks.Value == snapshot.ProcessStartTicks;
    }

    internal static bool TryTerminate(BrokerStateSnapshot snapshot, TimeSpan timeout) {
        long? currentStartTicks = TryReadProcessStartTicks(snapshot.Pid);
        if (!currentStartTicks.HasValue || currentStartTicks.Value != snapshot.ProcessStartTicks)
            return true;

        try {
            using Process process = Process.GetProcessById(snapshot.Pid);
            if (process.HasExited)
                return true;

            process.Kill(entireProcessTree: true);
            return process.WaitForExit((int)timeout.TotalMilliseconds);
        } catch {
            return false;
        }
    }

    private static long? TryReadProcessStartTicks(int pid) {
        try {
            string statPath = $"/proc/{pid}/stat";
            if (!File.Exists(statPath))
                return null;

            string stat = File.ReadAllText(statPath);
            int closeParen = stat.LastIndexOf(')');
            if (closeParen < 0 || closeParen + 2 >= stat.Length)
                return null;

            string[] fields = stat[(closeParen + 2)..].Split(' ', StringSplitOptions.RemoveEmptyEntries);
            return fields.Length > 19 && long.TryParse(fields[19], out long startTicks)
                ? startTicks
                : null;
        } catch {
            return null;
        }
    }
}
