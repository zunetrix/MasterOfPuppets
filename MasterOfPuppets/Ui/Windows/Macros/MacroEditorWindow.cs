using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;

using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Components;
using Dalamud.Interface.ImGuiNotification;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;

using MasterOfPuppets.Extensions;
using MasterOfPuppets.Extensions.Dalamud;
using MasterOfPuppets.Resources;
using MasterOfPuppets.Util.ImGuiExt;

namespace MasterOfPuppets;

public class MacroEditorWindow : Window {
    private Plugin Plugin { get; }
    private PluginUi Ui { get; }
    private Macro MacroItem = new();
    private int MacroIndex;
    private int SelectedCommandIndex = 0;
    private bool EditingExistingMacro = false;
    private string TagName = string.Empty;
    private string TagSelected = string.Empty;
    private string CharSelected = string.Empty;
    private string GroupSelected = string.Empty;
    private uint _inputLines = 15;
    private float _rightPanelWidth = 320f;

    private List<string> MacrosTags = new();

    private readonly ImGuiInputTextMultiline InputTextMultiline;
    private readonly ImGuiModalDialog ImGuiModalDialog = new("##MacroEditorModalDialog");

    private void RefreshMacrosTags() {
        MacrosTags = Plugin.MacroManager.GetAllTags().ToList();
    }

    public MacroEditorWindow(Plugin plugin, PluginUi ui) : base($"{Plugin.Name} {Language.MacroEditorTitle}###MacroEditorWindow") {
        Plugin = plugin;
        Ui = ui;
        InputTextMultiline = new ImGuiInputTextMultiline(plugin);

        Size = ImGuiHelpers.ScaledVector2(650, 730);
        SizeCondition = ImGuiCond.FirstUseEver;
        // SizeCondition = ImGuiCond.Always;
        // Flags = ImGuiWindowFlags.NoResize;
    }

    public override void PreDraw() {
        base.PreDraw();
        RefreshMacrosTags();
    }

    public override void OnClose() {
        if (Plugin.Config.AutoSaveMacro && EditingExistingMacro) {
            bool isMacroSaved = SaveMacro();

            if (!isMacroSaved) {
                IsOpen = true;
                return;
            }
        }

        RessetState();
        base.OnClose();
    }

    private void RessetState() {
        MacroItem = new();
        EditingExistingMacro = false;
        MacroIndex = Plugin.MacroManager.GetMacrosCount();
        SelectedCommandIndex = 0;
        TagName = string.Empty;
        CharSelected = string.Empty;
        GroupSelected = string.Empty;
    }

    public void EditMacro(int macroIndex) {
        RessetState();

        MacroItem = Plugin.MacroManager.GetMacroByIndex(macroIndex).Clone();
        MacroIndex = macroIndex;
        EditingExistingMacro = true;

        this.Toggle();
    }

    public void AddNewMacro() {
        RessetState();
        this.Toggle();
    }

    private bool SaveMacro() {
        var isNewMacro = MacroIndex == Plugin.MacroManager.GetMacrosCount();

        try {
            if (isNewMacro) {
                Plugin.MacroManager.AddMacro(MacroItem);
                DalamudApi.ShowNotification($"Macro saved", NotificationType.Success, 5000);
                return true;
            } else {
                Plugin.MacroManager.UpdateMacro(MacroIndex, MacroItem);
                DalamudApi.ShowNotification($"Macro updated", NotificationType.Success, 5000);
                return true;
            }

        } catch (Exception error) {
            ImGuiModalDialog.Show("Error", error.Message, ("OK", () => { }));
            return false;
        }
    }

    public override void Draw() {
        ImGuiModalDialog.Draw();

        ImGui.BeginGroup();
        DrawHeader();
        ImGui.EndGroup();

        DrawMainArea();
    }

