using System;

using MasterOfPuppets.Extensions.Dalamud;
using MasterOfPuppets.Util;

namespace MasterOfPuppets.Ipc;

internal partial class IpcProvider {

    //  Invite

    /// <summary>
    /// Broadcasts a request asking all peers to send their character info,
    /// then invites each respondent to the party (master-initiated).
    /// </summary>
    public void RequestInviteAllToParty() {
        BroadCast(IpcMessage.Create(IpcMessageType.RequestInviteAllToParty).Serialize(), includeSelf: false);
    }

    [IpcHandle(IpcMessageType.RequestInviteAllToParty)]
    private void HandleRequestInviteAllToParty(IpcMessage message) {
        DalamudApi.Framework.RunOnFrameworkThread(() => BroadcastCharacterData(IpcMessageType.InviteToParty));
    }

    /// <summary>
    /// Sends our character info to all peers so they can invite us to their party (puppet-initiated).
    /// </summary>
    public void RequestInviteMe() {
        DalamudApi.Framework.RunOnFrameworkThread(() => BroadcastCharacterData(IpcMessageType.InviteToParty));
    }

    [IpcHandle(IpcMessageType.InviteToParty)]
    private void HandleInviteToParty(IpcMessage message) {
        var info = ParseCharacterInfo(message);
        if (string.IsNullOrEmpty(info.CharacterName)) return;

        PeerCharacterData[message.BroadcasterId] = info with { LastSeen = DateTime.UtcNow };
        DalamudApi.Framework.RunOnTick(() => Party.Invite(info.CharacterName, (ushort)info.HomeWorldId));
    }

    //  Party management
    // /partycmd breakup
    // /partycmd join

    /// <summary>Broadcasts a request to all peers to leave their current party.</summary>
    public void RequestDisbandParty() {
        BroadCast(IpcMessage.Create(IpcMessageType.DisbandParty).Serialize(), includeSelf: true);
    }

    [IpcHandle(IpcMessageType.DisbandParty)]
    private void HandleDisbandParty(IpcMessage message) {
        DalamudApi.Framework.RunOnTick(() => {
            if (DalamudApi.PartyList.IsInParty())
                Chat.SendMessage("/leave");
        });
    }

    /// <summary>Requests the current party leader to promote us to leader.</summary>
    public void RequestPartyLeader() {
        if (DalamudApi.PartyList.IsPartyLeader() || !DalamudApi.PlayerState.IsLoaded) return;
        BroadCast(IpcMessage.Create(IpcMessageType.RequestPartyLeader, DalamudApi.PlayerState.ContentId).Serialize(), includeSelf: false);
    }

    [IpcHandle(IpcMessageType.RequestPartyLeader)]
    private void HandleRequestPartyLeader(IpcMessage message) {
        if (!DalamudApi.PartyList.IsPartyLeader()) return;

        var requesterCid = message.DataStruct<long>();
        var partyMember = DalamudApi.PartyList.GetPartyMemberFromCid(requesterCid);
        if (partyMember == null) return;

        Party.Promote(partyMember.Name.ToString(), (ulong)requesterCid);
        Coroutine.StartRunOnFramework(
            runFunction: () => { },
            timeoutMs: 500,
            callback: () => DalamudApi.Framework.RunOnTick(GameDialogManager.ClickYes));
    }
}
