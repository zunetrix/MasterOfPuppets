using System.Buffers.Binary;

using XivIpc.Internal;

namespace XivIpc.IO;

internal sealed class UnixTinyMemoryMappedFile : IDisposable {
    private const uint FileMagic = 0x54494D46; // "TIMF"
    private const int FileVersion = 1;

    private const int MagicOffset = 0;
    private const int VersionOffset = 4;
    private const int LengthOffset = 8;
    private const int ReservedOffset = 12;
    private const int HeaderSize = 16;
    private static readonly TimeSpan LockWaitTimeout = TimeSpan.FromSeconds(5);

    private readonly long _declaredMaxFileSize;
    private readonly long _minimumPhysicalLength;
    private readonly string _path;
    private readonly object _processGate;
    private bool _disposed;
    private readonly string _name;
    private int _initializedFile;

    public UnixTinyMemoryMappedFile(string name, long maxFileSize) {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("File must be named.", nameof(name));

        if (maxFileSize <= 0 || maxFileSize > int.MaxValue) {
            throw new ArgumentOutOfRangeException(
                nameof(maxFileSize),
                "Max file size must be between 1 and Int32.MaxValue.");
        }

        _name = name;
        _declaredMaxFileSize = maxFileSize;
        _minimumPhysicalLength = HeaderSize + maxFileSize;

        UnixSharedStorageHelpers.EnsureSharedDirectoryExists();
        _path = UnixSharedStorageHelpers.BuildSharedFilePath(name, "mmf");
        _processGate = UnixSharedStorageHelpers.GetProcessGate(_path);

        TinyIpcLogger.Info(
            nameof(UnixTinyMemoryMappedFile),
            "Initialized",
            "Initialized Unix TinyMemoryMappedFile.",
            ("name", name),
            ("path", _path),
            ("maxFileSize", maxFileSize));

        if (!ShouldDeferInitializationToSidecar())
            EnsureInitializedFileIfNeeded();
    }

    public event EventHandler? FileUpdated;

    public long MaxFileSize => _declaredMaxFileSize;
    public string? Name => _name;

    public int GetFileSize() => GetFileSize(default);

    public int GetFileSize(CancellationToken cancellationToken = default) {
        ThrowIfDisposed();
        cancellationToken.ThrowIfCancellationRequested();
        EnsureInitializedFileIfNeeded();

        lock (_processGate) {
            using IDisposable processLock = UnixSharedStorageHelpers.AcquireProcessLock(_path);
            using FileStream stream = OpenSharedStream();
            LockStream(stream);

            try {
                EnsureStorageLength(stream);
                EnsureHeader(stream);

                byte[] header = ReadHeader(stream);
                int length = ReadInt32(header, LengthOffset);
                long payloadCapacity = GetPayloadCapacity(stream);
                if (length < 0 || length > payloadCapacity)
                    return 0;

                return length;
            } finally {
                UnlockStream(stream);
            }
        }
    }

    public byte[] Read()
        => Read(static stream => {
            byte[] buffer = new byte[stream.Length];
            _ = stream.Read(buffer, 0, buffer.Length);
            return buffer;
        });

    public T Read<T>(Func<MemoryStream, T> readData, CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(readData);
        ThrowIfDisposed();
        cancellationToken.ThrowIfCancellationRequested();
        EnsureInitializedFileIfNeeded();

        lock (_processGate) {
            using IDisposable processLock = UnixSharedStorageHelpers.AcquireProcessLock(_path);
            using FileStream stream = OpenSharedStream();
            LockStream(stream);

            try {
                EnsureStorageLength(stream);
                EnsureHeader(stream);

                byte[] payload = ReadCurrentPayload(stream);
                using var memoryStream = new MemoryStream(payload, writable: false);
                return readData(memoryStream);
            } finally {
                UnlockStream(stream);
            }
        }
    }

    public void Write(byte[] data) {
        using var stream = new MemoryStream(data ?? throw new ArgumentNullException(nameof(data)), writable: false);
        Write(stream);
    }

    public void Write(MemoryStream data, CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(data);
        cancellationToken.ThrowIfCancellationRequested();

        if (data.Length > _declaredMaxFileSize)
            throw new ArgumentOutOfRangeException(nameof(data), "Length greater than max file size.");

        ThrowIfDisposed();
        EnsureInitializedFileIfNeeded();

        byte[] payload = data.ToArray();

        lock (_processGate) {
            using IDisposable processLock = UnixSharedStorageHelpers.AcquireProcessLock(_path);
            using FileStream stream = OpenSharedStream();
            LockStream(stream);

            try {
                EnsureStorageLength(stream);
                EnsureHeader(stream);
                WritePayload(stream, payload);
            } finally {
                UnlockStream(stream);
            }
        }

        RaiseFileUpdated();
    }

