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
using MasterOfPuppets.Util;
using MasterOfPuppets.Util.ImGuiExt;

namespace MasterOfPuppets;

public class MainWindow : Window {
    private Plugin Plugin { get; }
    private PluginUi Ui { get; }
    public bool IsVisible { get; private set; }
    private static readonly Version Version = typeof(MainWindow).Assembly.GetName().Version;
    // private static readonly string VersionString = Version?.ToString();

    private string _macroSearchString = string.Empty;
    private readonly List<int> MacroListSearchedIndexes = new();
    private readonly HashSet<string> _selectedTags = new(StringComparer.OrdinalIgnoreCase);
    private bool _filterNoTags = false;
    private float _leftPanelWidth = 200f;
    private bool _showTagsPanel = true;
    private bool _isGlobalMacroCheckboxChecked = false;

    internal MainWindow(Plugin plugin, PluginUi ui) : base($"{Plugin.Name}") {
        Plugin = plugin;
        Ui = ui;

        Size = ImGuiHelpers.ScaledVector2(600, 400);
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

        ImGui.BeginGroup();
        DrawMacroHeader();
        ImGui.EndGroup();

        ImGui.BeginChild("##MopMacroListScrollableContent", new Vector2(-1, 0), false, ImGuiWindowFlags.HorizontalScrollbar);
        DrawMacroPanels();
        // DrawMacrosTable();
        ImGui.EndChild();

        ImGui.EndDisabled();
    }

