namespace MasterOfPuppets.Ipc;

internal partial class IpcProvider {

    public void ChangeGearset(int gearsetIndex) {
        BroadCast(IpcMessage.Create(IpcMessageType.ChangeGearset, gearsetIndex).Serialize(), includeSelf: true);
    }

    [IpcHandle(IpcMessageType.ChangeGearset)]
    private void HandleChangeGearset(IpcMessage message) {
        GearsetManager.ChangeGearset(Plugin, message.DataStruct<int>());
    }

    public void RenameGearset(int gearsetIndex, string gearsetName) {
        BroadCast(IpcMessage.Create(IpcMessageType.RenameGearset, gearsetIndex, gearsetName).Serialize(), includeSelf: true);
    }

    [IpcHandle(IpcMessageType.RenameGearset)]
    private void HandleRenameGearset(IpcMessage message) {
        int gearsetIndex = message.DataStruct<int>();
        string gearsetName = message.StringData[0];
        GearsetManager.RenameGearset(gearsetIndex, gearsetName);
    }

    public struct ReorderGearsetData {
        public int GearsetIndex;
        public int NewGearsetIndex;
    }

    public void ReorderGearset(int gearsetIndex, int newGearsetIndex) {
        var payload = new ReorderGearsetData {
            GearsetIndex = gearsetIndex,
            NewGearsetIndex = newGearsetIndex
        };

        BroadCast(IpcMessage.Create(IpcMessageType.ReorderGearset, payload).Serialize(), includeSelf: true);
    }

    [IpcHandle(IpcMessageType.ReorderGearset)]
    private void HandleReorderGearset(IpcMessage message) {
        var data = message.DataStruct<ReorderGearsetData>();

        GearsetManager.ReorderGearset(data.GearsetIndex, data.NewGearsetIndex);
    }

    public struct MoveGearsetData {
        public int GearsetIndex;
        public bool IgnoreWeapons;
        public bool IgnoreAccessories;
        public bool IgnoreSoulCrystal;
    }

    public void MoveGearsetToArmoury(int gearsetIndex, bool ignoreWeapons = false, bool ignoreAccessories = false, bool ignoreSoulCrystal = false) {
        var payload = new MoveGearsetData {
            GearsetIndex = gearsetIndex,
            IgnoreWeapons = ignoreWeapons,
            IgnoreAccessories = ignoreAccessories,
            IgnoreSoulCrystal = ignoreSoulCrystal,
        };

        BroadCast(IpcMessage.Create(IpcMessageType.MoveGearsetToArmoury, payload).Serialize(), includeSelf: true);
    }

    [IpcHandle(IpcMessageType.MoveGearsetToArmoury)]
    private void HandleMoveGearsetToArmoury(IpcMessage message) {
        var data = message.DataStruct<MoveGearsetData>();
        GearsetManager.MoveGearsetItemsToArmoury(Plugin, data.GearsetIndex, data.IgnoreWeapons, data.IgnoreAccessories, data.IgnoreSoulCrystal);
    }

    public void MoveGearsetToInventory(int gearsetIndex, bool ignoreWeapons = false, bool ignoreAccessories = false, bool ignoreSoulCrystal = false) {
        var payload = new MoveGearsetData {
            GearsetIndex = gearsetIndex,
            IgnoreWeapons = ignoreWeapons,
            IgnoreAccessories = ignoreAccessories,
            IgnoreSoulCrystal = ignoreSoulCrystal,
        };

        BroadCast(IpcMessage.Create(IpcMessageType.MoveGearsetToInventory, payload).Serialize(), includeSelf: true);
    }

    [IpcHandle(IpcMessageType.MoveGearsetToInventory)]
    private void HandleMoveGearsetToInventory(IpcMessage message) {
        var data = message.DataStruct<MoveGearsetData>();
        GearsetManager.MoveGearsetItemsToInventory(Plugin, data.GearsetIndex, data.IgnoreWeapons, data.IgnoreAccessories, data.IgnoreSoulCrystal);
    }
}
