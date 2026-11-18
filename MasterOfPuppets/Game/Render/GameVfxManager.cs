using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

using Dalamud.Hooking;

namespace MasterOfPuppets;

/// <summary>
/// Blocks new VFX (.avfx) by redirecting the resource request to a non-existent path.
/// Two independent per-client switches: block every VFX, or only the paths in <see cref="Configuration.BlockedVfxPaths"/>
/// (the list itself is shared between clients through the synced configuration).
/// </summary>
public sealed unsafe class GameVfxManager : IDisposable {
    // based on: Penumbra
    private const string GetResourceSyncSig = "E8 ?? ?? ?? ?? 48 8B C8 8B C3 F0 0F C0 81";
    private const string GetResourceAsyncSig = "E8 ?? ?? ?? 00 48 8B D8 EB ?? F0 FF 83 ?? ?? 00 00";

    private const uint ResourceTypeAvfx = 0x61766678; // "avfx"
    private const string NothingPath = "vfx/path/nothing.avfx";
    private const int MaxRecentPaths = 200;

    private delegate void* GetResourceSyncDelegate(void* resourceManager, void* category, uint* type, int* hash,
        byte* path, void* parameters, byte* file, uint line);

    private delegate void* GetResourceAsyncDelegate(void* resourceManager, void* category, uint* type, int* hash,
        byte* path, void* parameters, byte hasHandleLock, byte* file, uint line);

    private readonly Plugin _plugin;
    private readonly Hook<GetResourceSyncDelegate>? _syncHook;
    private readonly Hook<GetResourceAsyncDelegate>? _asyncHook;
    private readonly byte* _nothingPath;
    private readonly int _nothingHash;

    // read from the game's file threads: flags are volatile, the list snapshot is immutable and swapped as a whole
    private volatile bool _blockAll;
    private volatile bool _blockList;
    private volatile bool _recording;
    private volatile BlockList _list = new([], [], []);
    private readonly ConcurrentQueue<(uint Hash, string Path)> _recentPaths = new();
    private readonly ConcurrentDictionary<uint, byte> _recentHashes = new();

    /// <summary>Full paths (ending in .avfx) match by hash, anything else is a prefix (e.g. "vfx/common/eff/emt_played").</summary>
    private sealed record BlockList(HashSet<uint> Exact, byte[][] Prefixes, string[] PrefixStrings);
    private bool _hooksEnabled;

    /// <summary>Music notes shown above characters while using Performance.</summary>
    public static readonly string[] PerformanceNotesPreset = [
        "vfx/common/eff/emt_played2f.avfx",
        "vfx/common/eff/emt_played3af.avfx",
        "vfx/common/eff/emt_played3bf.avfx",
        "vfx/common/eff/emt_played4af.avfx",
        "vfx/common/eff/emt_played4bf.avfx",
        "vfx/common/eff/emt_played6at.avfx",
        "vfx/common/eff/emt_played6bt.avfx",
        "vfx/common/eff/emt_played6ct.avfx",
    ];

    public bool Available { get; }

    /// <summary>Block every new VFX.</summary>
    public bool Enabled => _blockAll;

    /// <summary>Block only the VFX in the configured list.</summary>
    public bool ListEnabled => _blockList;

    /// <summary>Log requested .avfx paths so they can be added to the list.</summary>
    public bool Recording {
        get => _recording;
        set {
            _recording = value;
            UpdateHooks();
        }
    }

    /// <summary>Recorded paths, most recent first, without the ones already covered by the list.</summary>
    public IEnumerable<string> RecentPaths => _recentPaths.Reverse().Select(e => e.Path).Where(p => !IsListed(p));

