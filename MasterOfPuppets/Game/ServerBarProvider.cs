using System;

using Dalamud.Game.Gui.Dtr;
// using Dalamud.Game.Text.SeStringHandling;
// using Dalamud.Game.Text.SeStringHandling.Payloads;

namespace MasterOfPuppets;

internal sealed class ServerBarProvider : IDisposable {
    private Plugin Plugin { get; }

    public readonly IDtrBarEntry _keyBroadcastDtrBarEntry = DalamudApi.DtrBar.Get("mop-keybroadcast");

    public ServerBarProvider(Plugin plugin) {
        Plugin = plugin;

        _keyBroadcastDtrBarEntry.OnClick += OnKeyBroadcastBarClick;
        _keyBroadcastDtrBarEntry.Shown = Plugin.Config.ShowKeyBroadcastBarInfo;
        _keyBroadcastDtrBarEntry.Text = Plugin.KeyboardBroadcastManager.IsCapturing ? "KB: On" : "KB: Off";
        _keyBroadcastDtrBarEntry.Tooltip = "Mop Key Broadcast: left click to toggle, right click to open window";

        // var icon = new IconPayload(Plugin.KeyboardBroadcastManager.IsCapturing
        //                     ? BitmapFontIcon.ControllerButton1
        //                     : BitmapFontIcon.ControllerButton0);

        // var payloadText = new TextPayload(Plugin.KeyboardBroadcastManager.IsCapturing ? "On" : "Off");
        // _keyBroadcastDtrBarEntry.Text = new SeString(new TextPayload("KB:"), icon, payloadText);
    }

    private void OnKeyBroadcastBarClick(DtrInteractionEvent ev) {
        if (ev.ClickType == MouseClickType.Left) {
            Plugin.IpcProvider.ToggleKeyboardBroadcast();

        } else if (ev.ClickType == MouseClickType.Right) {
            Plugin.Ui.MainWindow.Toggle();
        }
        // if (ev.ModifierKeys.HasFlag(ClickModifierKeys.Ctrl))
    }

    public void Dispose() {
        _keyBroadcastDtrBarEntry.OnClick -= OnKeyBroadcastBarClick;
        _keyBroadcastDtrBarEntry.Remove();
    }

    public void Update() {
        _keyBroadcastDtrBarEntry.Shown = Plugin.Config.ShowKeyBroadcastBarInfo;
        _keyBroadcastDtrBarEntry.Text = Plugin.KeyboardBroadcastManager.IsCapturing ? "KB: On" : "KB: Off";
    }
}
