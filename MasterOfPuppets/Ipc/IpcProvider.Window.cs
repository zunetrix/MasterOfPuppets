using System.Diagnostics;

using MasterOfPuppets.Util;

namespace MasterOfPuppets.Ipc;

internal partial class IpcProvider {

    public void SetWindowTitle(bool enabled) {
        BroadCast(IpcMessage.Create(IpcMessageType.SetWindowTitle, enabled).Serialize(), includeSelf: true);
    }

    [IpcHandle(IpcMessageType.SetWindowTitle)]
    private void HandleSetWindowTitle(IpcMessage message) {
        var enabled = message.DataStruct<bool>();
        DalamudApi.Framework.RunOnTick(() => {
            var title = enabled
                ? $"{DalamudApi.PlayerState.CharacterName}@{DalamudApi.PlayerState.HomeWorld.Value.Name}"
                : "FINAL FANTASY XIV";
            WindowsApi.SetWindowText(Process.GetCurrentProcess().MainWindowHandle, title);
        });
    }
}
