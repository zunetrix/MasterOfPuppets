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

        Size = ImGuiHelpers.ScaledVector2(700, 500);
        SizeCondition = ImGuiCond.FirstUseEver;
        // SizeCondition = ImGuiCond.Always;
    }

    public override void PreDraw()
    {
        base.PreDraw();
    }

    public override void OnClose()
    {
        if (Plugin.Config.AutoSaveMacro && EditingExistingMacro)
        {
            SaveMacro();
        }

        RessetState();

        base.OnClose();
    }

    private void RessetState()
    {
        MacroItem = new() { Commands = new List<Command>() };
        EditingExistingMacro = false;
        MacroIndex = Plugin.Config.Macros.Count;
        SelectedCommandIndex = 0;

        _suggestions = [];
        _currentWord = string.Empty;
        _lastWord = string.Empty;
    }

    public void EditMacro(int macroIndex)
    {
        RessetState();

        var isEmptyList = Plugin.Config.Macros == null || Plugin.Config.Macros.Count == 0;
        var isValidIndex = macroIndex >= 0 && macroIndex < Plugin.Config.Macros.Count;
        if (isEmptyList || !isValidIndex) return;

        MacroIndex = macroIndex;
        MacroItem = Plugin.Config.Macros[MacroIndex];
        EditingExistingMacro = true;

        this.Toggle();
    }

    public void AddNewMacro()
    {
        RessetState();
        this.Toggle();
    }

    private void SaveMacro()
    {
        var isNewMacro = MacroIndex == Plugin.Config.Macros.Count;
        MacroItem.SanitizeAllActions();

        if (isNewMacro)
        {
            Plugin.Config.Macros.Add(MacroItem);
        }
        else
        {
            Plugin.Config.Macros[MacroIndex] = MacroItem;
        }

        Plugin.Config.Save();
        Plugin.IpcProvider.SyncConfiguration();
        DalamudApi.ShowNotification($"Macro saved", NotificationType.Success, 5000);
        this.IsOpen = false;
    }

    public override void Draw()
    {
        ImGui.BeginChild("##MacroEditorHeaderFixedHeight", new Vector2(-1, 90 * ImGuiHelpers.GlobalScale), false, ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse);
        DrawHeader();
        ImGui.EndChild();

        ImGui.BeginChild("##MacroEditorListScrollableContent", new Vector2(-1, 0), false, ImGuiWindowFlags.HorizontalScrollbar);
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
            ImGuiUtil.IconButton(FontAwesomeIcon.Trash, $"##RemoveCommand_{commandIndex}", "Remove (Double Click)");
            if (ImGui.IsItemHovered())
            {
                if (ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left))
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


        // // modal delete confirm
        // if (ImGui.Button("Delete.."))
        //     ImGui.OpenPopup("Delete?");

        // // Centraliza a janela quando aparecer
        // var viewport = ImGui.GetMainViewport();
        // Vector2 center = viewport.GetCenter();
        // ImGui.SetNextWindowPos(center, ImGuiCond.Appearing, new Vector2(0.5f, 0.5f));

        // if (ImGui.BeginPopupModal("Delete?", ImGuiWindowFlags.AlwaysAutoResize))
        // {
        //     ImGui.Text("All those beautiful files will be deleted.\nThis operation cannot be undone!");
        //     ImGui.Separator();

        //     // Checkbox "Don't ask me next time"
        //     bool dontAskNextTime = false;
        //     ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(0, 0));
        //     ImGui.Checkbox("Don't ask me next time", ref dontAskNextTime);
        //     ImGui.PopStyleVar();

        //     if (ImGui.Button("OK", new Vector2(120, 0)))
        //         ImGui.CloseCurrentPopup();

        //     ImGui.SetItemDefaultFocus();
        //     ImGui.SameLine();
        //     if (ImGui.Button("Cancel", new Vector2(120, 0)))
        //         ImGui.CloseCurrentPopup();

        //     ImGui.EndPopup();
        // }
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

                if (ImGui.Selectable($"{character.Name}##command_{commandIndex}_character_{characterIndex}", false, ImGuiSelectableFlags.AllowDoubleClick))
                {
                    if (ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left))
                        MacroItem.Commands[commandIndex].Cids.RemoveAll(cid => cid == targetCid);
                }

                ImGuiUtil.ToolTip("Doubleclick to remove");
            }
            ImGui.EndListBox();
        }
        ImGui.EndChild();

        ImGui.SameLine();
        ImGui.BeginGroup();

        ImGui.BeginDisabled(availableCharacters.Count == 0);
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
        ImGui.EndDisabled();

        ImGui.Spacing();

        ImGui.PushStyleColor(ImGuiCol.Button, Style.Components.ButtonDangerNormal);
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, Style.Components.ButtonDangerHovered);
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, Style.Components.ButtonDangerActive);
        ImGui.Button(Language.RemoveAllBtn);
        if (ImGui.IsItemHovered())
        {
            if (ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left))
            {
                MacroItem.Commands[commandIndex].Cids = new();
            }
        }
        ImGuiUtil.ToolTip("Double Click");
        ImGui.PopStyleColor(3);

        ImGui.EndGroup();
        ImGui.EndGroup();

        ImGui.Spacing();
        ImGui.Spacing();
        ImGui.TextUnformatted("Actions");
        if (ImGui.InputTextMultiline($"##InputAction_command_{commandIndex}", ref MacroItem.Commands[commandIndex].Actions, 65535, new Vector2(-1, 150)))
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

    // private void DrawCommandListCollapsing()
    // {
    //     var usedCids = MacroItem.Commands?
    //     .SelectMany(c => c.Cids)
    //     .ToHashSet() ?? new HashSet<ulong>();

    //     var availableCharacters = Plugin.Config.Characters
    //     .Where(character => !usedCids.Contains(character.Cid))
    //     .ToList();

    //     // new macro dont have any command
    //     if (MacroItem.Commands == null || MacroItem.Commands.Count == 0) return;

    //     float halfWidth = ImGui.GetContentRegionAvail().X / 2f - ImGui.GetStyle().ItemSpacing.X - 10 * ImGuiHelpers.GlobalScale;
    //     float totalWidth = ImGui.GetContentRegionAvail().X;
    //     float listWidth = totalWidth * 0.6f;
    //     float comboWidth = totalWidth * 0.4f - ImGui.GetStyle().ItemSpacing.X - 20 * ImGuiHelpers.GlobalScale;

    //     for (var commandIndex = 0; commandIndex < MacroItem.Commands.Count; commandIndex++)
    //     {
    //         if (ImGuiUtil.IconButton(FontAwesomeIcon.ArrowUp, $"##MoveCommandUpBtn_command{commandIndex}", "Move Up"))
    //         {
    //             MacroItem.Commands.MoveItemToIndex(commandIndex, commandIndex - 1);
    //         }

    //         ImGui.SameLine();
    //         if (ImGuiUtil.IconButton(FontAwesomeIcon.ArrowDown, $"##MoveCommandDownBtn_command{commandIndex}", "Move Down"))
    //         {
    //             MacroItem.Commands.MoveItemToIndex(commandIndex, commandIndex + 1);
    //         }

    //         ImGui.SameLine();
    //         ImGuiUtil.IconButton(FontAwesomeIcon.Trash, $"##RemoveCommandBtn_command{commandIndex}", "Remove Command (double click)");
    //         if (ImGui.IsItemHovered())
    //         {
    //             if (ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left))
    //             {
    //                 MacroItem.Commands.RemoveAt(commandIndex);
    //             }
    //         }

    //         // prevent render if all commands deleted
    //         if (!MacroItem.Commands.IndexExists(commandIndex)) return;

    //         ImGui.SameLine();

    //         if (ImGui.CollapsingHeader($"Command ({commandIndex + 1})"))
    //         {
    //             ImGui.Indent();
    //             ImGui.Spacing();

    //             // horizontal layout
    //             ImGui.BeginGroup();
    //             ImGui.BeginChild($"##CharactersListChild_command_{commandIndex}", new Vector2(listWidth, 150), true);
    //             ImGui.TextUnformatted(Language.CharactersLabel);

    //             if (ImGui.BeginListBox($"##CharactersList_command_{commandIndex}", new Vector2(-1, -1)))
    //             {
    //                 for (var characterIndex = 0; characterIndex < MacroItem.Commands[commandIndex].Cids.Count; characterIndex++)
    //                 {
    //                     var targetCid = MacroItem.Commands[commandIndex].Cids[characterIndex];
    //                     var character = Plugin.Config.Characters.FirstOrDefault(c => c.Cid == targetCid)
    //                         ?? new Character { Cid = targetCid, Name = $"Unknown ({targetCid})" };

    //                     if (ImGui.Selectable($"{character.Name}##command_{commandIndex}_character_{characterIndex}", false, ImGuiSelectableFlags.AllowDoubleClick))
    //                     {
    //                         if (ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left))
    //                             MacroItem.Commands[commandIndex].Cids.RemoveAll(cid => cid == targetCid);
    //                     }

    //                     ImGuiUtil.ToolTip("Doubleclick to remove");
    //                 }
    //                 ImGui.EndListBox();
    //             }
    //             ImGui.EndChild();

    //             ImGui.SameLine();
    //             ImGui.BeginGroup();

    //             ImGui.BeginDisabled(availableCharacters.Count == 0);
    //             ImGui.PushItemWidth(comboWidth);
    //             var charactersListPreviewLabel = Plugin.Config.Characters.Count == 0 ? "Set up the characters first" : "Select character to add";
    //             if (ImGui.BeginCombo($"##CharacterSelectList_command_{commandIndex}", charactersListPreviewLabel))
    //             {
    //                 foreach (var character in availableCharacters)
    //                 {
    //                     if (ImGui.Selectable($"{character.Name}##Cid_{character.Cid}", false))
    //                     {
    //                         MacroItem.Commands[commandIndex].Cids.AddUnique(character.Cid);
    //                     }
    //                 }
    //                 ImGui.EndCombo();
    //             }
    //             ImGui.PopItemWidth();

    //             ImGui.Spacing();

    //             ImGui.PushItemWidth(comboWidth);
    //             if (ImGui.BeginCombo($"##CidsGroupSelectList_command_{commandIndex}", "Select group to add"))
    //             {
    //                 for (var groupIndex = 0; groupIndex < Plugin.Config.CidsGroups.Count; groupIndex++)
    //                 {
    //                     if (ImGui.Selectable($"{Plugin.Config.CidsGroups[groupIndex].Name}##CidGroup_{groupIndex}", false))
    //                     {
    //                         var availableCidsToAdd = Plugin.Config.CidsGroups[groupIndex].Cids
    //                             .Where(cid => !MacroItem.Commands[commandIndex].Cids.Contains(cid)).ToList();

    //                         if (availableCidsToAdd.Count == 0)
    //                             DalamudApi.ShowNotification($"No available characters to add", NotificationType.Info, 3000);

    //                         MacroItem.Commands[commandIndex].Cids.AddRangeUnique(availableCidsToAdd);
    //                     }
    //                 }
    //                 ImGui.EndCombo();
    //             }
    //             ImGui.PopItemWidth();
    //             ImGui.EndDisabled();

    //             ImGui.Spacing();

    //             ImGui.Button(Language.RemoveAllBtn);
    //             if (ImGui.IsItemHovered())
    //             {
    //                 if (ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left))
    //                 {
    //                     MacroItem.Commands[commandIndex].Cids = new();
    //                 }
    //             }
    //             ImGuiUtil.ToolTip("Double Click");

    //             ImGui.EndGroup();
    //             ImGui.EndGroup();

    //             ImGui.Spacing();
    //             ImGui.Spacing();
    //             ImGui.TextUnformatted("Actions");
    //             if (ImGui.InputTextMultiline($"##InputAction_command_{commandIndex}", ref MacroItem.Commands[commandIndex].Actions, 65535, new Vector2(-1, 150)))
    //             {
    //                 _currentWord = GetCurrentWord(MacroItem.Commands[commandIndex].Actions);

    //                 // prevent suggestions for new line
    //                 if (string.IsNullOrWhiteSpace(_currentWord))
    //                 {
    //                     _suggestions.Clear();
    //                     _lastWord = string.Empty;
    //                 }
    //             }

    //             if (!string.IsNullOrWhiteSpace(_currentWord) && _currentWord != _lastWord)
    //             {
    //                 _suggestions = MopMacroActionsHelper.Actions
    //                     .Select(x => x.SuggestionCommand)
    //                     .Concat(EmoteHelper.GetAllowedItems().Select(x => x.TextCommand))
    //                     .Concat(ItemHelper.GetAllowedItems().Select(x => x.TextCommand))
    //                     .Where(s => s.Contains(_currentWord, StringComparison.OrdinalIgnoreCase))
    //                     .ToList();

    //                 _lastWord = _currentWord;
    //             }

    //             if (_suggestions.Any())
    //             {
    //                 ImGui.BeginChild($"##AutoCompleteChild_{commandIndex}", new Vector2(-1, 150), true);
    //                 int? selectedIndex = null;
    //                 for (int i = 0; i < _suggestions.Count; i++)
    //                 {
    //                     if (ImGui.Selectable(_suggestions[i]))
    //                         selectedIndex = i;
    //                 }

    //                 if (selectedIndex.HasValue)
    //                 {
    //                     ReplaceCurrentWord(ref MacroItem.Commands[commandIndex].Actions, _currentWord, _suggestions[selectedIndex.Value]);
    //                     _suggestions.Clear();
    //                     _lastWord = string.Empty;
    //                 }
    //                 ImGui.EndChild();
    //             }

    //             ImGui.Spacing();
    //             ImGui.Spacing();
    //             ImGui.Spacing();
    //             ImGui.Separator();
    //             ImGui.Unindent();
    //         }

    //         ImGui.Spacing();
    //         ImGui.Spacing();
    //     }
    // }
}
