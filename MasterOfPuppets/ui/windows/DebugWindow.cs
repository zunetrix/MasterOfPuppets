using System;
using System.Linq;
using System.Diagnostics.CodeAnalysis;
using System.Collections.Generic;

using Dalamud.Interface;
using Dalamud.Interface.ImGuiFileDialog;
using Dalamud.Interface.Windowing;
using Dalamud.Interface.ImGuiNotification;
using Dalamud.Interface.Utility;
using Dalamud.Bindings.ImGui;

using FFXIVClientStructs.FFXIV.Client.UI.Misc;

namespace MasterOfPuppets;

public class DebugWindow : Window
{
    private Plugin Plugin { get; }
    private FileDialogManager FileDialogManager { get; }

    private static string _targetName = string.Empty;
    private static string _search = string.Empty;
    private static HashSet<object>? _filtered;
    private static int _hoveredItem;
    private static readonly Dictionary<string, (bool toogle, bool wasEnterClickedLastTime)> _comboDic = [];


    public DebugWindow(Plugin plugin) : base($"{Plugin.Name} Debug###DebugWindow")
    {
        Plugin = plugin;

        Size = ImGuiHelpers.ScaledVector2(500, 300);
        SizeCondition = ImGuiCond.FirstUseEver;
        // Flags = ImGuiWindowFlags.NoResize;
        // SizeConstraints = new WindowSizeConstraints()
        // {
        //     MinimumSize = ImGuiHelpers.ScaledVector2(350),
        //     MaximumSize = ImGuiHelpers.ScaledVector2(2000)
        // };

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
        DrawElementsDebugTab();

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


            ImGui.InputTextWithHint("##TargetNameDebugInput", "Target name", ref _targetName, 255, ImGuiInputTextFlags.AutoSelectAll);
            ImGui.SameLine();
            if (ImGui.Button("Target"))
            {
                TargetManager.TargetByName(_targetName);
            }

            ImGui.SameLine();
            if (ImGui.Button("Target Clear"))
            {
                TargetManager.ClearTarget();
            }

            if (ImGui.Button("Target My Target"))
            {
                Plugin.IpcProvider.ExecuteTargetMyTarget();
            }

            if (ImGui.Button("Print Game Chat Error"))
            {
                DalamudApi.ChatGui.PrintError($"Test error message");
            }

            if (ImGui.Button("Chat SendChatRunMacro(2)"))
            {
                Plugin.ChatWatcher.SendChatRunMacro("2");
            }

            if (ImGui.Button("Chat SendChatRunMacro(Parasol action 1)"))
            {
                Plugin.ChatWatcher.SendChatRunMacro("\"Parasol action 1\"");
            }

            if (ImGui.Button("Chat SendChatStopMacroExecution"))
            {
                Plugin.ChatWatcher.SendChatStopMacroExecution();
            }

            ImGui.Button("Resset all Config data (double click)");
            if (ImGui.IsItemHovered())
            {
                if (ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left))
                {
                    Plugin.Config.ResetData();
                    Plugin.IpcProvider.SyncConfiguration();
                }
            }

