using FFXIVClientStructs.FFXIV.Component.GUI;


namespace MasterOfPuppets;

internal static unsafe partial class GameDialogManager {
    //  HousingSelectBlock
    /// <summary>Selects ward (1-indexed) in the <c>HousingSelectBlock</c> addon.</summary>
    public static bool SelectWardInHousingBlock(int ward) {
        if (!IsAddonVisible(AddonName.HousingSelectBlock)) return false;
        if (ward <= 1) return true;
        return FireCallback(AddonName.HousingSelectBlock, 1, ward - 1); // arg0=type, arg1=0-indexed ward
    }

    //  TeleportHousingFriend
    /// <summary>Clicks the teleport option at zero-based <paramref name="optionIndex"/> in the <c>TeleportHousingFriend</c> addon.</summary>
    public static bool ClickEstateTeleportOption(int optionIndex) {
        var addon = GetAddonByName(AddonName.TeleportHousingFriend);
        if (addon == null) { DalamudApi.PluginLog.Debug("[TeleportHousingFriend] addon null"); return false; }
        return FireCallback(AddonName.TeleportHousingFriend, optionIndex);
    }

    /// <summary>Returns true when the confirm button (ID 34) in <c>HousingSelectBlock</c> is enabled.</summary>
    public static bool IsHousingBlockConfirmEnabled() {
        var addon = GetAddonByName(AddonName.HousingSelectBlock);
        if (addon == null) return false;
        var btn = addon->GetComponentButtonById(34);
        return btn != null && btn->IsEnabled;
    }

    /// <summary>
    /// Clicks the confirm button (ID 34) in <c>HousingSelectBlock</c>.
    /// Walks NodeList directly - <c>OwnerNode</c> is null for this button.
    /// </summary>
    public static bool ClickHousingBlockConfirm() {
        var addon = GetAddonByName(AddonName.HousingSelectBlock);
        if (addon == null) { DalamudApi.PluginLog.Debug("[Confirm] addon null"); return false; }
        for (var i = 0; i < addon->UldManager.NodeListCount; i++) {
            var node = addon->UldManager.NodeList[i];
            if (node == null || node->NodeId != 34 || (int)node->Type < 1000) continue;
            var evt = (AtkEvent*)((AtkComponentNode*)node)->AtkResNode.AtkEventManager.Event;
            if (evt == null) { DalamudApi.PluginLog.Debug("[Confirm] evt null"); return false; }
            addon->ReceiveEvent(evt->State.EventType, (int)evt->Param, ((AtkComponentNode*)node)->AtkResNode.AtkEventManager.Event);
            return true;
        }
        DalamudApi.PluginLog.Debug("[Confirm] node 34 not found");
        return false;
    }

    //  WorldTravelSelect
    /// <summary>Selects world at zero-based <paramref name="index"/> in the <c>WorldTravelSelect</c> addon.</summary>
    public static bool SelectWorldTravelEntry(int index) =>
        FireCallback(AddonName.WorldTravelSelect, index + 2); // Ls: Callback.Fire(addon, true, index + 2)

    // town aetheryte
    public static bool ClickTeleportTownOption(int teleportOptionIndex) {
        var addon = GetAddonByName(AddonName.TeleportTown);
        if (addon == null) return false;
        var eventData = new AtkEvent();
        var inputData = new AtkEventData();
        addon->ReceiveEvent(AtkEventType.ListItemClick, teleportOptionIndex, &eventData, &inputData);
        return true;
    }

}
