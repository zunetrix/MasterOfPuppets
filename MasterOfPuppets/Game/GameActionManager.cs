using System;
using System.Numerics;
using System.Runtime.InteropServices;

using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;

namespace MasterOfPuppets;

public static class GameActionManager {
    public const uint PetActionPlaceId = 3;

    // Pet Place bypasses UseActionLocation. The native ground-command dispatcher
    // takes a world-space position and command 1800, with pet action 3 as param1.
    // Native call/signature sourced from BossMod's ActionManagerEx:
    // https://github.com/awgil/ffxiv_bossmod/blob/master/BossMod/Framework/ActionManagerEx.cs
    // Baseline: commit 162fde51b5e56ca133cbc13502ae03548a23f461; look for
    // ExecuteCommandGTDelegate, executeCommandGTAddress, and ExecuteAction's PetAction case.
    // After a game patch, check those upstream definitions for signature/ABI changes.
    // Our trailing "41 8B C8 8B F7" disambiguates two matches of BossMod's shorter
    // signature in the executable validated on 2026-08-30. Revalidate uniqueness
    // and command 1800's position/argument setup before replacing it; do not copy blindly.
    private const string PetPlaceCommandSignature = "E8 ?? ?? ?? ?? EB 3D 8B 93 ?? ?? ?? ?? 41 8B C8 8B F7";
    private const uint PetPlaceCommandId = 1800;
    private const int PetPlaceIntervalMs = 100;

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private unsafe delegate void ExecuteGroundCommandDelegate(
        uint commandId, Vector3* position, uint param1, uint param2, uint param3, uint param4);

    private static ExecuteGroundCommandDelegate? executePetPlaceCommand;
    private static bool petPlaceSignatureAttempted;
    private static bool petPlacementDisposed;
    private static long nextPetPlaceTick;

    /// <summary>
    /// Submits a pet Place command at an already-resolved world-space location.
    /// Does not change the selected target or open the ground-target cursor.
    /// Submission does not confirm server acceptance or the pet's arrival.
    /// </summary>
    public static void PlacePet(
        Vector3 location,
        Dalamud.Game.ClientState.Objects.Types.IGameObject? targetActor = null,
        bool isSelf = false) {
        // Anchor resolution and rotation belong to the caller; the native command
        // only needs coordinates, including when there is no target actor.
        _ = DalamudApi.Framework.RunOnFrameworkThread(() => SubmitPetPlace(location));
    }

    private static unsafe void SubmitPetPlace(Vector3 location) {
        try {
            if (petPlacementDisposed)
                return;

            if (!float.IsFinite(location.X) || !float.IsFinite(location.Y) || !float.IsFinite(location.Z)) {
                DalamudApi.PluginLog.Warning("[PlacePet] Rejected non-finite coordinates");
                return;
            }

            var player = DalamudApi.ObjectTable.LocalPlayer;
            var pet = DalamudApi.BuddyList.PetBuddy;
            if (player == null || pet == null || pet.Address == IntPtr.Zero || pet.GameObject == null) {
                DalamudApi.PluginLog.Warning("[PlacePet] Local player or summoned pet was unavailable");
                return;
            }

            var actionManager = ActionManager.Instance();
            if (actionManager == null) {
                DalamudApi.PluginLog.Warning("[PlacePet] ActionManager was unavailable");
                return;
            }

            var status = actionManager->GetActionStatus(ActionType.PetAction, PetActionPlaceId);
            if (status != 0) {
                DalamudApi.PluginLog.Warning($"[PlacePet] Pet Place is unavailable: actionStatus={status}");
                return;
            }

            var now = Environment.TickCount64;
            if (now < nextPetPlaceTick) {
                DalamudApi.PluginLog.Warning("[PlacePet] Request skipped: minimum interval is 100 ms");
                return;
            }

            if (!EnsurePetPlaceCommand())
                return;

            executePetPlaceCommand!(PetPlaceCommandId, &location, PetActionPlaceId, 0, 0, 0);
            nextPetPlaceTick = now + PetPlaceIntervalMs;
            DalamudApi.PluginLog.Information(
                $"[PlacePet] ground-command submitted command={PetPlaceCommandId} action={PetActionPlaceId} " +
                $"location=({location.X:F3}, {location.Y:F3}, {location.Z:F3}); arrival not yet verified");
        } catch (Exception e) {
            DalamudApi.PluginLog.Error(e, $"[PlacePet] Failed to submit pet placement at {location}");
        }
    }

    private static bool EnsurePetPlaceCommand() {
        if (executePetPlaceCommand != null)
            return true;
        if (petPlaceSignatureAttempted)
            return false;

        petPlaceSignatureAttempted = true;
        if (!DalamudApi.SigScanner.TryScanText(PetPlaceCommandSignature, out var address) || address == IntPtr.Zero) {
            DalamudApi.PluginLog.Error("[PlacePet] Ground-command signature was unresolved; placement disabled until plugin reload");
            return false;
        }

        executePetPlaceCommand = Marshal.GetDelegateForFunctionPointer<ExecuteGroundCommandDelegate>(address);
        DalamudApi.PluginLog.Information($"[PlacePet] Ground-command dispatcher resolved at 0x{address:X}");
        return true;
    }

    public static void Dispose() {
        petPlacementDisposed = true;
        executePetPlaceCommand = null;
        nextPetPlaceTick = 0;
    }

    public static unsafe void UseAction(ActionType type, uint actionId) {
        UseAction(type, actionId, 0xE0000000);
    }

