namespace XivIpc.Messaging;

internal sealed class UnixSharedMemoryBackendUnavailableException : Exception {
    public UnixSharedMemoryBackendUnavailableException(string message, Exception? innerException = null)
        : base(message, innerException) {
    }
}

