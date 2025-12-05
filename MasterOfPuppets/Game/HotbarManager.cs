using System;
using System.Runtime.InteropServices;

using FFXIVClientStructs.FFXIV.Client.System.Framework;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;

using static FFXIVClientStructs.FFXIV.Client.UI.Misc.RaptureHotbarModule;

namespace MasterOfPuppets;

internal static class HotbarManager {
    public static unsafe void ExecuteHotbarAction(HotbarSlotType commandType, uint commandId) {
        var hotbarModulePtr = Framework.Instance()->GetUIModule()->GetRaptureHotbarModule();
        // var hotbars = Framework.Instance()->GetUIModule()->GetRaptureHotbarModule()->Hotbars;
        // if (hotbars.IsEmpty) return;

        // var hotbar = hotbars[1];
        // if (hotbar.Slots.IsEmpty) return;

        // var slot1 = hotbar.GetHotbarSlot(0);
        // var slot2 = hotbar.GetHotbarSlot(1);

        var slot = new HotbarSlot {
            CommandType = commandType,
            CommandId = commandId
        };

        var ptr = Marshal.AllocHGlobal(Marshal.SizeOf(slot));
        Marshal.StructureToPtr(slot, ptr, false);

        hotbarModulePtr->ExecuteSlot((HotbarSlot*)ptr);

        Marshal.FreeHGlobal(ptr);
    }

    // public static unsafe void SetHotbarEmoteAction(uint hotbarIndex, uint slotIndex, uint actionId) {
    //     try {
    //         RaptureHotbarModule.Instance()->SetAndSaveSlot(hotbarIndex, slotIndex, HotbarSlotType.Emote, actionId);
    //     } catch (Exception e) {
    //         DalamudApi.PluginLog.Error(e, $"Error while using hotbar slot {slotIndex}");
    //     }
    // }

    public static void ExecuteHotbarEmoteAction(uint commandId) {
        try {
            ExecuteHotbarAction(HotbarSlotType.Emote, commandId);
        } catch (Exception e) {
            DalamudApi.PluginLog.Error(e, $"Error while using hotbar action {commandId}");
        }
    }

    public static unsafe void ExecuteHotbarActionByIndex(uint hotbarIndex, uint slotIndex) {
        try {
            // bars indexes = 0-17
            // bar slots 1-15

            // settings bars indexes 1-10
            // settings bar slots 1-12

            // var hotbarModulePtr = Framework.Instance()->GetUIModule()->GetRaptureHotbarModule();
            // var hotbars = Framework.Instance()->GetUIModule()->GetRaptureHotbarModule()->Hotbars;
            var maxHotBars = 10;
            var maxHotBarSlots = 12;
            bool isValidHotbar = hotbarIndex >= 0 && hotbarIndex <= maxHotBars;
            bool isValidHotBarSlot = slotIndex >= 0 || slotIndex <= maxHotBarSlots;
            if (!isValidHotBarSlot || !isValidHotbar) {
                DalamudApi.PluginLog.Debug($"Invalid Hotbars args {hotbarIndex}, {slotIndex}");
                return;
            }

            if (RaptureHotbarModule.Instance()->Hotbars.IsEmpty) {
                DalamudApi.PluginLog.Debug($"Invalid Hotbars");
                return;
            }

            if (RaptureHotbarModule.Instance()->Hotbars[(int)hotbarIndex].Slots.IsEmpty) {
                DalamudApi.PluginLog.Debug($"Invalid Hotbar Slots");
                return;
            }

            if (RaptureHotbarModule.Instance()->Hotbars[(int)hotbarIndex].GetHotbarSlot(slotIndex) == null) {
                DalamudApi.PluginLog.Debug($"Invalid hotbar slot {slotIndex}");
                return;
            }

            DalamudApi.Framework.RunOnFrameworkThread(delegate {
                RaptureHotbarModule.Instance()->ExecuteSlot(RaptureHotbarModule.Instance()->Hotbars[(int)hotbarIndex].GetHotbarSlot(slotIndex));
            });

        } catch (Exception e) {
            DalamudApi.PluginLog.Error(e, $"Error while using hotbar slot {slotIndex}");
        }
    }

    public static unsafe void ExecutePetHotbarActionByIndex(uint slotIndex) {
        try {
            if (RaptureHotbarModule.Instance()->PetHotbar.Slots.IsEmpty) {
                DalamudApi.PluginLog.Debug($"Invalid Pet Hotbars Slots");
                return;
            }

            if (RaptureHotbarModule.Instance()->PetHotbar.GetHotbarSlot(slotIndex) == null) {
                DalamudApi.PluginLog.Debug($"Invalid pet hotbar slot {slotIndex}");
                return;
            }

            RaptureHotbarModule.Instance()->ExecuteSlot(RaptureHotbarModule.Instance()->PetHotbar.GetHotbarSlot(slotIndex));
        } catch (Exception e) {
            DalamudApi.PluginLog.Error(e, $"Error while using pet hotbar slot {slotIndex}");
        }
    }
}
