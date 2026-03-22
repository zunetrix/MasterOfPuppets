namespace XivIpc.Messaging;

internal sealed class SidecarStartupException : InvalidOperationException {
    public SidecarStartupException(string message)
        : base(message) {
    }

    public SidecarStartupException(string message, Exception innerException)
        : base(message, innerException) {
    }
}
