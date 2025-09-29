using System;
using System.Linq;
using System.Numerics;
using System.Collections.Generic;

using Dalamud.Interface;
using Dalamud.Interface.Windowing;
using Dalamud.Interface.Utility;
using Dalamud.Interface.ImGuiNotification;
using Dalamud.Bindings.ImGui;

using MasterOfPuppets.Resources;
using MasterOfPuppets.Util.ImGuiExt;
using MasterOfPuppets.Extensions;

namespace MasterOfPuppets;

public class MacroEditorWindow : Window
{
    private Plugin Plugin { get; }
    private Macro MacroItem = new() { Commands = new List<Command>() };
    private int MacroIndex;
    private int SelectedCommandIndex = 0;
    private bool EditingExistingMacro = false;
    private List<string> _suggestions = [];
    private string _currentWord = string.Empty;
    private string _lastWord = string.Empty;

    public MacroEditorWindow(Plugin plugin) : base($"{Plugin.Name} {Language.MacroEditorTitle}###MacroEditorWindow")
    {
        Plugin = plugin;

        Size = ImGuiHelpers.ScaledVector2(700, 550);
        SizeCondition = ImGuiCond.FirstUseEver;
        // SizeCondition = ImGuiCond.Always;
    }

    public override void PreDraw()
    {
        base.PreDraw();
    }

    public override void OnClose()
    {
        var isMacroSaved = false;
        if (Plugin.Config.AutoSaveMacro && EditingExistingMacro)
        {
            isMacroSaved = SaveMacro();
        }

        if (isMacroSaved)
        {
            RessetState();
            base.OnClose();
        }
    }

    private void RessetState()
    {
        MacroItem = new() { Commands = new List<Command>() };
        EditingExistingMacro = false;
        MacroIndex = Plugin.MacroManager.GetMacrosCount();
        SelectedCommandIndex = 0;

        _suggestions = [];
        _currentWord = string.Empty;
        _lastWord = string.Empty;
    }

    public void EditMacro(int macroIndex)
    {
        RessetState();

        MacroItem = Plugin.MacroManager.GetMacroByIndex(macroIndex);
        MacroIndex = macroIndex;
        EditingExistingMacro = true;

        this.Toggle();
    }

    public void AddNewMacro()
    {
        RessetState();
        this.Toggle();
    }

    private bool SaveMacro()
    {
        var isNewMacro = MacroIndex == Plugin.MacroManager.GetMacrosCount();

        bool isMacroSaved;
        if (isNewMacro)
        {
            isMacroSaved = Plugin.MacroManager.AddMacro(MacroItem);
        }
        else
        {
            isMacroSaved = Plugin.MacroManager.UpdateMacro(MacroIndex, MacroItem);
        }

        return isMacroSaved;
    }

    public override void Draw()
    {
        ImGui.BeginChild("##MacroEditorHeaderFixedHeight", ImGuiHelpers.ScaledVector2(-1, 90), false, ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse);
        DrawHeader();
        ImGui.EndChild();

        ImGui.BeginChild("##MacroEditorListScrollableContent", ImGuiHelpers.ScaledVector2(-1, 0), false, ImGuiWindowFlags.HorizontalScrollbar);
        DrawCommandList();
        ImGui.EndChild();
    }

