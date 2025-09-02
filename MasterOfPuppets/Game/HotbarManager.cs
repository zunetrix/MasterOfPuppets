using System;
using System.Runtime.InteropServices;
using Lumina.Excel.Sheets;

using FFXIVClientStructs.FFXIV.Client.System.Framework;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;

using static FFXIVClientStructs.FFXIV.Client.UI.Misc.RaptureHotbarModule;

namespace MasterOfPuppets;

internal static class HotbarManager
{
    private static unsafe void ExecuteHotbarAction(HotbarSlotType commandType, uint commandId)
    {
        var hotbarModulePtr = Framework.Instance()->GetUIModule()->GetRaptureHotbarModule();
        var hotbars = Framework.Instance()->GetUIModule()->GetRaptureHotbarModule()->Hotbars;
        if (hotbars.IsEmpty) return;

        var hotbar = hotbars[1];
        if (hotbar.Slots.IsEmpty) return;

        var slot1 = hotbar.GetHotbarSlot(0);
        // var slot2 = hotbar.GetHotbarSlot(1);

        var slot = new HotbarSlot
        {
            CommandType = commandType,
            CommandId = commandId
        };

        var ptr = Marshal.AllocHGlobal(Marshal.SizeOf(slot));
        Marshal.StructureToPtr(slot, ptr, false);

        hotbarModulePtr->ExecuteSlot((HotbarSlot*)ptr);

        Marshal.FreeHGlobal(ptr);
    }

    public static unsafe void ExecuteHotbarActionBySlotIndex(uint hotbarId, uint slotIndex)
    {
        try
        {
            // var hotbarModulePtr = Framework.Instance()->GetUIModule()->GetRaptureHotbarModule();
            // var hotbars = Framework.Instance()->GetUIModule()->GetRaptureHotbarModule()->Hotbars;
            var hotbars = RaptureHotbarModule.Instance()->Hotbars;
            if (hotbars.IsEmpty || hotbars.Length <= 0)
            {
                DalamudApi.PluginLog.Debug($"Invalid Hotbars");
                return;
            }

            var hotbar = hotbars[(int)hotbarId];
            if (hotbar.Slots.IsEmpty)
            {
                DalamudApi.PluginLog.Debug($"Invalid Hotbar Slots");
                return;
            }

            var slot = hotbar.GetHotbarSlot(slotIndex);
            if (slot == null)
            {
                DalamudApi.PluginLog.Debug($"Invalid hotbar slot {slotIndex}");
                return;
            }

            RaptureHotbarModule.Instance()->ExecuteSlot(slot);
            // RaptureHotbarModule.Instance()->ExecuteSlotById(hotbarId, slotIndex);
            // hotbarModulePtr->ExecuteSlotById(hotbarId, slotIndex);
        }
        catch (Exception e)
        {
            DalamudApi.PluginLog.Error(e, $"Error while using hotbar slot {slotIndex}");
        }
    }

    public static unsafe void ExecutePetHotbarActionBySlotIndex(uint slotIndex)
    {
        try
        {
            var petHotbar = RaptureHotbarModule.Instance()->PetHotbar;
            if (petHotbar.Slots.IsEmpty)
            {
                DalamudApi.PluginLog.Debug($"Invalid Pet Hotbars Slots");
                return;
            }

            var slot = petHotbar.GetHotbarSlot(slotIndex);
            if (slot == null)
            {
                DalamudApi.PluginLog.Debug($"Invalid pet hotbar slot {slotIndex}");
                return;
            }

            RaptureHotbarModule.Instance()->ExecuteSlot(slot);
        }
        catch (Exception e)
        {
            DalamudApi.PluginLog.Error(e, $"Error while using pet hotbar slot {slotIndex}");
        }
    }

    public static void ExecuteEmote(uint actionId)
    {
        DalamudApi.Framework.RunOnFrameworkThread(delegate
        {
            ExecuteHotbarAction(HotbarSlotType.Emote, actionId);
        });
    }

    public static void ExecuteOrnament(uint actionId)
    {
        DalamudApi.Framework.RunOnFrameworkThread(delegate
        {
            ExecuteHotbarAction(HotbarSlotType.Ornament, actionId);
        });
    }

    private static Emote? GetEmoteById(uint id)
    {
        return DalamudApi.DataManager.Excel.GetSheet<Emote>().GetRowOrDefault(id);
    }
}
