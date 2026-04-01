using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace MasterOfPuppets;

// ST - LA
public class MultiboxManager {
    private readonly Plugin _plugin;

    private static readonly HashSet<string> FfxivMutexNames = new() {
        "\\BaseNamedObjects\\6AA83AB5-BAC4-4a36-9F66-A309770760CB_ffxiv_game00",
        "\\BaseNamedObjects\\6AA83AB5-BAC4-4a36-9F66-A309770760CB_ffxiv_game01",
    };

    public MultiboxManager(Plugin plugin) {
        _plugin = plugin;

        if (_plugin.Config.MultiboxEnabled)
            RemoveMutexes();
    }

    public static void RemoveMutexes() {
        Task.Run(() => {
            var pid = Process.GetCurrentProcess().Id;
            foreach (var hi in HandleUtil.GetHandles(pid)) {
                if (hi.Type != HandleUtil.HandleType.Mutant) continue;
                if (string.IsNullOrEmpty(hi.Name)) continue;
                if (!FfxivMutexNames.Contains(hi.Name)) continue;

                DalamudApi.PluginLog.Debug($"[Multibox] Close Mutex: {hi.Name}");
                hi.CloseRemote();
            }
        });
    }

    private static class HandleUtil {
        public enum HandleType { Other, Mutant }

        public class HandleInfo {
            public int ProcessId { get; }
            public ushort Handle { get; }
            public int GrantedAccess { get; }
            public byte RawType { get; }

            public HandleInfo(int processId, ushort handle, int grantedAccess, byte rawType) {
                ProcessId = processId;
                Handle = handle;
                GrantedAccess = grantedAccess;
                RawType = rawType;
            }

            // Closes the handle in the source process using DUPLICATE_CLOSE_SOURCE
            public void CloseRemote() {
                var procHandle = NativeMethods.OpenProcess(0x40, true, ProcessId);
                try {
                    NativeMethods.DuplicateHandle(procHandle, (IntPtr)Handle, IntPtr.Zero, out _, 0, false, 1 /* DUPLICATE_CLOSE_SOURCE */);
                } finally {
                    NativeMethods.CloseHandle(procHandle);
                }
            }

            private static readonly Dictionary<byte, string?> RawTypeMap = new();
            private string? name, typeStr;
            private HandleType? type;
            private bool typeAndNameAttempted;

            public string? Name {
                get {
                    if (name == null) InitTypeAndName();
                    return name;
                }
            }

            public HandleType? Type {
                get {
                    if (typeStr == null) InitType();
                    return type;
                }
            }

            private void InitType() {
                if (RawTypeMap.TryGetValue(RawType, out var value) && value != null) {
                    typeStr = value;
                    type = HandleTypeFromString(typeStr);
                } else {
                    InitTypeAndName();
                }
            }

            private void InitTypeAndName() {
                if (typeAndNameAttempted) return;
                typeAndNameAttempted = true;

                var sourceProcessHandle = IntPtr.Zero;
                var handleDuplicate = IntPtr.Zero;
                try {
                    sourceProcessHandle = NativeMethods.OpenProcess(0x40, true, ProcessId);
                    if (!NativeMethods.DuplicateHandle(sourceProcessHandle, (IntPtr)Handle, NativeMethods.GetCurrentProcess(), out handleDuplicate, 0, false, 2))
                        return;

                    if (RawTypeMap.TryGetValue(RawType, out var cached)) {
                        typeStr = cached;
                    } else {
                        NativeMethods.NtQueryObject(handleDuplicate, ObjectInformationClass.ObjectTypeInformation, IntPtr.Zero, 0, out var length);
                        var ptr = IntPtr.Zero;
                        try {
                            ptr = Marshal.AllocHGlobal(length);
                            if (NativeMethods.NtQueryObject(handleDuplicate, ObjectInformationClass.ObjectTypeInformation, ptr, length, out length) != NtStatus.StatusSuccess)
                                return;
                            typeStr = Marshal.PtrToStringUni((IntPtr)((long)ptr + 0x58 + 2 * IntPtr.Size));
                            RawTypeMap[RawType] = typeStr;
                        } finally {
                            Marshal.FreeHGlobal(ptr);
                        }
                    }

                    type = HandleTypeFromString(typeStr ?? throw new Exception("Invalid Type String"));

                    if (typeStr != null && GrantedAccess != 0x0012019f && GrantedAccess != 0x00120189 && GrantedAccess != 0x120089) {
                        NativeMethods.NtQueryObject(handleDuplicate, ObjectInformationClass.ObjectNameInformation, IntPtr.Zero, 0, out var length);
                        var ptr = IntPtr.Zero;
                        try {
                            ptr = Marshal.AllocHGlobal(length);
                            if (NativeMethods.NtQueryObject(handleDuplicate, ObjectInformationClass.ObjectNameInformation, ptr, length, out length) != NtStatus.StatusSuccess)
                                return;
                            name = Marshal.PtrToStringUni((IntPtr)((long)ptr + 2 * IntPtr.Size));
                        } finally {
                            Marshal.FreeHGlobal(ptr);
                        }
                    }
                } finally {
                    NativeMethods.CloseHandle(sourceProcessHandle);
                    if (handleDuplicate != IntPtr.Zero)
                        NativeMethods.CloseHandle(handleDuplicate);
                }
            }

