using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;

namespace MasterOfPuppets.Ipc;

public record PeerCharacterInfo(
    ulong ContentId,
    string CharacterName,
    string HomeWorld,
    uint HomeWorldId,
    string CurrentWorld,
    uint CurrentWorldId,
    DateTime LastSeen = default
);

public enum PeerStatus {
    Online,
    Stale
}

internal partial class IpcProvider {
    private static readonly TimeSpan CharacterDataBroadcastInterval = TimeSpan.FromSeconds(5);

    /// <summary>Peers seen within this window are considered online (≈3 missed heartbeats).</summary>
    public static readonly TimeSpan PeerOnlineMaxAge = TimeSpan.FromSeconds(15);

    /// <summary>Stale peers are kept (for display) until this age, then removed.</summary>
    public static readonly TimeSpan PeerRemoveMaxAge = TimeSpan.FromSeconds(60);

    private DateTime _nextCharacterDataBroadcast = DateTime.MinValue;

    // last ContentId announced by this client, still valid during logout/unload when PlayerState may be cleared
    private ulong _lastAnnouncedContentId;

    /// <summary>
    /// Character info received from each peer. Key = ContentId.
    /// Populated by the heartbeat and when peers respond to <see cref="RequestCharacterData"/>.
    /// Includes stale peers; use <see cref="GetOnlinePeers"/> to act on peers.
    /// </summary>
    public ConcurrentDictionary<long, PeerCharacterInfo> PeerCharacterData { get; } = new();

    /// <summary>
    /// Broadcasts a request asking all peers to send their character info.
    /// </summary>
    public void RequestCharacterData() {
        BroadCast(IpcMessage.Create(IpcMessageType.RequestCharacterData).Serialize(), includeSelf: true);
    }

    /// <summary>Forces the heartbeat to be sent on the next frame (e.g. right after login).</summary>
    public void SendCharacterDataNow() {
        _nextCharacterDataBroadcast = DateTime.MinValue;
    }

    public void UpdateCharacterDataHeartbeat() {
        var now = DateTime.UtcNow;

        // don't consume the interval while player data isn't available yet (e.g. right after login)
        if (now < _nextCharacterDataBroadcast || !DalamudApi.PlayerState.IsLoaded)
            return;

        _nextCharacterDataBroadcast = now + CharacterDataBroadcastInterval;
        PruneStaleCharacterData(now);
        BroadcastCharacterData(IpcMessageType.CharacterData);
    }

    /// <summary>
    /// Online peers (including self) ordered by ContentId, so every client computes the same order.
    /// </summary>
    public IReadOnlyList<PeerCharacterInfo> GetOnlinePeers() {
        var now = DateTime.UtcNow;
        PruneStaleCharacterData(now);
        return PeerCharacterData.Values
            .Where(peer => GetPeerStatus(peer, now) == PeerStatus.Online)
            .OrderBy(peer => peer.ContentId)
            .ToList();
    }

    public static PeerStatus GetPeerStatus(PeerCharacterInfo peer, DateTime now) =>
        peer.LastSeen != default && now - peer.LastSeen <= PeerOnlineMaxAge ? PeerStatus.Online : PeerStatus.Stale;

    /// <summary>
    /// Notifies peers that this client is leaving (logout / plugin unload) so they drop it right away
    /// instead of waiting for it to go stale.
    /// </summary>
    public void BroadcastPeerDisconnected(bool immediate = false) {
        if (_lastAnnouncedContentId == 0) return;

        var serialized = IpcMessage.Create(IpcMessageType.PeerDisconnected, _lastAnnouncedContentId).Serialize();
        if (immediate) {
            // queue worker is about to be cancelled on dispose, publish synchronously
            if (Plugin.Config.SyncClients) _bus.PublishNow(serialized, TimeSpan.FromMilliseconds(500));
        } else {
            BroadCast(serialized);
        }

        PeerCharacterData.TryRemove((long)_lastAnnouncedContentId, out _);
        _lastAnnouncedContentId = 0;
    }

    [IpcHandle(IpcMessageType.RequestCharacterData)]
    private void HandleRequestCharacterData(IpcMessage message) {
        DalamudApi.Framework.RunOnFrameworkThread(() => BroadcastCharacterData(IpcMessageType.CharacterData));
    }

    [IpcHandle(IpcMessageType.CharacterData)]
    private void HandleCharacterData(IpcMessage message) {
        PeerCharacterData[message.BroadcasterId] = ParseCharacterInfo(message) with { LastSeen = DateTime.UtcNow };
    }

    [IpcHandle(IpcMessageType.PeerDisconnected)]
    private void HandlePeerDisconnected(IpcMessage message) {
        PeerCharacterData.TryRemove((long)message.DataStruct<ulong>(), out _);
    }

    private void PruneStaleCharacterData(DateTime now) {
        foreach (var pair in PeerCharacterData) {
            if (now - pair.Value.LastSeen > PeerRemoveMaxAge)
                PeerCharacterData.TryRemove(pair.Key, out _);
        }
    }

    internal void BroadcastCharacterData(IpcMessageType type) {
        if (!DalamudApi.PlayerState.IsLoaded) return;
        _lastAnnouncedContentId = DalamudApi.PlayerState.ContentId;

        BroadCast(IpcMessage.Create(
            type,
            new CharacterDataPayload {
                ContentId = DalamudApi.PlayerState.ContentId,
                HomeWorldId = DalamudApi.PlayerState.HomeWorld.RowId,
                CurrentWorldId = DalamudApi.PlayerState.CurrentWorld.RowId,
            },
            DalamudApi.PlayerState.CharacterName,
            DalamudApi.PlayerState.HomeWorld.ValueNullable?.Name.ToString() ?? "",
            DalamudApi.PlayerState.CurrentWorld.ValueNullable?.Name.ToString() ?? ""
        ).Serialize(), includeSelf: true);
    }

    internal void BroadcastTargetedCharacterData(IpcMessageType type, long targetId) {
        if (!DalamudApi.PlayerState.IsLoaded) return;

        BroadCast(IpcMessage.Create(
            type,
            new CharacterDataPayload {
                ContentId = DalamudApi.PlayerState.ContentId,
                HomeWorldId = DalamudApi.PlayerState.HomeWorld.RowId,
                CurrentWorldId = DalamudApi.PlayerState.CurrentWorld.RowId,
            },
            DalamudApi.PlayerState.CharacterName,
            DalamudApi.PlayerState.HomeWorld.ValueNullable?.Name.ToString() ?? "",
            DalamudApi.PlayerState.CurrentWorld.ValueNullable?.Name.ToString() ?? "",
            targetId.ToString()
        ).Serialize(), includeSelf: true);
    }

    internal static PeerCharacterInfo ParseCharacterInfo(IpcMessage message) {
        var p = message.DataStruct<CharacterDataPayload>();
        var name = message.StringData?.ElementAtOrDefault(0) ?? "";
        var homeWorld = message.StringData?.ElementAtOrDefault(1) ?? "";
        var currentWorld = message.StringData?.ElementAtOrDefault(2) ?? "";
        return new PeerCharacterInfo(p.ContentId, name, homeWorld, p.HomeWorldId, currentWorld, p.CurrentWorldId);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct CharacterDataPayload {
        public ulong ContentId;
        public uint HomeWorldId;
        public uint CurrentWorldId;
    }
}
