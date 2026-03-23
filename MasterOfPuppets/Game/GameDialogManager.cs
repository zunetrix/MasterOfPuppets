using System;
using System.Runtime.InteropServices;
using System.Text;

using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;

using Lumina.Excel.Sheets;

using ValueType = FFXIVClientStructs.FFXIV.Component.GUI.ValueType;

namespace MasterOfPuppets;

internal static unsafe class GameDialogManager {

    public static class AddonName {
        public const string SelectYesno = "SelectYesno";
        public const string SelectOk = "SelectOk";
        public const string Repair = "Repair";
        public const string SelectString = "SelectString";
        public const string HousingMenu = "HousingMenu";
        public const string HousingSelectBlock = "HousingSelectBlock";
        public const string WorldTravelSelect = "WorldTravelSelect";
        public const string TeleportHousingFriend = "TeleportHousingFriend";
        public const string EasterMowingResult = "EasterMowingResult";
        public const string FGSEnterDialog = "FGSEnterDialog";
        public const string ContentsFinderConfirm = "ContentsFinderConfirm";
    }

    private static string TextAcceptJoinParty =>
        DalamudApi.DataManager.GetExcelSheet<Addon>(DalamudApi.ClientState.ClientLanguage)
            ?.GetRow(120).Text.ExtractText()
        ?? "Join <string(lstr1)>'s party?";
    private static string TextAcceptTeleport =>
            DalamudApi.DataManager.GetExcelSheet<Addon>(DalamudApi.ClientState.ClientLanguage)
                ?.GetRow(1800).Text.ExtractText()
            ?? "Accept Teleport to <ennoun(PlaceName,2,<sheet(Aetheryte,lnum1,8)>,2,1)>?";


    public static bool IsAddonVisible(string name) {
        var addon = RaptureAtkUnitManager.Instance()->GetAddonByName(name);
        return addon != null && addon->IsVisible;
    }

    public static bool IsAddonReady(string name) {
        var addon = RaptureAtkUnitManager.Instance()->GetAddonByName(name);
        return addon != null && addon->IsVisible && addon->IsReady && addon->UldManager.LoadedState != AtkLoadState.Loaded;
    }

    /// <summary>Finds and clicks the first entry whose text contains <paramref name="searchText"/> in <paramref name="addonName"/>.</summary>
    public static bool SelectStringByText(string searchText, string addonName = AddonName.SelectString) {
        var addon = (AddonSelectString*)GetAddonByName(addonName);
        if (addon == null) return false;
        var count = addon->PopupMenu.PopupMenu.EntryCount;
        if (count == 0) return false;
        for (var i = 0; i < count; i++) {
            var text = Marshal.PtrToStringUTF8((nint)addon->PopupMenu.PopupMenu.EntryNames[i].Value)?.Trim();
            if (string.IsNullOrEmpty(text)) continue;
            if (text.Contains(searchText, System.StringComparison.OrdinalIgnoreCase))
                return FireCallback(addonName, i);
        }
        return false;
    }

    /// <summary>Clicks the entry at zero-based <paramref name="index"/> in <paramref name="addonName"/>. Locale-independent.</summary>
    public static bool SelectStringAtIndex(int index, string addonName = AddonName.SelectString) {
        var addon = GetAddonByName(addonName);
        if (addon == null) return false;
        var count = ((AddonSelectString*)addon)->PopupMenu.PopupMenu.EntryCount;
        return count > index && FireCallback(addonName, index);
    }

    public static bool ClickYes() => FireCallback(AddonName.SelectYesno, 0);
    public static bool ClickNo() => FireCallback(AddonName.SelectYesno, (uint)1);
    public static bool ClickOk() => ClickAddonButton<AddonSelectOk>(AddonName.SelectOk, a => a->OkButton);
    public static bool ClickRepairAll() => ClickAddonButton<AddonRepair>(AddonName.Repair, a => a->RepairAllButton);
    public static bool ClickContentsFinderConfirm() => ClickAddonButton<AddonContentsFinderConfirm>(AddonName.ContentsFinderConfirm, a => a->CommenceButton);
    // ContentsFinderConfirm // addon->ReceiveEvent(AtkEventType.ButtonClick, 8, &eventData, &inputData);

