using System;

using FFXIVClientStructs.FFXIV.Client.Game;

namespace MasterOfPuppets;

public static class GameActionManager
{
    public static unsafe void UseAction(ActionType type, uint actionId)
    {
        try
        {
            // animation locked
            // if (ActionManager.Instance()->AnimationLock > 0) return;
            // ActionManager.Instance()->QueuedActionId

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
        // ActionManager.Instance()->UseAction(ActionType.Item, actionId);

        // ActionManager.Instance()->UseAction(ActionType.Item, actionId, DalamudApi.ClientState.LocalContentId);
        // ActionManager.Instance()->UseAction(ActionType.Item, actionId, DalamudApi.Client.LocalPlayer.TargetObjectId);
        // bool isActionOffCooldown = ActionManager.Instance()->IsActionOffCooldown(ActionType.Item, actionId);

        // DalamudApi.Framework.RunOnTick(() => ActionManager.Instance()->UseAction(ActionType.Item, actionId));
        // ActionManager.Instance()->UseAction(ActionType.Item, actionId);

        var adjustedActionId = ActionManager.Instance()->GetAdjustedActionId(actionId);

        DalamudApi.Framework.RunOnFrameworkThread(delegate
        {
            ActionManager.Instance()->UseAction(ActionType.Item, adjustedActionId);
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

        // if (ActionManager.Instance()->GetActionStatus(ActionType.Item, item.ActionId) == 0) return;

        DalamudApi.Framework.RunOnFrameworkThread(delegate
        {
            ActionManager.Instance()->UseAction(ActionType.Item, item.ActionId);
        });
    }
}
