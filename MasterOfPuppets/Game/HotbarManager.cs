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

    public static void ExecuteEmote(uint actionId)
    {
        ExecuteHotbarAction(HotbarSlotType.Emote, actionId);
    }

    public static void ExecuteOrnament(uint actionId)
    {
        ExecuteHotbarAction(HotbarSlotType.Ornament, actionId);
    }

    private static Emote? GetEmoteById(uint id)
    {
        return DalamudApi.DataManager.Excel.GetSheet<Emote>().GetRowOrDefault(id);
    }

    public static unsafe void CalcBForSlot(HotbarSlot* slot, out HotbarSlotType actionType, out uint actionId)
    {
        // short circuit, just a micro-optimization.
        if (slot->CommandType == 0 && slot->CommandId == 0)
        {
            actionType = HotbarSlotType.Empty;
            actionId = 0;

            return;
        }

        var hotbarModule = Framework.Instance()->GetUIModule()->GetRaptureHotbarModule();

        // Take in default values, just in case GetSlotAppearance fails for some reason
        var acType = slot->ApparentSlotType;
        var acId = slot->ApparentActionId;
        ushort actionCost = slot->CostType;

        RaptureHotbarModule.GetSlotAppearance(&acType, &acId, &actionCost, hotbarModule, slot);

        actionType = acType;
        actionId = acId;
    }

}
