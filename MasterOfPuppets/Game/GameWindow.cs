using Dalamud.Bindings.ImGui;
using Dalamud.Utility;

using FFXIVClientStructs.FFXIV.Client.System.Framework;

namespace MasterOfPuppets;

public static class GameWindow {
    public static unsafe void Draw() {
        var gameWindow = Framework.Instance()->GameWindow;
        if (gameWindow == null) return;

        var i = 0;
        foreach (var arg in gameWindow->ArgumentsSpan) {
            ImGui.Text($"[{i++}] {arg.ExtractText()}");
        }
    }
}
