using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Numerics;

using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.ImGuiFileDialog;
using Dalamud.Interface.ImGuiNotification;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Windowing;

using FFXIVClientStructs.FFXIV.Client.UI.Misc;

using MasterOfPuppets.Extensions;
using MasterOfPuppets.Extensions.Dalamud;
using MasterOfPuppets.Resources;
using MasterOfPuppets.Util;
using MasterOfPuppets.Util.ImGuiExt;

namespace MasterOfPuppets;

public class DebugWindow : Window {
    private Plugin Plugin { get; }
    private PluginUi Ui { get; }
    private FileDialogManager FileDialogManager { get; }

    private uint _macroIconId = 60042;
    private static string _inputTextContent = string.Empty;
    private static string _targetName = string.Empty;
    private static int _macroIdx = 0;
    private static string _search = string.Empty;
    private static HashSet<object>? _filtered;
    private static int _hoveredItem;
    private static readonly Dictionary<string, (bool toogle, bool wasEnterClickedLastTime)> _comboDic = [];
    private readonly ImGuiInputTextMultiline InputTextMultiline;
    private readonly ImGuiModalDialog ImGuiModalDialog = new("##ConfirmModal", new Vector2(400, 200));

    public DebugWindow(Plugin plugin, PluginUi ui) : base($"{Plugin.Name} Debug###DebugWindow") {
        Plugin = plugin;
        Ui = ui;

        Size = ImGuiHelpers.ScaledVector2(500, 450);
        SizeCondition = ImGuiCond.FirstUseEver;
        // Flags = ImGuiWindowFlags.NoResize;
        // SizeConstraints = new WindowSizeConstraints()
        // {
        //     MinimumSize = ImGuiHelpers.ScaledVector2(350),
        //     MaximumSize = ImGuiHelpers.ScaledVector2(2000)
        // };

        FileDialogManager = new FileDialogManager();
        InputTextMultiline = new ImGuiInputTextMultiline(plugin);
    }

    public override void PreDraw() {
        FileDialogManager.Draw();
        base.PreDraw();
    }

    public override void Draw() {
        if (!ImGui.BeginTabBar("##DebugTabs")) return;

        DrawGeneralDebugTab();
        DrawHotbarDebugTab();
        DrawPetHotbarDebugTab();
        DrawElementsDebugTab();
        DrawFontAwesomeIconsDebugTab();

        ImGui.EndTabBar();
    }

    public bool IsConflictingPluginDetected() {
        var conflictingPluginNames = new[] { "WrathCombo", "RotationSolver", "BossMod" };

        return DalamudApi.PluginInterface.InstalledPlugins
            .Any(plugin =>
                plugin.IsLoaded &&
                conflictingPluginNames.Contains(plugin.InternalName, StringComparer.OrdinalIgnoreCase));
    }