            if (ImGui.Button("ExecuteActionCommand Umbrella Dance"))
            {
                Plugin.IpcProvider.ExecuteActionCommand(ActionHelper.FavoriteActions.RainCheck.ActionId);
                // GameActionManager.UseAction(30868);
                DalamudApi.ShowNotification($"ExecuteActionCommand", NotificationType.Info, 5000);
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
            if (ImGui.Button("UseActionByName(Peloton)"))
            {
                GameActionManager.UseActionByName("Peloton");
                DalamudApi.ShowNotification($"UseActionByName", NotificationType.Info, 5000);
            }

            if (ImGui.Button("UseItemByName(Lominsan Sparkler)"))
            {
                GameActionManager.UseItemByName("Lominsan Sparkler");
                DalamudApi.ShowNotification($"UseItemByName", NotificationType.Info, 5000);
            }

            if (ImGui.Button("UseItemById(5893)"))
            {
                uint lominsanSparklere = 5893;
                GameActionManager.UseItemById(lominsanSparklere);
                DalamudApi.ShowNotification($"UseItemById", NotificationType.Info, 5000);
            }
            if (ImGui.Button("Broadcast UseItemById(5893)"))
            {
                uint lominsanSparklere = 5893;
                Plugin.IpcProvider.ExecuteItemCommand(lominsanSparklere);
                DalamudApi.ShowNotification($"Broadcast UseItemById(5893)", NotificationType.Info, 5000);
            }
            // unsafe
            // {
            //     ImGui.TextUnformatted($"{ActionManager.Instance()->QueuedActionId}");
            // }

            if (ImGui.Button("Use Invalid Item name"))
            {
                var item = ItemHelper.GetExecutableActionByName("Lominsan Sparkler Flare");
                DalamudApi.PluginLog.Warning($"item: {item?.ActionName}");

                GameActionManager.UseItemByName("Lominsan Sparkler Flare");
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
                            var iconSize = ImGuiHelpers.ScaledVector2(30, 30);
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
        if (RaptureHotbarModule.Instance()->PetHotbar.Slots.IsEmpty)
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

                    for (var slotIndex = 0; slotIndex < RaptureHotbarModule.Instance()->PetHotbar.Slots.Length; slotIndex++)
                    {
                        var slot = RaptureHotbarModule.Instance()->PetHotbar.Slots[slotIndex];

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
                        var iconSize = ImGuiHelpers.ScaledVector2(30, 30);
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
            var items = ItemHelper.GetAllowedItems();
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

    public static bool SearchableCombo<T>(string id, [NotNullWhen(true)] out T? selected, string preview, IEnumerable<T> possibilities, Func<T, string> toName, Func<T, string, bool> searchPredicate, Func<T, bool> preFilter, ImGuiComboFlags flags = ImGuiComboFlags.None) where T : notnull
    {
        _comboDic.TryAdd(id, (false, false));
        (var toggle, var wasEnterClickedLastTime) = _comboDic[id];
        selected = default;
        if (!ImGui.BeginCombo(id + (toggle ? "##x" : ""), preview, flags)) return false;

        if (wasEnterClickedLastTime || ImGui.IsKeyPressed(ImGuiKey.Escape))
        {
            toggle = !toggle;
            _search = string.Empty;
            _filtered = null;
        }
        var enterClicked = ImGui.IsKeyPressed(ImGuiKey.Enter) || ImGui.IsKeyPressed(ImGuiKey.KeypadEnter);
        wasEnterClickedLastTime = enterClicked;
        _comboDic[id] = (toggle, wasEnterClickedLastTime);
        if (ImGui.IsKeyPressed(ImGuiKey.UpArrow))
            _hoveredItem--;
        if (ImGui.IsKeyPressed(ImGuiKey.DownArrow))
            _hoveredItem++;
        _hoveredItem = Math.Clamp(_hoveredItem, 0, Math.Max(_filtered?.Count - 1 ?? 0, 0));
        if (ImGui.IsWindowAppearing() && ImGui.IsWindowFocused() && !ImGui.IsAnyItemActive())
        {
            _search = string.Empty;
            _filtered = null;
            ImGui.SetKeyboardFocusHere(0);
        }

        if (ImGui.InputText("##ExcelSheetComboSearch", ref _search, 128))
            _filtered = null;

        if (_filtered == null)
        {
            _filtered = possibilities.Where(preFilter).Where(s => searchPredicate(s, _search)).Cast<object>().ToHashSet();
            _hoveredItem = 0;
        }

        var i = 0;
        foreach (var row in _filtered.Cast<T>())
        {
            var hovered = _hoveredItem == i;
            ImGui.PushID(i);
            if (ImGui.Selectable(toName(row), hovered) || (enterClicked && hovered))
            {
                selected = row;
                ImGui.PopID();
                ImGui.EndCombo();
                return true;
            }
            ImGui.PopID();
            i++;
        }

        ImGui.EndCombo();
        return false;
    }

    private void DrawElementsDebugTab()
    {
        if (ImGui.BeginTabItem($"Gui Elements###GuiElementsDebugTab"))
        {
            ImGui.TextUnformatted("ImGui Elements");

            // SearchableCombo();

            ImGui.EndTabItem();
        }
    }
}
