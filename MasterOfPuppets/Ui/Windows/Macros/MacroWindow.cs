using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.ImGuiNotification;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;

using MasterOfPuppets.Extensions.Dalamud;
using MasterOfPuppets.Resources;
using MasterOfPuppets.Util.ImGuiExt;

namespace MasterOfPuppets;

public partial class MacroWindow : Window {
    private Plugin Plugin { get; }
    private PluginUi Ui { get; }

    private string _macroSearchString = string.Empty;
    private readonly List<int> MacroListSearchedIndexes = new();
    private readonly HashSet<string> _selectedTags = new(StringComparer.OrdinalIgnoreCase);
    private bool _filterNoTags = false;
    private float _leftPanelWidth = 200f;
    private bool _isGlobalMacroCheckboxChecked = false;

    internal MacroWindow(Plugin plugin, PluginUi ui) : base($"Macros###MopMacrosWindow") {
        Plugin = plugin;
        Ui = ui;

        Size = ImGuiHelpers.ScaledVector2(600, 400);
        SizeCondition = ImGuiCond.FirstUseEver;
    }

    public override void PreDraw() {
        Flags = ImGuiWindowFlags.None;
        if (!Plugin.Config.AllowMovement) Flags |= ImGuiWindowFlags.NoMove;
        if (!Plugin.Config.AllowResize) Flags |= ImGuiWindowFlags.NoResize;
        base.PreDraw();
    }

