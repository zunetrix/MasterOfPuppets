using Dalamud.Memory;

using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;

using ValueType = FFXIVClientStructs.FFXIV.Component.GUI.ValueType;

namespace MasterOfPuppets;

internal static unsafe class GameDialogManager {
    public static bool ClickYes() => FireCallback("SelectYesno", 0); // 0 = button index yes
    public static bool ClickNo() => FireCallback("SelectYesno", (uint)1); // 1 = button index no
    public static bool ClickOk() => ClickAddonButton<AddonSelectOk>("SelectOk", a => a->OkButton);
    public static bool ClickRepairAll() => ClickAddonButton<AddonRepair>("Repair", a => a->RepairAllButton);

    public static bool IsAddonVisible(string name) {
        var addon = RaptureAtkUnitManager.Instance()->GetAddonByName(name);
        return addon != null && addon->IsVisible;
    }

    /// <summary>Finds and clicks the first entry whose text contains <paramref name="searchText"/> in <paramref name="addonName"/>.</summary>
    public static bool SelectStringByText(string searchText, string addonName = "SelectString") {
        var addon = (AddonSelectString*)GetAddonByName(addonName);
        if (addon == null) return false;
        var count = addon->PopupMenu.PopupMenu.EntryCount;
        if (count == 0) return false;
        for (var i = 0; i < count; i++) {
            var entry = addon->PopupMenu.PopupMenu.EntryNames[i];
            if (entry.Value == null) continue;
            var text = MemoryHelper.ReadSeStringNullTerminated((nint)entry.Value).ToString().Trim();
            if (text.Contains(searchText, System.StringComparison.OrdinalIgnoreCase))
                return FireCallback(addonName, i);
        }
        return false;
    }

    /// <summary>Logs all entries in <paramref name="addonName"/> to the plugin log. Useful for finding text/indices.</summary>
    public static void LogAddonEntries(string addonName = "SelectString") {
        var addon = (AddonSelectString*)GetAddonByName(addonName);
        if (addon == null) { DalamudApi.PluginLog.Debug($"[{addonName}] not visible"); return; }
        var count = addon->PopupMenu.PopupMenu.EntryCount;
        DalamudApi.PluginLog.Debug($"[{addonName}] {count} entries:");
        for (var i = 0; i < count; i++) {
            var entry = addon->PopupMenu.PopupMenu.EntryNames[i];
            var text = entry.Value != null
                ? MemoryHelper.ReadSeStringNullTerminated((nint)entry.Value).ToString().Trim()
                : "(null)";
            DalamudApi.PluginLog.Debug($"  [{i}] '{text}'");
        }
    }

    /// <summary>Clicks the entry at zero-based <paramref name="index"/> in <paramref name="addonName"/>. Locale-independent.</summary>
    public static bool SelectStringAtIndex(int index, string addonName = "SelectString") {
        var addon = GetAddonByName(addonName);
        if (addon == null) return false;
        var count = ((AddonSelectString*)addon)->PopupMenu.PopupMenu.EntryCount;
        return count > index && FireCallback(addonName, index);
    }

    /// <summary>Selects world at zero-based <paramref name="index"/> in the <c>WorldTravelSelect</c> addon.</summary>
    public static bool SelectWorldTravelEntry(int index) =>
        FireCallback("WorldTravelSelect", index + 2); // Ls: Callback.Fire(addon, true, index + 2)

    /// <summary>Selects ward (1-indexed) in the <c>HousingSelectBlock</c> addon.</summary>
    public static bool SelectWardInHousingBlock(int ward) {
        if (!IsAddonVisible("HousingSelectBlock")) return false;
        if (ward <= 1) return true;
        return FireCallback("HousingSelectBlock", 1, ward - 1); // arg0=type, arg1=0-indexed ward
    }

    /// <summary>Returns true when the confirm button (ID 34) in <c>HousingSelectBlock</c> is enabled.</summary>
    public static bool IsHousingBlockConfirmEnabled() {
        var addon = GetAddonByName("HousingSelectBlock");
        if (addon == null) return false;
        var btn = addon->GetComponentButtonById(34);
        return btn != null && btn->IsEnabled;
    }

    /// <summary>
    /// Clicks the confirm button (ID 34) in <c>HousingSelectBlock</c>.
    /// Walks the addon NodeList to reach the <c>AtkComponentNode</c> directly,
    /// bypassing <c>OwnerNode</c> which is null for this button.
    /// </summary>
    public static bool ClickHousingBlockConfirm() {
        var addon = GetAddonByName("HousingSelectBlock");
        if (addon == null) { DalamudApi.PluginLog.Debug("[Confirm] addon null"); return false; }
        for (var i = 0; i < addon->UldManager.NodeListCount; i++) {
            var node = addon->UldManager.NodeList[i];
            if (node == null || node->NodeId != 34 || (int)node->Type < 1000) continue;
            var evt = (AtkEvent*)((AtkComponentNode*)node)->AtkResNode.AtkEventManager.Event;
            if (evt == null) { DalamudApi.PluginLog.Debug("[Confirm] evt null"); return false; }
            // DalamudApi.PluginLog.Debug($"[Confirm] ReceiveEvent type={evt->State.EventType} param={evt->Param}");
            addon->ReceiveEvent(evt->State.EventType, (int)evt->Param, ((AtkComponentNode*)node)->AtkResNode.AtkEventManager.Event);
            return true;
        }
        DalamudApi.PluginLog.Debug("[Confirm] node 34 not found");
        return false;
    }

    private static AtkUnitBase* GetAddonByName(string name) {
        var addon = RaptureAtkUnitManager.Instance()->GetAddonByName(name);
        return addon != null && addon->IsVisible ? addon : null;
    }

    // C# type → AtkValue.Type: int→Int, uint→UInt, float→Float, bool→Bool
    private static bool FireCallback(string name, params object[] args) {
        var addon = GetAddonByName(name);
        if (addon == null) return false;

        var values = new AtkValue[args.Length];

        for (var i = 0; i < args.Length; i++) {
            values[i] = args[i] switch {
                int v => new AtkValue { Type = ValueType.Int, Int = v },
                uint v => new AtkValue { Type = ValueType.UInt, UInt = v },
                float v => new AtkValue { Type = ValueType.Float, Float = v },
                bool v => new AtkValue { Type = ValueType.Bool, Byte = (byte)(v ? 1 : 0) },
                _ => default
            };
        }
        fixed (AtkValue* u = values)
            addon->FireCallback((uint)values.Length, u, true);
        return true;
    }

    private delegate AtkComponentButton* ButtonSelector<T>(T* addon) where T : unmanaged;

    private static bool ClickAddonButton<T>(string name, ButtonSelector<T> selector)
            where T : unmanaged {
        var addon = GetAddonByName(name);
        if (addon == null) return false;
        var btn = selector((T*)addon);
        if (btn == null || !btn->IsEnabled || !btn->AtkResNode->IsVisible()) return false;
        var btnRes = btn->AtkComponentBase.OwnerNode->AtkResNode;
        var evt = (AtkEvent*)btnRes.AtkEventManager.Event;
        addon->ReceiveEvent(evt->State.EventType, (int)evt->Param, btnRes.AtkEventManager.Event);
        return true;
    }
}

