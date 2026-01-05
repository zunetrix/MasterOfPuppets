using System;

using Dalamud.Bindings.ImGui;
using Dalamud.Game.Inventory;
using Dalamud.Utility;

using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;

using MasterOfPuppets.Util;

using static FFXIVClientStructs.FFXIV.Client.UI.Misc.RaptureGearsetModule;

namespace MasterOfPuppets.Debug;

public sealed class InventoryDebugWidget : Widget {
    public override string Title => "Inventory";

    public InventoryDebugWidget(WidgetContext ctx) : base(ctx) {
    }

    public override void Draw() {
        ListInventory();
    }

    public unsafe void ListInventory() {
        // var container = InventoryManager.Instance()->GetInventoryContainer(InventoryType.Inventory1);
        // for (var i = 0; i < 13; i++) {
        //     if (i == 5) continue;
        //     var slot = container->GetInventorySlot(i);
        //     var itemId = slot->ItemId;
        // }

        var raptureGearSetModule = RaptureGearsetModule.Instance();
        var gearsetCount = InventoryManager.Instance()->GetPermittedGearsetCount();

        if (ImGui.BeginTable($"##GearSetItems", 7, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg)) {
            ImGui.TableSetupColumn("Gear Set");
            ImGui.TableSetupColumn("Item");
            ImGui.TableSetupColumn("Glamour");
            ImGui.TableSetupColumn("Missing Gearset");
            ImGui.TableSetupColumn("Missing Appearance");
            ImGui.TableSetupColumn("Missing Color");
            ImGui.TableSetupColumn("Missing Glamour");

            ImGui.TableHeadersRow();

            for (var gearsetIndex = 0; gearsetIndex < gearsetCount; gearsetIndex++) {
                var gearset = raptureGearSetModule->GetGearset(gearsetIndex);
                var items = gearset->Items;

                foreach (var item in items) {
                    var itemName = ItemUtil.GetItemName(item.ItemId).ExtractText();
                    ImGui.TableNextRow();

                    ImGui.TableNextColumn();
                    ImGui.Text($"{gearset->NameString} ({gearsetIndex + 1})");

                    ImGui.TableNextColumn();
                    ImGui.Text($"{itemName} ({item.ItemId})");

                    ImGui.TableNextColumn();
                    ImGui.Text($"{item.GlamourId}");

                    ImGui.TableNextColumn();
                    ImGui.Text(item.Flags.HasFlag(GearsetItemFlag.ItemMissing) ? "Yes" : "No");

                    ImGui.TableNextColumn();
                    ImGui.Text(item.Flags.HasFlag(GearsetItemFlag.AppearanceDiffers) ? "Yes" : "No");

                    ImGui.TableNextColumn();
                    ImGui.Text(item.Flags.HasFlag(GearsetItemFlag.ColorDiffers) ? "Yes" : "No");

                    ImGui.TableNextColumn();
                    var missingGlamour = item.Flags.HasFlag(GearsetItemFlag.ItemMissing) || item.Flags.HasFlag(GearsetItemFlag.AppearanceDiffers) || item.Flags.HasFlag(GearsetItemFlag.ColorDiffers);
                    ImGui.Text(missingGlamour ? "Yes" : "No");
                }
            }
            ImGui.EndTable();
        }

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();


        // foreach (var inventoryType in Enum.GetValues<GameInventoryType>()) {
        //     // var items = GameInventoryItem.GetReadOnlySpanOfInventory(inventoryType);
        //     var inventoryManager = InventoryManager.Instance();
        //     if (inventoryManager is null) return;

        //     var inventory = inventoryManager->GetInventoryContainer((InventoryType)inventoryType);
        //     if (inventory is null) return;

        //     var items = new ReadOnlySpan<GameInventoryItem>(inventory->Items, (int)inventory->Size);

        //     // var container = InventoryManager.Instance()->GetInventoryContainer((InventoryType)inventoryType);

        //     ImGui.Columns(3, "##InventoryItems");
        //     for (var slotIndex = 0; slotIndex < items.Length; slotIndex++) {
        //         var item = items[slotIndex];
        //         ImGui.Text($"{item.ItemId}");

        //         ImGui.NextColumn();
        //         ImGui.Text($"{item.GlamourId}");

        //         ImGui.NextColumn();
        //         ImGui.Text($"{item.IsHq}");

        //         ImGui.NextColumn();
        //         ImGui.Text($"{item.Stains}");
        //     }
        //     ImGui.Columns(1);
        // }
    }
}

