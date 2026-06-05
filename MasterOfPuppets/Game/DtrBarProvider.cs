using System;

using Dalamud.Game.Gui.Dtr;

namespace MasterOfPuppets;

internal sealed class DtrBarProvider : IDisposable {
    private Plugin Plugin { get; }

    public readonly IDtrBarEntry _keyBroadcastDtrBarEntry = DalamudApi.DtrBar.Get("mop-keybroadcast");

    public DtrBarProvider(Plugin plugin) {
        Plugin = plugin;

        _keyBroadcastDtrBarEntry.OnClick = ev => {
            if (ev.ClickType == MouseClickType.Left) {
                Plugin.IpcProvider.ToggleKeyboardBroadcast();

            } else if (ev.ClickType == MouseClickType.Right) {
                Plugin.Ui.MainWindow.Toggle();
            }
            // if (ev.ModifierKeys.HasFlag(ClickModifierKeys.Ctrl))
        };

        _keyBroadcastDtrBarEntry.Shown = Plugin.Config.ShowKeyBroadcastBarInfo;
        _keyBroadcastDtrBarEntry.Text = Plugin.KeyboardBroadcastManager.IsCapturing ? "KB: On" : "KB: Off";
        _keyBroadcastDtrBarEntry.Tooltip = "Mop Key Broadcast: left click to toggle, right click to open window";
    }

    public void Dispose() {
        _keyBroadcastDtrBarEntry.Remove();
    }

    public void Update() {
        _keyBroadcastDtrBarEntry.Shown = Plugin.Config.ShowKeyBroadcastBarInfo;
        _keyBroadcastDtrBarEntry.Text = Plugin.KeyboardBroadcastManager.IsCapturing ? "KB: On" : "KB: Off";
    }
}
