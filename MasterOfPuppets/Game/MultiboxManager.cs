using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace MasterOfPuppets;

// ffxiv_bossmod
public unsafe class MultiboxManager {
    private readonly Plugin _plugin;

    public MultiboxManager(Plugin plugin) {
        _plugin = plugin;

        if (_plugin.Config.MultiboxEnabled)
            RemoveMutexes();
    }

    public static void RemoveMutexes() {
        Task.Run(() => {
            foreach (var handle in EnumHandles()) {
                if (ObjectNameOrTypeName(handle, true) == "Mutant") {
                    var name = ObjectNameOrTypeName(handle, false);
                    if (name.Contains("6AA83AB5-BAC4-4a36-9F66-A309770760CB_ffxiv_game0", StringComparison.Ordinal)) {
                        DalamudApi.PluginLog.Debug($"[Multibox] Close Mutex: {handle:X} '{name}'");
                        NativeMethods.CloseHandle(handle);
                    }
                }
            }
        });
    }

    private static List<ulong> EnumHandles() {
        List<ulong> ret = new();
        // Initial buffer capacity (32KB). If it's too small, Windows won't fit the data and will tell us to enlarge it
        uint bufferSize = 0x8000;

        while (true) {
            var buffer = new byte[bufferSize];
            fixed (byte* pbuf = &buffer[0]) {
                var psnap = (PROCESS_HANDLE_SNAPSHOT_INFORMATION*)pbuf;
                psnap->NumberOfHandles = 0;
                uint retSize = 0;

                // ulong.MaxValue is mathematically -1, which acts as a pseudo-handle targeting "Current Process".
                // 51 is the magic internal index for ProcessHandleInformation class.
                // Using 51 natively provides ONLY the handles of this process, optimizing speed to instant.
                var status = NativeMethods.NtQueryInformationProcess(ulong.MaxValue, 51, pbuf, bufferSize, &retSize);

                // 0xC0000004 is the NTSTATUS code for STATUS_INFO_LENGTH_MISMATCH error.
                // It means our memory buffer was too small to hold all elements.
                if ((uint)status == 0xC0000004) {
                    bufferSize = retSize; // retSize was automatically populated with the memory bytes we actually need
                    continue; // Try again with the exact enlarged size
                }

                // If NTSTATUS is >= 0, that's equivalent to STATUS_SUCCESS and the data is safely retrieved.
                if (status >= 0) {
                    // The handle entries structs immediately follow the snapshot header in memory
                    var handles = (PROCESS_HANDLE_TABLE_ENTRY_INFO*)(psnap + 1);
                    for (ulong i = 0; i < psnap->NumberOfHandles; ++i)
                        ret.Add(handles[i].HandleValue);
                }
                break;
            }
        }
        return ret;
    }

    private static string ObjectNameOrTypeName(ulong handle, bool typeName) {
        // A 1024 bytes buffer is typically more than enough for fetching most Windows string objects
        uint bufferSize = 1024;
        var buffer = new byte[bufferSize];
        fixed (byte* pbuf = &buffer[0]) {
            uint retSize = 0;

            // 2 corresponds to ObjectTypeInformation (returns structure classifications like "Mutant", "File", "Thread").
            // 1 corresponds to ObjectNameInformation (returns the literal identifier string/name of the object).
            var status = NativeMethods.NtQueryObject(handle, typeName ? 2 : 1, pbuf, bufferSize, &retSize);

            // Once again, NTSTATUS >= 0 ensures the result block is STATUS_SUCCESS
            if (status >= 0) {
                // The memory output fits directly into the native UNICODE_STRING architecture pattern
                var name = (UNICODE_STRING*)pbuf;
                if (name->Buffer != null)
                    return Encoding.Unicode.GetString(name->Buffer, name->Length);
            }
        }
        return string.Empty;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PROCESS_HANDLE_TABLE_ENTRY_INFO {
        public ulong HandleValue;
        public ulong HandleCount;
        public ulong PointerCount;
        public uint GrantedAccess;
        public uint ObjectTypeIndex;
        public uint HandleAttributes;
        public uint Reserved;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PROCESS_HANDLE_SNAPSHOT_INFORMATION {
        public ulong NumberOfHandles;
        public ulong Reserved;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct UNICODE_STRING {
        public ushort Length;
        public ushort MaximumLength;
        public byte* Buffer;
    }

    private static class NativeMethods {
        [DllImport("ntdll.dll", ExactSpelling = true)]
        internal static extern int NtQueryInformationProcess(ulong ProcessHandle, int ProcessInformationClass, void* ProcessInformation, uint ProcessInformationLength, uint* ReturnLength);

        [DllImport("ntdll.dll", ExactSpelling = true)]
        internal static extern int NtQueryObject(ulong Handle, int ObjectInformationClass, void* ObjectInformation, uint ObjectInformationLength, uint* ReturnLength);

        [DllImport("kernel32.dll", ExactSpelling = true)]
        internal static extern bool CloseHandle(ulong Handle);
    }
}