    /// <summary>
    /// Called every framework tick. Auto-accepts party invites and/or teleport requests
    /// by matching the SelectYesno prompt text against known Addon row fragments.
    /// </summary>
    public static void AutoAcceptUpdate(bool acceptParty, bool acceptTeleport) {
        if (!acceptParty && !acceptTeleport) return;
        if (!IsAddonVisible(AddonName.SelectYesno)) return;

        var addon = (AddonSelectYesno*)GetAddonByName(AddonName.SelectYesno);
        if (addon == null || addon->PromptText == null) return;

        var text = addon->PromptText->NodeText.ToString();
        if (string.IsNullOrEmpty(text)) return;

        if (acceptParty && ContainsAllFragments(text, TextAcceptJoinParty)) { ClickYes(); return; }
        if (acceptTeleport && ContainsAllFragments(text, TextAcceptTeleport)) { ClickYes(); return; }
    }

    // Splits the extracted Addon row text (which has template vars stripped) into words ≥ 3 chars
    // and checks that the dialog text contains each one. Locale-aware since TextAccept* use ClientLanguage.
    private static bool ContainsAllFragments(string dialogText, string pattern) {
        foreach (var part in pattern.Split(' ', StringSplitOptions.RemoveEmptyEntries)) {
            if (part.Length < 3) continue;
            if (!dialogText.Contains(part, StringComparison.OrdinalIgnoreCase)) return false;
        }
        return true;
    }

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

    // seasonal events
    public static bool ClickEasterMowingLeave() {
        var addon = GetAddonByName(AddonName.EasterMowingResult);
        if (addon == null) return false;
        var eventData = new AtkEvent();
        var inputData = new AtkEventData();
        addon->ReceiveEvent(AtkEventType.ButtonClick, 1, &eventData, &inputData);
        return true;
    }

    public static bool ClickFallGuysEnterDialog() {
        var addon = GetAddonByName(AddonName.FGSEnterDialog);
        if (addon == null) return false;
        var eventData = new AtkEvent();
        var inputData = new AtkEventData();
        addon->ReceiveEvent(AtkEventType.ButtonClick, 0, &eventData, &inputData);
        return true;
    }

    //  Private
    private static AtkUnitBase* GetAddonByName(string name) {
        var addon = RaptureAtkUnitManager.Instance()->GetAddonByName(name);
        return addon != null && addon->IsVisible ? addon : null;
    }

    // C# type → AtkValue.Type: int→Int, uint→UInt, float→Float, bool→Bool, string→String
    // Uses Marshal.AllocHGlobal so string values can be allocated and freed safely (supports string args).
    private static bool FireCallback(string name, params object[] args) {
        var addon = GetAddonByName(name);
        if (addon == null) return false;

        var atkValues = (AtkValue*)Marshal.AllocHGlobal(args.Length * sizeof(AtkValue));
        try {
            for (var i = 0; i < args.Length; i++) {
                atkValues[i] = args[i] switch {
                    int v => new AtkValue { Type = ValueType.Int, Int = v },
                    uint v => new AtkValue { Type = ValueType.UInt, UInt = v },
                    float v => new AtkValue { Type = ValueType.Float, Float = v },
                    bool v => new AtkValue { Type = ValueType.Bool, Byte = (byte)(v ? 1 : 0) },
                    string v => AllocStringValue(v),
                    _ => default,
                };
            }
            addon->FireCallback((uint)args.Length, atkValues, true);
            return true;
        } finally {
            for (var i = 0; i < args.Length; i++) {
                if (atkValues[i].Type == ValueType.String)
                    Marshal.FreeHGlobal((nint)(byte*)atkValues[i].String);
            }
            Marshal.FreeHGlobal((nint)atkValues);
        }
    }

    private static AtkValue AllocStringValue(string s) {
        var bytes = Encoding.UTF8.GetBytes(s);
        var ptr = Marshal.AllocHGlobal(bytes.Length + 1);
        Marshal.Copy(bytes, 0, ptr, bytes.Length);
        Marshal.WriteByte(ptr, bytes.Length, 0);
        return new AtkValue { Type = ValueType.String, String = (byte*)ptr };
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