    public override void Draw() {
        ImGui.BeginDisabled(Ui.MacroEditorWindow.IsOpen);

        DrawMacroToolbar();

        ImGui.BeginGroup();
        DrawMacroHeader();
        ImGui.EndGroup();

        ImGui.BeginChild("##MopMacroListScrollableContent", new Vector2(-1, 0), false, ImGuiWindowFlags.HorizontalScrollbar);
        DrawMacroPanels();
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

    private void DrawMacroHeader() {
        DrawConflictingPluginAlert();

        float spacing = ImGui.GetStyle().ItemSpacing.X;
        float buttonWidth = ImGui.GetFrameHeight();
        float marginRight = 15f * ImGuiHelpers.GlobalScale;

        if (ImGuiUtil.IconButton(FontAwesomeIcon.Tags, $"##ToggleMacroTagsPanelBtn", Language.TogglePanelBtn)) {
            Plugin.Config.ShowPanelMacroTags = !Plugin.Config.ShowPanelMacroTags;
            Plugin.IpcProvider.SyncConfiguration();
        }

        ImGui.SameLine();

        if (ImGui.InputTextWithHint("##MacroSearchInput", Language.MacroSearchInputLabel, ref _macroSearchString, 255, ImGuiInputTextFlags.AutoSelectAll)) {
            SearchMacro();
        }

        int buttonMacroCount = 4;
        float totalButtonsMacroWidth = (buttonWidth * buttonMacroCount) + (spacing * (buttonMacroCount - 1)) + marginRight;
        ImGui.SameLine(ImGui.GetWindowContentRegionMax().X - totalButtonsMacroWidth);

        if (ImGuiUtil.IconButton(FontAwesomeIcon.Plus, $"##AddMacroBtn", Language.AddMacroBtn)) {
            Ui.MacroEditorWindow.AddNewMacro();
        }

        using (ImRaii.PushColor(ImGuiCol.Button, Style.Components.ButtonDangerNormal)
                .Push(ImGuiCol.ButtonHovered, Style.Components.ButtonDangerHovered)
                .Push(ImGuiCol.ButtonActive, Style.Components.ButtonDangerActive)) {
            ImGui.SameLine();
            if (ImGuiUtil.IconButton(FontAwesomeIcon.Ban, $"##StopMovementBtn", Language.StopMovementBtn)) {
                Plugin.IpcProvider.StopMovement();
                DalamudApi.ShowNotification($"Movement stoped", NotificationType.Info, 3000);
            }

            ImGui.SameLine();
            if (ImGuiUtil.IconButton(FontAwesomeIcon.Stop, $"##StopMacroExecutionBtn", Language.StopMacroExecutionBtn)) {
                Plugin.IpcProvider.StopMacroExecution();
                DalamudApi.ShowNotification($"Macro execution queue stoped", NotificationType.Info, 3000);
            }
        }

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
        bool isChecked = Plugin.MacroManager.SelectedMacrosIndexes.Contains(macroIdx);
        if (ImGui.Checkbox($"##SelectedMacroCheckbox_{macroIdx}", ref isChecked)) {
            if (isChecked)
                Plugin.MacroManager.SelectedMacrosIndexes.Add(macroIdx);
            else
                Plugin.MacroManager.SelectedMacrosIndexes.Remove(macroIdx);
        }

        ImGui.TableNextColumn();
        ImGui.Text($"{macroIdx + 1:000}");

        ImGui.TableNextColumn();
        DalamudApi.TextureProvider.DrawIcon(macro.IconId, ImGuiHelpers.ScaledVector2(30, 30));
        if (macro.Tags.Count > 0) {
            ImGuiUtil.ToolTip($"{string.Join("\n", macro.Tags)}");
        }

        ImGui.TableNextColumn();
        using (ImRaii.PushColor(ImGuiCol.Text, macro.Color)) {
            ImGui.Selectable($"{macro.Name}");
        }

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
            ImGui.EndDragDropSource();
        }

        using (ImRaii.PushColor(ImGuiCol.DragDropTarget, Style.Components.DragDropTarget)) {
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
                            Plugin.MacroManager.MoveMacroToIndex(originalIndex, targetIndex);
                            Plugin.MacroManager.SelectedMacrosIndexes.Clear();
                            Plugin.IpcProvider.SyncConfiguration();
                        }
                    }
                }

                ImGui.EndDragDropTarget();
            }
        }

        ImGui.TableNextColumn();
        if (ImGuiUtil.IconButton(FontAwesomeIcon.Trash, $"##DeleteMacro_{macroIdx}", Language.DeleteInstructionTooltip)) {
            if (ImGui.GetIO().KeyCtrl) {
                Plugin.MacroManager.DeleteMacro(macroIdx);
                Plugin.IpcProvider.SyncConfiguration();
            }
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

    private void DrawMacrosTableFiltered() {
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
                    var normalized = tags.Select(t => (t ?? string.Empty).Trim()).ToList();
                    return _selectedTags.All(sel => normalized.Any(t => string.Equals(t, sel, StringComparison.OrdinalIgnoreCase)));
                })
                .ToList();
        }

        var tableFlags = ImGuiTableFlags.RowBg | ImGuiTableFlags.PadOuterX |
                ImGuiTableFlags.NoSavedSettings | ImGuiTableFlags.BordersInnerV;

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

            ImGui.TableNextRow();

            ImGui.TableSetColumnIndex(0);
            if (ImGui.Checkbox($"##GlobalMacroCheckbox", ref _isGlobalMacroCheckboxChecked)) {
                if (_isGlobalMacroCheckboxChecked)
                    Plugin.MacroManager.SelectAllMacros();
                else
                    Plugin.MacroManager.ClearMacroSelection();
            }
            ImGuiUtil.ToolTip("Select / Unselect All");

            ImGui.TableSetColumnIndex(1);
            ImGui.TableHeader("#");

            ImGui.TableSetColumnIndex(2);
            ImGui.TableHeader("Icon");

            ImGui.TableSetColumnIndex(3);
            ImGui.TableHeader(Language.MacroNameLabel);

            ImGui.TableSetColumnIndex(4);
            ImGui.TableHeader("Options");

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
        if (Plugin.Config.ShowPanelMacroTags) {
            DrawLeftPanel();
        }

        ImGui.SameLine();

        DrawRightPanel();
    }

    private void DrawLeftPanel() {
        var allTags = Plugin.MacroManager.GetAllTags();
        var totalAvail = ImGui.GetContentRegionAvail().X;
        var minPanelPx = 120f * ImGuiHelpers.GlobalScale;
        var maxPanelPx = Math.Max(minPanelPx, totalAvail - minPanelPx);
        _leftPanelWidth = MathF.Max(minPanelPx, MathF.Min(_leftPanelWidth, maxPanelPx));

        ImGui.BeginChild("##MacroTags", ImGuiHelpers.ScaledVector2(_leftPanelWidth, -1), true);
        ImGui.Text(Language.MacroTagsLabel);

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

            ImGui.SameLine();
            if (ImGuiUtil.IconButton(FontAwesomeIcon.Eraser, $"##ClearAllTagsBtn", "Clear filter")) {
                _selectedTags.Clear();
                _filterNoTags = false;
            }
        }
        ImGui.EndGroup();

        ImGui.Separator();
        ImGui.Spacing();

        using (ImRaii.PushColor(ImGuiCol.Header, Style.Components.ButtonBlueHovered)
            .Push(ImGuiCol.HeaderHovered, Style.Components.ButtonBlueHovered)
            .Push(ImGuiCol.HeaderActive, Style.Components.ButtonBlueHovered)) {
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
        }
        ImGui.EndChild();

        ImGui.SameLine();

        var splitterId = "##MacroTagsSplitter";
        var splitterWidth = 6f * ImGuiHelpers.GlobalScale;
        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(0, 0));
        ImGui.InvisibleButton(splitterId, new Vector2(splitterWidth, -1));
        if (ImGui.IsItemHovered())
            ImGui.SetMouseCursor(ImGuiMouseCursor.ResizeEw);

        if (ImGui.IsItemActive() && ImGui.IsMouseDragging(ImGuiMouseButton.Left)) {
            var io = ImGui.GetIO();
            _leftPanelWidth += io.MouseDelta.X;
            _leftPanelWidth = MathF.Max(minPanelPx, MathF.Min(_leftPanelWidth, maxPanelPx));
        }
        ImGui.PopStyleVar();
    }

    private void DrawRightPanel() {
        ImGui.BeginChild("##MacroList", new Vector2(0, -1), false, ImGuiWindowFlags.HorizontalScrollbar);
        DrawMacrosTableFiltered();
        ImGui.EndChild();
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
