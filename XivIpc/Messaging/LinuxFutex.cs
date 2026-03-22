using System.Runtime.InteropServices;

using XivIpc.Internal;

namespace XivIpc.Messaging;

internal static unsafe class LinuxFutex {
    private const int FutexWait = 0;
    private const int FutexWake = 1;

    public static bool IsSupported
        => OperatingSystem.IsLinux() && ResolveSyscallNumber() >= 0;

    public static void Wait(int* address, int expectedValue, int timeoutMs) {
        if (!IsSupported)
            throw new PlatformNotSupportedException("Futex is only supported on Linux.");

        Timespec timeout = CreateTimeout(timeoutMs);
        _ = syscall(
            ResolveSyscallNumber(),
            (IntPtr)address,
            FutexWait,
            unchecked((uint)expectedValue),
            (IntPtr)(&timeout),
            IntPtr.Zero,
            0);
    }

    public static void WakeAll(int* address) {
        if (!IsSupported)
            throw new PlatformNotSupportedException("Futex is only supported on Linux.");

        if (TinyIpcLogger.IsEnabled(TinyIpcLogLevel.Trace)) {
            TinyIpcLogger.Trace(
                nameof(LinuxFutex),
                "WakeAll",
                "Waking futex waiters.",
                ("address", new IntPtr(address)),
                ("architecture", RuntimeInformation.ProcessArchitecture));
        }

        _ = syscall(
            ResolveSyscallNumber(),
            (IntPtr)address,
            FutexWake,
            int.MaxValue,
            IntPtr.Zero,
            IntPtr.Zero,
            0);
    }

    private static Timespec CreateTimeout(int timeoutMs) {
        if (timeoutMs < 0)
            timeoutMs = 0;

        return new Timespec {
            tv_sec = timeoutMs / 1000,
            tv_nsec = (timeoutMs % 1000) * 1_000_000L
        };
    }

    private static long ResolveSyscallNumber() {
        return RuntimeInformation.ProcessArchitecture switch {
            Architecture.X64 => 202,
            Architecture.Arm64 => 98,
            _ => -1
        };
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Timespec {
        public long tv_sec;
        public long tv_nsec;
    }

    [DllImport("libc", SetLastError = true)]
    private static extern long syscall(long number, IntPtr arg1, int arg2, uint arg3, IntPtr arg4, IntPtr arg5, uint arg6);
}

