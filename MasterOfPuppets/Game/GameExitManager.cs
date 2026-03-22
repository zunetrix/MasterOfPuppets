
using System.Diagnostics;

// using FFXIVClientStructs.FFXIV.Client.System.Input;
// using FFXIVClientStructs.FFXIV.Client.UI;

using MasterOfPuppets.Util;

namespace MasterOfPuppets;

// https://github.com/Caraxi/SimpleTweaksPlugin/blob/main/Tweaks/ExitGame.cs
public static class GameExitManager {
    internal static void Logout() {
        var ok = false;
        Chat.SendMessage("/logout");

        Coroutine.StartRunOnFramework(
            runFunction: () => { },
            stopWhen: () => ok = GameDialogManager.IsAddonVisible(GameDialogManager.AddonName.SelectYesno),
            callback: () => {
                if (!ok) { DalamudApi.PluginLog.Warning("[Logout] timeout: SelectYesno not visible"); return; }
                GameDialogManager.ClickYes();
            },
            timeoutMs: 5000);

        DalamudApi.Framework.RunOnTick(() => {
            GameDialogManager.ClickYes();
        });
    }

    internal static void Shutdown() {
        DalamudApi.Framework.RunOnFrameworkThread(ForceCloseGame);

        var ok = false;
        if (DalamudApi.PlayerState.IsLoaded && DalamudApi.ClientState.IsLoggedIn) {
            Chat.SendMessage("/shutdown");
        } else {
            // not loggedin cant use chat command
            ForceCloseGame();
        }

        Coroutine.StartRunOnFramework(
        runFunction: () => { },
        stopWhen: () => ok = GameDialogManager.IsAddonVisible(GameDialogManager.AddonName.SelectYesno),
        callback: () => {
            if (!ok) { DalamudApi.PluginLog.Warning("[Shutdown] timeout: SelectYesno not visible"); return; }
            GameDialogManager.ClickYes();
        },
        timeoutMs: 5000);
    }

    //Alt + F4 close game safely
    private static void ForceCloseGame() {
        // if (UIInputData.Instance()->IsKeyDown(SeVirtualKey.MENU) && UIInputData.Instance()->IsKeyPressed(SeVirtualKey.F4))
        WindowsApi.SendMessage(Process.GetCurrentProcess().MainWindowHandle, WindowsApi.WM_CLOSE, 0, 0);
    }
}