    private void DrawHeader()
    {
        ImGui.Text(Language.MacroNameLabel);
        ImGui.InputText("##InputMacroName", ref MacroItem.Name);

        ImGui.SameLine();
        ImGui.PushStyleColor(ImGuiCol.Button, Style.Components.ButtonSuccessnNormal);
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, Style.Components.ButtonSuccessHovered);
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, Style.Components.ButtonSuccessActive);
        if (ImGuiUtil.IconButton(FontAwesomeIcon.Save, $"##SaveMacroBtn", Language.SaveMacroBtn))
        {
            SaveMacro();
            this.IsOpen = false;
        }
        ImGui.PopStyleColor(3);

        ImGui.SameLine();

        var autoSaveMacro = Plugin.Config.AutoSaveMacro;
        if (ImGui.Checkbox(Language.SettingsWindowAutoSaveMacro, ref autoSaveMacro))
        {
            Plugin.Config.AutoSaveMacro = autoSaveMacro;
            Plugin.IpcProvider.SyncConfiguration();
        }

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        if (ImGuiUtil.IconButton(FontAwesomeIcon.Plus, $"##AddCommandBtn", "Add Command"))
        {
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
        if (ImGuiUtil.IconButton(FontAwesomeIcon.Question, $"##ShowMacroHelpBtn", Language.ShowMacroHelpBtn))
        {
            Plugin.Ui.MacroHelpWindow.Toggle();
        }

        ImGui.SameLine();
        if (ImGuiUtil.IconButton(FontAwesomeIcon.Users, $"##ShowCharactersBtn", Language.ShowCharactersBtn))
        {
            Plugin.Ui.CharactersWindow.Toggle();
        }

        ImGui.Spacing();
        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();
        ImGui.Spacing();
    }

    private unsafe void DrawCommandList()
    {
        //https://github.com/ocornut/imgui/blob/master/imgui_demo.cpp

        // new macro dont have any command
        if (MacroItem.Commands == null || MacroItem.Commands.Count == 0) return;

        // left pane
        ImGui.BeginChild("##CommandList", ImGuiHelpers.ScaledVector2(150, 0), true);
        for (var commandIndex = 0; commandIndex < MacroItem.Commands.Count; commandIndex++)
        {
            if (ImGuiUtil.IconButton(FontAwesomeIcon.Trash, $"##RemoveCommand_{commandIndex}", Language.DeleteInstructionTooltip))
            {
                if (ImGui.GetIO().KeyCtrl)
                {
                    MacroItem.Commands.RemoveAt(commandIndex);
                }
            }

            ImGui.SameLine();

            bool isSelected = SelectedCommandIndex == commandIndex;
            if (isSelected)
            {
                ImGui.PushStyleColor(ImGuiCol.Header, Style.Components.ButtonBlueHovered);
                ImGui.PushStyleColor(ImGuiCol.HeaderHovered, Style.Components.ButtonBlueHovered);
                ImGui.PushStyleColor(ImGuiCol.HeaderActive, Style.Components.ButtonBlueHovered);
            }

            string label = $"Command ({commandIndex + 1})";
            if (ImGui.Selectable(label, isSelected))
            {
                SelectedCommandIndex = commandIndex;
            }
            ImGuiUtil.ToolTip($"Drag to reorder");

            if (isSelected)
                ImGui.PopStyleColor(3);

            if (ImGui.BeginDragDropSource())
            {
                ImGui.SetDragDropPayload("DND_COMMAND_LIST", new ReadOnlySpan<byte>(&commandIndex, sizeof(int)), ImGuiCond.None);
                ImGui.Button(label);
                // DalamudApi.PluginLog.Warning($"Drag start [{commandIndex}]: {commandIndex}");
                ImGui.EndDragDropSource();
            }

            ImGui.PushStyleColor(ImGuiCol.DragDropTarget, Style.Components.DragDropTarget);
            if (ImGui.BeginDragDropTarget())
            {
                ImGuiPayloadPtr dragDropPayload = ImGui.AcceptDragDropPayload("DND_COMMAND_LIST");
                bool isDropping = false;
                isDropping = !dragDropPayload.IsNull;

                if (isDropping && dragDropPayload.IsDelivery())
                {
                    int originalIndex = *(int*)dragDropPayload.Data;

                    int offset = commandIndex - originalIndex;
                    if (offset != 0 && originalIndex + offset >= 0)
                    {
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

    private void DrawCommandEditor(int commandIndex)
    {
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

        if (ImGui.BeginListBox($"##CharactersList_command_{commandIndex}", new Vector2(-1, -1)))
        {
            for (var characterIndex = 0; characterIndex < MacroItem.Commands[commandIndex].Cids.Count; characterIndex++)
            {
                var targetCid = MacroItem.Commands[commandIndex].Cids[characterIndex];
                var character = Plugin.Config.Characters.FirstOrDefault(c => c.Cid == targetCid)
                    ?? new Character { Cid = targetCid, Name = $"Unknown ({targetCid})" };

                if (ImGui.Selectable($"{character.Name}##command_{commandIndex}_character_{characterIndex}", false))
                {
                    if (ImGui.GetIO().KeyCtrl)
                    {
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
        if (ImGui.BeginCombo($"##CharacterSelectList_command_{commandIndex}", charactersListPreviewLabel))
        {
            foreach (var character in availableCharacters)
            {
                if (ImGui.Selectable($"{character.Name}##Cid_{character.Cid}", false))
                {
                    MacroItem.Commands[commandIndex].Cids.AddUnique(character.Cid);
                }
            }
            ImGui.EndCombo();
        }
        ImGui.PopItemWidth();


        ImGui.Spacing();
        ImGui.PushItemWidth(characterComboWidth);
        if (ImGui.BeginCombo($"##CidsGroupSelectList_command_{commandIndex}", "Select group to add"))
        {
            for (var groupIndex = 0; groupIndex < Plugin.Config.CidsGroups.Count; groupIndex++)
            {
                if (ImGui.Selectable($"{Plugin.Config.CidsGroups[groupIndex].Name}##CidGroup_{groupIndex}", false))
                {
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
        if (ImGui.Button(Language.RemoveAllBtn))
        {
            if (ImGui.GetIO().KeyCtrl)
            {
                MacroItem.Commands[commandIndex].Cids = new();
            }
        }
        ImGuiUtil.ToolTip(Language.DeleteInstructionTooltip);
        ImGui.PopStyleColor(3);

        ImGui.EndGroup();
        ImGui.EndGroup();

        ImGui.Spacing();
        ImGui.Spacing();
        ImGui.TextUnformatted(Language.ActionsTitle);
        if (ImGui.InputTextMultiline($"##InputAction_command_{commandIndex}", ref MacroItem.Commands[commandIndex].Actions, 65535, new Vector2(-1, 200)))
        {
            _currentWord = GetCurrentWord(MacroItem.Commands[commandIndex].Actions);

            // prevent suggestions for new line
            if (string.IsNullOrWhiteSpace(_currentWord))
            {
                _suggestions.Clear();
                _lastWord = string.Empty;
            }
        }

        if (!string.IsNullOrWhiteSpace(_currentWord) && _currentWord != _lastWord)
        {
            _suggestions = MopMacroActionsHelper.Actions
                .Select(x => x.SuggestionCommand)
                .Concat(EmoteHelper.GetAllowedItems().Select(x => x.TextCommand))
                .Concat(ItemHelper.GetAllowedItems().Select(x => x.TextCommand))
                .Where(s => s.Contains(_currentWord, StringComparison.OrdinalIgnoreCase))
                .ToList();

            _lastWord = _currentWord;
        }

        if (_suggestions.Any())
        {
            ImGui.Spacing();
            ImGui.Spacing();

            ImGui.BeginChild($"##AutoCompleteChild_{commandIndex}", new Vector2(-1, 150), true);
            int? selectedIndex = null;
            for (int i = 0; i < _suggestions.Count; i++)
            {
                if (ImGui.Selectable(_suggestions[i]))
                    selectedIndex = i;
            }

            if (selectedIndex.HasValue)
            {
                ReplaceCurrentWord(ref MacroItem.Commands[commandIndex].Actions, _currentWord, _suggestions[selectedIndex.Value]);
                _suggestions.Clear();
                _lastWord = string.Empty;
            }
            ImGui.EndChild();
        }

        ImGui.Spacing();
        ImGui.Spacing();

        ImGui.EndChild();
        ImGui.EndGroup();
    }

    private string GetCurrentWord(string text)
    {
        if (string.IsNullOrEmpty(text))
            return "";

        var lines = text.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
        var lastLine = lines.Length > 0 ? lines[^1] : "";

        if (string.IsNullOrWhiteSpace(lastLine))
            return "";

        var tokens = lastLine.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        return tokens.Length > 0 ? tokens[^1] : "";
    }

    private void ReplaceCurrentWord(ref string text, string oldWord, string newWord)
    {
        if (string.IsNullOrEmpty(oldWord)) return;
        int index = text.LastIndexOf(oldWord, StringComparison.OrdinalIgnoreCase);
        if (index >= 0)
        {
            text = text.Substring(0, index) + newWord + text.Substring(index + oldWord.Length);
        }
    }
}
