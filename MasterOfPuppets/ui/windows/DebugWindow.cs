using System.Numerics;

using Dalamud.Interface.ImGuiFileDialog;
using Dalamud.Interface.Windowing;
using Dalamud.Interface.ImGuiNotification;
using Dalamud.Interface.Utility;
using Dalamud.Bindings.ImGui;

using FFXIVClientStructs.FFXIV.Client.System.Framework;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using static FFXIVClientStructs.FFXIV.Client.UI.Misc.RaptureHotbarModule;
using Lumina.Excel.Sheets;

namespace MasterOfPuppets;

public class DebugWindow : Window
{
    private Plugin Plugin { get; }
    private FileDialogManager FileDialogManager { get; }

    public DebugWindow(Plugin plugin) : base($"{Plugin.Name} Debug###DebugWindow")
    {
        Plugin = plugin;

        Size = new Vector2(500, 300);
        SizeCondition = ImGuiCond.FirstUseEver;
        // Flags = ImGuiWindowFlags.NoResize;

        FileDialogManager = new FileDialogManager();
    }

    public override void PreDraw()
    {
        FileDialogManager.Draw();
        base.PreDraw();
    }

    public override void Draw()
    {
        if (!ImGui.BeginTabBar("##DebugTabs")) return;

        DrawGeneralDebugTab();
        DrawHotbarDebugTab();
        DrawPetHotbarDebugTab();
        DrawItemsDebugTab();

        ImGui.EndTabBar();
    }

    private void DrawGeneralDebugTab()
    {
        if (ImGui.BeginTabItem($"General###GeneralDebugTab"))
        {
            ImGui.TextUnformatted("Actions Test");

            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();

            if (ImGui.Button("BroadcastActionCommand UmbrellaDance"))
            {
                Plugin.IpcProvider.BroadcastActionCommand(GameActionManager.CustomActions["UmbrellaDance"].ActionId);
                // GameActionManager.UseAction(30868);
                DalamudApi.ShowNotification($"BroadcastActionCommand", NotificationType.Info, 5000);
            }

            if (ImGui.Button("ExecuteHotbarActionBySlotIndex(1, 5)"))
            {
                HotbarManager.ExecuteHotbarActionBySlotIndex(1, 5);
                DalamudApi.ShowNotification($"ExecuteHotbarActionBySlotIndex", NotificationType.Info, 5000);
            }

            if (ImGui.Button("ExecutePetHotbarActionBySlotIndex(0)"))
            {
                HotbarManager.ExecutePetHotbarActionBySlotIndex(0);
                DalamudApi.ShowNotification($"ExecutePetHotbarActionBySlotIndex", NotificationType.Info, 5000);
            }

            if (ImGui.Button("ExecutePetHotbarActionBySlotIndex(1)"))
            {
                HotbarManager.ExecutePetHotbarActionBySlotIndex(1);
                DalamudApi.ShowNotification($"ExecuteHotbarActionBySlotIndex", NotificationType.Info, 5000);
            }

            if (ImGui.Button("UseItemByName(Heavenscracker)"))
            {
                GameActionManager.UseItemByName("Heavenscracker");
                DalamudApi.ShowNotification($"UseItemByName", NotificationType.Info, 5000);
            }

            ImGui.EndTabItem();
        }
    }

    private unsafe void DrawHotbarDebugTab()
    {
        var hotbars = RaptureHotbarModule.Instance()->Hotbars;
        if (hotbars.IsEmpty || hotbars.Length <= 0)
        {
            DalamudApi.PluginLog.Warning($"Invalid Hotbars");
            return;
        }

        if (ImGui.BeginTabItem($"Hotbars###HotbarsDebugTab"))
        {
            // for (var hotbarIndex = 0; hotbarIndex < hotbars.Length; hotbarIndex++)
            int hotbarIndex = 0;
            foreach (var hotbar in hotbars)
            {
                if (ImGui.CollapsingHeader($"Hotbar [{hotbarIndex}]"))
                {
                    if (hotbar.Slots.IsEmpty)
                    {
                        DalamudApi.PluginLog.Warning($"hotbar.Slots.IsEmpty");
                        return;
                    }

                    var tableFlags = ImGuiTableFlags.RowBg | ImGuiTableFlags.PadOuterX |
                        ImGuiTableFlags.NoSavedSettings | ImGuiTableFlags.BordersInnerV;
                    var tableColumnCount = 5;

                    if (ImGui.BeginTable($"##HotbarTable_{hotbarIndex}", tableColumnCount, tableFlags))
                    {
                        ImGui.TableSetupColumn("  ", ImGuiTableColumnFlags.WidthFixed);
                        ImGui.TableSetupColumn("Type", ImGuiTableColumnFlags.WidthFixed);
                        ImGui.TableSetupColumn("ID", ImGuiTableColumnFlags.WidthFixed);
                        ImGui.TableSetupColumn("Icon", ImGuiTableColumnFlags.WidthFixed);
                        ImGui.TableSetupColumn("Name", ImGuiTableColumnFlags.WidthStretch);

                        // hotbar.GetHotbarSlot(slotIndex);

                        for (var slotIndex = 0; slotIndex < hotbar.Slots.Length; slotIndex++)
                        {
                            // if (hotbar.Slots[slotIndex].IsEmpty) return;
                            var slot = hotbar.Slots[slotIndex];

                            ImGui.PushID(slotIndex);
                            ImGui.TableNextRow();
                            ImGui.TableNextColumn();
                            ImGui.TextUnformatted($"{slotIndex + 1:000}");

                            ImGui.TableNextColumn();
                            ImGui.TextUnformatted($"{slot.CommandType} - ({slot.ApparentSlotType})");

                            ImGui.TableNextColumn();
                            ImGui.TextUnformatted($"{slot.CommandId} - ({slot.ApparentActionId})");
                            if (ImGui.IsItemClicked())
                            {
                                ImGui.SetClipboardText($"{slot.ApparentActionId}");
                                DalamudApi.ShowNotification($"ID copied to clipboard", NotificationType.Info, 5000);
                            }
                            ImGuiUtil.ToolTip("Click to copy");

                            ImGui.TableNextColumn();
                            var icon = DalamudApi.TextureProvider.GetFromGameIcon(slot.IconId).GetWrapOrEmpty().Handle;
                            var iconSize = new Vector2(30 * ImGuiHelpers.GlobalScale, 30 * ImGuiHelpers.GlobalScale);
                            ImGui.TextUnformatted($"{slot.IconId}");
                            ImGui.Image(icon, iconSize);
                            if (ImGui.IsItemClicked())
                            {
                                HotbarManager.ExecuteHotbarActionBySlotIndex((uint)hotbarIndex, (uint)slotIndex);
                            }
                            ImGuiUtil.ToolTip("Click to execute");

                            ImGui.TableNextColumn();
                            ImGui.TextUnformatted($"{slot.GetDisplayNameForSlot(slot.ApparentSlotType, slot.ApparentActionId)}");

                            ImGui.PopID();
                        }
                        ImGui.EndTable();
                    }
                }

                hotbarIndex++;
            }

            ImGui.EndTabItem();
        }
    }

