using Lumina.Text.ReadOnly;

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
        if (addon == null) { return false; }
        for (var i = 0; i < addon->UldManager.NodeListCount; i++) {
            var node = addon->UldManager.NodeList[i];
            if (node == null || node->NodeId != 34 || (int)node->Type < 1000) continue;
            var evt = (AtkEvent*)((AtkComponentNode*)node)->AtkResNode.AtkEventManager.Event;
            if (evt == null) { return false; }
            addon->ReceiveEvent(evt->State.EventType, (int)evt->Param, ((AtkComponentNode*)node)->AtkResNode.AtkEventManager.Event);
            return true;
        }
        DalamudApi.PluginLog.Debug("[Confirm] node 34 not found");
        return false;
    }

    //  WorldTravelSelect
    /// <summary>Selects world at zero-based <paramref name="index"/> in the <c>WorldTravelSelect</c> addon.</summary>
    public static bool SelectWorldTravelEntry(int index) {
        // var addon = GetAddonByName(AddonName.WorldTravelSelect);
        // if (addon == null) return false;
        // return addon->FireCallbackInt(index + 2);

        return FireCallback(AddonName.WorldTravelSelect, 0, index + 2); // Ls: Callback.Fire(addon, true, 0, index + 2)
    }

    /// <summary>
    /// Switches the datacenter tab of <c>WorldTravelSelect</c>
    /// Ls 2.6: Callback.Fire(addon, true, 1, tab)
    /// </summary>
    public static bool SelectWorldTravelTab(int tab) {
        if (!IsAddonVisible(AddonName.WorldTravelSelect)) return false;
        return FireCallback(AddonName.WorldTravelSelect, 1, tab);
    }

    //  TelepotTown (aethernet list)
    //  AtkValues layout (from Lifestream ReaderTelepotTown):
    //    [0] entry count, [6 + 4*i .. 9 + 4*i] = type/state/icon/callbackData, [262 + i] = destination name
    private const int TelepotTownDataOffset = 6;
    private const int TelepotTownNameOffset = 262;
    private const int TelepotTownMaxEntries = 20;

    internal readonly record struct AethernetDestination(string Name, uint CallbackData);

    /// <summary>Reads the destinations listed in the open <c>TelepotTown</c> addon.</summary>
    internal static System.Collections.Generic.List<AethernetDestination> GetTelepotTownDestinations() {
        var list = new System.Collections.Generic.List<AethernetDestination>();
        if (!TryGetAddonByName<AtkUnitBase>(AddonName.TelepotTown, out var addon) || !IsAddonReady(addon)) return list;

        var values = addon->AtkValues;
        var valuesCount = addon->AtkValuesCount;
        if (values == null || valuesCount == 0) return list;

        // [0] = number of entries; slots past it hold stale data (reading them as strings crashes)
        var entries = values[0].Type == AtkValueType.UInt ? (int)values[0].UInt : TelepotTownMaxEntries;
        entries = System.Math.Min(entries, TelepotTownMaxEntries);

        for (var i = 0; i < entries; i++) {
            var nameIdx = TelepotTownNameOffset + i;
            var dataIdx = TelepotTownDataOffset + 4 * i + 3;
            if (nameIdx >= valuesCount || dataIdx >= valuesCount) break;

            var name = ReadAtkString(values[nameIdx]);
            if (string.IsNullOrEmpty(name)) continue;

            var data = values[dataIdx];
            if (data.Type != AtkValueType.UInt) continue;

            list.Add(new AethernetDestination(name, data.UInt));
        }
        return list;
    }

    /// <summary>Reads an AtkValue as text only when it actually holds a string (ECommons AtkReader rule).</summary>
    private static string? ReadAtkString(AtkValue value) {
        if (value.Type is not (AtkValueType.String or AtkValueType.ManagedString or AtkValueType.ConstString or AtkValueType.WideString))
            return null;
        if (value.String.Value == null) return null;
        return new ReadOnlySeStringSpan(value.String.Value).ExtractText().Trim();
    }

    /// <summary>Teleports to the aethernet destination in <c>TelepotTown</c> (Ls: Callback.Fire(addon, true, 11, callbackData)).</summary>
    public static bool SelectTelepotTownDestination(uint callbackData) {
        return FireCallback(AddonName.TelepotTown, 11, callbackData);
    }

    /// <summary>Closes the addon if it is open.</summary>
    public static bool CloseAddon(string addonName) {
        var addon = GetAddonByName(addonName);
        if (addon == null) return false;
        addon->Close(true);
        return true;
    }

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
