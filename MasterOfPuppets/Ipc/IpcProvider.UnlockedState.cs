using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

using MasterOfPuppets.Extensions;

namespace MasterOfPuppets.Ipc;

internal partial class IpcProvider {

    // Global sets of IDs in common with all peers for each type
    public HashSet<uint> CommonEmotes { get; private set; } = [];
    public HashSet<uint> CommonFaceWear { get; private set; } = [];
    public HashSet<uint> CommonFashionAccessories { get; private set; } = [];
    public HashSet<uint> CommonItems { get; private set; } = [];
    public HashSet<uint> CommonMinions { get; private set; } = [];
    public HashSet<uint> CommonMounts { get; private set; } = [];

    // Used to accumulate peer responses for unlocked state
    private bool _hasAnyUnlockedStateResponse = false;
    private readonly object _unlockedStateLock = new();

    /// <summary>
    /// Emote IDs received from each peer. Key = ContentId, Value = set of unlocked emote IDs
    /// Populated when peers respond to <see cref="RequestEmoteList"/>.
    /// </summary>
    public ConcurrentDictionary<long, HashSet<uint>> PeerEmoteLists { get; } = new();

    /// <summary>
    /// Broadcasts a request asking all peers to send their unlocked emote list
    /// </summary>
    public void RequestEmoteList() {
        BroadCast(IpcMessage.Create(IpcMessageType.RequestEmoteList).Serialize(), includeSelf: false);
    }

    [IpcHandle(IpcMessageType.RequestEmoteList)]
    private void HandleRequestEmoteList(IpcMessage message) {
        // Must access game data on the framework thread
        DalamudApi.Framework.RunOnFrameworkThread(BroadcastMyEmoteList);
    }

    [IpcHandle(IpcMessageType.EmoteList)]
    private void HandleEmoteList(IpcMessage message) {
        var ids = new uint[message.Data.Length / sizeof(uint)];
        Buffer.BlockCopy(message.Data, 0, ids, 0, message.Data.Length);
        PeerEmoteLists[message.BroadcasterId] = [.. ids];
    }

    private void BroadcastMyEmoteList() {
        var ids = EmoteHelper.GetUnlockedItemsIds();

        var bytes = new byte[ids.Length * sizeof(uint)];
        Buffer.BlockCopy(ids, 0, bytes, 0, bytes.Length);
        BroadCast(IpcMessage.Create(IpcMessageType.EmoteList, bytes).Serialize(), includeSelf: false);
    }

    private class UnlockedStatePayload {
        public HashSet<uint> Emotes { get; set; } = [];
        public HashSet<uint> FaceWear { get; set; } = [];
        public HashSet<uint> FashionAccessories { get; set; } = [];
        public HashSet<uint> Items { get; set; } = [];
        public HashSet<uint> Minions { get; set; } = [];
        public HashSet<uint> Mounts { get; set; } = [];
    }

    private void BroadcastMyUnlockedState() {
        var payload = new UnlockedStatePayload {
            Emotes = [.. EmoteHelper.GetUnlockedItemsIds()],
            FaceWear = [.. FacewearHelper.GetUnlockedItemsIds()],
            FashionAccessories = [.. FashionAccessoriesHelper.GetUnlockedItemsIds()],
            Items = [.. ItemHelper.GetUnlockedItemsIds()],
            Minions = [.. MinionHelper.GetUnlockedItemsIds()],
            Mounts = [.. MountHelper.GetUnlockedItemsIds()]
        };

        var json = payload.JsonSerialize();
        BroadCast(IpcMessage.Create(IpcMessageType.UnlockedState, json).Serialize(), includeSelf: false);
    }

    /// <summary>
    /// Broadcasts a request asking all peers to send their unlocked state (all types).
    /// </summary>
    public void RequestUnlockedState() {
        lock (_unlockedStateLock) {
            _hasAnyUnlockedStateResponse = false;
            CommonEmotes = [];
            CommonFaceWear = [];
            CommonFashionAccessories = [];
            CommonItems = [];
            CommonMinions = [];
            CommonMounts = [];
        }
        BroadCast(IpcMessage.Create(IpcMessageType.RequestUnlockedState).Serialize(), includeSelf: false);
    }

    [IpcHandle(IpcMessageType.RequestUnlockedState)]
    private void HandleRequestUnlockedState(IpcMessage message) {
        DalamudApi.Framework.RunOnFrameworkThread(BroadcastMyUnlockedState);
    }

    [IpcHandle(IpcMessageType.UnlockedState)]
    private void HandleUnlockedState(IpcMessage message) {
        if (message.StringData == null || message.StringData.Length == 0 || string.IsNullOrWhiteSpace(message.StringData[0]))
            return;

        var incoming = message.StringData[0].JsonDeserialize<UnlockedStatePayload>();
        if (incoming == null)
            return;

        lock (_unlockedStateLock) {
            if (!_hasAnyUnlockedStateResponse) {
                CommonEmotes = incoming.Emotes ?? [];
                CommonFaceWear = incoming.FaceWear ?? [];
                CommonFashionAccessories = incoming.FashionAccessories ?? [];
                CommonItems = incoming.Items ?? [];
                CommonMinions = incoming.Minions ?? [];
                CommonMounts = incoming.Mounts ?? [];
                _hasAnyUnlockedStateResponse = true;
            } else {
                // Incremental intersection (atomic property updates for thread safety)
                CommonEmotes = Intersect(CommonEmotes, incoming.Emotes ?? []);
                CommonFaceWear = Intersect(CommonFaceWear, incoming.FaceWear ?? []);
                CommonFashionAccessories = Intersect(CommonFashionAccessories, incoming.FashionAccessories ?? []);
                CommonItems = Intersect(CommonItems, incoming.Items ?? []);
                CommonMinions = Intersect(CommonMinions, incoming.Minions ?? []);
                CommonMounts = Intersect(CommonMounts, incoming.Mounts ?? []);
            }
        }
    }

    private static HashSet<uint> Intersect(HashSet<uint> current, HashSet<uint> incoming) {
        var next = new HashSet<uint>(current);
        next.IntersectWith(incoming);
        return next;
    }
}
