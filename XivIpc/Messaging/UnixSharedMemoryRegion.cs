using System.ComponentModel;
using System.Runtime.InteropServices;

using XivIpc.Internal;

namespace XivIpc.Messaging;

internal sealed class UnixSharedMemoryRegion : IDisposable {
    private const int O_RDONLY = 0x0000;
    private const int O_RDWR = 0x0002;
    private const int O_CREAT = 0x0040;
    private const int O_EXCL = 0x0080;

    private const int PROT_READ = 0x1;
    private const int PROT_WRITE = 0x2;
    private const int MAP_SHARED = 0x01;

    private const int SEEK_END = 2;

    private const int ENOENT = 2;
    private const int EEXIST = 17;

    private static readonly uint SharedMode = Convert.ToUInt32("666", 8);

    private readonly string _name;
    private readonly int _fd;
    private bool _disposed;

    public UnixSharedMemoryRegion(string busName, string kind, int requestedLength, UnixSharedFileLock initLock)
        : this(busName, kind, requestedLength, initLock, recreateIfSizeMismatch: false) {
    }

    public UnixSharedMemoryRegion(string busName, string kind, int requestedLength, UnixSharedFileLock initLock, bool recreateIfSizeMismatch) {
        if (!OperatingSystem.IsLinux())
            throw new UnixSharedMemoryBackendUnavailableException("POSIX shared memory backend is only available on native Linux.");

        if (!LinuxFutex.IsSupported)
            throw new UnixSharedMemoryBackendUnavailableException("Futex is not available on this Linux architecture.");

        ArgumentNullException.ThrowIfNull(initLock);

        if (string.IsNullOrWhiteSpace(busName))
            throw new ArgumentException("Bus name must be provided.", nameof(busName));

        if (string.IsNullOrWhiteSpace(kind))
            throw new ArgumentException("Kind must be provided.", nameof(kind));

        if (requestedLength <= 0)
            throw new ArgumentOutOfRangeException(nameof(requestedLength));

        _name = UnixSharedStorageHelpers.BuildSharedObjectName(busName, kind);

        TinyIpcLogger.Debug(
            nameof(UnixSharedMemoryRegion),
            "Open",
            "Opening shared-memory region.",
            ("name", _name),
            ("requestedLength", requestedLength),
            ("recreateIfSizeMismatch", recreateIfSizeMismatch));

        (int fd, int actualLength, bool createdNew) = initLock.Execute(() =>
            OpenOrCreateLocked(_name, requestedLength, recreateIfSizeMismatch));

        _fd = fd;
        Length = actualLength;
        CreatedNew = createdNew;

        IntPtr pointer = IntPtr.Zero;

        try {
            pointer = mmap(IntPtr.Zero, (nuint)Length, PROT_READ | PROT_WRITE, MAP_SHARED, _fd, IntPtr.Zero);
            if (pointer == new IntPtr(-1)) {
                int error = Marshal.GetLastWin32Error();
                pointer = IntPtr.Zero;
                throw new UnixSharedMemoryBackendUnavailableException(
                    $"mmap failed for '{_name}'.",
                    new Win32Exception(error));
            }

            Pointer = pointer;

            TinyIpcLogger.Info(
                nameof(UnixSharedMemoryRegion),
                "Opened",
                "Opened shared-memory region.",
                ("name", _name),
                ("length", Length),
                ("createdNew", CreatedNew));
        } catch {
            try {
                if (pointer != IntPtr.Zero && pointer != new IntPtr(-1))
                    _ = munmap(pointer, (nuint)Length);
            } catch {
            }

            try {
                _ = close(_fd);
            } catch {
            }

            throw;
        }
    }

    public IntPtr Pointer { get; }

    public int Length { get; }

    public bool CreatedNew { get; }

    public string Name => _name;

    public bool Exists() {
        ThrowIfDisposed();

        int fd = shm_open(_name, O_RDONLY, SharedMode);
        if (fd >= 0) {
            _ = close(fd);
            return true;
        }

        int error = Marshal.GetLastWin32Error();
        if (error == ENOENT)
            return false;

        throw new UnixSharedMemoryBackendUnavailableException(
            $"Failed checking existence for shared memory object '{_name}'.",
            new Win32Exception(error));
    }

    public void Unlink() {
        ThrowIfDisposed();

        int rc = shm_unlink(_name);
        if (rc == 0) {
            TinyIpcLogger.Info(
                nameof(UnixSharedMemoryRegion),
                "Unlinked",
                "Unlinked shared-memory region name.",
                ("name", _name));

            return;
        }

        int error = Marshal.GetLastWin32Error();
        if (error == ENOENT)
            return;

        throw new UnixSharedMemoryBackendUnavailableException(
            $"shm_unlink failed for '{_name}'.",
            new Win32Exception(error));
    }

    public static void UnlinkIfExists(string busName, string kind, UnixSharedFileLock initLock) {
        if (!OperatingSystem.IsLinux())
            return;

        ArgumentNullException.ThrowIfNull(initLock);

        string name = UnixSharedStorageHelpers.BuildSharedObjectName(busName, kind);

        initLock.Execute(() => {
            int rc = shm_unlink(name);
            if (rc == 0) {
                TinyIpcLogger.Info(
                    nameof(UnixSharedMemoryRegion),
                    "UnlinkedIfExists",
                    "Removed stale shared-memory region.",
                    ("name", name));

                return;
            }

            int error = Marshal.GetLastWin32Error();
            if (error != ENOENT) {
                throw new UnixSharedMemoryBackendUnavailableException(
                    $"shm_unlink failed for '{name}'.",
                    new Win32Exception(error));
            }
        });
    }

