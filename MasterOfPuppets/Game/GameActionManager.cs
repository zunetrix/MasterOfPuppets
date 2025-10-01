using System;

using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;

namespace MasterOfPuppets;

public static class GameActionManager {
    public static unsafe void UseAction(ActionType type, uint actionId) {
        try {
            // animation locked
            // if (ActionManager.Instance()->AnimationLock > 0) return;
            // ActionManager.Instance()->QueuedActionId

            // 0 = target self, 1 = target current
            DalamudApi.Framework.RunOnFrameworkThread(delegate {
                ActionManager.Instance()->UseAction(type, actionId);
            });
        } catch (Exception e) {
            DalamudApi.PluginLog.Error(e, $"Error while using action {actionId}");
        }
    }

    public static unsafe void UseGeneralActionById(uint actionId) {
        DalamudApi.Framework.RunOnFrameworkThread(delegate {
            ActionManager.Instance()->UseAction(ActionType.GeneralAction, actionId);
        });
    }

    public static unsafe void UseGeneralActionByName(string actionName) {
        var action = GeneralActionHelper.GetExecutableActionByName(actionName);
        if (action == null) {
            DalamudApi.PluginLog.Debug("Invalid general action name");
            return;
        }

        DalamudApi.Framework.RunOnFrameworkThread(delegate {
            ActionManager.Instance()->UseAction(ActionType.GeneralAction, action.ActionId);
        });

        // DalamudApi.PluginLog.Debug($"[USE GENERL ACTION] {action.ActionId}");
    }

    public static unsafe void UseActionById(uint actionId) {
        DalamudApi.Framework.RunOnFrameworkThread(delegate {
            ActionManager.Instance()->UseAction(ActionType.Action, actionId);
        });
    }

    public static unsafe void UseActionByName(string actionName) {
        var action = ActionHelper.GetExecutableActionByName(actionName);
        if (action == null) {
            DalamudApi.PluginLog.Debug("Invalid action name");
            return;
        }

        DalamudApi.Framework.RunOnFrameworkThread(delegate {
            ActionManager.Instance()->UseAction(ActionType.Action, action.ActionId);
        });

        // DalamudApi.PluginLog.Debug($"[USE ACTION] {action.ActionId}");
    }

    public static unsafe void UseItemById(uint actionId) {
        // bool isActionOffCooldown = ActionManager.Instance()->IsActionOffCooldown(ActionType.Item, actionId);
        // var adjustedActionId = ActionManager.Instance()->GetAdjustedActionId(actionId);
        // AgentInventoryContext.Instance()->UseItem(actionId);

        // DalamudApi.Framework.RunOnFrameworkThread(delegate
        // {
        //     // ActionManager.Instance()->UseAction(ActionType.Item, actionId, 0xE0000000, 65535);
        //     ActionManager.Instance()->UseAction(ActionType.Item, actionId, extraParam: 65535);
        // });

        DalamudApi.Framework.RunOnTick(delegate {
            ActionManager.Instance()->UseAction(ActionType.Item, actionId, extraParam: 65535);
        }, delayTicks: 3);

        // DalamudApi.PluginLog.Debug($"[USE ITEM] {actionId}");
    }

    public static unsafe void UseItemByName(string itemName) {
        var item = ItemHelper.GetExecutableActionByName(itemName);
        if (item == null) {
            DalamudApi.PluginLog.Debug("Invalid item name");
            return;
        }

        // DalamudApi.Framework.RunOnFrameworkThread(delegate
        // {
        //     ActionManager.Instance()->UseAction(ActionType.Item, item.ActionId, extraParam: 65535);
        // });

        DalamudApi.Framework.RunOnTick(delegate {
            ActionManager.Instance()->UseAction(ActionType.Item, item.ActionId, extraParam: 65535);
        }, delayTicks: 3);

        // DalamudApi.PluginLog.Debug($"[USE ITEM NAME] {itemName}");
    }

    public static unsafe void UseInventoryItemById(uint itemId) {
        // DalamudApi.Framework.RunOnFrameworkThread(delegate
        // {
        //     AgentInventoryContext.Instance()->UseItem(itemId);
        // });

        DalamudApi.Framework.RunOnTick(delegate {
            AgentInventoryContext.Instance()->UseItem(itemId);
        }, delayTicks: 3);

        // DalamudApi.PluginLog.Debug($"[USE INVENTORY ITEM] {itemId}");
    }

    public static unsafe void UseInventoryItemByName(string itemName) {
        var item = ItemHelper.GetExecutableActionByName(itemName);
        if (item == null) {
            DalamudApi.PluginLog.Debug("Invalid item name");
            return;
        }

        // DalamudApi.Framework.RunOnFrameworkThread(delegate
        // {
        //     AgentInventoryContext.Instance()->UseItem(item.ActionId);
        // });
        DalamudApi.Framework.RunOnTick(delegate {
            AgentInventoryContext.Instance()->UseItem(item.ActionId);
        }, delayTicks: 3);

        // DalamudApi.PluginLog.Debug($"[USE INVENTORY ITEM NAME] {itemName}");
    }
}