    public GameVfxManager(Plugin plugin) {
        _plugin = plugin;

        var bytes = Encoding.ASCII.GetBytes(NothingPath);
        _nothingPath = (byte*)Marshal.AllocHGlobal(bytes.Length + 1);
        Marshal.Copy(bytes, 0, (nint)_nothingPath, bytes.Length);
        _nothingPath[bytes.Length] = 0;
        _nothingHash = (int)Crc32(bytes);

        RefreshBlockList();

        try {
            if (!DalamudApi.SigScanner.TryScanText(GetResourceSyncSig, out var syncAddr)
                || !DalamudApi.SigScanner.TryScanText(GetResourceAsyncSig, out var asyncAddr)) {
                DalamudApi.PluginLog.Warning("[VfxHack] GetResource signatures not found, VFX blocking unavailable");
                return;
            }

            _syncHook = DalamudApi.GameInteropProvider.HookFromAddress<GetResourceSyncDelegate>(syncAddr, SyncDetour);
            _asyncHook = DalamudApi.GameInteropProvider.HookFromAddress<GetResourceAsyncDelegate>(asyncAddr, AsyncDetour);
            Available = true;
        } catch (Exception ex) {
            DalamudApi.PluginLog.Error(ex, "[VfxHack] failed to create hooks");
            _syncHook?.Dispose();
            _asyncHook?.Dispose();
            _syncHook = null;
            _asyncHook = null;
        }
    }

    /// <summary>Block every VFX. Mutually exclusive with <see cref="SetListBlocking"/>: enabling it turns list mode off.</summary>
    public void SetBlocking(bool block) {
        if (!CheckAvailable() || block == _blockAll) return;
        if (block && _blockList) {
            _blockList = false;
            PrintState("VfxHack List", false);
        }
        _blockAll = block;
        UpdateHooks();
        PrintState("VfxHack", block);
    }

    /// <summary>Block only the listed VFX. Mutually exclusive with <see cref="SetBlocking"/>: enabling it turns block-all off.</summary>
    public void SetListBlocking(bool block) {
        if (!CheckAvailable() || block == _blockList) return;
        if (block && _blockAll) {
            _blockAll = false;
            PrintState("VfxHack", false);
        }
        _blockList = block;
        UpdateHooks();
        PrintState("VfxHack List", block);
    }

    /// <summary>Rebuilds the list snapshot from the configuration (call after editing or syncing the list).</summary>
    public void RefreshBlockList() {
        var exact = new HashSet<uint>();
        var prefixes = new List<string>();
        foreach (var path in _plugin.Config.BlockedVfxPaths) {
            var normalized = NormalizePath(path);
            if (normalized.Length == 0) continue;
            if (normalized.EndsWith(".avfx")) exact.Add(Crc32(Encoding.ASCII.GetBytes(normalized)));
            else prefixes.Add(normalized);
        }
        _list = new BlockList(exact, prefixes.Select(Encoding.ASCII.GetBytes).ToArray(), prefixes.ToArray());
    }

    /// <summary>True when the path is covered by an exact or partial entry of the list.</summary>
    public bool IsListed(string path) {
        var normalized = NormalizePath(path);
        var list = _list;
        if (list.Exact.Contains(Crc32(Encoding.ASCII.GetBytes(normalized)))) return true;
        foreach (var prefix in list.PrefixStrings)
            if (normalized.StartsWith(prefix, StringComparison.Ordinal)) return true;
        return false;
    }

    public void ClearRecentPaths() {
        _recentPaths.Clear();
        _recentHashes.Clear();
    }

    public static string NormalizePath(string path) => path.Trim().Replace('\\', '/').ToLowerInvariant();

    private bool CheckAvailable() {
        if (Available) return true;
        DalamudApi.ChatGui.PrintError("MOP: VfxHack unavailable (signature not found)");
        return false;
    }

    private static void PrintState(string name, bool on) {
        if (on) DalamudApi.ChatGui.Print("", $"MOP: {name} ON", Style.Colors.SeGreen);
        else DalamudApi.ChatGui.Print("", $"MOP: {name} OFF", Style.Colors.SeRed);
    }

