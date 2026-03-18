using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;

using ValueType = FFXIVClientStructs.FFXIV.Component.GUI.ValueType;

namespace MasterOfPuppets;

internal static unsafe class GameDialogManager {
    public static bool ClickYes() => FireCallback("SelectYesno", 0); // 0 = button index yes
    public static bool ClickNo() => FireCallback("SelectYesno", (uint)1); // 1 = button index no
    public static bool ClickOk() => ClickAddonButton<AddonSelectOk>("SelectOk", a => a->OkButton);
    public static bool ClickRepairAll() => ClickAddonButton<AddonRepair>("Repair", a => a->RepairAllButton);

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

