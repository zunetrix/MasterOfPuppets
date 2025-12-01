using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.ImGuiNotification;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Windowing;

using MasterOfPuppets.Extensions;
using MasterOfPuppets.Extensions.Dalamud;
using MasterOfPuppets.Resources;
using MasterOfPuppets.Util.ImGuiExt;

namespace MasterOfPuppets;

public class MacroEditorWindow : Window {
    private Plugin Plugin { get; }
    private PluginUi Ui { get; }
    private Macro MacroItem = new() { Commands = new List<Command>() };
    private int MacroIndex;
    private int SelectedCommandIndex = 0;
    private bool EditingExistingMacro = false;
    private readonly ImGuiInputTextMultiline InputTextMultiline;
    private readonly ImGuiModalDialog ImGuiModalDialog = new("##MacroEditorModalDialog");

    public MacroEditorWindow(Plugin plugin, PluginUi ui) : base($"{Plugin.Name} {Language.MacroEditorTitle}###MacroEditorWindow") {
        Plugin = plugin;
        Ui = ui;
        InputTextMultiline = new ImGuiInputTextMultiline(plugin);

        Size = ImGuiHelpers.ScaledVector2(650, 700);
        // SizeCondition = ImGuiCond.FirstUseEver;
        SizeCondition = ImGuiCond.Always;
        Flags = ImGuiWindowFlags.NoResize;
    }

    public override void PreDraw() {
        base.PreDraw();
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
        MacroItem = new() { Commands = new List<Command>() };
        EditingExistingMacro = false;
        MacroIndex = Plugin.MacroManager.GetMacrosCount();
        SelectedCommandIndex = 0;
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

        ImGui.BeginChild("##MacroEditorHeaderFixedHeight", ImGuiHelpers.ScaledVector2(-1, 200), false, ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse);
        DrawHeader();
        ImGui.EndChild();

        ImGui.BeginChild("##MacroEditorListScrollableContent", ImGuiHelpers.ScaledVector2(-1, 0), false, ImGuiWindowFlags.HorizontalScrollbar);
        DrawCommandList();
        ImGui.EndChild();
    }

