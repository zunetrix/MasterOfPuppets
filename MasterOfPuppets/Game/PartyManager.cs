using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Client.UI.Info;

using MasterOfPuppets.Extensions;

namespace MasterOfPuppets;

// https://github.com/Infiziert90/ChatTwo/blob/main/ChatTwo/GameFunctions/Party.cs
internal static unsafe class PartyManager {

    // full character name: name@world
    public static void Invite(string characterFullName) {
        if (string.IsNullOrWhiteSpace(characterFullName))
            return;

        var atIndex = characterFullName.IndexOf('@');

        if (atIndex <= 0 || atIndex == characterFullName.Length - 1)
            return;

        var charName = characterFullName[..atIndex];
        var worldName = characterFullName[(atIndex + 1)..];

        var worldId = WorldHelper.GetWorldId(worldName);
        if (worldId is null)
            return;

        Invite(charName, (ushort)worldId);
    }

    public static void Invite(string characterName, ushort worldId) {
        if (string.IsNullOrWhiteSpace(characterName))
            return;

        var atIndex = characterName.IndexOf('@');
        var charName = atIndex >= 0
            ? characterName[..atIndex].Trim()
            : characterName.Trim();

        if (string.IsNullOrEmpty(charName))
            return;

        DalamudApi.Framework.RunOnTick(() => {
            InfoProxyPartyInvite.Instance()->InviteToParty(0, charName, worldId);
        });
    }

    internal static void InviteSameWorld(string name, ushort world, ulong contentId) {
        // this only works if target is on the same world
        fixed (byte* namePtr = name.ToTerminatedBytes()) {
            InfoProxyPartyInvite.Instance()->InviteToParty(contentId, namePtr, world);
        }
    }

    internal static void InviteOtherWorld(ulong contentId, ushort worldId = 0) {
        // third param is world, but it requires a specific world
        // if they're not on that world, it will fail
        // pass 0 and it will work on any world EXCEPT for the world the
        // current player is on
        if (contentId == 0) {
            DalamudApi.PluginLog.Warning($"Invalid Cid");
            return;
        }

        InfoProxyPartyInvite.Instance()->InviteToPartyContentId(contentId, worldId);
    }

    internal static void InviteInInstance(ulong contentId) {
        if (contentId == 0) {
            DalamudApi.PluginLog.Warning($"Invalid Cid");
            return;
        }

        InfoProxyPartyInvite.Instance()->InviteToPartyInInstanceByContentId(contentId);
    }

    internal static void Kick(string name, ulong contentId) {
        fixed (byte* namePtr = name.ToTerminatedBytes()) {
            AgentPartyMember.Instance()->Kick(namePtr, 0, contentId);
        }
    }

    internal static void Promote(string name, ulong contentId) {
        fixed (byte* namePtr = name.ToTerminatedBytes()) {
            AgentPartyMember.Instance()->Promote(namePtr, 0, contentId);
        }
    }
}