    public void ReadWrite(Func<byte[], byte[]> updateFunc) {
        ArgumentNullException.ThrowIfNull(updateFunc);

        ReadWrite((readStream, writeStream) => {
            byte[] current = new byte[readStream.Length];
            _ = readStream.Read(current, 0, current.Length);
            byte[] updated = updateFunc(current) ?? throw new InvalidOperationException("Update function returned null.");
            writeStream.Write(updated, 0, updated.Length);
        });
    }

    public void ReadWrite(Action<MemoryStream, MemoryStream> updateFunc, CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(updateFunc);
        ThrowIfDisposed();
        cancellationToken.ThrowIfCancellationRequested();
        EnsureInitializedFileIfNeeded();

        byte[] updated;

        lock (_processGate) {
            using IDisposable processLock = UnixSharedStorageHelpers.AcquireProcessLock(_path);
            using FileStream stream = OpenSharedStream();
            LockStream(stream);

            try {
                EnsureStorageLength(stream);
                EnsureHeader(stream);

                byte[] current = ReadCurrentPayload(stream);
                using var readStream = new MemoryStream(current, writable: false);
                using var writeStream = new MemoryStream();
                updateFunc(readStream, writeStream);
                updated = writeStream.ToArray();

                if (updated.Length > _declaredMaxFileSize) {
                    throw new ArgumentOutOfRangeException(
                        nameof(updateFunc),
                        "Updated content length greater than max file size.");
                }

                WritePayload(stream, updated);
            } finally {
                UnlockStream(stream);
            }
        }

        RaiseFileUpdated();
    }

    public void Dispose() {
        _disposed = true;
        TinyIpcLogger.Debug(
            nameof(UnixTinyMemoryMappedFile),
            "Disposed",
            "Disposed Unix TinyMemoryMappedFile.",
            ("name", _name));
    }

    private void EnsureInitializedFileIfNeeded() {
        if (Volatile.Read(ref _initializedFile) != 0)
            return;

        lock (_processGate) {
            if (_initializedFile != 0)
                return;

            EnsureInitializedFileCore();
            Volatile.Write(ref _initializedFile, 1);
        }
    }

    private void EnsureInitializedFileCore() {
        using IDisposable processLock = UnixSharedStorageHelpers.AcquireProcessLock(_path);
        using FileStream stream = OpenSharedStream();
        LockStream(stream);

        try {
            EnsureStorageLength(stream);
            EnsureHeader(stream);
        } finally {
            UnlockStream(stream);
        }

        try {
            UnixSharedStorageHelpers.ApplyPermissions(_path, isDirectory: false);
        } catch {
        }
    }

    private static bool ShouldDeferInitializationToSidecar() {
        string? backend = TinyIpcEnvironment.GetEnvironmentVariable(TinyIpcEnvironment.MessageBusBackend);
        if (string.IsNullOrWhiteSpace(backend))
            backend = "auto";

        string normalized = backend.Trim().ToLowerInvariant();

        normalized = normalized switch {
            "sidecar-shared-memory" => "sidecar",
            "sidecar_shared_memory" => "sidecar",
            "shm" => "shared-memory",
            "sharedmemory" => "shared-memory",
            "shared_memory" => "shared-memory",
            _ => normalized
        };

        return normalized switch {
            "sidecar" => true,
            "auto" => RuntimeEnvironmentDetector.Detect().IsWindowsProcess,
            "direct" => false,
            "shared-memory" => false,
            _ => RuntimeEnvironmentDetector.Detect().IsWindowsProcess
        };
    }

    private FileStream OpenSharedStream() {
        return new FileStream(
            _path,
            FileMode.OpenOrCreate,
            FileAccess.ReadWrite,
            FileShare.ReadWrite | FileShare.Delete);
    }

    private void EnsureStorageLength(FileStream stream) {
        if (stream.Length < _minimumPhysicalLength) {
            stream.SetLength(_minimumPhysicalLength);
            stream.Flush(true);
        }
    }

