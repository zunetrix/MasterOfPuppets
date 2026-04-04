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
}
