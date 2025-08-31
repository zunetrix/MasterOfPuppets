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
    private bool EditingExistingMacro = false;

    public MacroEditorWindow(Plugin plugin) : base($"{Plugin.Name} {Language.MacroEditorTitle}###MacroEditorWindow")
    {
        Plugin = plugin;

        Size = new Vector2(700, 500);
        SizeCondition = ImGuiCond.FirstUseEver;
        // SizeCondition = ImGuiCond.Always;
    }

    public override void PreDraw()
    {
        base.PreDraw();
    }

    public override void OnClose()
    {
        if (EditingExistingMacro)
        {
            SaveMacro();
        }

        base.OnClose();
    }

    private void RessetState()
    {
        MacroItem = new() { Commands = new List<Command>() };
        EditingExistingMacro = false;
        MacroIndex = Plugin.Config.Macros.Count;
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
        // MacroText = Plugin.Config.Macros[macroIndex].Commands.JsonSerialize();
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
        ImGui.Text(Language.MacroNameLabel);
        ImGui.InputText("##InputMacroName", ref MacroItem.Name);

        ImGui.SameLine();
        if (ImGuiUtil.IconButton(FontAwesomeIcon.Save, $"##SaveMacroBtn", Language.SaveMacroBtn))
        {
            SaveMacro();
        }

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        DrawCommands();
    }

    private void DrawCommandsHeader()
    {
        if (ImGuiUtil.IconButton(FontAwesomeIcon.Plus, $"##AddCommandBtn", "Add Command"))
        {
            var newCommand = new Command { Cids = new(), Actions = "" };
            MacroItem.Commands.Add(newCommand);
        }

        ImGui.SameLine();
        ImGui.TextUnformatted("Commands");
        ImGuiUtil.HelpMarker("""
        ===========================
            Special Actions
        ===========================

        /wait <time>
            Ex: /wait 3
                /wait 10

        /petbarslot <slot_number>
            Umbrella Dance:
            /petbarslot 2

        /action <action_id>
            Heavenscracker:
            /item 12042

        /item <item_id> | "Action Name"
            Peloton:
            /action 7557
            /action "Peloton"

        ---------------------------
        Call it recursively
        ---------------------------

        /mop run "macro name"
        """);

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();
    }

    private void DrawCommands()
    {
        var usedCids = MacroItem.Commands?
        .SelectMany(c => c.Cids)
        .ToHashSet() ?? new HashSet<ulong>();

        var availableCharacters = Plugin.Config.Characters
        .Where(character => !usedCids.Contains(character.Cid))
        .ToList();

        DrawCommandsHeader();

        // new macro dont have any command
        if (MacroItem.Commands == null || MacroItem.Commands.Count == 0) return;

        float halfWidth = ImGui.GetContentRegionAvail().X / 2f - ImGui.GetStyle().ItemSpacing.X - 20 * ImGuiHelpers.GlobalScale;

        for (var commandIndex = 0; commandIndex < MacroItem.Commands.Count; commandIndex++)
        {
            if (ImGuiUtil.IconButton(FontAwesomeIcon.ArrowUp, $"##MoveCommandUpBtn_command{commandIndex}", "Move Up"))
            {
                MacroItem.Commands.MoveItemToIndex(commandIndex, commandIndex - 1);
            }

            ImGui.SameLine();
            if (ImGuiUtil.IconButton(FontAwesomeIcon.ArrowDown, $"##MoveCommandDownBtn_command{commandIndex}", "Move Down"))
            {
                MacroItem.Commands.MoveItemToIndex(commandIndex, commandIndex + 1);
            }

            ImGui.SameLine();
            ImGuiUtil.IconButton(FontAwesomeIcon.Trash, $"##RemoveCommandBtn_command{commandIndex}", "Remove Command (double click)");
            if (ImGui.IsItemHovered())
            {
                if (ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left))
                {
                    MacroItem.Commands.RemoveAt(commandIndex);
                }
            }

            //prevent render if all commands deleted
            if (!MacroItem.Commands.IndexExists(commandIndex)) return;

            ImGui.SameLine();

            if (ImGui.CollapsingHeader($"Command ({commandIndex + 1})"))
            {
                ImGui.Indent();
                ImGui.BeginDisabled(availableCharacters.Count == 0);
                ImGui.TextUnformatted(Language.CharactersLabel);
                ImGui.PushItemWidth(halfWidth);
                if (ImGui.BeginCombo($"##CharacterSelectList_command{commandIndex}", "Select character to add"))
                {
                    foreach (var character in availableCharacters)
                    {
                        if (ImGui.Selectable($"{character.Name}##cid_{character.Cid}", false))
                        {
                            MacroItem.Commands[commandIndex].Cids.AddUnique(character.Cid);
                        }
                    }
                    ImGui.EndCombo();
                }
                ImGui.PopItemWidth();

                ImGui.SameLine();
                ImGui.Dummy(new Vector2(0, 20 * ImGuiHelpers.GlobalScale));
                ImGui.SameLine();

                ImGui.PushItemWidth(halfWidth);
                if (ImGui.BeginCombo($"##CidsGroupSelectList_command{commandIndex}", "Select group to add"))
                {
                    for (var groupIndex = 0; groupIndex < Plugin.Config.CidsGroups.Count; groupIndex++)
                    {
                        if (ImGui.Selectable($"{Plugin.Config.CidsGroups[groupIndex].Name}##cidGroup_{groupIndex}", false))
                        {
                            var availableCidsToAdd = Plugin.Config.CidsGroups[groupIndex].Cids.Where(cid => !usedCids.Contains(cid)).ToList();

                            if (availableCidsToAdd.Count == 0)
                            {
                                DalamudApi.ShowNotification($"No available characters to add", NotificationType.Info, 3000);
                            }

                            MacroItem.Commands[commandIndex].Cids.AddRangeUnique(availableCidsToAdd);
                        }
                    }
                    ImGui.EndCombo();
                }
                ImGui.PopItemWidth();
                ImGui.EndDisabled();

                ImGui.Spacing();

                if (ImGui.BeginListBox($"##CharactersList_command{commandIndex}", new Vector2(-1, 100)))
                {
                    for (var characterIndex = 0; characterIndex < MacroItem.Commands[commandIndex].Cids.Count; characterIndex++)
                    {
                        var targetCid = MacroItem.Commands[commandIndex].Cids[characterIndex];
                        // find cid name
                        var character = Plugin.Config.Characters.FirstOrDefault(c => c.Cid == targetCid)
                            ?? new Character { Cid = targetCid, Name = $"Unknown ({targetCid})" };

                        if (ImGui.Selectable($"{character.Name}##command{commandIndex}_character{characterIndex}", false, ImGuiSelectableFlags.AllowDoubleClick))
                        {
                            if (ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left))
                                MacroItem.Commands[commandIndex].Cids.RemoveAll(cid => cid == targetCid);
                        }
                        ImGuiUtil.ToolTip("Doubleclick to remove");
                    }
                    ImGui.EndListBox();
                }

                ImGui.Spacing();
                ImGui.Spacing();
                ImGui.TextUnformatted("Actions");
                if (ImGui.InputTextMultiline($"##InputAction_command{commandIndex}", ref MacroItem.Commands[commandIndex].Actions, 65535, new Vector2(-1, 100)))
                {
                    // Plugin.Config.MarkTargeted = macroText;
                }

                ImGui.Spacing();
                ImGui.Spacing();
                ImGui.Spacing();
                ImGui.Separator();
                ImGui.Unindent();
            }
        }

        ImGui.Spacing();
        ImGui.Spacing();
        ImGui.Spacing();
        ImGui.Spacing();
    }
}
