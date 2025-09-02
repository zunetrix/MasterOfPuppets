using System;
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
        try
        {
            // animation locked
            // if (ActionManager.Instance()->AnimationLock > 0) return;

            // 0 = target self, 1 = target current
            DalamudApi.Framework.RunOnFrameworkThread(delegate
            {
                ActionManager.Instance()->UseAction(type, actionId);
            });
        }
        catch (Exception e)
        {
            DalamudApi.PluginLog.Error(e, $"Error while using action {actionId}");
        }
    }

    public static unsafe void UseActionById(uint actionId)
    {
        DalamudApi.Framework.RunOnFrameworkThread(delegate
        {
            ActionManager.Instance()->UseAction(ActionType.Action, actionId);
        });
    }

    public static unsafe void UseActionByName(string actionName)
    {
        var action = ActionHelper.GetExecutableActionByName(actionName);
        if (action == null)
        {
            DalamudApi.PluginLog.Debug("Invalid action name");
            return;
        }

        DalamudApi.Framework.RunOnFrameworkThread(delegate
        {
            ActionManager.Instance()->UseAction(ActionType.Action, action.ActionId);
        });

    }

    public static unsafe void UseItemById(uint actionId)
    {
        DalamudApi.Framework.RunOnFrameworkThread(delegate
        {
            ActionManager.Instance()->UseAction(ActionType.Item, actionId);
        });
    }

    public static unsafe void UseItemByName(string itemName)
    {
        var item = ItemHelper.GetExecutableActionByName(itemName);
        if (item == null)
        {
            DalamudApi.PluginLog.Debug("Invalid item name");
            return;
        }

        DalamudApi.Framework.RunOnFrameworkThread(delegate
        {
            ActionManager.Instance()->UseAction(ActionType.Item, item.ActionId);
        });
    }
}