    public void Dispose() {
        if (_disposed)
            return;

        _disposed = true;

        try {
            if (Pointer != IntPtr.Zero && Pointer != new IntPtr(-1))
                _ = munmap(Pointer, (nuint)Length);
        } catch (Exception ex) {
            TinyIpcLogger.Warning(
                nameof(UnixSharedMemoryRegion),
                "MunmapFailed",
                "Failed to unmap shared-memory region.",
                ex,
                ("name", _name));
        }

        try {
            _ = close(_fd);
        } catch (Exception ex) {
            TinyIpcLogger.Warning(
                nameof(UnixSharedMemoryRegion),
                "CloseFailed",
                "Failed to close shared-memory file descriptor.",
                ex,
                ("name", _name));
        }
    }

    private static (int Fd, int Length, bool CreatedNew) OpenOrCreateLocked(string name, int requestedLength, bool recreateIfSizeMismatch) {
        int fd = -1;
        bool createdNew = false;

        try {
            fd = shm_open(name, O_CREAT | O_EXCL | O_RDWR, SharedMode);
            if (fd >= 0) {
                createdNew = true;

                if (ftruncate(fd, requestedLength) != 0) {
                    throw new UnixSharedMemoryBackendUnavailableException(
                        $"ftruncate failed for newly created shared memory object '{name}'.",
                        new Win32Exception(Marshal.GetLastWin32Error()));
                }

                _ = fchmod(fd, SharedMode);

                return (fd, requestedLength, true);
            }

            int createError = Marshal.GetLastWin32Error();
            if (createError != EEXIST) {
                throw new UnixSharedMemoryBackendUnavailableException(
                    $"shm_open create failed for '{name}'.",
                    new Win32Exception(createError));
            }

            fd = shm_open(name, O_RDWR, SharedMode);
            if (fd < 0) {
                throw new UnixSharedMemoryBackendUnavailableException(
                    $"shm_open open-existing failed for '{name}'.",
                    new Win32Exception(Marshal.GetLastWin32Error()));
            }

            long existingLength = lseek(fd, 0, SEEK_END);
            if (existingLength <= 0 || existingLength > int.MaxValue) {
                throw new UnixSharedMemoryBackendUnavailableException(
                    $"Shared memory object '{name}' has invalid size {existingLength}.");
            }

            _ = fchmod(fd, SharedMode);

            if (recreateIfSizeMismatch && existingLength != requestedLength) {
                TinyIpcLogger.Warning(
                    nameof(UnixSharedMemoryRegion),
                    "RecreateSizeMismatch",
                    "Existing shared-memory region size does not match requested size; recreating.",
                    null,
                    ("name", name),
                    ("existingLength", existingLength),
                    ("requestedLength", requestedLength));

                _ = close(fd);
                fd = -1;

                if (shm_unlink(name) != 0) {
                    int unlinkError = Marshal.GetLastWin32Error();
                    if (unlinkError != ENOENT) {
                        throw new UnixSharedMemoryBackendUnavailableException(
                            $"shm_unlink failed while recreating '{name}'.",
                            new Win32Exception(unlinkError));
                    }
                }

                fd = shm_open(name, O_CREAT | O_EXCL | O_RDWR, SharedMode);
                if (fd < 0) {
                    throw new UnixSharedMemoryBackendUnavailableException(
                        $"shm_open recreate failed for '{name}'.",
                        new Win32Exception(Marshal.GetLastWin32Error()));
                }

                createdNew = true;

                if (ftruncate(fd, requestedLength) != 0) {
                    throw new UnixSharedMemoryBackendUnavailableException(
                        $"ftruncate failed for recreated shared memory object '{name}'.",
                        new Win32Exception(Marshal.GetLastWin32Error()));
                }

                _ = fchmod(fd, SharedMode);
                return (fd, requestedLength, true);
            }

            return (fd, checked((int)existingLength), false);
        } catch {
            if (fd >= 0) {
                try {
                    _ = close(fd);
                } catch {
                }
            }

            if (createdNew) {
                try {
                    _ = shm_unlink(name);
                } catch {
                }
            }

            throw;
        }
    }

    private void ThrowIfDisposed() {
        if (_disposed)
            throw new ObjectDisposedException(nameof(UnixSharedMemoryRegion));
    }

    [DllImport("libc", SetLastError = true)]
    private static extern int shm_open(string name, int oflag, uint mode);

    [DllImport("libc", SetLastError = true)]
    private static extern int shm_unlink(string name);

    [DllImport("libc", SetLastError = true)]
    private static extern int ftruncate(int fd, int length);

    [DllImport("libc", SetLastError = true)]
    private static extern long lseek(int fd, long offset, int whence);

    [DllImport("libc", SetLastError = true)]
    private static extern int fchmod(int fd, uint mode);

    [DllImport("libc", SetLastError = true)]
    private static extern IntPtr mmap(IntPtr addr, nuint length, int prot, int flags, int fd, IntPtr offset);

    [DllImport("libc", SetLastError = true)]
    private static extern int munmap(IntPtr addr, nuint length);

    [DllImport("libc", SetLastError = true)]
    private static extern int close(int fd);
}
