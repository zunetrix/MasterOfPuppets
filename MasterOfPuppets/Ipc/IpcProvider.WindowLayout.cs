using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;

using MasterOfPuppets.Util;

namespace MasterOfPuppets.Ipc;

internal partial class IpcProvider {

    /// <summary>Returns a snapshot of currently known connected peers (from PeerCharacterData).</summary>
    public IReadOnlyList<PeerCharacterInfo> GetConnectedPeers()
        => GetFreshPeerCharacterData();

    //  Apply Layout

    /// <summary>
    /// Broadcasts an apply-layout request to all connected clients (including self).
    /// Each peer looks up the layout by name in its local config and moves its own window
    /// to whichever slot contains its CID.
    /// </summary>
    public void ApplyWindowLayout(string layoutName) {
        BroadCast(IpcMessage.Create(IpcMessageType.ApplyWindowLayout, layoutName).Serialize(), includeSelf: true);
    }

    [IpcHandle(IpcMessageType.ApplyWindowLayout)]
    private void HandleApplyWindowLayout(IpcMessage message) {
        if (message.StringData == null || message.StringData.Length < 1) return;
        var layoutName = message.StringData[0];

        Plugin.GameWindowManager.ApplyWindowLayout(layoutName);
    }

    /// <summary>
    /// Broadcasts an apply-autotiled request to all connected clients (including self).
    /// </summary>
    public void ApplyAutoTiledLayout(bool keepAspect) {
        BroadCast(IpcMessage.Create(IpcMessageType.ApplyAutoTiledLayout, keepAspect).Serialize(), includeSelf: true);
    }

    [IpcHandle(IpcMessageType.ApplyAutoTiledLayout)]
    private void HandleApplyAutoTiledLayout(IpcMessage message) {
        if (message.Data == null || message.Data.Length == 0) return;
        bool keepAspect = BitConverter.ToBoolean(message.Data, 0);

        Plugin.GameWindowManager.ApplyAutoTiledLayoutInternal(keepAspect);
    }


    //  Request Window Info (Capture From Screen)

    /// <summary>
    /// Asks all connected clients (including self) to report their current window size/position.
    /// Responses arrive via <see cref="HandleWindowInfo"/>.
    /// </summary>
    public void RequestWindowInfo() {
        DalamudApi.PluginLog.Debug("[WindowLayout] Requesting window infos...");
        BroadCast(IpcMessage.Create(IpcMessageType.RequestWindowInfo).Serialize(), includeSelf: true);
    }

    [IpcHandle(IpcMessageType.RequestWindowInfo)]
    private void HandleRequestWindowInfo(IpcMessage _) {
        DalamudApi.PluginLog.Debug("[WindowLayout] Received RequestWindowInfo, querying HWND...");

        if (!Plugin.GameWindowManager.GetWindowVisualBounds(out var rect)) {
            DalamudApi.PluginLog.Error("[WindowLayout] GetWindowRect failed.");
            return;
        }

        var cid = DalamudApi.PlayerState.ContentId;
        var payload = new WindowInfoPayload {
            Cid = cid,
            X = rect.Left,
            Y = rect.Top,
            Width = rect.Width,
            Height = rect.Height,
        };
        DalamudApi.PluginLog.Debug($"[WindowLayout] Sending WindowInfo for {cid}: X={payload.X} Y={payload.Y} W={payload.Width} H={payload.Height}");
        BroadCast(IpcMessage.Create(IpcMessageType.WindowInfo, payload).Serialize(), includeSelf: true);
    }

    // Pending captures waiting to be collected by the UI
    private readonly List<WindowInfoPayload> _pendingWindowInfos = new();
    private bool _collectingWindowInfos;

    /// <summary>Starts a new capture session, clearing any previous results.</summary>
    public void BeginCaptureWindowInfos() {
        lock (_pendingWindowInfos) {
            _pendingWindowInfos.Clear();
            _collectingWindowInfos = true;
        }
        RequestWindowInfo();
    }

    /// <summary>Returns all collected window info payloads and ends the session.</summary>
    public IReadOnlyList<WindowInfoPayload> EndCaptureWindowInfos() {
        lock (_pendingWindowInfos) {
            _collectingWindowInfos = false;
            return [.. _pendingWindowInfos];
        }
    }

    [IpcHandle(IpcMessageType.WindowInfo)]
    private void HandleWindowInfo(IpcMessage message) {
        if (message.Data == null || message.Data.Length == 0) return;
        var payload = message.DataStruct<WindowInfoPayload>();
        DalamudApi.PluginLog.Debug($"[WindowLayout] Received WindowInfo for {payload.Cid}");
        lock (_pendingWindowInfos) {
            if (!_collectingWindowInfos) {
                DalamudApi.PluginLog.Debug("[WindowLayout] Ignored WindowInfo (not collecting)");
                return;
            }
            // deduplicate by CID
            _pendingWindowInfos.RemoveAll(p => p.Cid == payload.Cid);
            _pendingWindowInfos.Add(payload);
        }
    }
}

[StructLayout(LayoutKind.Sequential)]
public struct WindowInfoPayload {
    public ulong Cid;
    public int X;
    public int Y;
    public int Width;
    public int Height;
}
