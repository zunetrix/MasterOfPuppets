using System;

namespace MasterOfPuppets.Ipc;

internal partial class IpcProvider {

    public void ExecuteChangeGearset(int gearsetIndex) {
        BroadCast(IpcMessage.Create(IpcMessageType.ExecuteChangeGearset, gearsetIndex).Serialize(), includeSelf: true);
    }

    [IpcHandle(IpcMessageType.ExecuteChangeGearset)]
    private void HandleExecuteChangeGearset(IpcMessage message) {
        GearsetManager.ChangeGearset(Plugin, message.DataStruct<int>());
    }

    public void ExecuteAbandonDuty() {
        BroadCast(IpcMessage.Create(IpcMessageType.ExecuteAbandonDuty).Serialize(), includeSelf: true);
    }

    [IpcHandle(IpcMessageType.ExecuteAbandonDuty)]
    private void HandleExecuteAbandonDuty(IpcMessage message) {
        GameFunctions.AbandonDuty();
    }

    public void SetGameSettingsObjectQuantity(SettingsDisplayObjectLimitType displayObjectLimitType) {
        BroadCast(IpcMessage.Create(IpcMessageType.SetGameSettingsObjectQuantity, displayObjectLimitType).Serialize(), includeSelf: true);
    }

    [IpcHandle(IpcMessageType.SetGameSettingsObjectQuantity)]
    private void HandleSetGameSettingsObjectQuantity(IpcMessage message) {
        var displayObjectLimitType = message.DataStruct<SettingsDisplayObjectLimitType>();
        if (!Enum.IsDefined(typeof(SettingsDisplayObjectLimitType), displayObjectLimitType)) {
            DalamudApi.PluginLog.Warning($"Invalid object quantity value: {displayObjectLimitType}");
            return;
        }
        GameSettingsManager.SetDisplayObjectLimit(displayObjectLimitType);
    }

    public void ExecuteEnterHouse() {
        BroadCast(IpcMessage.Create(IpcMessageType.EnterHouse).Serialize(), includeSelf: true);
    }

    [IpcHandle(IpcMessageType.EnterHouse)]
    private void HandleExecuteEnterHouse(IpcMessage message) {
        GameHousingManager.InteractWithNearestHouseEntrance();
    }

    public void ExecuteExitHouse() {
        BroadCast(IpcMessage.Create(IpcMessageType.ExitHouse).Serialize(), includeSelf: true);
    }

    [IpcHandle(IpcMessageType.ExitHouse)]
    private void HandleExecuteExitHouse(IpcMessage message) {
        GameHousingManager.InteractWithNearestHouseExit();
    }

    public void ExecuteMoveToFrontDoor() {
        BroadCast(IpcMessage.Create(IpcMessageType.MoveToFrontDoor).Serialize(), includeSelf: true);
    }

    [IpcHandle(IpcMessageType.MoveToFrontDoor)]
    private void HandleExecuteMoveToFrontDoor(IpcMessage message) {
        GameHousingManager.MoveToFrontDoor();
    }

    public void ExecuteTeleportToWard(int ward) {
        BroadCast(IpcMessage.Create(IpcMessageType.TeleportToWard, ward).Serialize(), includeSelf: true);
    }

    [IpcHandle(IpcMessageType.TeleportToWard)]
    private void HandleExecuteTeleportToWard(IpcMessage message) {
        ResidentialTeleportManager.TeleportToWard(message.DataStruct<int>());
    }

    public void ExecuteTravelToWorld(string world) {
        BroadCast(IpcMessage.Create(IpcMessageType.TravelToWorld, world).Serialize(), includeSelf: true);
    }

    [IpcHandle(IpcMessageType.TravelToWorld)]
    private void HandleExecuteTravelToWorld(IpcMessage message) {
        WorldTravelManager.TravelToWorld(message.StringData[0]);
    }
}
