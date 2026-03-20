namespace MasterOfPuppets.Ipc;

internal partial class IpcProvider {

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

    public void ExecuteTeleportToEstate(string contentIdOrFriendName, string teleportOptionIndex) {
        BroadCast(IpcMessage.Create(IpcMessageType.TeleportToEstate, contentIdOrFriendName, teleportOptionIndex).Serialize(), includeSelf: true);
    }

    [IpcHandle(IpcMessageType.TeleportToEstate)]
    private void HandleExecuteTeleportToEstate(IpcMessage message) {
        bool parsed = int.TryParse(message.StringData[1], out int teleportOptionIndex);
        if (!parsed) return;

        EstateTeleportManager.TeleportToEstate(message.StringData[0], teleportOptionIndex);
    }
}