    private void DrawHeader() {
        ImGui.Text(Language.MacroNameLabel);
        ImGui.InputText("##MacroNameInput", ref MacroItem.Name);

        ImGui.SameLine();
        ImGui.PushStyleColor(ImGuiCol.Button, Style.Components.ButtonSuccessnNormal);
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, Style.Components.ButtonSuccessHovered);
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, Style.Components.ButtonSuccessActive);
        if (ImGuiUtil.IconButton(FontAwesomeIcon.Save, $"##SaveMacroBtn", Language.SaveMacroBtn)) {
            if (SaveMacro()) {
                this.IsOpen = false;
            }
        }
        ImGui.PopStyleColor(3);

        ImGui.SameLine();

        var autoSaveMacro = Plugin.Config.AutoSaveMacro;
        if (ImGui.Checkbox(Language.SettingsWindowAutoSaveMacro, ref autoSaveMacro)) {
            Plugin.Config.AutoSaveMacro = autoSaveMacro;
            Plugin.IpcProvider.SyncConfiguration();
        }

        ImGui.TextUnformatted(Language.MacroPathLabel);
        ImGui.Spacing();
        ImGui.InputText("##MacroPathInput", ref MacroItem.Path);

        ImGui.BeginGroup();
        {
            ImGui.Spacing();
            ImGui.TextUnformatted(Language.MacroColorLabel);
            ImGui.PushItemWidth(300);
            ImGui.ColorEdit4("##MacroColorInput", ref MacroItem.Color, ImGuiColorEditFlags.NoAlpha | ImGuiColorEditFlags.NoLabel);
            ImGui.SameLine();
            if (ImGuiUtil.IconButton(FontAwesomeIcon.Undo, "##ResetMacroColorBtn", "Reset")) {
                MacroItem.Color = default;
            }
            ImGui.PopItemWidth();
        }
        ImGui.EndGroup();

        ImGui.SameLine();
        ImGui.Dummy(ImGuiHelpers.ScaledVector2(20));
        ImGui.SameLine();

        ImGui.BeginGroup();
        {
            DrawMacroIconPicker();
        }
        ImGui.EndGroup();

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        if (ImGuiUtil.IconButton(FontAwesomeIcon.Plus, $"##AddCommandBtn", "Add Command")) {
            var newCommand = new Command { Cids = new(), Actions = "" };
            MacroItem.Commands.Add(newCommand);
        }

        ImGui.SameLine();
        ImGui.TextUnformatted("Commands");
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
        //https://github.com/ocornut/imgui/blob/master/imgui_demo.cpp

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
            if (isSelected) {
                ImGui.PushStyleColor(ImGuiCol.Header, Style.Components.ButtonBlueHovered);
                ImGui.PushStyleColor(ImGuiCol.HeaderHovered, Style.Components.ButtonBlueHovered);
                ImGui.PushStyleColor(ImGuiCol.HeaderActive, Style.Components.ButtonBlueHovered);
            }

            string label = $"Command ({commandIndex + 1})";
            if (ImGui.Selectable(label, isSelected)) {
                SelectedCommandIndex = commandIndex;
            }
            ImGuiUtil.ToolTip($"Drag to reorder");

            if (isSelected)
                ImGui.PopStyleColor(3);

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
        var usedCids = MacroItem.Commands?
        .SelectMany(c => c.Cids)
        .ToHashSet() ?? new HashSet<ulong>();

        var availableCharacters = Plugin.Config.Characters
        .Where(character => !usedCids.Contains(character.Cid))
        .ToList();

        ImGui.BeginGroup();
        ImGui.BeginChild("##CommandEditor", new Vector2(0, -ImGui.GetFrameHeightWithSpacing()));
        ImGuiUtil.DrawColoredBanner($"COMMAND {SelectedCommandIndex + 1}", Style.Components.ButtonBlueHovered);
        ImGui.Spacing();
        ImGui.Spacing();

        float totalWidth = ImGui.GetContentRegionAvail().X;
        float characterListWidth = totalWidth * 0.6f;
        float characterComboWidth = totalWidth * 0.4f - ImGui.GetStyle().ItemSpacing.X - 20 * ImGuiHelpers.GlobalScale;

        // horizontal layout
        ImGui.BeginGroup();
        ImGui.BeginChild($"##CharactersListChild_command_{commandIndex}", new Vector2(characterListWidth, 150), true);
        ImGui.TextUnformatted(Language.CharactersLabel);

        if (ImGui.BeginListBox($"##CharactersList_command_{commandIndex}", new Vector2(-1, -1))) {
            for (var characterIndex = 0; characterIndex < MacroItem.Commands[commandIndex].Cids.Count; characterIndex++) {
                var targetCid = MacroItem.Commands[commandIndex].Cids[characterIndex];
                var character = Plugin.Config.Characters.FirstOrDefault(c => c.Cid == targetCid)
                    ?? new Character { Cid = targetCid, Name = $"Unknown ({targetCid})" };

                if (ImGui.Selectable($"{character.Name}##command_{commandIndex}_character_{characterIndex}", false)) {
                    if (ImGui.GetIO().KeyCtrl) {
                        MacroItem.Commands[commandIndex].Cids.RemoveAll(cid => cid == targetCid);
                    }
                }
                ImGuiUtil.ToolTip(Language.DeleteInstructionTooltip);
            }
            ImGui.EndListBox();
        }
        ImGui.EndChild();

        ImGui.SameLine();

        ImGui.BeginGroup();
        ImGui.BeginDisabled(availableCharacters.Count == 0);

        ImGui.PushStyleColor(ImGuiCol.Border, Style.Components.TooltipBorderColor);
        ImGui.PushStyleVar(ImGuiStyleVar.PopupBorderSize, 1);
        ImGui.PushItemWidth(characterComboWidth);
        var charactersListPreviewLabel = Plugin.Config.Characters.Count == 0 ? "Set up the characters first" : "Select character to add";
        if (ImGui.BeginCombo($"##CharacterSelectList_command_{commandIndex}", charactersListPreviewLabel)) {
            foreach (var character in availableCharacters) {
                if (ImGui.Selectable($"{character.Name}##Cid_{character.Cid}", false)) {
                    MacroItem.Commands[commandIndex].Cids.AddUnique(character.Cid);
                }
            }
            ImGui.EndCombo();
        }
        ImGui.PopItemWidth();


        ImGui.Spacing();
        ImGui.PushItemWidth(characterComboWidth);
        if (ImGui.BeginCombo($"##CidsGroupSelectList_command_{commandIndex}", "Select group to add")) {
            for (var groupIndex = 0; groupIndex < Plugin.Config.CidsGroups.Count; groupIndex++) {
                if (ImGui.Selectable($"{Plugin.Config.CidsGroups[groupIndex].Name}##CidGroup_{groupIndex}", false)) {
                    var availableCidsToAdd = Plugin.Config.CidsGroups[groupIndex].Cids
                        .Where(cid => !MacroItem.Commands[commandIndex].Cids.Contains(cid)).ToList();

                    if (availableCidsToAdd.Count == 0)
                        DalamudApi.ShowNotification($"No available characters to add", NotificationType.Info, 3000);

                    MacroItem.Commands[commandIndex].Cids.AddRangeUnique(availableCidsToAdd);
                }
            }
            ImGui.EndCombo();
        }
        ImGui.PopItemWidth();

        ImGui.PopStyleVar();
        ImGui.PopStyleColor();

        ImGui.EndDisabled();

        ImGui.Spacing();

        ImGui.PushStyleColor(ImGuiCol.Button, Style.Components.ButtonDangerNormal);
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, Style.Components.ButtonDangerHovered);
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, Style.Components.ButtonDangerActive);
        if (ImGui.Button(Language.RemoveAllBtn)) {
            if (ImGui.GetIO().KeyCtrl) {
                MacroItem.Commands[commandIndex].Cids = new();
            }
        }
        ImGuiUtil.ToolTip(Language.DeleteInstructionTooltip);
        ImGui.PopStyleColor(3);

        ImGui.EndGroup();
        ImGui.EndGroup();

        ImGui.Spacing();
        ImGui.Spacing();
    }

    private void DrawCommandEditor(int commandIndex) {

        DrawCharacterAssignList(commandIndex);

        ImGui.TextUnformatted(Language.ActionsTitle);
        ImGuiUtil.HelpMarker("Press TAB to autocomplete");
        ImGui.Spacing();

        if (InputTextMultiline.Draw(
            $"##InputActionCommand_{commandIndex}",
            ref MacroItem.Commands[commandIndex].Actions,
            ushort.MaxValue,
            new Vector2(
                MathF.Min(ImGui.GetContentRegionAvail().X, 500f * ImGuiHelpers.GlobalScale),
                ImGui.GetTextLineHeight() * 20
            ),
            ImGuiInputTextFlags.None
        )) {
            // DalamudApi.PluginLog.Debug($"{_inputTextContent}");
        }

        ImGui.Spacing();
        ImGui.Spacing();

        ImGui.EndChild();
        ImGui.EndGroup();
    }

    private void DrawMacroIconPicker() {
        ImGui.TextUnformatted(Language.MacroIconLabel);
        var iconSize = ImGuiHelpers.ScaledVector2(30, 30);
        // uint undefinedIconId = 60042;
        // var icon = DalamudApi.TextureProvider.GetFromGameIcon(MacroItem.IconId).GetWrapOrEmpty().Handle;
        var icon = DalamudApi.TextureProvider.GetMacroIcon(MacroItem.IconId).GetWrapOrEmpty().Handle;
        // ImGui.Image(icon, iconSize);

        var drawList = ImGui.GetWindowDrawList();
        Vector2 pos = ImGui.GetCursorScreenPos();
        Vector2 size = iconSize;
        ImGui.Image(icon, size);
        uint borderColor = ImGui.ColorConvertFloat4ToU32(Style.Components.TooltipBorderColor);
        float thickness = 1f;
        drawList.AddRect(pos, pos + size, borderColor, 0f, ImDrawFlags.None, thickness);

        if (ImGui.IsItemClicked()) {
            Ui.IconPickerDialogWindow.Open(MacroItem.IconId, (selectedIconId) => {
                MacroItem.IconId = selectedIconId;
                // DalamudApi.PluginLog.Debug($"selectedIconId: {selectedIconId}");
            });
        }
    }
}