    private void UpdateHooks() {
        if (!Available) return;
        var needed = _blockAll || _blockList || _recording;
        if (needed == _hooksEnabled) return;

        if (needed) {
            _syncHook!.Enable();
            _asyncHook!.Enable();
        } else {
            _syncHook!.Disable();
            _asyncHook!.Disable();
        }
        _hooksEnabled = needed;
    }

    private void Redirect(uint* type, int* hash, ref byte* path) {
        if (type == null || *type != ResourceTypeAvfx || path == null) return;

        // the list is only checked when it is used (list blocking or recording); the two block modes never overlap
        if (_blockList || _recording) {
            var pathHash = Crc32LowerCase(path);
            var listed = MatchesList(_list, path, pathHash);
            if (_recording && !listed) Record(path, pathHash);
            if (!_blockAll && !(_blockList && listed)) return;
        } else if (!_blockAll) return;

        path = _nothingPath;
        if (hash != null) *hash = _nothingHash;
    }

    private static bool MatchesList(BlockList list, byte* path, uint hash) {
        if (list.Exact.Contains(hash)) return true;
        foreach (var prefix in list.Prefixes)
            if (StartsWithLowerCase(path, prefix)) return true;
        return false;
    }

    private static bool StartsWithLowerCase(byte* path, byte[] prefix) {
        for (var i = 0; i < prefix.Length; i++) {
            var b = path[i];
            if (b == 0) return false;
            if (b is >= (byte)'A' and <= (byte)'Z') b += 32;
            else if (b == (byte)'\\') b = (byte)'/';
            if (b != prefix[i]) return false;
        }
        return true;
    }

    /// <summary>Adds a path to the history once (string allocated only for new paths).</summary>
    private void Record(byte* path, uint hash) {
        if (!_recentHashes.TryAdd(hash, 0)) return;
        _recentPaths.Enqueue((hash, Marshal.PtrToStringAnsi((nint)path) ?? string.Empty));
        while (_recentPaths.Count > MaxRecentPaths && _recentPaths.TryDequeue(out var old))
            _recentHashes.TryRemove(old.Hash, out _);
    }

    private void* SyncDetour(void* resourceManager, void* category, uint* type, int* hash,
        byte* path, void* parameters, byte* file, uint line) {
        Redirect(type, hash, ref path);
        return _syncHook!.Original(resourceManager, category, type, hash, path, parameters, file, line);
    }

    private void* AsyncDetour(void* resourceManager, void* category, uint* type, int* hash,
        byte* path, void* parameters, byte hasHandleLock, byte* file, uint line) {
        Redirect(type, hash, ref path);
        return _asyncHook!.Original(resourceManager, category, type, hash, path, parameters, hasHandleLock, file, line);
    }

    private static uint Crc32(byte[] data) {
        var crc = 0xFFFFFFFFu;
        foreach (var b in data) crc = Crc32Step(crc, b);
        return ~crc;
    }

    /// <summary>CRC32 of a null-terminated path read straight from game memory, lowercased, no allocation.</summary>
    private static uint Crc32LowerCase(byte* path) {
        var crc = 0xFFFFFFFFu;
        for (var p = path; *p != 0; p++) {
            var b = *p;
            if (b is >= (byte)'A' and <= (byte)'Z') b += 32;
            else if (b == (byte)'\\') b = (byte)'/';
            crc = Crc32Step(crc, b);
        }
        return ~crc;
    }

    private static uint Crc32Step(uint crc, byte b) {
        crc ^= b;
        for (var i = 0; i < 8; i++)
            crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xEDB88320u : crc >> 1;
        return crc;
    }

    public void Dispose() {
        _syncHook?.Dispose();
        _asyncHook?.Dispose();
        _blockAll = false;
        _blockList = false;
        // _nothingPath is intentionally not freed: an in-flight async request may still read it (22 bytes)
    }
}
