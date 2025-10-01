using System;
using System.Runtime.InteropServices;

using FFXIVClientStructs.FFXIV.Client.System.Framework;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;

using Lumina.Excel.Sheets;

using static FFXIVClientStructs.FFXIV.Client.UI.Misc.RaptureHotbarModule;

namespace MasterOfPuppets;

internal static class HotbarManager {
    private static unsafe void ExecuteHotbarAction(HotbarSlotType commandType, uint commandId) {
        var hotbarModulePtr = Framework.Instance()->GetUIModule()->GetRaptureHotbarModule();
        var hotbars = Framework.Instance()->GetUIModule()->GetRaptureHotbarModule()->Hotbars;
        if (hotbars.IsEmpty) return;

        var hotbar = hotbars[1];
        if (hotbar.Slots.IsEmpty) return;

        var slot1 = hotbar.GetHotbarSlot(0);
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

    public static unsafe void ExecuteHotbarActionBySlotIndex(uint hotbarId, uint slotIndex) {
        try {
            // var hotbarModulePtr = Framework.Instance()->GetUIModule()->GetRaptureHotbarModule();
            // var hotbars = Framework.Instance()->GetUIModule()->GetRaptureHotbarModule()->Hotbars;
            if (RaptureHotbarModule.Instance()->Hotbars.IsEmpty) {
                DalamudApi.PluginLog.Debug($"Invalid Hotbars");
                return;
            }

            if (RaptureHotbarModule.Instance()->Hotbars[(int)hotbarId].Slots.IsEmpty) {
                DalamudApi.PluginLog.Debug($"Invalid Hotbar Slots");
                return;
            }

            if (RaptureHotbarModule.Instance()->Hotbars[(int)hotbarId].GetHotbarSlot(slotIndex) == null) {
                DalamudApi.PluginLog.Debug($"Invalid hotbar slot {slotIndex}");
                return;
            }

            RaptureHotbarModule.Instance()->ExecuteSlot(RaptureHotbarModule.Instance()->Hotbars[(int)hotbarId].GetHotbarSlot(slotIndex));
        } catch (Exception e) {
            DalamudApi.PluginLog.Error(e, $"Error while using hotbar slot {slotIndex}");
        }
    }

    public static unsafe void ExecutePetHotbarActionBySlotIndex(uint slotIndex) {
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
