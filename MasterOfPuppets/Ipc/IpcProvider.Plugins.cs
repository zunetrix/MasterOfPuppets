using System;
using System.Linq;

namespace MasterOfPuppets.Ipc;

internal partial class IpcProvider {

    /// <summary>
    /// Broadcasts a Dalamud command (e.g. /xlenableplugin) that each client runs one after another,
    /// waiting <paramref name="delayMs"/> between clients so heavy plugins don't load on every client at once.
    /// Order: sender first, then connected peers in the Characters list order, then remaining peers.
    /// </summary>
    public void ExecutePluginCommandStaggered(string command, int delayMs) {
        var selfCid = DalamudApi.PlayerState.ContentId;
        var characterOrder = Plugin.Config.Characters.Select(c => c.Cid).ToList();

        var peerOrder = GetOnlinePeers()
            .Select(p => p.ContentId)
            .Where(cid => cid != 0 && cid != selfCid)
            .OrderBy(cid => characterOrder.IndexOf(cid) is var idx && idx >= 0 ? idx : int.MaxValue)
            .ThenBy(cid => cid);

        string[] stringData = [command, selfCid.ToString(), .. peerOrder.Select(cid => cid.ToString())];
        BroadCast(IpcMessage.Create(IpcMessageType.ExecutePluginCommandStaggered, Math.Max(0, delayMs), stringData).Serialize(), includeSelf: true);
    }

    [IpcHandle(IpcMessageType.ExecutePluginCommandStaggered)]
    private void HandleExecutePluginCommandStaggered(IpcMessage message) {
        var command = message.StringData[0];
        var order = message.StringData.Skip(1).ToList();
        var delayMs = message.DataStruct<int>();

        // clients not in the order (e.g. not logged in yet) run after every known client
        var slot = order.IndexOf(DalamudApi.PlayerState.ContentId.ToString());
        if (slot < 0) slot = order.Count;

        DalamudApi.Framework.RunOnTick(
            () => DalamudApi.CommandManager.ProcessCommand(command),
            TimeSpan.FromMilliseconds((long)slot * delayMs));
    }
}
