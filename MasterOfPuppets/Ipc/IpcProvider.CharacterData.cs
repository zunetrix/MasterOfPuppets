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

internal partial class IpcProvider {
    private static readonly TimeSpan CharacterDataBroadcastInterval = TimeSpan.FromSeconds(1);
    public static readonly TimeSpan PeerCharacterDataMaxAge = TimeSpan.FromSeconds(5);
    private DateTime _nextCharacterDataBroadcast = DateTime.MinValue;

    /// <summary>
    /// Character info received from each peer. Key = ContentId.
    /// Populated when peers respond to <see cref="RequestCharacterData"/>.
    /// </summary>
    public ConcurrentDictionary<long, PeerCharacterInfo> PeerCharacterData { get; } = new();

    /// <summary>
    /// Broadcasts a request asking all peers to send their character info.
    /// </summary>
    public void RequestCharacterData() {
        BroadCast(IpcMessage.Create(IpcMessageType.RequestCharacterData).Serialize(), includeSelf: true);
    }

    public void UpdateCharacterDataHeartbeat() {
        var now = DateTime.UtcNow;
        PruneStaleCharacterData(now);

        if (now < _nextCharacterDataBroadcast)
            return;

        _nextCharacterDataBroadcast = now + CharacterDataBroadcastInterval;
        BroadcastCharacterData(IpcMessageType.CharacterData);
    }

    public IReadOnlyList<PeerCharacterInfo> GetFreshPeerCharacterData() {
        var now = DateTime.UtcNow;
        PruneStaleCharacterData(now);
        return PeerCharacterData.Values
            .Where(peer => IsFreshPeer(peer, now))
            .OrderBy(peer => peer.ContentId)
            .ToList();
    }

    [IpcHandle(IpcMessageType.RequestCharacterData)]
    private void HandleRequestCharacterData(IpcMessage message) {
        DalamudApi.Framework.RunOnFrameworkThread(() => BroadcastCharacterData(IpcMessageType.CharacterData));
    }

    [IpcHandle(IpcMessageType.CharacterData)]
    private void HandleCharacterData(IpcMessage message) {
        PeerCharacterData[message.BroadcasterId] = ParseCharacterInfo(message) with { LastSeen = DateTime.UtcNow };
    }

    private void PruneStaleCharacterData(DateTime now) {
        foreach (var pair in PeerCharacterData) {
            if (!IsFreshPeer(pair.Value, now))
                PeerCharacterData.TryRemove(pair.Key, out _);
        }
    }

    private static bool IsFreshPeer(PeerCharacterInfo peer, DateTime now) =>
        peer.LastSeen != default && now - peer.LastSeen <= PeerCharacterDataMaxAge;

    internal void BroadcastCharacterData(IpcMessageType type) {
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
            DalamudApi.PlayerState.CurrentWorld.ValueNullable?.Name.ToString() ?? ""
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
