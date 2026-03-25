using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

using Lumina.Text.ReadOnly;

using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;

using ValueType = FFXIVClientStructs.FFXIV.Component.GUI.ValueType;

namespace MasterOfPuppets;

internal static unsafe partial class GameDialogManager {

    public static class AddonName {
        //common
        public const string SelectYesno = "SelectYesno";
        public const string SelectOk = "SelectOk";
        public const string Repair = "Repair";
        public const string SelectString = "SelectString";
        //teleport
        public const string HousingMenu = "HousingMenu";
        public const string HousingSelectBlock = "HousingSelectBlock";
        public const string WorldTravelSelect = "WorldTravelSelect";
        public const string TeleportHousingFriend = "TeleportHousingFriend";
        public const string TeleportTown = "TeleportTown";
        //seasonal events
        public const string EasterMowingResult = "EasterMowingResult";
        public const string FGSEnterDialog = "FGSEnterDialog";
        public const string ContentsFinderConfirm = "ContentsFinderConfirm";
    }

    public static bool IsAddonVisible(string name) {
        var addon = RaptureAtkUnitManager.Instance()->GetAddonByName(name);
        return addon != null && addon->IsVisible;
    }

    public static bool IsAddonReady(string name) {
        var addon = RaptureAtkUnitManager.Instance()->GetAddonByName(name);
        return addon != null && addon->IsVisible && addon->IsReady && addon->IsFullyLoaded(); //&& addon->UldManager.LoadedState != AtkLoadState.Loaded;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsAddonReady(AtkUnitBase* Addon)
    => Addon->IsVisible && Addon->UldManager.LoadedState == AtkLoadState.Loaded && Addon->IsFullyLoaded();

    public static bool IsReady(this ref AtkUnitBase Addon)
        => Addon.IsVisible && Addon.UldManager.LoadedState == AtkLoadState.Loaded && Addon.IsFullyLoaded();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsAddonReady(AtkComponentNode* Addon)
        => Addon->AtkResNode.IsVisible() && Addon->Component->UldManager.LoadedState == AtkLoadState.Loaded;


    /// <summary>Finds and clicks the first entry whose text contains <paramref name="searchText"/> in <paramref name="addonName"/>.</summary>
    public static bool SelectStringByText(string searchText, string addonName = AddonName.SelectString) {
        var addon = (AddonSelectString*)GetAddonByName(addonName);
        if (addon == null) return false;
        var count = addon->PopupMenu.PopupMenu.EntryCount;
        if (count == 0) return false;
        for (var i = 0; i < count; i++) {
            var ptr = addon->PopupMenu.PopupMenu.EntryNames[i].Value;
            if (ptr == null) continue;
            var text = new ReadOnlySeStringSpan(ptr).ExtractText();
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

    // public static bool ClickYes() => FireCallback(AddonName.SelectYesno, 0);
    public static bool ClickYes() => ClickAddonButton<AddonSelectYesno>(AddonName.SelectOk, a => a->YesButton);
    // public static bool ClickNo() => FireCallback(AddonName.SelectYesno, (uint)1);
    public static bool ClickNo() => ClickAddonButton<AddonSelectYesno>(AddonName.SelectOk, a => a->NoButton);
    public static bool ClickOk() => ClickAddonButton<AddonSelectOk>(AddonName.SelectOk, a => a->OkButton);
    public static bool ClickRepairAll() => ClickAddonButton<AddonRepair>(AddonName.Repair, a => a->RepairAllButton);
    public static bool ClickContentsFinderConfirm() => ClickAddonButton<AddonContentsFinderConfirm>(AddonName.ContentsFinderConfirm, a => a->CommenceButton);
    // ContentsFinderConfirm // addon->ReceiveEvent(AtkEventType.ButtonClick, 8, &eventData, &inputData);

    //  Private
    private static AtkUnitBase* GetAddonByName(string name) {
        var addon = RaptureAtkUnitManager.Instance()->GetAddonByName(name);
        return addon != null && addon->IsVisible ? addon : null;
    }

    /// <summary>
    /// Attempts to get first instance of addon by name.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="Addon"></param>
    /// <param name="AddonPtr"></param>
    /// <returns></returns>
    public static bool TryGetAddonByName<T>(string name, out T* AddonPtr) where T : unmanaged {
        var addon = RaptureAtkUnitManager.Instance()->GetAddonByName(name);
        if (addon != null && addon->IsVisible) {
            AddonPtr = (T*)addon;
            return true;
        } else {
            AddonPtr = null;
            return false;
        }
    }

    private delegate AtkComponentButton* ButtonSelector<T>(T* addon) where T : unmanaged;
    private static bool ClickAddonButton<T>(string name, ButtonSelector<T> selector)
            where T : unmanaged {
        var addon = GetAddonByName(name);
        if (addon == null) return false;
        // if (!IsAddonReady(name)) return false;
        var btn = selector((T*)addon);
        if (btn == null || !btn->IsEnabled || !btn->AtkResNode->IsVisible()) return false;
        var btnRes = btn->AtkComponentBase.OwnerNode->AtkResNode;
        var evt = (AtkEvent*)btnRes.AtkEventManager.Event;
        addon->ReceiveEvent(evt->State.EventType, (int)evt->Param, btnRes.AtkEventManager.Event);
        return true;
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
}