    private void DrawMainArea() {
        var contentAvail = ImGui.GetContentRegionAvail();
        var splitterWidth = 6f * ImGuiHelpers.GlobalScale;
        var spacing = ImGui.GetStyle().ItemSpacing.X;
        var minRightPx = 250f * ImGuiHelpers.GlobalScale;
        var maxRightPx = MathF.Max(minRightPx, contentAvail.X - 200f * ImGuiHelpers.GlobalScale);
        _rightPanelWidth = MathF.Max(minRightPx, MathF.Min(_rightPanelWidth, maxRightPx));
        var leftPanelWidth = contentAvail.X - _rightPanelWidth - splitterWidth - spacing * 2f;

        ImGui.BeginChild("##MacroEditorMain", new Vector2(0, 0), false);

        ImGui.BeginChild("##MacroEditorLeft", new Vector2(leftPanelWidth, 0), false);
        DrawCommandList();
        ImGui.EndChild();

        ImGui.SameLine();
        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(0, 0));
        ImGui.InvisibleButton("##MacroEditorSplitter", new Vector2(splitterWidth, -1));
        if (ImGui.IsItemHovered())
            ImGui.SetMouseCursor(ImGuiMouseCursor.ResizeEw);
        if (ImGui.IsItemActive() && ImGui.IsMouseDragging(ImGuiMouseButton.Left)) {
            _rightPanelWidth -= ImGui.GetIO().MouseDelta.X;
            _rightPanelWidth = MathF.Max(minRightPx, MathF.Min(_rightPanelWidth, maxRightPx));
        }
        ImGui.PopStyleVar();

        ImGui.SameLine();
        ImGui.BeginChild("##MacroEditorRight", new Vector2(_rightPanelWidth, 0), true);
        DrawDetailsPanel();
        ImGui.EndChild();

