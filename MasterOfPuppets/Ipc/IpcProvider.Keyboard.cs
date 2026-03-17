using System;
using System.Linq;

namespace MasterOfPuppets.Ipc;

internal partial class IpcProvider {

    public void EnableKeyboardBroadcast() => SetKeyboardBroadcast(true);
    public void DisableKeyboardBroadcast() => SetKeyboardBroadcast(false);

    /// <summary>
    /// Toggles keyboard capture on this client and notifies peers to start/stop receiving.
    /// When capturing: master polls keys and broadcasts each transition via <see cref="KeyboardInput"/>.
    /// When peers receive <see cref="IpcMessageType.KeyboardBroadcastToggle"/>, they enable/disable
    /// forwarding keys to their own FFXIV window.
    /// </summary>
    public void ToggleKeyboardBroadcast() => SetKeyboardBroadcast(!Plugin.KeyboardBroadcastManager.IsCapturing);

    private void SetKeyboardBroadcast(bool enable) {
        if (enable) {
            Plugin.KeyboardBroadcastManager.StartCapture();
            DalamudApi.ChatGui.Print("", "MOP: Key Broadcast ON", Style.Colors.SeGreen);
        } else {
            Plugin.KeyboardBroadcastManager.StopCapture();
            DalamudApi.ChatGui.Print("", "MOP: Key Broadcast OFF", Style.Colors.SeRed);
        }

        var payload = new byte[] { enable ? (byte)1 : (byte)0 };
        BroadCast(IpcMessage.Create(IpcMessageType.KeyboardBroadcastToggle, payload).Serialize());
    }

    /// <summary>Sends a single key-down or key-up event to all peers.</summary>
    public void BroadcastKeyInput(uint msg, uint vkCode, uint scanCode, uint flags) {
        var bytes = new byte[16];
        BitConverter.TryWriteBytes(bytes.AsSpan(0), msg);
        BitConverter.TryWriteBytes(bytes.AsSpan(4), vkCode);
        BitConverter.TryWriteBytes(bytes.AsSpan(8), scanCode);
        BitConverter.TryWriteBytes(bytes.AsSpan(12), flags);
        BroadCast(IpcMessage.Create(IpcMessageType.KeyboardInput, bytes).Serialize());
    }

    [IpcHandle(IpcMessageType.KeyboardBroadcastToggle)]
    private void HandleKeyboardBroadcastToggle(IpcMessage message) {
        bool enable = message.Data.Length > 0 && message.Data[0] != 0;

        // If this client is currently broadcasting (master), never switch to receiving mode.
        if (Plugin.KeyboardBroadcastManager.IsCapturing) return;

        if (enable) {
            if (!Plugin.Config.KeyboardBroadcastEnabled) {
                DalamudApi.PluginLog.Information("[KeyboardBroadcast] ignored (disabled in settings)");
                return;
            }
            var playerCid = DalamudApi.PlayerState.ContentId;
            var character = Plugin.Config.Characters.FirstOrDefault(c => c.Cid == playerCid);
            if (character != null && !character.KeyboardBroadcastEnabled) {
                DalamudApi.PluginLog.Information("[KeyboardBroadcast] ignored (disabled for current character)");
                return;
            }
        }

        Plugin.KeyboardBroadcastManager.IsReceiving = enable;
        DalamudApi.PluginLog.Information($"[KeyboardBroadcast] {(enable ? "enabled" : "disabled")} (peer request)");
    }

    [IpcHandle(IpcMessageType.KeyboardInput)]
    private void HandleKeyboardInput(IpcMessage message) {
        // Master should never receive its own broadcasts (defence-in-depth).
        if (Plugin.KeyboardBroadcastManager.IsCapturing) return;
        if (message.Data.Length < 16) return;
        uint msg_ = BitConverter.ToUInt32(message.Data, 0);
        uint vkCode = BitConverter.ToUInt32(message.Data, 4);
        uint scanCode = BitConverter.ToUInt32(message.Data, 8);
        uint flags = BitConverter.ToUInt32(message.Data, 12);
        Plugin.KeyboardBroadcastManager.ForwardKey(msg_, vkCode, scanCode, flags);
    }
}
