using FFXIVClientStructs.FFXIV.Client.Game;

using Lumina.Excel.Sheets;

using MasterOfPuppets.Util;

using ObjectKind = Dalamud.Game.ClientState.Objects.Enums.ObjectKind;

namespace MasterOfPuppets;

public static class GameHousingManager {
    private static string TextMoveToFrontDoor =>
        DalamudApi.DataManager.GetExcelSheet<Addon>(DalamudApi.ClientState.ClientLanguage)
            ?.GetRow(6210).Text.ExtractText()
        ?? "Move to Front Door";

    private const uint HouseEntranceBaseId = 2002737;
    private const uint HouseExitBaseId = 2002738;
    private const uint ApartmentEntranceBaseId = 2007402;

    public static void InteractWithNearestHouseEntrance() => InteractWithNearestObject(HouseEntranceBaseId);

    public unsafe static void InteractWithNearestHouseExit() {
        var housingManager = HousingManager.Instance();
        if (!housingManager->IsInside())
            return;

        InteractWithNearestObject(HouseExitBaseId);
    }

    public static void InteractWithNearestApartmentEntrance() => InteractWithNearestObject(ApartmentEntranceBaseId);

    public unsafe static void MoveToFrontDoor() {
        var housingManager = HousingManager.Instance();
        if (!housingManager->IsInside()) return;

        housingManager->MoveToEntry();
    }

    private static void InteractWithNearestObject(uint baseId) =>
        GameTargetManager.TargetThenInteract(
            setTarget: () => GameTargetManager.TargetNearestObjectInternal(actor =>
                actor.BaseId == baseId
                && actor.ObjectKind == ObjectKind.EventObj
                && actor.IsTargetable
                && actor.IsValid()),
            afterInteract: () => GameDialogManager.ClickYes());
}
