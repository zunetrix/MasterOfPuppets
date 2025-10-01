using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.ImGuiNotification;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Windowing;

using MasterOfPuppets.Resources;
using MasterOfPuppets.Util.ImGuiExt;

namespace MasterOfPuppets;

internal class MainWindow : Window {
    private Plugin Plugin { get; }
    private PluginUi Ui { get; }
    public bool IsVisible { get; private set; }
    // private static readonly Version Version = typeof(MainWindow).Assembly.GetName().Version;
    // private static readonly string VersionString = Version?.ToString();

    private string _macroSearchString = string.Empty;
    private readonly List<int> MacroListSearchedIndexes = new();

    internal MainWindow(Plugin plugin, PluginUi ui) : base(Plugin.Name) {
        Plugin = plugin;
        Ui = ui;

        Size = ImGuiHelpers.ScaledVector2(300, 250);
        SizeCondition = ImGuiCond.FirstUseEver;
        UpdateWindowConfig();
    }

    public override void Update() {
        IsVisible = false;
        base.Update();
    }

    public override void PreDraw() {
        // Flags = ImGuiWindowFlags.None;
        Flags = ImGuiWindowFlags.MenuBar;
        if (!Plugin.Config.AllowMovement) {
            Flags |= ImGuiWindowFlags.NoMove;
        }

        if (!Plugin.Config.AllowResize) {
            Flags |= ImGuiWindowFlags.NoResize;
        }

        base.PreDraw();
    }

    public override bool DrawConditions() {
        // var inCombat = DalamudApi.Condition[ConditionFlag.InCombat];
        // var inInstance = DalamudApi.Condition[ConditionFlag.BoundByDuty]
        //                  || DalamudApi.Condition[ConditionFlag.BoundByDuty56]
        //                  || DalamudApi.Condition[ConditionFlag.BoundByDuty95];
        // var inCutscene = DalamudApi.Condition[ConditionFlag.WatchingCutscene]
        //                  || DalamudApi.Condition[ConditionFlag.WatchingCutscene78]
        //                  || DalamudApi.Condition[ConditionFlag.OccupiedInCutSceneEvent];

        // if (inCombat && !Plugin.Config.ShowInCombat) return false;
        // if (inInstance && !Plugin.Config.ShowInInstance) return false;
        // if (inCutscene && !Plugin.Config.ShowInCutscenes) return false;

        return true;
    }

    public override void Draw() {
        IsVisible = true;

        // prevent change macro index while editing
        ImGui.BeginDisabled(Ui.MacroEditorWindow.IsOpen);
        DrawMenuBar();

        ImGui.BeginChild("##MopHeaderFixedHeight", new Vector2(-1, 60 * ImGuiHelpers.GlobalScale), false, ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse);
        DrawMacroHeader();
        ImGui.EndChild();

        ImGui.BeginChild("##MopMacroListScrollableContent", new Vector2(-1, 0), false, ImGuiWindowFlags.HorizontalScrollbar);
        DrawMacrosTable();
        ImGui.EndChild();

        ImGui.EndDisabled();
    }

    private void ImportMacroFromClipboard() {
        try {
            string macroImportString = ImGui.GetClipboardText();
            Plugin.MacroManager.ImportMacroFromString(macroImportString);
            Plugin.IpcProvider.SyncConfiguration();
            DalamudApi.ShowNotification($"Macro imported", NotificationType.Success, 5000);
        }
        catch {
            DalamudApi.ShowNotification($"Unable to import invalid macro", NotificationType.Error, 5000);
        }
    }