    private void ImportMacroFromClipboard() {
        try {
            string macroImportString = ImGui.GetClipboardText();
            Plugin.MacroManager.ImportMacroFromString(macroImportString);
            Plugin.IpcProvider.SyncConfiguration();
            DalamudApi.ShowNotification($"Macro imported", NotificationType.Success, 5000);
        } catch {
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

                if (ImGuiUtil.IconButton(FontAwesomeIcon.ExchangeAlt, $"##MacroImportExportMenu")) {
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

                if (ImGuiUtil.IconButton(FontAwesomeIcon.FilePen, $"##MacroBatchEditorMenu")) {
                    Ui.MacroBatchEditorWindow.Toggle();
                }
                ImGui.SameLine();
                if (ImGui.Selectable(Language.MacroBatchEditorTitle)) {
                    Ui.MacroBatchEditorWindow.Toggle();
                }

                // -----------------------

                if (ImGuiUtil.IconButton(FontAwesomeIcon.FileArchive, $"##MacroBackupMenu")) {
                    Plugin.MacroManager.BackupMacros();
                }
                ImGui.SameLine();
                if (ImGui.Selectable(Language.MacroBackup)) {
                    Plugin.MacroManager.BackupMacros();
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

                // -----------------------

                if (ImGuiUtil.IconButton(FontAwesomeIcon.PersonWalkingArrowRight, $"##ExecuteAbandonDutyCommand")) {
                    Plugin.IpcProvider.ExecuteAbandonDuty();
                }
                ImGui.SameLine();
                if (ImGui.Selectable("Abandon Duty")) {
                    Plugin.IpcProvider.ExecuteAbandonDuty();
                }

                ImGui.EndMenu();
            }

            if (ImGui.MenuItem("Help")) {
                Plugin.Ui.MacroHelpWindow.Toggle();
            }

            var versionText = $"v{Version}";
            var textSize = ImGui.CalcTextSize(versionText);
            var padding = ImGui.GetStyle().FramePadding.X + 5;
            var regionMaxX = ImGui.GetWindowContentRegionMax().X;
            // align to right
            ImGui.SameLine(regionMaxX - textSize.X - (padding * 2));
            ImGui.TextUnformatted(versionText);

            ImGui.EndMenuBar();
        }

        ImGui.PopStyleVar();
        ImGui.PopStyleColor();
    }

    private void DrawMacroHeader() {
        DrawConflictingPluginAlert();

        // align right
        float spacing = ImGui.GetStyle().ItemSpacing.X;
        float buttonWidth = ImGui.GetFrameHeight();
        float marginRight = 15f * ImGuiHelpers.GlobalScale;

        // ImGui.TextUnformatted(Language.MacroListTitle);
        // toggle left tags panel show/hide
        if (ImGuiUtil.IconButton(FontAwesomeIcon.Tags, $"##ToggleMacroTagsPanelBtn", Language.ToggleMacroTagsPanelBtn)) {
            _showTagsPanel = !_showTagsPanel;
        }

        ImGui.SameLine();

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
        var icon = DalamudApi.TextureProvider.GetFromGameIcon(macro.IconId).GetWrapOrEmpty().Handle;
        var iconSize = ImGuiHelpers.ScaledVector2(30, 30);
        ImGui.Image(icon, iconSize);
        if (macro.Tags.Count > 0) {
            ImGuiUtil.ToolTip($"{string.Join("\n", macro.Tags)}");
        }

        ImGui.TableNextColumn();
        ImGui.PushStyleColor(ImGuiCol.Text, macro.Color);
        ImGui.Selectable($"{macro.Name}");
        ImGui.PopStyleColor();

        // context menu
        ImGui.OpenPopupOnItemClick("ContextMenuMacro", ImGuiPopupFlags.MouseButtonRight);

        ImGui.PushStyleColor(ImGuiCol.Border, Style.Components.TooltipBorderColor);
        ImGui.PushStyleVar(ImGuiStyleVar.PopupBorderSize, 1);
        if (ImGui.BeginPopup("ContextMenuMacro")) {
            if (ImGui.MenuItem($"{Language.CloneMacroBtn}##CloneMacro_{macroIdx}")) {
                Plugin.MacroManager.CloneMacro(macroIdx);
                Plugin.IpcProvider.SyncConfiguration();
                DalamudApi.ShowNotification("Macro cloned", NotificationType.Info, 5000);
            }

            if (ImGui.MenuItem($"{Language.ExportMacroBtn}##ExportMacro_{macroIdx}")) {
                var macroExportData = Plugin.MacroManager.ExportMacroToString(macroIdx, includeCids: false);
                ImGui.SetClipboardText(macroExportData);
                DalamudApi.ShowNotification(Language.ClipboardCopyMessage, NotificationType.Info, 5000);
            }

            if (ImGui.MenuItem($"{Language.ExportMacroBtn} (include CIDs)##ExportMacroCids_{macroIdx}")) {
                var macroExportData = Plugin.MacroManager.ExportMacroToString(macroIdx, includeCids: true);
                ImGui.SetClipboardText(macroExportData);
                DalamudApi.ShowNotification(Language.ClipboardCopyMessage, NotificationType.Info, 5000);
            }

            ImGui.EndPopup();
        }
        ImGui.PopStyleVar();
        ImGui.PopStyleColor();

        ImGuiUtil.ToolTip("""
        Right click for more options
        Drag to reorder
        """);

        if (ImGui.BeginDragDropSource()) {
            unsafe {
                ImGui.SetDragDropPayload("DND_MACROS_TABLE", new ReadOnlySpan<byte>(&macroIdx, sizeof(int)), ImGuiCond.None);
                ImGui.PushStyleColor(ImGuiCol.Text, macro.Color);
                ImGui.Button($"({macroIdx + 1}) {macro.Name}");
                ImGui.PopStyleColor();
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

        // ImGui.SameLine();
        // if (ImGuiUtil.IconButton(FontAwesomeIcon.ArrowUpRightFromSquare, $"##ExportMacro_{macroIdx}", Language.ExportMacroBtn)) {
        //     var macroExportData = Plugin.MacroManager.ExportMacroToString(macroIdx, includeCids: false);
        //     ImGui.SetClipboardText(macroExportData);
        //     DalamudApi.ShowNotification(Language.ClipboardCopyMessage, NotificationType.Info, 5000);
        // }
        // ImGui.OpenPopupOnItemClick("ContextMenuExportMacro", ImGuiPopupFlags.MouseButtonRight);

        // if (ImGui.BeginPopup("ContextMenuExportMacro")) {
        //     if (ImGui.MenuItem("Export with characters")) {
        //         var macroExportData = Plugin.MacroManager.ExportMacroToString(macroIdx, includeCids: true);
        //         ImGui.SetClipboardText(macroExportData);
        //         DalamudApi.ShowNotification(Language.ClipboardCopyMessage, NotificationType.Info, 5000);
        //     }
        //     ImGui.EndPopup();
        // }

        // ImGui.SameLine();
        // if (ImGuiUtil.IconButton(FontAwesomeIcon.Copy, $"##CloneMacro_{macroIdx}", Language.CloneMacroBtn)) {
        //     Plugin.MacroManager.CloneMacro(macroIdx);
        //     Plugin.IpcProvider.SyncConfiguration();
        // }


        ImGui.SameLine();
        if (ImGuiUtil.IconButton(FontAwesomeIcon.Edit, $"##EditMacro_{macroIdx}", Language.EditMacroBtn)) {
            Ui.MacroEditorWindow.EditMacro(macroIdx);
        }

        ImGui.SameLine();

        if (ImGuiUtil.IconButton(FontAwesomeIcon.Play, $"##RunMacro_{macroIdx}", Language.RunMacroBtn)) {
            Plugin.IpcProvider.RunMacro(macroIdx);
        }
        ImGui.OpenPopupOnItemClick("ContextMenuRunMacro", ImGuiPopupFlags.MouseButtonRight);

        ImGui.PushStyleColor(ImGuiCol.Border, Style.Components.TooltipBorderColor);
        ImGui.PushStyleVar(ImGuiStyleVar.PopupBorderSize, 1);
        if (ImGui.BeginPopup("ContextMenuRunMacro")) {
            if (ImGui.MenuItem("Copy Run Command")) {
                ImGui.SetClipboardText($"/mop run \"{macro.Name}\"");
                DalamudApi.ShowNotification(Language.ClipboardCopyMessage, NotificationType.Info, 5000);
            }

            if (ImGui.MenuItem("Copy Chat Sync Command")) {
                var macroRunMessage = $"{Plugin.Config.DefaultChatSyncPrefix} moprun \"{macro.Name}\"";
                ImGui.SetClipboardText(macroRunMessage);
                DalamudApi.ShowNotification(Language.ClipboardCopyMessage, NotificationType.Info, 5000);
            }

            if (ImGui.MenuItem("Run Chat Sync Command")) {
                var macroRunMessage = $"{Plugin.Config.DefaultChatSyncPrefix} moprun \"{macro.Name}\"";
                Chat.SendMessage(macroRunMessage);
            }
            ImGui.EndPopup();
        }
        ImGui.PopStyleVar();
        ImGui.PopStyleColor();

        ImGui.PopID();
    }

    // private void DrawMacrosTable() {
    //     var isFiltered = !string.IsNullOrEmpty(_macroSearchString);
    //     var noSearchResults = MacroListSearchedIndexes.Count == 0;
    //     if (isFiltered && noSearchResults) {
    //         ImGuiUtil.DrawColoredBanner("Your search did not match any result", Style.Colors.Red);
    //     }

    //     var tableFlags = ImGuiTableFlags.RowBg | ImGuiTableFlags.PadOuterX |
    //             ImGuiTableFlags.NoSavedSettings | ImGuiTableFlags.BordersInnerV;
    //     var tableColumnCount = 5;
    //     var itemCount = isFiltered ? MacroListSearchedIndexes.Count : Plugin.Config.Macros.Count;

    //     if (ImGui.BeginTable("##MacrosTable", tableColumnCount, tableFlags)) {
    //         ImGui.TableSetupColumn("##CheckMacro", ImGuiTableColumnFlags.WidthFixed);
    //         ImGui.TableSetupColumn("#", ImGuiTableColumnFlags.WidthFixed);
    //         ImGui.TableSetupColumn("Icon", ImGuiTableColumnFlags.WidthFixed);
    //         ImGui.TableSetupColumn("Macro", ImGuiTableColumnFlags.WidthStretch);
    //         ImGui.TableSetupColumn("Options", ImGuiTableColumnFlags.WidthFixed);

    //         ImGuiListClipperPtr clipper;
    //         unsafe {
    //             clipper = new ImGuiListClipperPtr(ImGuiNative.ImGuiListClipper());
    //         }

    //         clipper.Begin(itemCount);

    //         while (clipper.Step()) {
    //             for (int i = clipper.DisplayStart; i < clipper.DisplayEnd; i++) {
    //                 if (i >= itemCount) break;
    //                 int realIndex = isFiltered ? MacroListSearchedIndexes[i] : i;
    //                 if (realIndex >= Plugin.Config.Macros.Count) continue;

    //                 DrawMacroEntry(realIndex);
    //             }
    //         }
    //         clipper.End();
    //         ImGui.EndTable();
    //     }

    //     ImGui.Spacing();
    //     ImGui.Spacing();
    //     ImGui.Spacing();
    // }

    private void DrawMacrosTableFiltered() {
        // Determine visible macro indexes based on search
        var isFiltered = !string.IsNullOrEmpty(_macroSearchString);
        var baseIndexes = isFiltered
            ? MacroListSearchedIndexes.ToList()
            : Enumerable.Range(0, Plugin.Config.Macros.Count).ToList();

        List<int> visibleIndexes;
        if (_filterNoTags) {
            visibleIndexes = baseIndexes
                .Where(idx => {
                    var tags = Plugin.Config.Macros[idx].Tags;
                    return tags == null || tags.Count == 0;
                })
                .ToList();
        } else if (_selectedTags.Count == 0) {
            visibleIndexes = baseIndexes;
        } else {
            visibleIndexes = baseIndexes
                .Where(idx => {
                    var tags = Plugin.Config.Macros[idx].Tags ?? new List<string>();
                    if (tags.Count == 0) return false;
                    // compare normalized trimmed tags
                    var normalized = tags.Select(t => (t ?? string.Empty).Trim()).ToList();
                    return _selectedTags.All(sel => normalized.Any(t => string.Equals(t, sel, StringComparison.OrdinalIgnoreCase)));
                })
                .ToList();
        }

        var tableFlags = ImGuiTableFlags.RowBg | ImGuiTableFlags.PadOuterX |
                ImGuiTableFlags.NoSavedSettings | ImGuiTableFlags.BordersInnerV; // ImGuiTableFlags.Resizable;

        var tableColumnCount = 5;
        var itemCount = visibleIndexes.Count;

        if (isFiltered && itemCount == 0) {
            ImGuiUtil.DrawColoredBanner("Your search did not match any result", Style.Colors.Red);
        }

        if (ImGui.BeginTable("##MacrosTableFiltered", tableColumnCount, tableFlags)) {
            ImGui.TableSetupColumn("##CheckMacro", ImGuiTableColumnFlags.WidthFixed);
            ImGui.TableSetupColumn("#", ImGuiTableColumnFlags.WidthFixed);
            ImGui.TableSetupColumn("Icon", ImGuiTableColumnFlags.WidthFixed);
            ImGui.TableSetupColumn("Macro", ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableSetupColumn("Options", ImGuiTableColumnFlags.WidthFixed);

            // header
            // ImGui.TableNextRow(ImGuiTableRowFlags.Headers);
            ImGui.TableNextRow();

            ImGui.TableSetColumnIndex(0);
            if (ImGui.Checkbox($"##GlobalMacroCheckbox", ref _isGlobalMacroCheckboxChecked)) {
                if (_isGlobalMacroCheckboxChecked)
                    Plugin.MacroManager.SelectAllMacros();
                else
                    Plugin.MacroManager.ClearMacroSelection();
            }

            // checkbox border
            var min = ImGui.GetItemRectMin();
            var max = ImGui.GetItemRectMax();
            var dl = ImGui.GetWindowDrawList();
            dl.AddRect(min, max, ImGui.ColorConvertFloat4ToU32(Style.Components.TooltipBorderColor), 0f, ImDrawFlags.None, 1.0f);

            ImGui.TableSetColumnIndex(1);
            ImGui.TableHeader("#");
            // ImGui.TextUnformatted("#");

            ImGui.TableSetColumnIndex(2);
            ImGui.TableHeader("Icon");
            // ImGui.TextUnformatted("Icon");

            ImGui.TableSetColumnIndex(3);
            ImGui.TableHeader(Language.MacroNameLabel);
            // ImGui.TextUnformatted(Language.MacroNameLabel);

            ImGui.TableSetColumnIndex(4);
            ImGui.TableHeader("Options");
            // ImGui.TextUnformatted("Options");

            ImGuiListClipperPtr clipper;
            unsafe {
                clipper = new ImGuiListClipperPtr(ImGuiNative.ImGuiListClipper());
            }

            clipper.Begin(itemCount);

            while (clipper.Step()) {
                for (int i = clipper.DisplayStart; i < clipper.DisplayEnd; i++) {
                    if (i >= itemCount) break;
                    var realIndex = visibleIndexes[i];
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

    private void DrawMacroPanels() {

        if (_showTagsPanel) {
            DrawLeftPanel();
        }

        ImGui.SameLine();

        DrawRightPanel();
    }

    private void DrawLeftPanel() {
        var allTags = Plugin.MacroManager.GetAllTags();
        // layout helpers
        var totalAvail = ImGui.GetContentRegionAvail().X;
        var minPanelPx = 120f * ImGuiHelpers.GlobalScale;
        var maxPanelPx = Math.Max(minPanelPx, totalAvail - minPanelPx);
        // clamp stored width to available range
        _leftPanelWidth = MathF.Max(minPanelPx, MathF.Min(_leftPanelWidth, maxPanelPx));

        // left panel fixed width tree
        ImGui.BeginChild("##MacroTags", ImGuiHelpers.ScaledVector2(_leftPanelWidth, -1), true);
        ImGui.TextUnformatted(Language.MacroTagsLabel);

        int buttonFilterCount = 2;
        float buttonWidth = ImGui.GetFrameHeight();
        float spacing = ImGui.GetStyle().ItemSpacing.X;
        float marginRight = 10f * ImGuiHelpers.GlobalScale;
        float totalButtonsMacroWidth = (buttonWidth * buttonFilterCount) + (spacing * (buttonFilterCount - 1)) + marginRight;
        ImGui.SameLine(ImGui.GetWindowContentRegionMax().X - totalButtonsMacroWidth);

        ImGui.BeginGroup();
        {
            if (ImGuiUtil.IconButton(FontAwesomeIcon.Filter, $"##SelectAllTagsBtn", "Filter all tags")) {
                _filterNoTags = false;
                _selectedTags.Clear();
                foreach (var t in allTags) _selectedTags.Add(t);
            }

            // ImGui.SameLine();
            // var pushedNoTags = false;
            // if (_filterNoTags) {
            //     ImGui.PushStyleColor(ImGuiCol.Button, Style.Components.ButtonBlueNormal);
            //     ImGui.PushStyleColor(ImGuiCol.ButtonHovered, Style.Components.ButtonBlueHovered);
            //     ImGui.PushStyleColor(ImGuiCol.ButtonActive, Style.Components.ButtonBlueActive);
            //     pushedNoTags = true;
            // }
            // if (ImGuiUtil.IconButton(FontAwesomeIcon.FilterCircleXmark, $"##SelectNoTagsBtn", "Filter macros without tags")) {
            //     _selectedTags.Clear();
            //     _filterNoTags = !_filterNoTags;
            // }
            // if (pushedNoTags)
            //     ImGui.PopStyleColor(3);

            ImGui.SameLine();
            if (ImGuiUtil.IconButton(FontAwesomeIcon.Eraser, $"##ClearAllTagsBtn", "Clear filter")) {
                _selectedTags.Clear();
                _filterNoTags = false;
            }

            // if (ImGui.Button("Select All##SelectAllTagsBtn")) {
            //     _filterNoTags = false;
            //     _selectedTags.Clear();
            //     foreach (var t in allTags) _selectedTags.Add(t);
            // }

            // ImGui.SameLine();
            // var pushedNoTags = false;
            // if (_filterNoTags) {
            //     ImGui.PushStyleColor(ImGuiCol.Button, Style.Components.ButtonBlueNormal);
            //     ImGui.PushStyleColor(ImGuiCol.ButtonHovered, Style.Components.ButtonBlueHovered);
            //     ImGui.PushStyleColor(ImGuiCol.ButtonActive, Style.Components.ButtonBlueActive);
            //     pushedNoTags = true;
            // }
            // if (ImGui.Button("No Tags##SelectNoTagsBtn")) {
            //     _selectedTags.Clear();
            //     _filterNoTags = true;
            // }
            // if (pushedNoTags)
            //     ImGui.PopStyleColor(3);

            // ImGui.SameLine();
            // if (ImGui.Button("Clear##ClearAllTagsBtn")) {
            //     _selectedTags.Clear();
            //     _filterNoTags = false;
            // }
        }
        ImGui.EndGroup();

        ImGui.Separator();

        ImGui.Spacing();
        // tag list
        ImGui.PushStyleColor(ImGuiCol.Header, Style.Components.ButtonBlueHovered);
        ImGui.PushStyleColor(ImGuiCol.HeaderHovered, Style.Components.ButtonBlueHovered);
        ImGui.PushStyleColor(ImGuiCol.HeaderActive, Style.Components.ButtonBlueHovered);

        int noTagCount = Plugin.Config.Macros.Count(m => m.Tags == null || m.Tags.Count == 0);

        bool isNoTagSelected = _filterNoTags;
        if (ImGui.Selectable($"No Tags ({noTagCount})##tag_notag", isNoTagSelected)) {
            _selectedTags.Clear();
            _filterNoTags = !_filterNoTags;
        }

        ImGui.Separator();

        for (int i = 0; i < allTags.Count; i++) {
            var tag = allTags[i];
            var isSelected = _selectedTags.Contains(tag);

            var count = Plugin.Config.Macros
                .Count(m => (m.Tags ?? new List<string>())
                    .Any(t => string.Equals(t, tag, StringComparison.OrdinalIgnoreCase)));

            var label = $"{tag} ({count})##tag_{i}";

            if (ImGui.Selectable(label, isSelected, ImGuiSelectableFlags.SpanAllColumns)) {
                _filterNoTags = false;

                if (isSelected)
                    _selectedTags.Remove(tag);
                else
                    _selectedTags.Add(tag);
            }
        }
        ImGui.PopStyleColor(3);
        ImGui.EndChild();

        // splitter resizable
        ImGui.SameLine();

        // thin invisible button
        var splitterId = "##MacroTagsSplitter";
        var splitterWidth = 6f * ImGuiHelpers.GlobalScale;
        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(0, 0));
        ImGui.InvisibleButton(splitterId, new Vector2(splitterWidth, -1));
        if (ImGui.IsItemHovered())
            ImGui.SetMouseCursor(ImGuiMouseCursor.ResizeEw);

        // dragging behavior: adjust _leftPanelWidth by mouse delta while dragging
        if (ImGui.IsItemActive() && ImGui.IsMouseDragging(ImGuiMouseButton.Left)) {
            var io = ImGui.GetIO();
            _leftPanelWidth += io.MouseDelta.X;
            // clamp while dragging
            _leftPanelWidth = MathF.Max(minPanelPx, MathF.Min(_leftPanelWidth, maxPanelPx));
        }
        ImGui.PopStyleVar();
    }

    private void DrawRightPanel() {
        // right panel: filtered list
        ImGui.BeginChild("##MacroList", new Vector2(0, -1), false, ImGuiWindowFlags.HorizontalScrollbar);
        DrawMacrosTableFiltered();
        ImGui.EndChild();
    }

    internal void UpdateWindowConfig() {
        RespectCloseHotkey = Plugin.Config.AllowCloseWithEscape;

        TitleBarButtons.Clear();
        if (Plugin.Config.ShowSettingsButton) {
            TitleBarButtons.Add(new TitleBarButton() {
                AvailableClickthrough = false,
                Icon = FontAwesomeIcon.Cog,
                ShowTooltip = () => ImGuiUtil.ToolTip(Language.SettingsTitle),
                Click = _ => Ui.SettingsWindow.Toggle()
            });

            TitleBarButtons.Add(new TitleBarButton() {
                AvailableClickthrough = false,
                Icon = FontAwesomeIcon.Heart,
                ShowTooltip = () => ImGuiUtil.ToolTip("Discord"),
                Click = _ => WindowsApi.OpenUrl("https://discord.gg/BTsHyBzGsN")
            });

#if DEBUG
            TitleBarButtons.Add(new TitleBarButton() {
                AvailableClickthrough = false,
                Icon = FontAwesomeIcon.Bug,
                ShowTooltip = () => ImGuiUtil.ToolTip("Debug"),
                Click = _ => Ui.DebugWindow.Toggle()
            });
#endif
        }
    }

    public void DrawConflictingPluginAlert() {
        var conflictPluginName = GetConflictingPluginName();
        if (!string.IsNullOrEmpty(conflictPluginName))
            ImGuiUtil.DrawColoredBanner($"Conflicting Plugin Detected: {conflictPluginName}", Style.Colors.Red);
    }

    public string? GetConflictingPluginName() {
        var conflictingPluginNames = new[] { "WrathCombo", "RotationSolver", "BossMod" };

        var plugin = DalamudApi.PluginInterface.InstalledPlugins
            .FirstOrDefault(p =>
                p.IsLoaded &&
                conflictingPluginNames.Contains(p.InternalName, StringComparer.OrdinalIgnoreCase));

        return plugin?.InternalName;
    }
}