        ImGui.EndChild(); // ##MacroEditorMain
    }

    private void DrawDetailsPanel() {
        ImGui.Text("Details");
        ImGui.Separator();

        var hasValidCommand = MacroItem.Commands != null && MacroItem.Commands.IndexExists(SelectedCommandIndex);

        if (ImGui.CollapsingHeader("Character Assignments")) {
            if (hasValidCommand)
                DrawCharacterAssignList(SelectedCommandIndex);
            else
                ImGui.TextDisabled("Select a command");
        }

        if (ImGui.CollapsingHeader("Group Assignments")) {
            if (hasValidCommand)
                DrawGroupAssignList(SelectedCommandIndex);
            else
                ImGui.TextDisabled("Select a command");
        }

        if (ImGui.CollapsingHeader("Appearance")) {
            DrawIconColorPicker();
        }

        if (ImGui.CollapsingHeader("Tags")) {
            DrawTagsSelector();
        }

        if (ImGui.CollapsingHeader("Macro Variables")) {
            DrawMacroVariables();
        }
    }

    private void DrawMacroVariables() {
        ImGui.Text(Language.MacroVariablesLabel);
        ImGui.SameLine();
        ImGuiUtil.HelpMarker("""
            * Macro variables can be overridden by action variables
            Variables usage:
            $name = "Character Name"
            $time = 0.5
            /moptarget "$name"
            /mopwait $time
            """);
        ImGui.InputTextMultiline("##MacroVariablesInput", ref MacroItem.Variables, size: new Vector2(-1, 250));
        ImGui.Spacing();
        ImGui.Spacing();
    }

    private void DrawIconColorPicker() {
        // color / icon
        ImGui.BeginGroup();
        {
            ImGui.Spacing();
            ImGui.Text(Language.MacroColorLabel);
            ImGui.PushItemWidth(250);

            MacroItem.Color = ImGuiComponents.ColorPickerWithPalette(1, "##MacroColorInput", MacroItem.Color);
            // ImGui.ColorEdit4("##MacroColorInput", ref MacroItem.Color, ImGuiColorEditFlags.NoAlpha | ImGuiColorEditFlags.NoLabel);
            ImGui.SameLine();
            if (ImGuiUtil.IconButton(FontAwesomeIcon.Undo, "##ResetMacroColorBtn", "Reset")) {
                MacroItem.Color = Style.Colors.White;
            }
            ImGui.PopItemWidth();
        }
        ImGui.EndGroup();

        ImGui.SameLine();
        ImGuiHelpers.ScaledDummy(10);
        ImGui.SameLine();

        ImGui.BeginGroup();
        {
            DrawMacroIconPicker();
            ImGuiUtil.ToolTip($"Click to select icon");
        }
        ImGui.EndGroup();
        ImGui.Spacing();
        ImGui.Spacing();
    }

    private void DrawTagsSelector() {
        ImGui.BeginGroup();
        {
            ImGui.Text("Tag Name");
            ImGui.InputText("##TagNameInput", ref TagName);

            ImGui.SameLine();
            if (ImGui.Button("Add Tag##AddTagBtn")) {
                if (!string.IsNullOrWhiteSpace(TagName)) {
                    MacroItem.Tags.AddUnique(TagName.Trim());
                    TagName = string.Empty;
                    RefreshMacrosTags();
                }
            }

            ImGui.Spacing();
            ImGui.Spacing();

            ImGui.PushStyleColor(ImGuiCol.Border, Style.Components.TooltipBorderColor);
            ImGui.PushStyleVar(ImGuiStyleVar.PopupBorderSize, 1);
            ImGui.SetNextItemWidth(-1);
            if (ImGuiUtil.DrawComboSearch("##MacroTagsSelectList", MacrosTags, ref TagSelected)) {
                MacroItem.Tags.AddUnique(TagSelected);
                TagSelected = string.Empty;
            }
            ImGui.PopStyleVar();
            ImGui.PopStyleColor();
        }
        ImGui.EndGroup();

        ImGuiHelpers.ScaledDummy(0, 10);

        {
            float deleteColWidth = ImGui.GetFrameHeight();
            int deleteTagIndex = -1;

            if (ImGui.BeginTable("##MacroTagsTable", 2,
                ImGuiTableFlags.RowBg | ImGuiTableFlags.ScrollY | ImGuiTableFlags.NoSavedSettings,
                new Vector2(-1, 150))) {
                ImGui.TableSetupScrollFreeze(0, 1);
                ImGui.TableSetupColumn("Tag", ImGuiTableColumnFlags.WidthStretch);
                ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthFixed, deleteColWidth);
                ImGui.TableHeadersRow();

                for (var i = 0; i < MacroItem.Tags.Count; i++) {
                    ImGui.TableNextRow();
                    ImGui.TableNextColumn();
                    ImGui.Text(MacroItem.Tags[i]);
                    ImGui.TableNextColumn();
                    if (ImGuiUtil.DangerIconButton(FontAwesomeIcon.Trash, $"##DeleteTag_{i}", Language.DeleteInstructionTooltip) && ImGui.GetIO().KeyCtrl)
                        deleteTagIndex = i;
                }

                ImGui.EndTable();
            }

            if (deleteTagIndex >= 0) {
                MacroItem.Tags.RemoveAt(deleteTagIndex);
                RefreshMacrosTags();
            }
        }
        ImGui.Spacing();
        ImGui.Spacing();
    }

    private void DrawHeader() {
        ImGui.Text(Language.MacroNameLabel);
        ImGui.InputText("##MacroNameInput", ref MacroItem.Name);

        ImGui.SameLine();
        using (ImRaii.PushColor(ImGuiCol.Button, Style.Components.ButtonSuccessnNormal)
        .Push(ImGuiCol.ButtonHovered, Style.Components.ButtonSuccessHovered)
        .Push(ImGuiCol.ButtonActive, Style.Components.ButtonSuccessActive)) {
            if (ImGuiUtil.IconButton(FontAwesomeIcon.Save, $"##SaveMacroBtn", Language.SaveMacroBtn)) {
                if (SaveMacro()) {
                    this.IsOpen = false;
                }
            }
        }

        ImGui.SameLine();

        var autoSaveMacro = Plugin.Config.AutoSaveMacro;
        if (ImGui.Checkbox(Language.SettingsWindowAutoSaveMacro, ref autoSaveMacro)) {
            Plugin.Config.AutoSaveMacro = autoSaveMacro;
            Plugin.IpcProvider.SyncConfiguration();
        }

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        if (ImGuiUtil.IconButton(FontAwesomeIcon.Plus, $"##AddCommandBtn", "Add Command")) {
            var newCommand = new Command { Cids = new(), Actions = "" };
            MacroItem.Commands.Add(newCommand);
        }

        ImGui.SameLine();
        ImGui.Text("Commands");
        ImGuiUtil.HelpMarker("""
        Start by adding characters to the list, then add commands and assign them to the characters or groups.
        After that, set the actions they should perform
        """);

        // align right
        float spacing = ImGui.GetStyle().ItemSpacing.X;
        float buttonWidth = ImGui.GetFrameHeight();
        int buttonCount = 2;
        float marginRight = 15f * ImGuiHelpers.GlobalScale;
        float totalButtonsWidth = (buttonWidth * buttonCount) + (spacing * (buttonCount - 1)) + marginRight;

        ImGui.SameLine(ImGui.GetWindowContentRegionMax().X - totalButtonsWidth);
        if (ImGuiUtil.IconButton(FontAwesomeIcon.Question, $"##ShowMacroHelpBtn", Language.ShowMacroHelpBtn)) {
            Plugin.Ui.MacroHelpWindow.Toggle();
        }

        ImGui.SameLine();
        if (ImGuiUtil.IconButton(FontAwesomeIcon.Users, $"##ShowCharactersBtn", Language.ShowCharactersBtn)) {
            Plugin.Ui.CharactersWindow.Toggle();
        }

        ImGui.Spacing();
        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();
        ImGui.Spacing();
    }

    private unsafe void DrawCommandList() {
        // https://github.com/ocornut/imgui/blob/master/imgui_demo.cpp

        // new macro dont have any command
        if (MacroItem.Commands == null || MacroItem.Commands.Count == 0) return;

        // left pane
        ImGui.BeginChild("##CommandList", ImGuiHelpers.ScaledVector2(150, 0), true);
        for (var commandIndex = 0; commandIndex < MacroItem.Commands.Count; commandIndex++) {
            if (ImGuiUtil.IconButton(FontAwesomeIcon.Trash, $"##RemoveCommand_{commandIndex}", Language.DeleteInstructionTooltip)) {
                if (ImGui.GetIO().KeyCtrl) {
                    MacroItem.Commands.RemoveAt(commandIndex);
                }
            }

            ImGui.SameLine();

            bool isSelected = SelectedCommandIndex == commandIndex;

            string label = $"Command {commandIndex + 1}";
            using (ImRaii.PushColor(ImGuiCol.Header, Style.Components.ButtonBlueHovered, isSelected)
            .Push(ImGuiCol.HeaderHovered, Style.Components.ButtonBlueHovered, isSelected)
            .Push(ImGuiCol.HeaderActive, Style.Components.ButtonBlueHovered, isSelected)) {
                if (ImGui.Selectable(label, isSelected)) {
                    SelectedCommandIndex = commandIndex;
                }
                ImGuiUtil.ToolTip($"Drag to reorder");
            }

            if (ImGui.BeginDragDropSource()) {
                ImGui.SetDragDropPayload("DND_COMMAND_LIST", new ReadOnlySpan<byte>(&commandIndex, sizeof(int)), ImGuiCond.None);
                ImGui.Button(label);
                // DalamudApi.PluginLog.Warning($"Drag start [{commandIndex}]: {commandIndex}");
                ImGui.EndDragDropSource();
            }

            ImGui.PushStyleColor(ImGuiCol.DragDropTarget, Style.Components.DragDropTarget);
            if (ImGui.BeginDragDropTarget()) {
                ImGuiPayloadPtr dragDropPayload = ImGui.AcceptDragDropPayload("DND_COMMAND_LIST");
                bool isDropping = false;
                isDropping = !dragDropPayload.IsNull;

                if (isDropping && dragDropPayload.IsDelivery()) {
                    int originalIndex = *(int*)dragDropPayload.Data;

                    int offset = commandIndex - originalIndex;
                    if (offset != 0 && originalIndex + offset >= 0) {
                        int targetIndex = originalIndex + offset;
                        // DalamudApi.PluginLog.Warning($"Drag end [{commandIndex}]: [{originalIndex}, {targetIndex}] {offset}");
                        MacroItem.Commands.MoveItemToIndex(originalIndex, targetIndex);
                    }
                }
                ImGui.EndDragDropTarget();
            }
            ImGui.PopStyleColor();

        }
        ImGui.EndChild();

        // prevent render if all commands removed
        if (!MacroItem.Commands.IndexExists(SelectedCommandIndex)) return;
        ImGui.SameLine();
        DrawCommandEditor(SelectedCommandIndex);
    }

    private void DrawCharacterAssignList(int commandIndex) {
        var usedCids = MacroItem.Commands
            .SelectMany(c => c.Cids)
            .ToHashSet();

        var availableCharacters = Plugin.Config.Characters
            .Where(c => !usedCids.Contains(c.Cid))
            .ToList();
        var charNames = availableCharacters.Select(c => c.Name).ToList();

        float removeAllBtnWidth = ImGui.CalcTextSize(Language.RemoveAllBtn).X + ImGui.GetStyle().FramePadding.X * 2;

        ImGui.BeginDisabled(charNames.Count == 0);
        ImGui.SetNextItemWidth(-removeAllBtnWidth - ImGui.GetStyle().ItemSpacing.X);
        ImGui.PushStyleColor(ImGuiCol.Border, Style.Components.TooltipBorderColor);
        ImGui.PushStyleVar(ImGuiStyleVar.PopupBorderSize, 1);
        if (ImGuiUtil.DrawComboSearch($"##CharSearch_{commandIndex}", charNames, ref CharSelected)) {
            var found = availableCharacters.FirstOrDefault(c => c.Name == CharSelected);
            if (found != null) {
                MacroItem.Commands[commandIndex].Cids.AddUnique(found.Cid);
                CharSelected = string.Empty;
            }
        }
        ImGui.PopStyleVar();
        ImGui.PopStyleColor();
        ImGui.EndDisabled();

        ImGui.SameLine();
        using (ImRaii.PushColor(ImGuiCol.Button, Style.Components.ButtonDangerNormal)
            .Push(ImGuiCol.ButtonHovered, Style.Components.ButtonDangerHovered)
            .Push(ImGuiCol.ButtonActive, Style.Components.ButtonDangerActive)) {
            if (ImGui.Button($"{Language.RemoveAllBtn}##RemoveAllChars_{commandIndex}") && ImGui.GetIO().KeyCtrl)
                MacroItem.Commands[commandIndex].Cids = new();
        }
        ImGuiUtil.ToolTip(Language.DeleteInstructionTooltip);

        ImGui.Separator();

        float deleteColWidth = ImGui.GetFrameHeight();
        int deleteIndex = -1;

        if (ImGui.BeginTable($"##CidsTable_cmd_{commandIndex}", 2,
            ImGuiTableFlags.RowBg | ImGuiTableFlags.ScrollY | ImGuiTableFlags.NoSavedSettings,
            new Vector2(-1, 120))) {
            ImGui.TableSetupScrollFreeze(0, 0);
            ImGui.TableSetupColumn("Name", ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthFixed, deleteColWidth);

            for (var i = 0; i < MacroItem.Commands[commandIndex].Cids.Count; i++) {
                var targetCid = MacroItem.Commands[commandIndex].Cids[i];
                var character = Plugin.Config.Characters.FirstOrDefault(c => c.Cid == targetCid)
                    ?? new Character { Cid = targetCid, Name = $"Unknown ({targetCid})" };

                ImGui.TableNextRow();
                ImGui.TableNextColumn();
                ImGui.Text(character.Name);

                ImGui.TableNextColumn();
                if (ImGuiUtil.DangerIconButton(FontAwesomeIcon.Trash, $"##DeleteChar_{commandIndex}_{i}", Language.DeleteInstructionTooltip) && ImGui.GetIO().KeyCtrl)
                    deleteIndex = i;
            }

            ImGui.EndTable();
        }

        if (deleteIndex >= 0)
            MacroItem.Commands[commandIndex].Cids.RemoveAt(deleteIndex);

        ImGui.Spacing();
    }

    private void DrawGroupAssignList(int commandIndex) {
        var assignedGroupIds = MacroItem.Commands[commandIndex].GroupIds;
        var allGroups = Plugin.Config.CidsGroups;

        var availableGroups = allGroups
            .Where(g => !assignedGroupIds.Contains(g.Name))
            .ToList();
        var groupNames = availableGroups.Select(g => g.Name).ToList();

        float removeAllBtnWidth = ImGui.CalcTextSize(Language.RemoveAllBtn).X + ImGui.GetStyle().FramePadding.X * 2;

        ImGui.BeginDisabled(groupNames.Count == 0);
        ImGui.SetNextItemWidth(-removeAllBtnWidth - ImGui.GetStyle().ItemSpacing.X);
        ImGui.PushStyleColor(ImGuiCol.Border, Style.Components.TooltipBorderColor);
        ImGui.PushStyleVar(ImGuiStyleVar.PopupBorderSize, 1);
        if (ImGuiUtil.DrawComboSearch($"##GroupSearch_{commandIndex}", groupNames, ref GroupSelected)) {
            if (!string.IsNullOrEmpty(GroupSelected)) {
                assignedGroupIds.AddUnique(GroupSelected);
                GroupSelected = string.Empty;
            }
        }
        ImGui.PopStyleVar();
        ImGui.PopStyleColor();
        ImGui.EndDisabled();

        ImGui.SameLine();
        using (ImRaii.PushColor(ImGuiCol.Button, Style.Components.ButtonDangerNormal)
            .Push(ImGuiCol.ButtonHovered, Style.Components.ButtonDangerHovered)
            .Push(ImGuiCol.ButtonActive, Style.Components.ButtonDangerActive)) {
            if (ImGui.Button($"{Language.RemoveAllBtn}##RemoveAllGroups_{commandIndex}") && ImGui.GetIO().KeyCtrl)
                MacroItem.Commands[commandIndex].GroupIds = new();
        }
        ImGuiUtil.ToolTip(Language.DeleteInstructionTooltip);

        ImGui.Separator();

        float deleteColWidth = ImGui.GetFrameHeight();
        int deleteIndex = -1;

        if (ImGui.BeginTable($"##GroupsTable_cmd_{commandIndex}", 2,
            ImGuiTableFlags.RowBg | ImGuiTableFlags.ScrollY | ImGuiTableFlags.NoSavedSettings,
            new Vector2(-1, 120))) {
            ImGui.TableSetupScrollFreeze(0, 0);
            ImGui.TableSetupColumn("Name", ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthFixed, deleteColWidth);

            for (var i = 0; i < assignedGroupIds.Count; i++) {
                var groupName = assignedGroupIds[i];
                var displayName = allGroups.FirstOrDefault(g => g.Name == groupName)?.Name ?? $"Unknown ({groupName})";

                ImGui.TableNextRow();
                ImGui.TableNextColumn();
                ImGui.Text(displayName);

                ImGui.TableNextColumn();
                if (ImGuiUtil.DangerIconButton(FontAwesomeIcon.Trash, $"##DeleteGroup_{commandIndex}_{i}", Language.DeleteInstructionTooltip) && ImGui.GetIO().KeyCtrl)
                    deleteIndex = i;
            }

            ImGui.EndTable();
        }

        if (deleteIndex >= 0)
            assignedGroupIds.RemoveAt(deleteIndex);

        ImGui.Spacing();
    }

    private void DrawCommandEditor(int commandIndex) {
        ImGui.BeginGroup();
        ImGui.BeginChild("##CommandEditor", new Vector2(0, -ImGui.GetFrameHeightWithSpacing()));
        ImGuiUtil.DrawColoredBanner($"COMMAND {SelectedCommandIndex + 1}", Style.Components.ButtonBlueHovered);
        ImGui.Spacing();

        ImGui.Spacing();

        // Macro Variables moved to the Details panel to declutter the command editor.

        ImGui.Spacing();
        ImGui.Spacing();

        ImGui.BeginGroup();
        ImGui.Text(Language.ActionsTitle);
        ImGuiUtil.HelpMarker("""
            Press TAB to autocomplete

            Use # for line comment (macro ignore line)
        """);

        ImGui.SameLine();
        ImGuiHelpers.ScaledDummy(20, 0);

        ImGui.SameLine();
        if (ImGuiUtil.IconButton(FontAwesomeIcon.Crosshairs, $"##CopyTargetNameBtn", "Copy Target Name")) {
            ImGui.SetClipboardText($"\"{GameTargetManager.GetTargetName()}\"");
        }
        ImGui.SameLine();
        if (ImGuiUtil.IconButton(FontAwesomeIcon.PersonArrowDownToLine, $"##CopyTargetPositionBtn", "Copy Target Offset Position")) {
            var offset = GameTargetManager.GetTargetOffsetFromMe();
            string commandPosition = $"{offset.X.ToString(CultureInfo.InvariantCulture)} {offset.Y.ToString(CultureInfo.InvariantCulture)} {offset.Z.ToString(CultureInfo.InvariantCulture)}";
            ImGui.SetClipboardText(commandPosition);
        }

        ImGui.SameLine();
        ImGuiHelpers.ScaledDummy(20, 0);
        ImGui.SameLine();
        ImGui.SetNextItemWidth(100);
        if (ImGui.InputUInt("Lines##InputLines", ref _inputLines, 1, 1)) {
            _inputLines = Math.Max(11u, _inputLines);
        }

        ImGui.Spacing();

        if (InputTextMultiline.Draw(
            $"##InputActionCommand_{commandIndex}",
            ref MacroItem.Commands[commandIndex].Actions,
            ushort.MaxValue,
            new Vector2(
                MathF.Min(ImGui.GetContentRegionAvail().X, 500f * ImGuiHelpers.GlobalScale),
                ImGui.GetTextLineHeight() * _inputLines
            ),
            ImGuiInputTextFlags.None
        )) {
            // DalamudApi.PluginLog.Debug($"{_inputTextContent}");
        }
        ImGui.EndGroup();

        ImGui.Spacing();
        ImGui.Spacing();

        ImGui.EndChild(); // ##CommandEditor
        ImGui.EndGroup();
    }

    private void DrawMacroIconPicker() {
        ImGui.Text(Language.MacroIconLabel);
        var iconSize = ImGuiHelpers.ScaledVector2(30, 30);

        var drawList = ImGui.GetWindowDrawList();
        Vector2 pos = ImGui.GetCursorScreenPos();

        DalamudApi.TextureProvider.DrawIcon(MacroItem.IconId, iconSize);
        uint borderColor = ImGui.ColorConvertFloat4ToU32(Style.Components.TooltipBorderColor);
        float thickness = 1f;
        drawList.AddRect(pos, pos + iconSize, borderColor, 0f, ImDrawFlags.None, thickness);

        if (ImGui.IsItemClicked()) {
            Ui.IconPickerDialogWindow.Open(MacroItem.IconId, (selectedIconId) => {
                MacroItem.IconId = selectedIconId;
                // DalamudApi.PluginLog.Debug($"selectedIconId: {selectedIconId}");
            });
        }
    }
}