    private unsafe void DrawPetHotbarDebugTab()
    {
        var petHotbar = RaptureHotbarModule.Instance()->PetHotbar;
        if (petHotbar.Slots.IsEmpty)
        {
            DalamudApi.PluginLog.Warning($"petHotbar.Slots");
            return;
        }

        if (ImGui.BeginTabItem($"Pet Hotbar###PetHotbarsDebugTab"))
        {
            if (ImGui.CollapsingHeader($"Pet Hotbar"))
            {
                var tableFlags = ImGuiTableFlags.RowBg | ImGuiTableFlags.PadOuterX |
                    ImGuiTableFlags.NoSavedSettings | ImGuiTableFlags.BordersInnerV;
                var tableColumnCount = 5;

                if (ImGui.BeginTable($"##PetHotbarTable", tableColumnCount, tableFlags))
                {
                    ImGui.TableSetupColumn("  ", ImGuiTableColumnFlags.WidthFixed);
                    ImGui.TableSetupColumn("Type", ImGuiTableColumnFlags.WidthFixed);
                    ImGui.TableSetupColumn("ID", ImGuiTableColumnFlags.WidthFixed);
                    ImGui.TableSetupColumn("Icon", ImGuiTableColumnFlags.WidthFixed);
                    ImGui.TableSetupColumn("Name", ImGuiTableColumnFlags.WidthStretch);

                    for (var slotIndex = 0; slotIndex < petHotbar.Slots.Length; slotIndex++)
                    {
                        var slot = petHotbar.Slots[slotIndex];

                        ImGui.PushID(slotIndex);
                        ImGui.TableNextRow();
                        ImGui.TableNextColumn();
                        ImGui.TextUnformatted($"{slotIndex + 1:000}");

                        ImGui.TableNextColumn();
                        ImGui.TextUnformatted($"{slot.CommandType} - ({slot.ApparentSlotType})");

                        ImGui.TableNextColumn();
                        ImGui.TextUnformatted($"{slot.CommandId} - ({slot.ApparentActionId})");
                        if (ImGui.IsItemClicked())
                        {
                            ImGui.SetClipboardText($"{slot.ApparentActionId}");
                            DalamudApi.ShowNotification($"ID copied to clipboard", NotificationType.Info, 5000);
                        }
                        ImGuiUtil.ToolTip("Click to copy");

                        ImGui.TableNextColumn();
                        var icon = DalamudApi.TextureProvider.GetFromGameIcon(slot.IconId).GetWrapOrEmpty().Handle;
                        var iconSize = new Vector2(30 * ImGuiHelpers.GlobalScale, 30 * ImGuiHelpers.GlobalScale);
                        ImGui.TextUnformatted($"{slot.IconId}");
                        ImGui.Image(icon, iconSize);
                        if (ImGui.IsItemClicked())
                        {
                            HotbarManager.ExecutePetHotbarActionBySlotIndex((uint)slotIndex);
                        }
                        ImGuiUtil.ToolTip("Click to execute");

                        ImGui.TableNextColumn();
                        ImGui.TextUnformatted($"{slot.GetDisplayNameForSlot(slot.ApparentSlotType, slot.ApparentActionId)}");

                        ImGui.PopID();
                    }
                    ImGui.EndTable();
                }
            }

            ImGui.EndTabItem();
        }
    }

    private void DrawItemsDebugTab()
    {
        if (ImGui.BeginTabItem($"Items###ItemsDebugTab"))
        {
            var items = ItemsManager.GetAllowedItems();
            foreach (var item in items)
            {
                ImGui.TextUnformatted($"{item.ActionId}");
                ImGui.TextUnformatted($"{item.ActionName}");
                ImGui.TextUnformatted($"{item.IconId}");
                ImGui.Spacing();
                ImGui.Separator();
                ImGui.Spacing();
            }

            ImGui.EndTabItem();
        }

    }
}
