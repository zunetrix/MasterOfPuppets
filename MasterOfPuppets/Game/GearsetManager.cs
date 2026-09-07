using System;
using System.Collections.Generic;
using System.Linq;

using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;

using MasterOfPuppets.Extensions.Dalamud;

namespace MasterOfPuppets;

public sealed record GearsetDescriptor(int Number, string Name, byte ClassJobId);

public sealed record GearsetSelector(int? Number = null, string Name = "") {
    public static GearsetSelector ByNumber(int number) => new(number);
    public static GearsetSelector ByName(string name) => new(null, name);
}

public sealed record GearsetResolution(
    bool Success,
    string Status,
    string Message,
    GearsetDescriptor? Gearset,
    IReadOnlyList<GearsetDescriptor> Candidates);

public static class GearsetManager {
    public static unsafe IReadOnlyList<GearsetDescriptor> GetGearsets(uint? classJobId = null) {
        if (classJobId is > byte.MaxValue)
            return [];
        var rapture = RaptureGearsetModule.Instance();
        var inventory = InventoryManager.Instance();
        if (rapture == null || inventory == null)
            return [];

        var result = new List<GearsetDescriptor>();
        var gearsetCount = Math.Min(100, (int)inventory->GetPermittedGearsetCount());
        for (var gearsetIndex = 0; gearsetIndex < gearsetCount; gearsetIndex++) {
            if (!rapture->IsValidGearset(gearsetIndex))
                continue;
            var gearset = rapture->GetGearset(gearsetIndex);
            if (gearset == null
                || !gearset->Flags.HasFlag(RaptureGearsetModule.GearsetFlag.Exists)
                || gearset->ClassJob == 0
                || (classJobId.HasValue && gearset->ClassJob != classJobId.Value))
                continue;
            result.Add(new GearsetDescriptor(
                gearsetIndex + 1,
                gearset->NameString?.Trim() ?? string.Empty,
                gearset->ClassJob));
        }
        return result;
    }

