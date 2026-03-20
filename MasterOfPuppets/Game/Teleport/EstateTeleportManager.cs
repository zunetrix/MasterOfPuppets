using System;

using FFXIVClientStructs.FFXIV.Client.UI.Agent;

using MasterOfPuppets.Util;

namespace MasterOfPuppets;

// https://github.com/Caraxi/SimpleTweaksPlugin/blob/main/Tweaks/EstateListCommand.cs
internal static class EstateTeleportManager {
    public static void TeleportToEstate(string contentIdOrFriendName, int teleportOptionIndex) {
        DalamudApi.Framework.RunOnFrameworkThread(() => {
            OpenEstateMenu(contentIdOrFriendName);
            Step_SelectTeleportOption(teleportOptionIndex);
        });
    }

    private static unsafe void OpenEstateMenu(string contentIdOrFriendName) {
        if (!DalamudApi.PlayerState.IsLoaded) return;
        if (string.IsNullOrWhiteSpace(contentIdOrFriendName)) {
            return;
        }
        var useContentId = ulong.TryParse(contentIdOrFriendName, out var contentId);

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

    private static void Step_SelectTeleportOption(int teleportOptionIndex) {
        var ok = false;
        Coroutine.StartRunOnFramework(
            runFunction: () => { },
                stopWhen: () => ok = GameDialogManager.IsAddonVisible(GameDialogManager.AddonName.TeleportHousingFriend),
                callback: () => {
                    if (!ok) { DalamudApi.PluginLog.Warning("[TeleportToEstate] step1 timeout: no TeleportHousingFriend visible"); return; }
                    DalamudApi.PluginLog.Warning($"TeleportHousingFriend found");
                    GameDialogManager.ClickEstateTeleportOption2(1);
                    // GameDialogManager.LogAddonEntries(GameDialogManager.AddonName.TeleportHousingFriend);
                    // GameDialogManager.SelectStringByText();
                },
                timeoutMs: 3000);
    }
}