    private void DrawGeneralDebugTab() {
        if (ImGui.BeginTabItem($"General###GeneralDebugTab")) {
            ImGui.TextUnformatted("Actions Test");

            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();

            ImGui.InputInt("##MacroIndexInput", ref _macroIdx);
            ImGui.SameLine();
            if (ImGui.Button("Print Macro")) {
                DalamudApi.PluginLog.Warning($"{Plugin.Config.Macros[_macroIdx].JsonSerialize()}");
            }

            ImGui.InputTextWithHint("##TargetNameDebugInput", "Target name", ref _targetName, 255, ImGuiInputTextFlags.AutoSelectAll);
            ImGui.SameLine();
            if (ImGui.Button("Target")) {
                TargetManager.TargetByName(_targetName);
            }

            ImGui.SameLine();
            if (ImGui.Button("Target Clear")) {
                TargetManager.TargetClear();
            }

            if (ImGui.Button("Target Clear Broadcast")) {
                Plugin.IpcProvider.ExecuteTargetClear();
            }

            if (ImGui.Button("Target My Target")) {
                Plugin.IpcProvider.ExecuteTargetMyTarget();
            }

            if (ImGui.Button("Enable Walk")) {
                MovementManager.EnableWalk();
            }
            ImGui.SameLine();
            if (ImGui.Button("Disable Walk")) {
                MovementManager.DisableWalk();
            }

            if (ImGui.Button("Get Object Quantity")) {
                GameSettingsManager.GetDisplayObjectLimit();
            }
            ImGui.SameLine();
            if (ImGui.Button("Set Object Quantity")) {
                GameSettingsManager.SetDisplayObjectLimit(SettingsDisplayObjectLimitType.Minimum);
            }

            if (ImGui.Button("Print Game Chat Error")) {
                DalamudApi.ChatGui.PrintError($"Test error message");
            }

            if (ImGui.Button("Chat SendChatRunMacro(2)")) {
                Chat.SendMessage($"/p moprun 2");
            }

            if (ImGui.Button("Chat SendChatRunMacro(Parasol action 1)")) {
                Chat.SendMessage($"\"Parasol action 1\"");
            }

            if (ImGui.Button("Chat SendChatStopMacroExecution")) {
                Plugin.ChatWatcher.SendChatStopMacroExecution();
            }

            ImGui.Button("Resset all Config data (double click)");
            if (ImGui.IsItemHovered()) {
                if (ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left)) {
                    Plugin.Config.ResetData();
                    Plugin.IpcProvider.SyncConfiguration();
                }
            }

            if (ImGui.Button("ExecuteHotbarAction (sit anywhere 96)")) {
                HotbarManager.ExecuteHotbarEmoteAction(96);
            }

            if (ImGui.Button("ExecuteHotbarAction (sleep anywhere pose 99)")) {
                HotbarManager.ExecuteHotbarEmoteAction(99);
            }
            if (ImGui.Button("ExecuteHotbarAction (sleep / wake anywhere)")) {
                HotbarManager.ExecuteHotbarEmoteAction(88);
            }

            if (ImGui.Button("ExecuteActionCommand Umbrella Dance")) {
                Plugin.IpcProvider.ExecuteActionCommand(30868);
                DalamudApi.ShowNotification($"ExecuteActionCommand", NotificationType.Info, 5000);
            }

            if (ImGui.Button("ExecuteHotbarActionBySlotIndex(1, 5)")) {
                HotbarManager.ExecuteHotbarActionByIndex(1, 5);
                DalamudApi.ShowNotification($"ExecuteHotbarActionBySlotIndex", NotificationType.Info, 5000);
            }

            if (ImGui.Button("ExecutePetHotbarActionBySlotIndex(0)")) {
                HotbarManager.ExecutePetHotbarActionByIndex(0);
                DalamudApi.ShowNotification($"ExecutePetHotbarActionBySlotIndex", NotificationType.Info, 5000);
            }

            if (ImGui.Button("ExecutePetHotbarActionBySlotIndex(1)")) {
                HotbarManager.ExecutePetHotbarActionByIndex(1);
                DalamudApi.ShowNotification($"ExecuteHotbarActionBySlotIndex", NotificationType.Info, 5000);
            }

            if (ImGui.Button("UseItemByName(Heavenscracker)")) {
                GameActionManager.UseItemByName("Heavenscracker");
                DalamudApi.ShowNotification($"UseItemByName", NotificationType.Info, 5000);
            }

            if (ImGui.Button("UseActionByName(Peloton)")) {
                GameActionManager.UseActionByName("Peloton");
                DalamudApi.ShowNotification($"UseActionByName", NotificationType.Info, 5000);
            }

            if (ImGui.Button("UseGeneralActionById(23) unmount")) {
                GameActionManager.UseGeneralActionById(23);
                DalamudApi.ShowNotification($"UseGeneralActionById", NotificationType.Info, 5000);
            }

            if (ImGui.Button("Broadcast ExecuteItemCommand")) {
                Plugin.IpcProvider.ExecuteItemCommand(5893);
                DalamudApi.ShowNotification($"ExecuteItemCommand", NotificationType.Info, 5000);
            }

            if (ImGui.Button("UseItemByName(Lominsan Sparkler)")) {
                GameActionManager.UseItemByName("Lominsan Sparkler");
                DalamudApi.ShowNotification($"UseItemByName", NotificationType.Info, 5000);
            }

            if (ImGui.Button("UseItemById(5893)")) {
                uint lominsanSparklere = 5893;
                GameActionManager.UseItemById(lominsanSparklere);

                DalamudApi.ShowNotification($"UseItemById", NotificationType.Info, 5000);
            }
            if (ImGui.Button("Broadcast UseItemById(5893)")) {
                uint lominsanSparklere = 5893;
                Plugin.IpcProvider.ExecuteItemCommand(lominsanSparklere);
                DalamudApi.ShowNotification($"Broadcast UseItemById(5893)", NotificationType.Info, 5000);
            }

            if (ImGui.Button("Abandon Duty")) {
                GameFunctions.AbandonDuty();
            }
            // unsafe
            // {
            //     ImGui.TextUnformatted($"{ActionManager.Instance()->QueuedActionId}");
            // }

            if (ImGui.Button("Use Invalid Item name")) {
                var item = ItemHelper.GetExecutableActionByName("Lominsan Sparkler Flare");
                DalamudApi.PluginLog.Warning($"item: {item?.ActionName}");

                GameActionManager.UseItemByName("Lominsan Sparkler Flare");
                DalamudApi.ShowNotification($"UseItemByName", NotificationType.Info, 5000);
            }

            if (ImGui.Button("Print Hotbar")) {
                PrintHotbar();
            }

            if (ImGui.Button("Reset Macros Color To White")) {
                for (var i = 0; i < Plugin.Config.Macros.Count; i++) {
                    Plugin.Config.Macros[i].Color = new Vector4(1f, 1f, 1f, 1f);
                }

                Plugin.Config.Save();
                Plugin.IpcProvider.SyncConfiguration();
            }

            ImGui.EndTabItem();
        }
    }

    private unsafe void PrintHotbar() {
        var hotbars = RaptureHotbarModule.Instance()->Hotbars;
        for (var hotbarIndex = 0; hotbarIndex < hotbars.Length; hotbarIndex++) {
            var hotbar = hotbars[hotbarIndex];

            for (var slotIndex = 0; slotIndex < hotbar.Slots.Length; slotIndex++) {
                var slot = hotbar.Slots[slotIndex];
                DalamudApi.PluginLog.Debug($" bar[{hotbarIndex},{slotIndex}] {slot.CommandType} - ({slot.ApparentSlotType}) - {slot.CommandId} - ({slot.ApparentActionId})");
            }
        }

    }

    private unsafe void DrawHotbarDebugTab() {
        var hotbars = RaptureHotbarModule.Instance()->Hotbars;
        if (hotbars.IsEmpty || hotbars.Length <= 0) {
            DalamudApi.PluginLog.Warning($"Invalid Hotbars");
            return;
        }

        if (ImGui.BeginTabItem($"Hotbars###HotbarsDebugTab")) {
            // for (var hotbarIndex = 0; hotbarIndex < hotbars.Length; hotbarIndex++)
            int hotbarIndex = 0;
            foreach (var hotbar in hotbars) {
                if (ImGui.CollapsingHeader($"Hotbar [{hotbarIndex}]")) {
                    if (hotbar.Slots.IsEmpty) {
                        DalamudApi.PluginLog.Warning($"hotbar.Slots.IsEmpty");
                        return;
                    }

                    var tableFlags = ImGuiTableFlags.RowBg | ImGuiTableFlags.PadOuterX |
                        ImGuiTableFlags.NoSavedSettings | ImGuiTableFlags.BordersInnerV;
                    var tableColumnCount = 5;

                    if (ImGui.BeginTable($"##HotbarTable_{hotbarIndex}", tableColumnCount, tableFlags)) {
                        ImGui.TableSetupColumn("  ", ImGuiTableColumnFlags.WidthFixed);
                        ImGui.TableSetupColumn("Type", ImGuiTableColumnFlags.WidthFixed);
                        ImGui.TableSetupColumn("ID", ImGuiTableColumnFlags.WidthFixed);
                        ImGui.TableSetupColumn("Icon", ImGuiTableColumnFlags.WidthFixed);
                        ImGui.TableSetupColumn("Name", ImGuiTableColumnFlags.WidthStretch);

                        // hotbar.GetHotbarSlot(slotIndex);

                        for (var slotIndex = 0; slotIndex < hotbar.Slots.Length; slotIndex++) {
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
                            if (ImGui.IsItemClicked()) {
                                ImGui.SetClipboardText($"{slot.ApparentActionId}");
                                DalamudApi.ShowNotification(Language.ClipboardCopyMessage, NotificationType.Info, 5000);
                            }
                            ImGuiUtil.ToolTip(Language.ClickToCopy);

                            ImGui.TableNextColumn();
                            var icon = DalamudApi.TextureProvider.GetFromGameIcon(slot.IconId).GetWrapOrEmpty().Handle;
                            var iconSize = ImGuiHelpers.ScaledVector2(30, 30);
                            ImGui.TextUnformatted($"{slot.IconId}");
                            ImGui.Image(icon, iconSize);
                            if (ImGui.IsItemClicked()) {
                                HotbarManager.ExecuteHotbarActionByIndex((uint)hotbarIndex, (uint)slotIndex);
                            }
                            ImGuiUtil.ToolTip(Language.ClickToExecute);

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

    private unsafe void DrawPetHotbarDebugTab() {
        if (RaptureHotbarModule.Instance()->PetHotbar.Slots.IsEmpty) {
            DalamudApi.PluginLog.Warning($"petHotbar.Slots");
            return;
        }

        if (ImGui.BeginTabItem($"Pet Hotbar###PetHotbarsDebugTab")) {
            if (ImGui.CollapsingHeader($"Pet Hotbar")) {
                var tableFlags = ImGuiTableFlags.RowBg | ImGuiTableFlags.PadOuterX |
                    ImGuiTableFlags.NoSavedSettings | ImGuiTableFlags.BordersInnerV;
                var tableColumnCount = 5;

                if (ImGui.BeginTable($"##PetHotbarTable", tableColumnCount, tableFlags)) {
                    ImGui.TableSetupColumn("  ", ImGuiTableColumnFlags.WidthFixed);
                    ImGui.TableSetupColumn("Type", ImGuiTableColumnFlags.WidthFixed);
                    ImGui.TableSetupColumn("ID", ImGuiTableColumnFlags.WidthFixed);
                    ImGui.TableSetupColumn("Icon", ImGuiTableColumnFlags.WidthFixed);
                    ImGui.TableSetupColumn("Name", ImGuiTableColumnFlags.WidthStretch);

                    for (var slotIndex = 0; slotIndex < RaptureHotbarModule.Instance()->PetHotbar.Slots.Length; slotIndex++) {
                        var slot = RaptureHotbarModule.Instance()->PetHotbar.Slots[slotIndex];

                        ImGui.PushID(slotIndex);
                        ImGui.TableNextRow();
                        ImGui.TableNextColumn();
                        ImGui.TextUnformatted($"{slotIndex + 1:000}");

                        ImGui.TableNextColumn();
                        ImGui.TextUnformatted($"{slot.CommandType} - ({slot.ApparentSlotType})");

                        ImGui.TableNextColumn();
                        ImGui.TextUnformatted($"{slot.CommandId} - ({slot.ApparentActionId})");
                        if (ImGui.IsItemClicked()) {
                            ImGui.SetClipboardText($"{slot.ApparentActionId}");
                            DalamudApi.ShowNotification(Language.ClipboardCopyMessage, NotificationType.Info, 5000);
                        }
                        ImGuiUtil.ToolTip(Language.ClickToCopy);

                        ImGui.TableNextColumn();
                        var icon = DalamudApi.TextureProvider.GetFromGameIcon(slot.IconId).GetWrapOrEmpty().Handle;
                        var iconSize = ImGuiHelpers.ScaledVector2(30, 30);
                        ImGui.TextUnformatted($"{slot.IconId}");
                        ImGui.Image(icon, iconSize);
                        if (ImGui.IsItemClicked()) {
                            HotbarManager.ExecutePetHotbarActionByIndex((uint)slotIndex);
                        }
                        ImGuiUtil.ToolTip(Language.ClickToExecute);

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

    public static bool SearchableCombo<T>(string id, [NotNullWhen(true)] out T? selected, string preview, IEnumerable<T> possibilities, Func<T, string> toName, Func<T, string, bool> searchPredicate, Func<T, bool> preFilter, ImGuiComboFlags flags = ImGuiComboFlags.None) where T : notnull {
        _comboDic.TryAdd(id, (false, false));
        (var toggle, var wasEnterClickedLastTime) = _comboDic[id];
        selected = default;
        if (!ImGui.BeginCombo(id + (toggle ? "##x" : ""), preview, flags)) return false;

        if (wasEnterClickedLastTime || ImGui.IsKeyPressed(ImGuiKey.Escape)) {
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
        if (ImGui.IsWindowAppearing() && ImGui.IsWindowFocused() && !ImGui.IsAnyItemActive()) {
            _search = string.Empty;
            _filtered = null;
            ImGui.SetKeyboardFocusHere(0);
        }

        if (ImGui.InputText("##ExcelSheetComboSearch", ref _search, 128))
            _filtered = null;

        if (_filtered == null) {
            _filtered = possibilities.Where(preFilter).Where(s => searchPredicate(s, _search)).Cast<object>().ToHashSet();
            _hoveredItem = 0;
        }

        var i = 0;
        foreach (var row in _filtered.Cast<T>()) {
            var hovered = _hoveredItem == i;
            ImGui.PushID(i);
            if (ImGui.Selectable(toName(row), hovered) || (enterClicked && hovered)) {
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

    // /// <summary>
    // /// Draw a "picker" popup to chose a plugin.
    // /// </summary>
    // /// <param name="id">The ID of the popup.</param>
    // /// <param name="pickerSearch">String holding the search input.</param>
    // /// <param name="onClicked">Action to be called if a plugin is clicked.</param>
    // /// <param name="pluginDisabled">Function that should return true if a plugin should show as disabled.</param>
    // /// <param name="pluginFiltered">Function that should return true if a plugin should not appear in the list.</param>
    // /// <returns>An ImGuiID to open the popup.</returns>
    // internal static uint DrawPluginPicker(string id, ref string pickerSearch, Action<LocalPlugin> onClicked, Func<LocalPlugin, bool> pluginDisabled, Func<LocalPlugin, bool>? pluginFiltered = null)
    // {
    //     var pm = Service<PluginManager>.GetNullable();
    //     if (pm == null)
    //         return 0;

    //     var addPluginToProfilePopupId = ImGui.GetID(id);
    //     using var popup = ImRaii.Popup(id);

    //     if (popup.Success)
    //     {
    //         var width = ImGuiHelpers.GlobalScale * 300;

    //         ImGui.SetNextItemWidth(width);
    //         ImGui.InputTextWithHint("###pluginPickerSearch"u8, Locs.SearchHint, ref pickerSearch, 255);

    //         var currentSearchString = pickerSearch;

    //         using var listBox = ImRaii.ListBox("###pluginPicker"u8, new Vector2(width, width - 80));
    //         if (listBox.Success)
    //         {
    //             // TODO: Plugin searching should be abstracted... installer and this should use the same search
    //             var plugins = pm.InstalledPlugins.Where(
    //                                 x => x.Manifest.SupportsProfiles &&
    //                                      (currentSearchString.IsNullOrWhitespace() || x.Manifest.Name.Contains(
    //                                           currentSearchString,
    //                                           StringComparison.InvariantCultureIgnoreCase)))
    //                             .Where(pluginFiltered ?? (_ => true));

    //             foreach (var plugin in plugins)
    //             {
    //                 using var disabled2 = ImRaii.Disabled(pluginDisabled(plugin));
    //                 if (ImGui.Selectable($"{plugin.Manifest.Name}{(plugin is LocalDevPlugin ? "(dev plugin)" : string.Empty)}###selector{plugin.Manifest.InternalName}"))
    //                 {
    //                     onClicked(plugin);
    //                 }
    //             }
    //         }
    //     }

    //     return addPluginToProfilePopupId;
    // }
    private void DrawSpinner() {
        var spinnerLabel = $"##Spinner_{1}";
        // var spinnerRadius = ImGui.GetTextLineHeight() / 4;
        var spinnerRadius = ImGui.GetTextLineHeight();
        var spinnerThickness = 5 * ImGuiHelpers.GlobalScale;
        ImGui.SetCursorPosY(ImGui.GetCursorPosY() + spinnerRadius);
        ImGuiUtil.Spinner(spinnerLabel, spinnerRadius, spinnerThickness, Style.Colors.Blue);
    }

    private void DrawIconPicker() {
        var iconSize = ImGuiHelpers.ScaledVector2(50, 50);
        // var icon = DalamudApi.TextureProvider.GetFromGameIcon(undefinedIconId).GetWrapOrEmpty().Handle;
        var icon = DalamudApi.TextureProvider.GetMacroIcon(_macroIconId).GetWrapOrEmpty().Handle;
        ImGui.Image(icon, iconSize);
        if (ImGui.IsItemClicked()) {
            Ui.IconPickerDialogWindow.Open(_macroIconId, selectedIconId => {
                _macroIconId = selectedIconId;
                DalamudApi.PluginLog.Warning($"selectedIconId: {selectedIconId}");
            });
        }
    }

    private void DrawConfirmModalDialog() {
        if (ImGui.Button("Delete"))
            ImGui.OpenPopup("##DeleteConfirmPopup");

        var viewport = ImGui.GetMainViewport();
        Vector2 center = viewport.GetCenter();
        ImGui.SetNextWindowPos(center, ImGuiCond.Appearing, new Vector2(0.5f, 0.5f));

        if (ImGui.BeginPopupModal("##DeleteConfirmPopup", ImGuiWindowFlags.AlwaysAutoResize)) {
            ImGui.Text("All those beautiful files will be deleted.\nThis operation cannot be undone!");
            ImGui.Separator();

            bool dontAskNextTime = false;
            ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(0, 0));
            ImGui.Checkbox("Don't ask me next time", ref dontAskNextTime);
            ImGui.PopStyleVar();

            if (ImGui.Button("OK", new Vector2(120, 0)))
                ImGui.CloseCurrentPopup();

            ImGui.SetItemDefaultFocus();
            ImGui.SameLine();
            if (ImGui.Button("Cancel", new Vector2(120, 0)))
                ImGui.CloseCurrentPopup();

            ImGui.EndPopup();
        }
    }

    private void DrawMultilineInput() {
        if (InputTextMultiline.Draw(
            "###MacroContent",
            ref _inputTextContent,
            ushort.MaxValue,
            new Vector2(
                MathF.Min(ImGui.GetContentRegionAvail().X, 500f * ImGuiHelpers.GlobalScale),
                ImGui.GetTextLineHeight() * 20
            ),
        ImGuiInputTextFlags.None
        )) {
            // DalamudApi.PluginLog.Warning($"{_inputTextContent}");
        }
    }

    private void DrawElementsDebugTab() {
        if (ImGui.BeginTabItem($"Gui Elements###GuiElementsDebugTab")) {
            ImGui.TextUnformatted("ImGui Elements");
            DrawSpinner();

            DrawIconPicker();

            DrawConfirmModalDialog();

            DrawMultilineInput();

            ImGuiModalDialog.Draw();

            // SearchableCombo();
            if (ImGui.Button("Modal")) {
                ImGuiModalDialog.Show(
                    "Confirm",
                    "Confirm Delete?",
                    ("YES", () => {
                        // DO DELETE
                        DalamudApi.ShowNotification("Item deleted", NotificationType.Success, 3000);
                    }
                ),
                    ("NO", () => {
                        // DO NOTHING
                    }
                )
                );
            }

            ImGui.EndTabItem();
        }
    }

    private void DrawFontAwesomeIconsDebugTab() {
        if (ImGui.BeginTabItem($"FontAwesome Icons###FontAwesomeIconsDebugTab")) {
            DrawFontAwesomeIconBrowser();
            ImGui.EndTabItem();
        }
    }

    static readonly (FontAwesomeIcon icon, string name)[] glyphs =
        Enumerable.Range(0xE000, 0xF000)
        .Select(i => ((FontAwesomeIcon)i, ((FontAwesomeIcon)i).ToString()))
        .Where(i => i.Item2.Any(char.IsLetter)) // remove unknown icons
        .ToArray();

    private static (FontAwesomeIcon icon, string name)[] searchedGlyphs = glyphs;
    private static string searchedString = string.Empty;

    private static void DrawFontAwesomeIconBrowser() {
        ImGui.Spacing();

        if (ImGui.InputTextWithHint("##FontAwesomeSearchInput", "Search", ref searchedString, 100)) {
            if (!string.IsNullOrWhiteSpace(searchedString)) {
                searchedGlyphs = glyphs.Where(i => i.name.ContainsIgnoreCase(searchedString)).ToArray();
            } else {
                searchedGlyphs = glyphs;
            }
        }

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();
        ImGui.Spacing();

        ImGui.PushFont(UiBuilder.IconFont);
        var windowWidth = ImGui.GetWindowWidth() - 60 * ImGui.GetIO().FontGlobalScale;
        var lineLength = 0f;

        foreach (var icon in searchedGlyphs) {
            ImGui.TextUnformatted(icon.icon.ToIconString());

            if (ImGui.IsItemHovered()) {
                ImGui.BeginTooltip();
                ImGui.SetWindowFontScale(3);
                ImGui.TextUnformatted(icon.icon.ToIconString());
                ImGui.SetWindowFontScale(1);
                ImGui.PushFont(UiBuilder.DefaultFont);
                ImGui.TextUnformatted($"{icon.name}\n{(int)icon.icon}\n0x{(int)icon.icon:X}");
                ImGui.EndTooltip();
                ImGui.PopFont();
            }

            if (ImGui.IsItemClicked()) {
                ImGui.SetClipboardText($"(FontAwesomeIcon){(int)icon.Item1}");
            }

            if (lineLength < windowWidth) {
                lineLength += 30 * ImGui.GetIO().FontGlobalScale;
                ImGui.SameLine(lineLength);
            } else {
                lineLength = 0;
                ImGui.Dummy(new Vector2(0, 10 * ImGui.GetIO().FontGlobalScale));
            }
        }

        ImGui.PopFont();
    }
}
