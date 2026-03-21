using System;
using System.Collections.Generic;

using FFXIVClientStructs.FFXIV.Client.UI.Agent;

using MasterOfPuppets.Util;

namespace MasterOfPuppets;

// https://github.com/Caraxi/SimpleTweaksPlugin/blob/main/Tweaks/EstateListCommand.cs
internal static class EstateTeleportManager {
    public static void TeleportToEstate(string contentIdOrFriendName, string teleportTarget) {
        DalamudApi.Framework.RunOnFrameworkThread(() => {
            OpenEstateMenu(contentIdOrFriendName);
            Step_SelectTeleportOption(teleportTarget);
        });
    }

    private static unsafe void OpenEstateMenu(string contentIdOrFriendName) {
        if (!DalamudApi.PlayerState.IsLoaded) return;
        if (string.IsNullOrWhiteSpace(contentIdOrFriendName)) {
            return;
        }
        var useContentId = ulong.TryParse(contentIdOrFriendName, out var contentId);
        // TODO: on load friend list data is empty need find a way to request friend data before call teleport
        // RequestFriedsData();

        var agent = AgentFriendlist.Instance();

        for (var i = 0U; i < agent->InfoProxy->EntryCount; i++) {
            var friend = agent->InfoProxy->GetEntry(i);
            if (friend == null) continue;
            if (friend->HomeWorld != DalamudApi.PlayerState.CurrentWorld.RowId) continue;
            if (friend->ContentId == 0) continue;
            if (friend->Name[0] == 0) continue;
            if ((friend->ExtraFlags & 32) != 0) continue;

            if (useContentId && contentId == friend->ContentId) {
                agent->OpenFriendEstateTeleportation(friend->ContentId);
                return;
            }

            var name = friend->NameString;
            if (name.StartsWith(contentIdOrFriendName, StringComparison.InvariantCultureIgnoreCase)) {
                agent->OpenFriendEstateTeleportation(friend->ContentId);
                return;
            }
        }
    }

    public static unsafe void RequestFriedsData(ulong contentId) {
        if (!DalamudApi.PlayerState.IsLoaded) return;
        var agent = AgentFriendlist.Instance();
        if (agent->InfoProxy == null) return;
        agent->RequestFriendInfo(contentId);
    }

    public static unsafe List<string> GetEstateFriends() {
        var result = new List<string>();
        if (!DalamudApi.PlayerState.IsLoaded) return result;
        var agent = AgentFriendlist.Instance();
        if (agent->InfoProxy == null) {
            DalamudApi.PluginLog.Warning("[TeleportToEstate] empty friend list");
            return result;
        }

        for (var i = 0U; i < agent->InfoProxy->EntryCount; i++) {
            var friend = agent->InfoProxy->GetEntry(i);
            if (friend == null) continue;
            if (friend->HomeWorld != DalamudApi.PlayerState.CurrentWorld.RowId) continue;
            if (friend->ContentId == 0) continue;
            if (friend->Name[0] == 0) continue;
            if ((friend->ExtraFlags & 32) != 0) continue;
            result.Add(friend->NameString);
        }
        return result;
    }

    private static int ResolveEstateTeleportIndex(string target) => target.Trim().ToLowerInvariant() switch {
        "fc" or "freecompany" => 0,
        "pe" or "private" => 1,
        "ap" or "apartments" => 2,
        var s when int.TryParse(s, out var i) => i,
        _ => -1
    };

    private static void Step_SelectTeleportOption(string teleportTarget) {
        var index = ResolveEstateTeleportIndex(teleportTarget);
        if (index < 0) { DalamudApi.PluginLog.Warning($"[TeleportToEstate] unknown target '{teleportTarget}'. Use: fc, pe, ap"); return; }

        var ok = false;
        Coroutine.StartRunOnFramework(
            runFunction: () => { },
                stopWhen: () => ok = GameDialogManager.IsAddonVisible(GameDialogManager.AddonName.TeleportHousingFriend),
                callback: () => {
                    if (!ok) { DalamudApi.PluginLog.Warning("[TeleportToEstate] timeout: TeleportHousingFriend not visible"); return; }
                    GameDialogManager.ClickEstateTeleportOption(index);
                },
                timeoutMs: 3000);
    }
}
