using System.Runtime.InteropServices;

namespace XivIpc.Internal;

internal enum RuntimeEnvironmentKind {
    WindowsProcess,
    UnixProcess
}

internal readonly record struct RuntimeEnvironmentInfo(RuntimeEnvironmentKind Kind) {
    public bool IsWindowsProcess => Kind == RuntimeEnvironmentKind.WindowsProcess;
    public bool IsUnixProcess => Kind == RuntimeEnvironmentKind.UnixProcess;
}

internal static class RuntimeEnvironmentDetector {
    public static RuntimeEnvironmentInfo Detect()
        => RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? new RuntimeEnvironmentInfo(RuntimeEnvironmentKind.WindowsProcess)
            : new RuntimeEnvironmentInfo(RuntimeEnvironmentKind.UnixProcess);

    public static int GetCurrentProcessId() => Environment.ProcessId;
}
