using System;
using System.Collections.Generic;
using System.Linq;

using Dalamud.Game.ClientState.Conditions;

using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Client.UI.Info;

using MasterOfPuppets.Util;

namespace MasterOfPuppets;

/// <summary>
/// Teleports to an estate (FC / private / apartment).
/// <para>Own character: direct teleport through the housing aetheryte entries</para>
/// <para>Friend: friend list → <c>TeleportHousingFriend</c> menu. Only works for friends whose home world is the current world.</para>
/// </summary>
// https://github.com/Caraxi/SimpleTweaksPlugin/blob/main/Tweaks/EstateListCommand.cs
internal static class EstateTeleportManager {
    private const string LogTag = "[TeleportToEstate]";

    private enum EstateType {
        FreeCompany = 0,
        Private = 1,
        Apartment = 2,
    }

    // housing aetheryte ids
    // Mist, Lavender Beds, Goblet, Shirogane, Empyreum
    private static readonly uint[] FreeCompanyAetherytes = [56, 57, 58, 96, 164];
    private static readonly uint[] PrivateAetherytes = [59, 60, 61, 97, 165];

    public static void TeleportToEstate(string contentIdOrFriendName, string teleportTarget) {
        if (string.IsNullOrWhiteSpace(contentIdOrFriendName)) return;
        var index = ResolveEstateTeleportIndex(teleportTarget);
        if (index < 0) {
            DalamudApi.PluginLog.Warning($"{LogTag} unknown target '{teleportTarget}'. Use: fc, pe, ap");
            return;
        }

        DalamudApi.Framework.RunOnFrameworkThread(() => {
            if (!CanTeleport(out var reason)) {
                DalamudApi.PluginLog.Warning($"{LogTag} cannot teleport: {reason}");
                return;
            }

            if (IsSelf(contentIdOrFriendName)) {
                TeleportToOwnEstate(index);
                return;
            }

            Step_WaitForFriendList(contentIdOrFriendName.Trim(), index);
        });
    }

    //  checks
    private static bool CanTeleport(out string reason) {
        reason = string.Empty;
        if (!DalamudApi.PlayerState.IsLoaded || DalamudApi.ObjectTable.LocalPlayer is not { } player) {
            reason = "player not loaded";
            return false;
        }

        var condition = DalamudApi.Condition;
        if (condition[ConditionFlag.InCombat]) reason = "in combat";
        else if (condition[ConditionFlag.BetweenAreas] || condition[ConditionFlag.BetweenAreas51]) reason = "between areas";
        else if (condition[ConditionFlag.OccupiedInCutSceneEvent] || condition[ConditionFlag.WatchingCutscene]) reason = "in cutscene";
        else if (condition[ConditionFlag.Casting] || player.IsCasting) reason = "casting";

        return reason.Length == 0;
    }

    private static bool IsSelf(string contentIdOrName) {
        var state = DalamudApi.PlayerState;
        if (ulong.TryParse(contentIdOrName, out var cid)) return cid == state.ContentId;
        return string.Equals(contentIdOrName.Trim(), state.CharacterName, StringComparison.OrdinalIgnoreCase);
    }

    //  own estate: direct teleport, no menu
    private static unsafe void TeleportToOwnEstate(int index) {
        var type = (EstateType)index;
        var entry = DalamudApi.AetheryteList.FirstOrDefault(x => x != null && type switch {
            EstateType.FreeCompany => !x.IsApartment && !x.IsSharedHouse && FreeCompanyAetherytes.Contains(x.AetheryteId),
            EstateType.Private => !x.IsApartment && !x.IsSharedHouse && PrivateAetherytes.Contains(x.AetheryteId),
            EstateType.Apartment => x.IsApartment && !x.IsSharedHouse && PrivateAetherytes.Contains(x.AetheryteId),
            _ => false,
        });

        if (entry == null) {
            DalamudApi.PluginLog.Warning($"{LogTag} own {type} estate not found in the aetheryte list");
            return;
        }

        if (!Telepo.Instance()->Teleport(entry.AetheryteId, entry.SubIndex))
            DalamudApi.PluginLog.Warning($"{LogTag} own {type} teleport rejected (aetheryte {entry.AetheryteId}, sub {entry.SubIndex})");
    }

    //  friend estate
    //  Step 1
    //  Friend list is empty until requested (no friend list window opened yet): request it and wait for entries.
    private static unsafe void Step_WaitForFriendList(string contentIdOrFriendName, int index) {
        var requested = false;
        Coroutine.StartRunOnFramework(
            runFunction: () => {
                if (requested) return;
                var proxy = AgentFriendlist.Instance()->InfoProxy;
                if (proxy == null || proxy->EntryCount > 0) return;
                ((InfoProxyInterface*)proxy)->RequestData();
                requested = true;
            },
            stopWhen: () => {
                var proxy = AgentFriendlist.Instance()->InfoProxy;
                return proxy != null && proxy->EntryCount > 0;
            },
            callback: () => {
                if (!OpenFriendEstateMenu(contentIdOrFriendName)) return;
                Step_SelectTeleportOption(index);
            },
            timeoutMs: 8000);
    }