    private static void EnsureHeader(FileStream stream) {
        byte[] header = new byte[HeaderSize];

        stream.Position = 0;
        ReadExactly(stream, header, 0, HeaderSize);

        bool valid =
            ReadUInt32(header, MagicOffset) == FileMagic &&
            ReadInt32(header, VersionOffset) == FileVersion;

        if (valid)
            return;

        Array.Clear(header, 0, header.Length);
        WriteUInt32(header, MagicOffset, FileMagic);
        WriteInt32(header, VersionOffset, FileVersion);
        WriteInt32(header, LengthOffset, 0);
        WriteInt32(header, ReservedOffset, 0);

        stream.Position = 0;
        stream.Write(header, 0, header.Length);
        stream.Flush(true);
    }

    private static byte[] ReadCurrentPayload(FileStream stream) {
        byte[] header = ReadHeader(stream);
        int length = ReadInt32(header, LengthOffset);
        long payloadCapacity = GetPayloadCapacity(stream);
        if (length < 0 || length > payloadCapacity)
            return Array.Empty<byte>();

        byte[] current = new byte[length];
        stream.Position = HeaderSize;
        ReadExactly(stream, current, 0, current.Length);
        return current;
    }

    private static void WritePayload(FileStream stream, byte[] data) {
        if (data.Length > GetPayloadCapacity(stream))
            throw new ArgumentOutOfRangeException(nameof(data), "Length greater than shared file capacity.");

        byte[] header = new byte[HeaderSize];
        WriteUInt32(header, MagicOffset, FileMagic);
        WriteInt32(header, VersionOffset, FileVersion);
        WriteInt32(header, LengthOffset, data.Length);
        WriteInt32(header, ReservedOffset, 0);

        stream.Position = 0;
        stream.Write(header, 0, header.Length);

        stream.Position = HeaderSize;
        if (data.Length > 0)
            stream.Write(data, 0, data.Length);

        stream.Flush(true);

        try {
            UnixSharedStorageHelpers.ApplyPermissions(GetPath(stream), isDirectory: false);
        } catch {
        }
    }

    private static byte[] ReadHeader(FileStream stream) {
        byte[] header = new byte[HeaderSize];
        stream.Position = 0;
        ReadExactly(stream, header, 0, header.Length);
        return header;
    }

    private static void ReadExactly(Stream stream, byte[] buffer, int offset, int count) {
        while (count > 0) {
            int read = stream.Read(buffer, offset, count);
            if (read == 0)
                break;

            offset += read;
            count -= read;
        }
    }

    private static long GetPayloadCapacity(FileStream stream)
        => Math.Max(0, stream.Length - HeaderSize);

    private static string GetPath(FileStream stream)
        => stream.Name;

    private void LockStream(FileStream stream) {
        if (RuntimeEnvironmentDetector.Detect().IsWindowsProcess)
            return;

        long length = Math.Max(stream.Length, _minimumPhysicalLength);
        DateTime deadline = DateTime.UtcNow + LockWaitTimeout;

        while (true) {
            try {
                stream.Lock(0, length);
                return;
            } catch (IOException) when (DateTime.UtcNow < deadline) {
                Thread.Sleep(2);
            }
        }
    }

    private void UnlockStream(FileStream stream) {
        if (RuntimeEnvironmentDetector.Detect().IsWindowsProcess)
            return;

        try {
            stream.Unlock(0, Math.Max(stream.Length, _minimumPhysicalLength));
        } catch (Exception ex) {
            TinyIpcLogger.Warning(
                nameof(UnixTinyMemoryMappedFile),
                "UnlockFailed",
                "Failed to unlock shared file stream.",
                ex,
                ("name", _name));
        }
    }

    private void ThrowIfDisposed() {
        if (_disposed)
            throw new ObjectDisposedException(nameof(UnixTinyMemoryMappedFile));
    }

    private void RaiseFileUpdated() {
        try {
            FileUpdated?.Invoke(this, EventArgs.Empty);
        } catch (Exception ex) {
            TinyIpcLogger.Error(
                nameof(UnixTinyMemoryMappedFile),
                "FileUpdatedHandlerFailed",
                "A FileUpdated handler threw an exception.",
                ex,
                ("name", _name));
        }
    }

    private static uint ReadUInt32(byte[] buffer, int offset)
        => BinaryPrimitives.ReadUInt32LittleEndian(buffer.AsSpan(offset, 4));

    private static int ReadInt32(byte[] buffer, int offset)
        => BinaryPrimitives.ReadInt32LittleEndian(buffer.AsSpan(offset, 4));

    private static void WriteUInt32(byte[] buffer, int offset, uint value)
        => BinaryPrimitives.WriteUInt32LittleEndian(buffer.AsSpan(offset, 4), value);

    private static void WriteInt32(byte[] buffer, int offset, int value)
        => BinaryPrimitives.WriteInt32LittleEndian(buffer.AsSpan(offset, 4), value);
}

