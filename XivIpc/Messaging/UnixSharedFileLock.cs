using XivIpc.Internal;

namespace XivIpc.Messaging;

internal sealed class UnixSharedFileLock {
    private static readonly TimeSpan LockWaitTimeout = TimeSpan.FromSeconds(5);

    private readonly string _path;
    private readonly object _processGate;
    private readonly bool _strictBrokerPermissions;

    public UnixSharedFileLock(string name, string kind, bool strictBrokerPermissions = false) {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Lock must be named.", nameof(name));

        if (string.IsNullOrWhiteSpace(kind))
            throw new ArgumentException("Lock kind must be provided.", nameof(kind));

        _path = ResolveNativeLockPath(name, kind);
        _strictBrokerPermissions = strictBrokerPermissions;

        string? directory = Path.GetDirectoryName(_path);
        if (!string.IsNullOrWhiteSpace(directory)) {
            Directory.CreateDirectory(directory);
            TryApplyPermissions(directory, isDirectory: true, strictBrokerPermissions);
        }

        _processGate = UnixSharedStorageHelpers.GetProcessGate(_path);

        TinyIpcLogger.Debug(
            nameof(UnixSharedFileLock),
            "Initialized",
            "Initialized shared file lock.",
            ("path", _path));
    }

    public T Execute<T>(Func<T> action) {
        ArgumentNullException.ThrowIfNull(action);

        lock (_processGate) {
            using FileStream stream = new FileStream(
                _path,
                FileMode.OpenOrCreate,
            FileAccess.ReadWrite,
            FileShare.ReadWrite | FileShare.Delete);

            TryApplyPermissions(_path, isDirectory: false, _strictBrokerPermissions);
            LockStream(stream);

            try {
                return action();
            } finally {
                UnlockStream(stream);
            }
        }
    }

    public void Execute(Action action) {
        ArgumentNullException.ThrowIfNull(action);

        Execute(() => {
            action();
            return 0;
        });
    }

    private static string ResolveNativeLockPath(string name, string kind) {
        string rawPath = UnixSharedStorageHelpers.BuildSharedFilePath(name, kind);

        string nativePath = TryConvertWinePathToUnix(rawPath) ?? rawPath;

        if (!Path.IsPathRooted(nativePath))
            nativePath = Path.GetFullPath(nativePath);

        return nativePath;
    }

    private static void TryApplyPermissions(string path, bool isDirectory, bool strictBrokerPermissions) {
        try {
            if (strictBrokerPermissions)
                UnixSharedStorageHelpers.ApplyBrokerPermissions(path, isDirectory);
            else
                UnixSharedStorageHelpers.ApplyPermissions(path, isDirectory);
        } catch (Exception ex) {
            TinyIpcLogger.Warning(
                nameof(UnixSharedFileLock),
                "PermissionApplyFailed",
                "Failed to apply permissions to shared file lock path.",
                ex,
                ("path", path));
        }
    }

    private static void LockStream(FileStream stream) {
        DateTime deadline = DateTime.UtcNow + LockWaitTimeout;

        while (true) {
            try {
                stream.Lock(0, 1);
                return;
            } catch (IOException) when (DateTime.UtcNow < deadline) {
                Thread.Sleep(2);
            } catch (IOException ex) {
                throw new TimeoutException("Timed out waiting for shared file lock.", ex);
            }
        }
    }

    private static void UnlockStream(FileStream stream) {
        try {
            stream.Unlock(0, 1);
        } catch (Exception ex) {
            TinyIpcLogger.Warning(
                nameof(UnixSharedFileLock),
                "UnlockFailed",
                "Failed to unlock shared file lock stream.",
                ex);
        }
    }

    private static string? TryConvertWinePathToUnix(string? path) {
        if (string.IsNullOrWhiteSpace(path))
            return null;

        string normalized = path.Replace('/', '\\').Trim();

        if (normalized.Length >= 3 &&
            (normalized[0] == 'Z' || normalized[0] == 'z') &&
            normalized[1] == ':' &&
            normalized[2] == '\\') {
            return normalized[2..].Replace('\\', '/');
        }

        return null;
    }
}