    private void DrawMenuBar() {
        ImGui.PushStyleColor(ImGuiCol.Border, Style.Components.TooltipBorderColor);
        ImGui.PushStyleVar(ImGuiStyleVar.PopupBorderSize, 1);

        if (ImGui.BeginMenuBar()) {
            if (ImGui.BeginMenu("Macro")) {
                if (ImGuiUtil.IconButton(FontAwesomeIcon.Plus, $"##AddMacroMenu")) {
                    Ui.MacroEditorWindow.AddNewMacro();
                }
                ImGui.SameLine();
                if (ImGui.Selectable(Language.AddMacroBtn)) {
                    Ui.MacroEditorWindow.AddNewMacro();
                }

                if (ImGuiUtil.IconButton(FontAwesomeIcon.Trash, $"##DeleteSelectedMacrosMenu")) {
                    if (ImGui.GetIO().KeyCtrl) {
                        Plugin.MacroManager.DeleteSelectedMacros();
                        Plugin.IpcProvider.SyncConfiguration();
                    }
                }
                ImGui.SameLine();
                if (ImGui.Selectable(Language.DeleteSelectedMacrosBtn)) {
                    if (ImGui.GetIO().KeyCtrl) {
                        Plugin.MacroManager.DeleteSelectedMacros();
                        Plugin.IpcProvider.SyncConfiguration();
                    }
                }
                ImGuiUtil.ToolTip(Language.DeleteInstructionTooltip);

                // -----------------------

                if (ImGuiUtil.IconButton(FontAwesomeIcon.FileExport, $"##MacroImportExportMenu")) {
                    Ui.MacroImportExportWindow.Toggle();
                }
                ImGui.SameLine();
                if (ImGui.Selectable("Import Export Macros")) {
                    Ui.MacroImportExportWindow.Toggle();
                }

                // -----------------------

                if (ImGuiUtil.IconButton(FontAwesomeIcon.FileImport, $"##ImportFromClipboardMenu")) {
                    ImportMacroFromClipboard();
                }
                ImGui.SameLine();
                if (ImGui.Selectable(Language.ImportMacroBtn)) {
                    ImportMacroFromClipboard();
                }

                // -----------------------

                if (ImGuiUtil.IconButton(FontAwesomeIcon.Users, $"##CharactersMenu")) {
                    Ui.CharactersWindow.Toggle();
                }
                ImGui.SameLine();
                if (ImGui.Selectable(Language.ShowCharactersBtn)) {
                    Ui.CharactersWindow.Toggle();
                }

                ImGui.EndMenu();
            }

            if (ImGui.BeginMenu("Actions")) {
                if (ImGuiUtil.IconButton(FontAwesomeIcon.SmileWink, $"##ShowEmotesMenu")) {
                    Ui.EmotesWindow.Toggle();
                }
                ImGui.SameLine();
                if (ImGui.Selectable(Language.ShowEmotesBtn)) {
                    Ui.EmotesWindow.Toggle();
                }

                // -----------------------

                if (ImGuiUtil.IconButton(FontAwesomeIcon.Umbrella, $"##ShowFashionAccessoriesMenu")) {
                    Ui.FashionAccessoriesWindow.Toggle();
                }
                ImGui.SameLine();
                if (ImGui.Selectable(Language.ShowFashionAccessoriesBtn)) {
                    Ui.FashionAccessoriesWindow.Toggle();
                }

                // -----------------------

                if (ImGuiUtil.IconButton(FontAwesomeIcon.Glasses, $"##ShowFacewearMenu")) {
                    Ui.FacewearWindow.Toggle();
                }
                ImGui.SameLine();
                if (ImGui.Selectable(Language.ShowFacewearBtn)) {
                    Ui.FacewearWindow.Toggle();
                }

                // -----------------------

                if (ImGuiUtil.IconButton(FontAwesomeIcon.Horse, $"##ShowMountMenu")) {
                    Ui.MountWindow.Toggle();
                }
                ImGui.SameLine();
                if (ImGui.Selectable(Language.ShowMountBtn)) {
                    Ui.MountWindow.Toggle();
                }

                // -----------------------

                if (ImGuiUtil.IconButton(FontAwesomeIcon.Cat, $"##ShowMinionMenu")) {
                    Ui.MinionWindow.Toggle();
                }
                ImGui.SameLine();
                if (ImGui.Selectable(Language.ShowMinionBtn)) {
                    Ui.MinionWindow.Toggle();
                }

                // -----------------------

                if (ImGuiUtil.IconButton(FontAwesomeIcon.Briefcase, $"##ShowItemMenu")) {
                    Ui.ItemWindow.Toggle();
                }
                ImGui.SameLine();
                if (ImGui.Selectable(Language.ShowItemBtn)) {
                    Ui.ItemWindow.Toggle();
                }

                // -----------------------

                if (ImGuiUtil.IconButton(FontAwesomeIcon.Crosshairs, $"##ExecuteTargetMyTargetCommand")) {
                    Plugin.IpcProvider.ExecuteTargetMyTarget();
                }
                ImGui.SameLine();
                if (ImGui.Selectable("Target My Target")) {
                    Plugin.IpcProvider.ExecuteTargetMyTarget();
                }
                // -----------------------

                if (ImGuiUtil.IconButton(FontAwesomeIcon.Times, $"##ExecuteTargetClearCommand")) {
                    Plugin.IpcProvider.ExecuteTargetClear();
                }
                ImGui.SameLine();
                if (ImGui.Selectable("Target Clear")) {
                    Plugin.IpcProvider.ExecuteTargetClear();
                }

                ImGui.EndMenu();
            }

            if (ImGui.MenuItem("Help")) {
                Plugin.Ui.MacroHelpWindow.Toggle();
            }

            ImGui.EndMenuBar();
            ImGui.PopStyleVar();
            ImGui.PopStyleColor();
        }
    }

