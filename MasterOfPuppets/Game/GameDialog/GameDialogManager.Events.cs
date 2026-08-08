using FFXIVClientStructs.FFXIV.Component.GUI;

namespace MasterOfPuppets;

internal static unsafe partial class GameDialogManager {
    // seasonal events
    public static bool ClickEasterMowingLeave() {
        var addon = GetAddonByName(AddonName.EasterMowingResult);
        if (addon == null) return false;
        var eventData = new AtkEvent();
        var inputData = new AtkEventData();
        int btnIndex = 1;
        addon->ReceiveEvent(AtkEventType.ButtonClick, btnIndex, &eventData, &inputData);
        return true;
    }

    public static bool ClickFallGuysEnterDialog() {
        var addon = GetAddonByName(AddonName.FGSEnterDialog);
        if (addon == null) return false;
        var eventData = new AtkEvent();
        var inputData = new AtkEventData();
        int btnIndex = 0;
        addon->ReceiveEvent(AtkEventType.ButtonClick, btnIndex, &eventData, &inputData);
        return true;
    }
}
