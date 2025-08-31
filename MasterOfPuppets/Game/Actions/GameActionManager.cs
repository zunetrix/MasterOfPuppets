using System.Collections.Generic;

using FFXIVClientStructs.FFXIV.Client.Game;

namespace MasterOfPuppets;

public static class GameActionManager
{
    public static readonly Dictionary<string, ExecutableAction> CustomActions = new()
    {
        ["RainCheck"] = new ExecutableAction
        {
            ActionId = 30869,
            ActionName = "Rain Check",
            IconId = 64276,
            TextCommand = "",
        },
        ["UmbrellaDance"] = new ExecutableAction
        {
            ActionId = 30868,
            ActionName = "Umbrella Dance",
            IconId = 64277,
            TextCommand = "",
        }
    };

    public static unsafe void UseAction(ActionType type, uint actionId)
    {
        var actionManager = ActionManager.Instance();
        if (actionManager == null) return;

        // 0 = target self, 1 = target current
        actionManager->UseAction(type, actionId);
    }
    public static unsafe void UseAction(uint actionId)
    {
        var actionManager = ActionManager.Instance();
        if (actionManager == null) return;

        actionManager->UseAction(ActionType.Action, actionId);
    }
}
