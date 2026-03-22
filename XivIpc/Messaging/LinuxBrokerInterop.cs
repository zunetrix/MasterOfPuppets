using System.ComponentModel;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.InteropServices;

namespace XivIpc.Messaging;

internal static unsafe class LinuxBrokerInterop {
    private const string LibcModule = "TinyIpcLinuxLibc";
    private const int SolSocket = 1;
    private const int SoPeerCred = 17;

    static LinuxBrokerInterop() {
        NativeLibrary.SetDllImportResolver(typeof(LinuxBrokerInterop).Assembly, ResolveImport);
    }

    internal readonly record struct PeerCredentials(int Pid, uint Uid, uint Gid);

    internal static PeerCredentials GetPeerCredentials(Socket socket) {
        ArgumentNullException.ThrowIfNull(socket);

        UCred credentials = default;
        uint optionLength = (uint)sizeof(UCred);
        int rc = getsockopt(GetRawFd(socket), SolSocket, SoPeerCred, &credentials, &optionLength);
        if (rc != 0)
            throw new IOException("getsockopt(SO_PEERCRED) failed.", new Win32Exception(Marshal.GetLastPInvokeError()));

        return new PeerCredentials(credentials.Pid, credentials.Uid, credentials.Gid);
    }

    internal static bool IsPeerInGroup(PeerCredentials credentials, uint requiredGid) {
        if (credentials.Gid == requiredGid)
            return true;

        string path = $"/proc/{credentials.Pid}/status";
        if (!File.Exists(path))
            return false;

        foreach (string line in File.ReadLines(path)) {
            if (!line.StartsWith("Groups:", StringComparison.Ordinal))
                continue;

            string[] parts = line["Groups:".Length..]
                .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);

            foreach (string part in parts) {
                if (uint.TryParse(part, out uint gid) && gid == requiredGid)
                    return true;
            }

            return false;
        }

        return false;
    }

    internal static int GetRawFd(Socket socket)
        => checked((int)socket.Handle);

    [StructLayout(LayoutKind.Sequential)]
    private struct UCred {
        internal int Pid;
        internal uint Uid;
        internal uint Gid;
    }

    private static IntPtr ResolveImport(string libraryName, Assembly assembly, DllImportSearchPath? searchPath) {
        if (!string.Equals(libraryName, LibcModule, StringComparison.Ordinal))
            return IntPtr.Zero;

        foreach (string candidate in GetLibcCandidates()) {
            if (NativeLibrary.TryLoad(candidate, assembly, searchPath, out IntPtr handle))
                return handle;
        }

        return IntPtr.Zero;
    }

    private static IEnumerable<string> GetLibcCandidates() {
        yield return "libc.so.6";
        yield return "libc";
    }

    [DllImport(LibcModule, SetLastError = true, CallingConvention = CallingConvention.Cdecl)]
    private static extern int getsockopt(int socket, int level, int optionName, void* optionValue, uint* optionLength);
}