    private void DrawMacroHeader() {
        // align right
        float spacing = ImGui.GetStyle().ItemSpacing.X;
        float buttonWidth = ImGui.GetFrameHeight();
        // int buttonCount = 6;
        float marginRight = 15f * ImGuiHelpers.GlobalScale;
        // float totalButtonsWidth = (buttonWidth * buttonCount) + (spacing * (buttonCount - 1)) + marginRight;

        // ImGui.SetCursorPosX(ImGui.GetWindowContentRegionMax().X - totalButtonsWidth);
        // ImGui.SameLine(ImGui.GetWindowContentRegionMax().X - totalButtonsWidth);
        // if (ImGuiUtil.IconButton(FontAwesomeIcon.SmileWink, $"##ShowEmotesBtn", Language.ShowEmotesBtn))
        // {
        //     Ui.EmotesWindow.Toggle();
        // }

        // ImGui.SameLine();
        // if (ImGuiUtil.IconButton(FontAwesomeIcon.Umbrella, $"##ShowFashionAccessoriesBtn", Language.ShowFashionAccessoriesBtn))
        // {
        //     Ui.FashionAccessoriesWindow.Toggle();
        // }

        // ImGui.SameLine();
        // if (ImGuiUtil.IconButton(FontAwesomeIcon.Glasses, $"##ShowFacewearBtn", Language.ShowFacewearBtn))
        // {
        //     Ui.FacewearWindow.Toggle();
        // }

        // ImGui.SameLine();
        // if (ImGuiUtil.IconButton(FontAwesomeIcon.Horse, $"##ShowMountBtn", Language.ShowMountBtn))
        // {
        //     Ui.MountWindow.Toggle();
        // }

        // ImGui.SameLine();
        // if (ImGuiUtil.IconButton(FontAwesomeIcon.Cat, $"##ShowMinionBtn", Language.ShowMinionBtn))
        // {
        //     Ui.MinionWindow.Toggle();
        // }

        // ImGui.SameLine();
        // if (ImGuiUtil.IconButton(FontAwesomeIcon.Briefcase, $"##ShowItemBtn", Language.ShowItemBtn))
        // {
        //     Ui.ItemWindow.Toggle();
        // }

        // ImGui.Spacing();
        // ImGui.Separator();
        // ImGui.Spacing();

        ImGui.TextUnformatted(Language.MacroListTitle);

        if (ImGui.InputTextWithHint("##MacroSearchInput", Language.MacroSearchInputLabel, ref _macroSearchString, 255, ImGuiInputTextFlags.AutoSelectAll)) {
            SearchMacro();
        }

        int buttonMacroCount = 3;
        float totalButtonsMacroWidth = (buttonWidth * buttonMacroCount) + (spacing * (buttonMacroCount - 1)) + marginRight;

        ImGui.SameLine(ImGui.GetWindowContentRegionMax().X - totalButtonsMacroWidth);
        if (ImGuiUtil.IconButton(FontAwesomeIcon.Plus, $"##AddMacroBtn", Language.AddMacroBtn)) {
            Ui.MacroEditorWindow.AddNewMacro();
        }

        // ImGui.SameLine();
        // if (ImGuiUtil.IconButton(FontAwesomeIcon.FileImport, $"##ImportMacroFromClipboardBtn", Language.ImportMacroBtn))
        // {
        //     ImportMacroFromClipboard();
        // }

        // ImGui.SameLine();
        // if (ImGuiUtil.IconButton(FontAwesomeIcon.Users, $"##ShowCharactersBtn", Language.ShowCharactersBtn))
        // {
        //     Ui.CharactersWindow.Toggle();
        // }

        ImGui.SameLine();
        ImGui.PushStyleColor(ImGuiCol.Button, Style.Components.ButtonDangerNormal);
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, Style.Components.ButtonDangerHovered);
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, Style.Components.ButtonDangerActive);
        if (ImGuiUtil.IconButton(FontAwesomeIcon.Stop, $"##StopMacroExecutionBtn", Language.StopMacroExecutionBtn)) {
            Plugin.IpcProvider.StopMacroExecution();
            DalamudApi.ShowNotification($"Macro execution queue stoped", NotificationType.Info, 3000);
        }
        ImGui.PopStyleColor(3);

        ImGui.SameLine();
        if (ImGuiUtil.IconButton(FontAwesomeIcon.List, $"##ShowMacroQueueBtn", Language.ShowMacroQueueBtn)) {
            Ui.MacroQueueWindow.Toggle();
        }

        ImGui.Spacing();
        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();
        ImGui.Spacing();
    }

    private void SearchMacro() {
        MacroListSearchedIndexes.Clear();

        MacroListSearchedIndexes.AddRange(
            Plugin.Config.Macros
            .Select((item, index) => new { item, index })
            .Where(x => x.item.Name.Contains(_macroSearchString, StringComparison.OrdinalIgnoreCase))
            .Select(x => x.index)
            .ToList()
        );
    }

    private void DrawMacroEntry(int macroIdx) {
        var macro = Plugin.Config.Macros[macroIdx];
        ImGui.PushID(macroIdx);
        ImGui.TableNextRow();
        ImGui.TableSetColumnIndex(0);
        // ImGui.TableNextColumn();
        bool isChecked = Plugin.MacroManager.SelectedMacrosIndexes.Contains(macroIdx);
        if (ImGui.Checkbox($"##SelectedMacroCheck_{macroIdx}", ref isChecked)) {
            if (isChecked)
                Plugin.MacroManager.SelectedMacrosIndexes.Add(macroIdx);
            else
                Plugin.MacroManager.SelectedMacrosIndexes.Remove(macroIdx);
        }

        ImGui.TableNextColumn();
        ImGui.TextUnformatted($"{macroIdx + 1:000}");

        ImGui.TableNextColumn();
        ImGui.Selectable($"{macro.Name}");
        ImGuiUtil.ToolTip("Drag to reorder");

        if (ImGui.BeginDragDropSource()) {
            unsafe {
                ImGui.SetDragDropPayload("DND_MACROS_TABLE", new ReadOnlySpan<byte>(&macroIdx, sizeof(int)), ImGuiCond.None);
                ImGui.Button($"({macroIdx + 1}) {macro.Name}");
            }

            // PluginLog.Warning($"Drag start [{i}]");
            ImGui.EndDragDropSource();
        }

        ImGui.PushStyleColor(ImGuiCol.DragDropTarget, Style.Components.DragDropTarget);
        if (ImGui.BeginDragDropTarget()) {
            ImGuiPayloadPtr dragDropPayload = ImGui.AcceptDragDropPayload("DND_MACROS_TABLE");

            bool isDropping = false;
            unsafe {
                isDropping = !dragDropPayload.IsNull;
            }

            if (isDropping && dragDropPayload.IsDelivery()) {
                unsafe {
                    int originalIndex = *(int*)dragDropPayload.Data;

                    int offset = macroIdx - originalIndex;
                    if (offset != 0 && originalIndex + offset >= 0) {
                        int targetIndex = originalIndex + offset;
                        // PluginLog.Warning($"Drag end [{i}]: [{originalIndex}, {targetIndex}] {offset}");
                        Plugin.MacroManager.MoveMacroToIndex(originalIndex, targetIndex);
                        Plugin.MacroManager.SelectedMacrosIndexes.Clear();
                        Plugin.IpcProvider.SyncConfiguration();
                    }
                }
            }

            ImGui.EndDragDropTarget();
        }
        ImGui.PopStyleColor();

        ImGui.TableNextColumn();
        if (ImGuiUtil.IconButton(FontAwesomeIcon.Trash, $"##DeleteMacro_{macroIdx}", Language.DeleteInstructionTooltip)) {
            if (ImGui.GetIO().KeyCtrl) {
                Plugin.MacroManager.DeleteMacro(macroIdx);
                Plugin.IpcProvider.SyncConfiguration();
            }
        }

        ImGui.SameLine();
        if (ImGuiUtil.IconButton(FontAwesomeIcon.ArrowUpRightFromSquare, $"##ExportMacro_{macroIdx}", Language.ExportMacroBtn)) {
            var macroExportData = Plugin.MacroManager.ExportMacroToString(macroIdx, includeCids: false);
            ImGui.SetClipboardText(macroExportData);
            DalamudApi.ShowNotification(Language.ClipboardCopyMessage, NotificationType.Info, 5000);
        }
        ImGui.OpenPopupOnItemClick("ContextMenuExportMacro", ImGuiPopupFlags.MouseButtonRight);

        if (ImGui.BeginPopup("ContextMenuExportMacro")) {
            if (ImGui.MenuItem("Export with characters")) {
                var macroExportData = Plugin.MacroManager.ExportMacroToString(macroIdx, includeCids: true);
                ImGui.SetClipboardText(macroExportData);
                DalamudApi.ShowNotification(Language.ClipboardCopyMessage, NotificationType.Info, 5000);
            }
            ImGui.EndPopup();
        }

        ImGui.SameLine();
        if (ImGuiUtil.IconButton(FontAwesomeIcon.Copy, $"##CloneMacro_{macroIdx}", Language.CloneMacroBtn)) {
            Plugin.MacroManager.CloneMacro(macroIdx);
            Plugin.IpcProvider.SyncConfiguration();
        }

        ImGui.SameLine();
        if (ImGuiUtil.IconButton(FontAwesomeIcon.Edit, $"##EditMacro_{macroIdx}", Language.EditMacroBtn)) {
            Ui.MacroEditorWindow.EditMacro(macroIdx);
        }

        ImGui.SameLine();

        if (ImGuiUtil.IconButton(FontAwesomeIcon.Play, $"##RunMacro_{macroIdx}", Language.RunMacroBtn)) {
            Plugin.IpcProvider.RunMacro(macroIdx);
        }
        ImGui.OpenPopupOnItemClick("ContextMenuRunMacro", ImGuiPopupFlags.MouseButtonRight);

        if (ImGui.BeginPopup("ContextMenuRunMacro")) {
            if (ImGui.MenuItem("Copy Run Command")) {
                ImGui.SetClipboardText($"/mop run \"{macro.Name}\"");
                DalamudApi.ShowNotification(Language.ClipboardCopyMessage, NotificationType.Info, 5000);
            }
            if (ImGui.MenuItem("Copy Chat Sync Command")) {
                ImGui.SetClipboardText($"moprun \"{macro.Name}\"");
                DalamudApi.ShowNotification(Language.ClipboardCopyMessage, NotificationType.Info, 5000);
            }
            ImGui.EndPopup();
        }

        ImGui.PopID();
    }

    private void DrawMacrosTable() {
        var isFiltered = !string.IsNullOrEmpty(_macroSearchString);
        var noSearchResults = MacroListSearchedIndexes.Count == 0;
        if (isFiltered && noSearchResults) {
            ImGuiUtil.DrawColoredBanner("Nothing found", Style.Colors.Red);
        }

        var tableFlags = ImGuiTableFlags.RowBg | ImGuiTableFlags.PadOuterX |
                ImGuiTableFlags.NoSavedSettings | ImGuiTableFlags.BordersInnerV;
        var tableColumnCount = 4;
        var itemCount = isFiltered ? MacroListSearchedIndexes.Count : Plugin.Config.Macros.Count;

        if (ImGui.BeginTable("##MacrosTable", tableColumnCount, tableFlags)) {
            ImGui.TableSetupColumn("##CheckMacro", ImGuiTableColumnFlags.WidthFixed);
            ImGui.TableSetupColumn("#", ImGuiTableColumnFlags.WidthFixed);
            ImGui.TableSetupColumn("Macro", ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableSetupColumn("Options", ImGuiTableColumnFlags.WidthFixed);

            ImGuiListClipperPtr clipper;
            unsafe {
                clipper = new ImGuiListClipperPtr(ImGuiNative.ImGuiListClipper());
            }

            clipper.Begin(itemCount);

            while (clipper.Step()) {
                for (int i = clipper.DisplayStart; i < clipper.DisplayEnd; i++) {
                    if (i >= itemCount) break;
                    int realIndex = isFiltered ? MacroListSearchedIndexes[i] : i;
                    if (realIndex >= Plugin.Config.Macros.Count) continue;

                    DrawMacroEntry(realIndex);
                }
            }
            clipper.End();
            ImGui.EndTable();
        }

        ImGui.Spacing();
        ImGui.Spacing();
        ImGui.Spacing();
    }

    internal void UpdateWindowConfig() {
        RespectCloseHotkey = Plugin.Config.AllowCloseWithEscape;

        TitleBarButtons.Clear();
        if (Plugin.Config.ShowSettingsButton) {
            TitleBarButtons.Add(new TitleBarButton() {
                AvailableClickthrough = false,
                Icon = FontAwesomeIcon.Cog,
                Click = _ => Ui.SettingsWindow.Toggle()
            });

            TitleBarButtons.Add(new TitleBarButton() {
                AvailableClickthrough = false,
                Icon = FontAwesomeIcon.Heart,
                // Click = _ => Ui.SettingsWindow.Toggle()
            });

#if DEBUG
            TitleBarButtons.Add(new TitleBarButton() {
                AvailableClickthrough = false,
                Icon = FontAwesomeIcon.Bug,
                Click = _ => Ui.DebugWindow.Toggle()
            });
#endif
        }
    }
}
