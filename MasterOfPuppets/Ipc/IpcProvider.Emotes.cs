using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

using Lumina.Excel.Sheets;

namespace MasterOfPuppets.Ipc;

internal partial class IpcProvider {

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
        var ids = DalamudApi.DataManager.GetExcelSheet<Emote>()
            .Where(e => e.IsUnlocked())
            .Select(e => e.RowId)
            .ToArray();

        var bytes = new byte[ids.Length * sizeof(uint)];
        Buffer.BlockCopy(ids, 0, bytes, 0, bytes.Length);
        BroadCast(IpcMessage.Create(IpcMessageType.EmoteList, bytes).Serialize(), includeSelf: false);
    }
}