    public static unsafe void UseAction(ActionType type, uint actionId, ulong targetId) {
        try {
            // animation locked
            // if (ActionManager.Instance()->AnimationLock > 0) return;
            // ActionManager.Instance()->QueuedActionId

            // 0 = target self, 1 = target current
            DalamudApi.Framework.RunOnFrameworkThread(() => {
                ActionManager.Instance()->UseAction(type, actionId, targetId);
            });
        } catch (Exception e) {
            DalamudApi.PluginLog.Error(e, $"Error while using action {actionId}");
        }
    }

    /// <summary>
    /// Submits a ground-targeted action at an explicit world position. This
    /// bypasses the placement cursor entirely.
    /// </summary>
    public static unsafe bool UseActionLocation(
        ActionType type,
        uint actionId,
        ulong targetId,
        Vector3 location) {
        if (!float.IsFinite(location.X) || !float.IsFinite(location.Y) || !float.IsFinite(location.Z))
            return false;
        var manager = ActionManager.Instance();
        if (manager == null)
            return false;
        return manager->UseActionLocation(type, actionId, targetId, &location, 0, 0);
    }

    public static unsafe void UseGeneralAction(uint actionId) {
        DalamudApi.Framework.RunOnFrameworkThread(() => {
            ActionManager.Instance()->UseAction(ActionType.GeneralAction, actionId);
        });
    }

    public static unsafe void UseGeneralAction(string actionName) {
        var action = GeneralActionHelper.GetExecutableAction(actionName);
        if (action == null) {
            DalamudApi.PluginLog.Debug("Invalid general action name");
            return;
        }

        DalamudApi.Framework.RunOnFrameworkThread(() => {
            ActionManager.Instance()->UseAction(ActionType.GeneralAction, action.ActionId);
        });

        // DalamudApi.PluginLog.Debug($"[USE GENERL ACTION] {action.ActionId}");
    }

    public static unsafe void UseAction(uint actionId) {
        DalamudApi.Framework.RunOnFrameworkThread(() => {
            ActionManager.Instance()->UseAction(ActionType.Action, actionId);
        });
    }

    public static unsafe void UseAction(string actionName) {
        var action = ActionHelper.GetExecutableAction(actionName);
        if (action == null) {
            DalamudApi.PluginLog.Debug("Invalid action name");
            return;
        }

        DalamudApi.Framework.RunOnFrameworkThread(() => {
            ActionManager.Instance()->UseAction(ActionType.Action, action.ActionId);
        });

        // DalamudApi.PluginLog.Debug($"[USE ACTION] {action.ActionId}");
    }

    public static unsafe void UseItem(uint actionId) {
        // bool isActionOffCooldown = ActionManager.Instance()->IsActionOffCooldown(ActionType.Item, actionId);
        // var adjustedActionId = ActionManager.Instance()->GetAdjustedActionId(actionId);
        // AgentInventoryContext.Instance()->UseItem(actionId);

        // DalamudApi.Framework.RunOnFrameworkThread(() =>
        // {
        //     // ActionManager.Instance()->UseAction(ActionType.Item, actionId, 0xE0000000, 65535);
        //     ActionManager.Instance()->UseAction(ActionType.Item, actionId, extraParam: 65535);
        // });

        // AgentInventoryContext.Instance()->UseItem(actionId);

        DalamudApi.Framework.RunOnTick(() => {
            ActionManager.Instance()->UseAction(ActionType.Item, actionId, extraParam: 65535);
        }, delayTicks: 3);
    }

    public static unsafe void UseItem(string itemName) {
        var item = ItemHelper.GetExecutableAction(itemName);
        if (item == null) {
            DalamudApi.PluginLog.Debug("Invalid item name");
            return;
        }

        // DalamudApi.Framework.RunOnFrameworkThread(() =>
        // {
        //     ActionManager.Instance()->UseAction(ActionType.Item, item.ActionId, extraParam: 65535);
        // });

        DalamudApi.Framework.RunOnTick(() => {
            ActionManager.Instance()->UseAction(ActionType.Item, item.ActionId, extraParam: 65535);
        }, delayTicks: 3);
    }

    public static unsafe void UseInventoryItem(uint itemId) {
        // DalamudApi.Framework.RunOnFrameworkThread(() =>
        // {
        //     AgentInventoryContext.Instance()->UseItem(itemId);
        // });

        DalamudApi.Framework.RunOnTick(() => {
            AgentInventoryContext.Instance()->UseItem(itemId);
        }, delayTicks: 3);

        // DalamudApi.PluginLog.Debug($"[USE INVENTORY ITEM] {itemId}");
    }

    public static unsafe void UseInventoryItem(string itemName) {
        var item = ItemHelper.GetExecutableAction(itemName);
        if (item == null) {
            DalamudApi.PluginLog.Debug("Invalid item name");
            return;
        }

        // DalamudApi.Framework.RunOnFrameworkThread(delegate
        // {
        //     AgentInventoryContext.Instance()->UseItem(item.ActionId);
        // });
        DalamudApi.Framework.RunOnTick(() => {
            AgentInventoryContext.Instance()->UseItem(item.ActionId);
        }, delayTicks: 3);

        // DalamudApi.PluginLog.Debug($"[USE INVENTORY ITEM NAME] {itemName}");
    }

    public static unsafe void UnequipGlasses() {
        DalamudApi.Framework.RunOnFrameworkThread(() => {
            var player = Control.GetLocalPlayer();
            if (player == null) return;

            var glassesIds = player->DrawData.GlassesIds;
            if (glassesIds.Length > 0) {
                var glassesId = glassesIds[0];
                if (glassesId == 0) return;

                var action = FacewearHelper.GetExecutableAction(glassesId);
                Chat.SendMessage(action.TextCommand);
            }
        });
    }
}
