using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Utility;

using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;

using MasterOfPuppets.Extensions.Dalamud;

namespace MasterOfPuppets.Debug;

public sealed class InventoryDebugWidget : Widget {
    public override string Title => "Inventory";
    private static int _gearsetIndex = 0;
    public InventoryDebugWidget(WidgetContext ctx) : base(ctx) {
    }

    public override void Draw() {
        ListInventory();
    }

    public unsafe void ListInventory() {
        ImGui.InputInt("##GearsetIndexInput", ref _gearsetIndex);

        ImGui.SameLine();
        if (ImGui.Button($"Change Gearset")) {
            GearsetManager.ChangeGearset(Context.Plugin, _gearsetIndex + 1);
        }

        var raptureGearSetModule = RaptureGearsetModule.Instance();
        var gearsetCount = InventoryManager.Instance()->GetPermittedGearsetCount();
        var iconSize = ImGuiHelpers.ScaledVector2(30, 30);

        if (ImGui.BeginTable($"##GearSetItems", 6, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg)) {
            ImGui.TableSetupColumn("N", ImGuiTableColumnFlags.WidthFixed);
            ImGui.TableSetupColumn("Slot", ImGuiTableColumnFlags.WidthFixed);
            ImGui.TableSetupColumn("Gear Set", ImGuiTableColumnFlags.WidthFixed);
            ImGui.TableSetupColumn("Item", ImGuiTableColumnFlags.WidthFixed);
            // ImGui.TableSetupColumn("Glamour", ImGuiTableColumnFlags.WidthFixed);
            // ImGui.TableSetupColumn("Is In Armoury", ImGuiTableColumnFlags.WidthFixed);
            ImGui.TableSetupColumn("Is In Inventory", ImGuiTableColumnFlags.WidthFixed);
            ImGui.TableSetupColumn("Actions", ImGuiTableColumnFlags.WidthStretch);

            ImGui.TableHeadersRow();
            for (var gearsetIndex = 0; gearsetIndex < gearsetCount; gearsetIndex++) {
                if (!raptureGearSetModule->IsValidGearset(gearsetIndex)) continue;

                var gearset = raptureGearSetModule->GetGearset(gearsetIndex);
                var geasrsetItems = gearset->Items;

                foreach (var gearsetItem in geasrsetItems) {
                    if (gearsetItem.ItemId == 0) continue;

                    var inventoryItem = InventoryHelper.FindGearsetItemInInventory(gearsetItem);
                    bool isGearsetItemInInvenotry = inventoryItem != null;
                    if (!isGearsetItemInInvenotry) continue;

                    var emptyInventorySlot = InventoryHelper.FindFirstEmptyInventorySlot();

                    var itemName = ItemUtil.GetItemName(gearsetItem.ItemId).ExtractText();
                    ImGui.TableNextRow();

                    ImGui.TableNextColumn();
                    ImGui.Text($"{gearsetIndex + 1:00}");

                    ImGui.TableNextColumn();
                    DalamudApi.TextureProvider.DrawIcon(gearsetItem.GetInventoryTypeIcon(), iconSize);

                    ImGui.TableNextColumn();
                    ImGui.Text($"{gearset->NameString}");

                    ImGui.TableNextColumn();
                    ImGui.Text($"{itemName} ({gearsetItem.ItemId})");

                    // ImGui.TableNextColumn();
                    // ImGui.Text($"{gearsetItem.GlamourId}");

                    // ImGui.TableNextColumn();
                    // ImGui.Text(gearsetItem.IsInArmoury() ? "Yes" : "No");

                    ImGui.TableNextColumn();
                    ImGui.Text(isGearsetItemInInvenotry ? "Yes" : "No");

                    ImGui.TableNextColumn();
                    if (ImGui.Button($"Move to Armoury##{gearsetIndex}_{gearsetItem.ItemId}")) {
                        if (emptyInventorySlot != null && isGearsetItemInInvenotry) {
                            InventoryType inventoryType = gearsetItem.GetInventoryType();
                            DalamudApi.PluginLog.Warning($"Moving Item: {itemName} ({inventoryItem.Value.Slot}) -> Armoury: {inventoryType} (0)");
                            ushort targetSlot = 0;
                            InventoryManager.Instance()->MoveItemSlot(inventoryItem.Value.Type, (ushort)inventoryItem.Value.Slot, inventoryType, targetSlot, true);
                        }
                    }
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