    internal static GearsetResolution ResolveGearset(
        IReadOnlyList<GearsetDescriptor> gearsets,
        byte? requiredClassJobId,
        GearsetSelector? selector) {
        ArgumentNullException.ThrowIfNull(gearsets);
        if (requiredClassJobId == 0)
            return Failure("invalid", "class/job ID must be between 1 and 255");

        var ordered = gearsets
            .Where(item => item.Number is >= 1 and <= 100 && item.ClassJobId != 0)
            .OrderBy(item => item.Number)
            .ToArray();
        var jobMatches = requiredClassJobId.HasValue
            ? ordered.Where(item => item.ClassJobId == requiredClassJobId.Value).ToArray()
            : ordered;

        if (selector == null) {
            return Failure(
                "selector_required",
                requiredClassJobId.HasValue
                    ? $"class/job {requiredClassJobId.Value} requires an exact gearset name or number"
                    : "an exact gearset name or number is required",
                jobMatches);
        }

        var hasNumber = selector.Number.HasValue;
        var name = selector.Name?.Trim() ?? string.Empty;
        var hasName = name.Length > 0;
        if (hasNumber == hasName)
            return Failure("invalid", "gearset selector must contain exactly one name or number");

        if (hasNumber) {
            if (selector.Number is < 1 or > 100)
                return Failure("invalid", "gearset number must be between 1 and 100");
            var selected = ordered.FirstOrDefault(item => item.Number == selector.Number.Value);
            if (selected == null)
                return Failure("missing", $"gearset {selector.Number.Value} does not exist");
            if (requiredClassJobId.HasValue && selected.ClassJobId != requiredClassJobId.Value)
                return Failure(
                    "job_mismatch",
                    $"gearset {selected.Number} '{selected.Name}' is class/job {selected.ClassJobId}, not {requiredClassJobId.Value}",
                    [selected]);
            return Found(selected);
        }

        var nameMatches = ordered
            .Where(item => item.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        if (nameMatches.Length == 0)
            return Failure("missing", $"gearset named '{name}' does not exist");
        var eligibleNameMatches = requiredClassJobId.HasValue
            ? nameMatches.Where(item => item.ClassJobId == requiredClassJobId.Value).ToArray()
            : nameMatches;
        if (eligibleNameMatches.Length == 0)
            return Failure(
                "job_mismatch",
                $"gearset named '{name}' does not match class/job {requiredClassJobId!.Value}",
                nameMatches);
        return eligibleNameMatches.Length == 1
            ? Found(eligibleNameMatches[0])
            : Failure(
                "ambiguous",
                $"gearset name '{name}' matches {eligibleNameMatches.Length} gearsets; use a gearset number",
                eligibleNameMatches);
    }

    public static GearsetResolution ResolveGearset(byte? requiredClassJobId, GearsetSelector? selector) =>
        ResolveGearset(GetGearsets(), requiredClassJobId, selector);

    private static GearsetResolution Found(GearsetDescriptor gearset) =>
        new(true, "found", $"resolved gearset {gearset.Number} '{gearset.Name}'", gearset, [gearset]);

    private static GearsetResolution Failure(
        string status,
        string message,
        IReadOnlyList<GearsetDescriptor>? candidates = null) =>
        new(false, status, message, null, candidates ?? []);

    internal static unsafe IReadOnlyList<uint> GetAvailableClassJobIds() {
        return GetGearsets()
            .Select(item => (uint)item.ClassJobId)
            .Distinct()
            .OrderBy(id => id)
            .ToArray();
    }

    // Direct gearset equip
    public static void ChangeGearset(Plugin plugin, int gearsetIndex) {
        EquipGearset(gearsetIndex);
    }

    public static unsafe void MoveGearsetsToArmoury(Plugin plugin, IReadOnlyList<int> gearsetIndices) {
        var rapture = RaptureGearsetModule.Instance();

        // Track source descriptors already scheduled so shared items are only moved once
        // (first gearset in the list wins the target slot).
        var scheduledSources = new HashSet<InventoryDescriptor>();

        for (int listIdx = 0; listIdx < gearsetIndices.Count; listIdx++) {
            var gearsetIndex = gearsetIndices[listIdx];
            if (!rapture->IsValidGearset(gearsetIndex)) continue;

            var gearset = rapture->GetGearset(gearsetIndex);
            int targetSlot = listIdx;

            foreach (var item in gearset->Items) {
                if (item.ItemId == 0) continue;

                var armouryType = item.GetInventoryType();
                if (armouryType == default) continue;

                var armouryContainer = InventoryManager.Instance()->GetInventoryContainer(armouryType);
                var inArmoury = InventoryHelper.FindGearsetItemInArmoury(item, armouryContainer);

                // Already at the correct slot - nothing to do
                if (inArmoury?.Slot == targetSlot) {
                    var info = ItemHelper.GetItem(item.ItemId % 1_000_000u);
                    var itemName = info != null ? info.Value.Name.ToString() : item.ItemId.ToString();
                    DalamudApi.PluginLog.Debug($"[ItemMover] GS{gearsetIndex + 1} \"{itemName}\" already at {armouryType}[{targetSlot}] - skip");
                    continue;
                }

                // Find item: prefer inventory, fall back to armoury (wrong slot)
                var src = item.FindGearsetItemInInventory() ?? inArmoury;
                if (src == null) continue;

                // Skip if this physical item was already scheduled by an earlier gearset
                if (!scheduledSources.Add(src.Value)) continue;

                plugin.ItemMover.Enqueue(src.Value, armouryType, targetSlot);
            }
        }
    }

    // swap gearset items between inventory -> amoury
    public static unsafe void SwapGearsets(Plugin plugin, int inventoryGearsetIndex, int armouryGearsetIndex) {
        var rapture = RaptureGearsetModule.Instance();
        if (!rapture->IsValidGearset(inventoryGearsetIndex) ||
            !rapture->IsValidGearset(armouryGearsetIndex))
            return;

        var invGearset = rapture->GetGearset(inventoryGearsetIndex);
        var armGearset = rapture->GetGearset(armouryGearsetIndex);
        var invManager = InventoryManager.Instance();

        for (int i = 0; i < invGearset->Items.Length; i++) {
            var invItem = invGearset->Items[i];
            var armItem = armGearset->Items[i];

            if (invItem.ItemId == 0) continue;

            var armouryType = invItem.GetInventoryType();
            if (armouryType == default) continue;

            var armouryContainer = invManager->GetInventoryContainer(armouryType);

            // skip it already in armoury
            if (InventoryHelper.FindGearsetItemInArmoury(invItem, armouryContainer) != null) continue;

            var invSrc = invItem.FindGearsetItemInInventory();
            if (invSrc == null) continue;

            // try move to 2nd gearset of empty slot
            var armItemInArmoury = armItem.ItemId != 0
                ? InventoryHelper.FindGearsetItemInArmoury(armItem, armouryContainer)
                : null;
            var targetSlot = armItemInArmoury?.Slot ?? InventoryHelper.FindFirstEmptyArmourySlot(armouryType)?.Slot;
            if (targetSlot == null) continue;

            plugin.ItemMover.Enqueue(invSrc.Value, armouryType, targetSlot.Value);
        }
    }

    private static unsafe bool EnqueueGearsetItemsToArmoury(Plugin plugin, int gearsetIndex) {
        var rapture = RaptureGearsetModule.Instance();
        if (!rapture->IsValidGearset(gearsetIndex))
            return false;

        var gearset = rapture->GetGearset(gearsetIndex);
        bool any = false;

        foreach (var item in gearset->Items) {
            if (item.ItemId == 0)
                continue;

            var inventoryItem = item.FindGearsetItemInInventory();
            if (inventoryItem == null)
                continue;

            plugin.ItemMover.Enqueue(inventoryItem.Value, item.GetInventoryType());
            any = true;
        }

        return any;
    }

    public static unsafe void EquipGearset(int gearsetIndex) {
        if (gearsetIndex is < 0 or > 99)
            return;
        var rapture = RaptureGearsetModule.Instance();
        if (rapture == null || !rapture->IsValidGearset(gearsetIndex))
            return;

        rapture->EquipGearset(gearsetIndex, 0);
    }

    public static unsafe void RenameGearset(int gearsetIndex, string gearsetName) {
        if (gearsetIndex < 0 || gearsetIndex > 99) return;
        if (string.IsNullOrEmpty(gearsetName) || gearsetName.Length > 15) return;

        var rapture = RaptureGearsetModule.Instance();
        if (!rapture->IsValidGearset(gearsetIndex))
            return;

        var agentGearSet = AgentGearSet.Instance();
        if (agentGearSet == null) return;

        DalamudApi.Framework.RunOnFrameworkThread(() => {
            agentGearSet->RenameGearset(gearsetIndex, gearsetName);
        });
    }

    public static unsafe void ReorderGearset(int gearsetIndex, int newGearsetIndex) {
        if (newGearsetIndex < 0 || newGearsetIndex > 99) return;
        if (gearsetIndex < 0 || gearsetIndex > 99) return;

        var rapture = RaptureGearsetModule.Instance();
        if (!rapture->IsValidGearset(gearsetIndex))
            return;

        var agentGearSet = AgentGearSet.Instance();
        if (agentGearSet == null) return;

        DalamudApi.Framework.RunOnFrameworkThread(() => {
            agentGearSet->ReassignGearsetId(gearsetIndex, newGearsetIndex);
            // agentGearSet->ReassignGearSetNumber(newGearsetIndex);
            // rapture->ReassignGearsetId(newGearsetIndex, gearsetIndex);
        });
    }

    // private static unsafe void ChangeGearsetGlamour(int gearsetIndex, int glamoutIndex) {
    //     if (glamoutIndex <= 0 || glamoutIndex > 20) return;

    //     var rapture = RaptureGearsetModule.Instance();
    //     if (!rapture->IsValidGearset(gearsetIndex))
    //         return;

    //     DalamudApi.Framework.RunOnFrameworkThread(() => {
    //         rapture->EquipGearset(gearsetIndex, (byte)glamoutIndex);
    //     });
    // }
}
