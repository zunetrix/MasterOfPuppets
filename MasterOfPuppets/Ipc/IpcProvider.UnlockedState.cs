using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace MasterOfPuppets.Ipc;

internal partial class IpcProvider {

    // Global sets of IDs in common with all peers for each type
    public HashSet<uint> CommonEmotes { get; private set; } = new();
    public HashSet<uint> CommonFaceWear { get; private set; } = new();
    public HashSet<uint> CommonFashionAccessories { get; private set; } = new();
    public HashSet<uint> CommonItems { get; private set; } = new();
    public HashSet<uint> CommonMinions { get; private set; } = new();
    public HashSet<uint> CommonMounts { get; private set; } = new();

    // Used to accumulate peer responses for unlocked state
    private bool _hasAnyUnlockedStateResponse = false;
    private readonly object _unlockedStateLock = new();

    /// <summary>
    /// Emote IDs received from each peer. Key = ContentId, Value = set of unlocked emote IDs.
    /// Populated when peers respond to <see cref="RequestEmoteList"/>.
    /// </summary>
    public ConcurrentDictionary<long, HashSet<uint>> PeerEmoteLists { get; } = new();

    /// <summary>
    /// Broadcasts a request asking all peers to send their unlocked emote list.
    /// </summary>
    public void RequestEmoteList() {
        BroadCast(IpcMessage.Create(IpcMessageType.RequestEmoteList).Serialize(), includeSelf: false);
    }

    [IpcHandle(IpcMessageType.RequestEmoteList)]
    private void HandleRequestEmoteList(IpcMessage message) {
        // Must access game data on the framework thread.
        DalamudApi.Framework.RunOnFrameworkThread(BroadcastMyEmoteList);
    }

    [IpcHandle(IpcMessageType.EmoteList)]
    private void HandleEmoteList(IpcMessage message) {
        var ids = new uint[message.Data.Length / sizeof(uint)];
        Buffer.BlockCopy(message.Data, 0, ids, 0, message.Data.Length);
        PeerEmoteLists[message.BroadcasterId] = new HashSet<uint>(ids);
    }

    private void BroadcastMyEmoteList() {
        var ids = EmoteHelper.GetUnlockedItemsIds();

        var bytes = new byte[ids.Length * sizeof(uint)];
        Buffer.BlockCopy(ids, 0, bytes, 0, bytes.Length);
        BroadCast(IpcMessage.Create(IpcMessageType.EmoteList, bytes).Serialize(), includeSelf: false);
    }

    private void BroadcastMyUnlockedState() {
        var emotesIds = EmoteHelper.GetUnlockedItemsIds();
        var faceWearIds = FacewearHelper.GetUnlockedItemsIds();
        var fashionAccessoriesIds = FashionAccessoriesHelper.GetUnlockedItemsIds();
        var itemIds = ItemHelper.GetUnlockedItemsIds();
        var minionIds = MinionHelper.GetUnlockedItemsIds();
        var mountIds = MountHelper.GetUnlockedItemsIds();
        // Serialize as: [len][data]...[len][data] for each type
        var parts = new List<byte[]>();
        void Add(uint[] arr) {
            var len = BitConverter.GetBytes(arr.Length);
            var bytes = new byte[arr.Length * 4];
            Buffer.BlockCopy(arr, 0, bytes, 0, bytes.Length);
            parts.Add(len);
            parts.Add(bytes);
        }
        Add(emotesIds);
        Add(faceWearIds);
        Add(fashionAccessoriesIds);
        Add(itemIds);
        Add(minionIds);
        Add(mountIds);
        var totalLen = parts.Sum(p => p.Length);
        var all = new byte[totalLen];
        int pos = 0;
        foreach (var p in parts) { Buffer.BlockCopy(p, 0, all, pos, p.Length); pos += p.Length; }
        BroadCast(IpcMessage.Create(IpcMessageType.UnlockedState, all).Serialize(), includeSelf: false);
    }

    /// <summary>
    /// Broadcasts a request asking all peers to send their unlocked state (all types).
    /// </summary>
    public void RequestUnlockedState() {
        lock (_unlockedStateLock) {
            _hasAnyUnlockedStateResponse = false;
            CommonEmotes = new();
            CommonFaceWear = new();
            CommonFashionAccessories = new();
            CommonItems = new();
            CommonMinions = new();
            CommonMounts = new();
        }
        BroadCast(IpcMessage.Create(IpcMessageType.RequestUnlockedState).Serialize(), includeSelf: false);
    }

    [IpcHandle(IpcMessageType.RequestUnlockedState)]
    private void HandleRequestUnlockedState(IpcMessage message) {
        DalamudApi.Framework.RunOnFrameworkThread(BroadcastMyUnlockedState);
    }

    [IpcHandle(IpcMessageType.UnlockedState)]
    private void HandleUnlockedState(IpcMessage message) {
        // Message format: 6 arrays of uints, each prefixed by a 4-byte length
        var data = message.Data;
        int offset = 0;
        HashSet<uint> ReadSet() {
            int len = BitConverter.ToInt32(data, offset);
            offset += 4;
            var arr = new uint[len];
            Buffer.BlockCopy(data, offset, arr, 0, len * 4);
            offset += len * 4;
            return new HashSet<uint>(arr);
        }

        var emotes = ReadSet();
        var faceWear = ReadSet();
        var fashionAccessories = ReadSet();
        var items = ReadSet();
        var minions = ReadSet();
        var mounts = ReadSet();

        lock (_unlockedStateLock) {
            if (!_hasAnyUnlockedStateResponse) {
                CommonEmotes = emotes;
                CommonFaceWear = faceWear;
                CommonFashionAccessories = fashionAccessories;
                CommonItems = items;
                CommonMinions = minions;
                CommonMounts = mounts;
                _hasAnyUnlockedStateResponse = true;
            } else {
                // Incremental intersection (atomic property updates for thread safety)
                CommonEmotes = Intersect(CommonEmotes, emotes);
                CommonFaceWear = Intersect(CommonFaceWear, faceWear);
                CommonFashionAccessories = Intersect(CommonFashionAccessories, fashionAccessories);
                CommonItems = Intersect(CommonItems, items);
                CommonMinions = Intersect(CommonMinions, minions);
                CommonMounts = Intersect(CommonMounts, mounts);
            }
        }
    }

    private static HashSet<uint> Intersect(HashSet<uint> current, HashSet<uint> incoming) {
        var next = new HashSet<uint>(current);
        next.IntersectWith(incoming);
        return next;
    }
}