    /// <summary>Finds the friend (CID, exact name, then name prefix) and opens its estate menu. Logs the reason on failure.</summary>
    private static unsafe bool OpenFriendEstateMenu(string contentIdOrFriendName) {
        var proxy = AgentFriendlist.Instance()->InfoProxy;
        if (proxy == null || proxy->EntryCount == 0) {
            DalamudApi.PluginLog.Warning($"{LogTag} friend list is empty");
            return false;
        }

        var useContentId = ulong.TryParse(contentIdOrFriendName, out var contentId);
        var currentWorld = DalamudApi.PlayerState.CurrentWorld.RowId;

        ulong exact = 0, onOtherWorld = 0;
        var prefixMatches = new List<(ulong Cid, string Name)>();

        for (var i = 0U; i < proxy->EntryCount; i++) {
            var friend = proxy->GetEntry(i);
            if (friend == null || friend->ContentId == 0 || friend->Name[0] == 0) continue;
            if ((friend->ExtraFlags & 32) != 0) continue; // pending friend request

            var name = friend->NameString;
            var isMatch = useContentId
                ? friend->ContentId == contentId
                : name.Equals(contentIdOrFriendName, StringComparison.OrdinalIgnoreCase);
            var isPrefix = !useContentId && !isMatch && name.StartsWith(contentIdOrFriendName, StringComparison.OrdinalIgnoreCase);
            if (!isMatch && !isPrefix) continue;

            // friend estate teleport only works on the friend's home world
            if (friend->HomeWorld != currentWorld) {
                if (isMatch) onOtherWorld = friend->ContentId;
                continue;
            }

            if (isMatch) { exact = friend->ContentId; break; }
            prefixMatches.Add((friend->ContentId, name));
        }

        var target = exact;
        if (target == 0 && prefixMatches.Count > 0) {
            if (prefixMatches.Count > 1)
                DalamudApi.PluginLog.Warning($"{LogTag} '{contentIdOrFriendName}' matches several friends: [{string.Join(", ", prefixMatches.Select(m => m.Name))}], using '{prefixMatches[0].Name}'");
            target = prefixMatches[0].Cid;
        }

        if (target == 0) {
            DalamudApi.PluginLog.Warning(onOtherWorld != 0
                ? $"{LogTag} '{contentIdOrFriendName}' home world is not the current world"
                : $"{LogTag} friend '{contentIdOrFriendName}' not found");
            return false;
        }

        AgentFriendlist.Instance()->OpenFriendEstateTeleportation(target);
        return true;
    }

    //  Step 2
    //  Wait for TeleportHousingFriend and click the option.
    private static void Step_SelectTeleportOption(int index) {
        var ok = false;
        Coroutine.StartRunOnFramework(
            runFunction: () => { },
            stopWhen: () => ok = GameDialogManager.IsAddonReady(GameDialogManager.AddonName.TeleportHousingFriend),
            callback: () => {
                if (!ok) { DalamudApi.PluginLog.Warning($"{LogTag} timeout: TeleportHousingFriend not visible"); return; }
                GameDialogManager.ClickEstateTeleportOption(index);
            },
            timeoutMs: 3000);
    }

    //  helpers
    public static unsafe void RequestFriedsData(ulong contentId) {
        if (!DalamudApi.PlayerState.IsLoaded) return;
        var agent = AgentFriendlist.Instance();
        if (agent->InfoProxy == null) return;
        agent->RequestFriendInfo(contentId);
    }

    /// <summary>Friends whose estate can be visited from the current world.</summary>
    public static unsafe List<string> GetEstateFriends() {
        var result = new List<string>();
        if (!DalamudApi.PlayerState.IsLoaded) return result;
        var proxy = AgentFriendlist.Instance()->InfoProxy;
        if (proxy == null) {
            DalamudApi.PluginLog.Warning($"{LogTag} empty friend list");
            return result;
        }

        var currentWorld = DalamudApi.PlayerState.CurrentWorld.RowId;
        for (var i = 0U; i < proxy->EntryCount; i++) {
            var friend = proxy->GetEntry(i);
            if (friend == null || friend->ContentId == 0 || friend->Name[0] == 0) continue;
            if ((friend->ExtraFlags & 32) != 0) continue;
            if (friend->HomeWorld != currentWorld) continue;
            result.Add(friend->NameString);
        }
        return result;
    }

    private static int ResolveEstateTeleportIndex(string target) => target.Trim().ToLowerInvariant() switch {
        "fc" or "freecompany" => (int)EstateType.FreeCompany,
        "pe" or "private" => (int)EstateType.Private,
        "ap" or "apartments" or "apartment" => (int)EstateType.Apartment,
        var s when int.TryParse(s, out var i) => i,
        _ => -1
    };
}