            private static HandleType HandleTypeFromString(string s) => s == "Mutant" ? HandleType.Mutant : HandleType.Other;
        }

        public static IEnumerable<HandleInfo> GetHandles(int processId) {
            var length = 0x10000;
            var ptr = IntPtr.Zero;
            try {
                while (true) {
                    ptr = Marshal.AllocHGlobal(length);
                    var result = NativeMethods.NtQuerySystemInformation(SystemInformationClass.SystemHandleInformation, ptr, length, out var wantedLength);
                    if (result == NtStatus.StatusInfoLengthMismatch) {
                        length = Math.Max(length, wantedLength);
                        Marshal.FreeHGlobal(ptr);
                        ptr = IntPtr.Zero;
                    } else if (result == NtStatus.StatusSuccess) {
                        break;
                    } else {
                        throw new Exception("Failed to retrieve system handle information.");
                    }
                }

                var handleCount = IntPtr.Size == 4 ? Marshal.ReadInt32(ptr) : (int)Marshal.ReadInt64(ptr);
                var offset = IntPtr.Size;
                var size = Marshal.SizeOf(typeof(SystemHandleEntry));
                for (var i = 0; i < handleCount; i++) {
                    var entry = (SystemHandleEntry)Marshal.PtrToStructure((IntPtr)((long)ptr + offset), typeof(SystemHandleEntry))!;
                    if (entry.OwnerProcessId == processId)
                        yield return new HandleInfo(entry.OwnerProcessId, entry.Handle, entry.GrantedAccess, entry.ObjectTypeNumber);
                    offset += size;
                }
            } finally {
                if (ptr != IntPtr.Zero)
                    Marshal.FreeHGlobal(ptr);
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct SystemHandleEntry {
            public int OwnerProcessId;
            public byte ObjectTypeNumber;
            public byte Flags;
            public ushort Handle;
            public IntPtr Object;
            public int GrantedAccess;
        }

        private enum NtStatus {
            StatusSuccess = 0x00000000,
            StatusInfoLengthMismatch = unchecked((int)0xC0000004L)
        }

        private enum SystemInformationClass { SystemHandleInformation = 16 }
        private enum ObjectInformationClass { ObjectNameInformation = 1, ObjectTypeInformation = 2 }

        private static class NativeMethods {
            [DllImport("ntdll.dll")] internal static extern NtStatus NtQuerySystemInformation([In] SystemInformationClass systemInformationClass, [In] IntPtr systemInformation, [In] int systemInformationLength, [Out] out int returnLength);
            [DllImport("ntdll.dll")] internal static extern NtStatus NtQueryObject([In] IntPtr handle, [In] ObjectInformationClass objectInformationClass, [In] IntPtr objectInformation, [In] int objectInformationLength, [Out] out int returnLength);
            [DllImport("kernel32.dll")] internal static extern IntPtr GetCurrentProcess();
            [DllImport("kernel32.dll", SetLastError = true)] internal static extern IntPtr OpenProcess([In] int dwDesiredAccess, [In, MarshalAs(UnmanagedType.Bool)] bool bInheritHandle, [In] int dwProcessId);
            [DllImport("kernel32.dll", SetLastError = true)][return: MarshalAs(UnmanagedType.Bool)] internal static extern bool CloseHandle([In] IntPtr hObject);
            [DllImport("kernel32.dll", SetLastError = true)][return: MarshalAs(UnmanagedType.Bool)] internal static extern bool DuplicateHandle([In] IntPtr hSourceProcessHandle, [In] IntPtr hSourceHandle, [In] IntPtr hTargetProcessHandle, [Out] out IntPtr lpTargetHandle, [In] int dwDesiredAccess, [In, MarshalAs(UnmanagedType.Bool)] bool bInheritHandle, [In] int dwOptions);
        }
    }
}
