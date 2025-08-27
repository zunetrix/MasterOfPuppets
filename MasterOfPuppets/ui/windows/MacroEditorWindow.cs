using System;
using System.Linq;
using System.Numerics;
using System.Collections.Generic;

using Dalamud.Interface;
using Dalamud.Interface.Windowing;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.ImGuiNotification;

using MasterOfPuppets.Resources;

namespace MasterOfPuppets;

public class MacroEditorWindow : Window
{
    private Plugin Plugin { get; }

    private Macro MacroItem = new() { Commands = new List<Command>() };

    private int MacroIndex;
    public MacroEditorWindow(Plugin plugin) : base(string.Format(Language.MacroEditorTitle, Plugin.Name) + "###MacroEditorWindow")
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

    private void RessetState()
    {
        MacroItem = new() { Commands = new List<Command>() };
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

        // if (ImGui.Button($"{Language.SaveMacroBtn}##SaveMacroBtn", new Vector2(40)))
        if (ImGuiUtil.IconButton(FontAwesomeIcon.Save, $"##SaveMacroBtn", Language.SaveMacroBtn))
        {
            SaveMacro();
        }

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        DrawCommands();
    }

    private void DrawCommands()
    {
        var usedCids = MacroItem.Commands?
        .SelectMany(cmd => cmd.Characters)
        .Select(c => c.Cid)
        .ToHashSet() ?? new HashSet<ulong>();

        var availablePartyMembers = DalamudApi.PartyList
        .Select((partyMember) => partyMember.GetPartyMemberData())
        .Where(pm => !usedCids.Contains(pm.Cid))
        .ToList();

        if (ImGuiUtil.IconButton(FontAwesomeIcon.Plus, $"##AddCommandBtn", "Add Command"))
        {
            MacroItem.Commands.Add(new Command { Characters = new(), Actions = "" });
        }

        ImGui.SameLine();
        ImGui.TextUnformatted("Commands");

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        // new macro dont have any command
        if (MacroItem.Commands == null || MacroItem.Commands.Count == 0) return;

        for (var commandIndex = 0; commandIndex < MacroItem.Commands.Count; commandIndex++)
        {
            if (ImGuiUtil.IconButton(FontAwesomeIcon.ArrowUp, $"##MoveCommandUpBtn_command{commandIndex}", "Move Up"))
            {
                ChangeCommmandOrder(commandIndex, -1);
            }

            ImGui.SameLine();
            if (ImGuiUtil.IconButton(FontAwesomeIcon.ArrowDown, $"##MoveCommandDownBtn_command{commandIndex}", "Move Down"))
            {
                ChangeCommmandOrder(commandIndex, 1);
            }

            ImGui.SameLine();
            ImGuiUtil.IconButton(FontAwesomeIcon.TrashAlt, $"##RemoveCommandBtn_command{commandIndex}", "Remove Command (double click)");
            if (ImGui.IsItemHovered())
            {
                if (ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left))
                {
                    MacroItem.Commands.RemoveAt(commandIndex);
                }
            }

            ImGui.SameLine();

            if (ImGui.CollapsingHeader($"Command ({commandIndex + 1})"))
            {
                ImGui.Indent();
                ImGui.BeginDisabled(availablePartyMembers.Count == 0);
                ImGui.TextUnformatted(Language.CharactersLabel);

                if (ImGui.BeginCombo($"##partyMemberSelectList_command{commandIndex}", "Select party character to add"))
                {
                    foreach (var partyMember in availablePartyMembers)
                    {
                        var playerInfo = $"{partyMember.Name}@{partyMember.World}";
                        if (ImGui.Selectable($"{playerInfo}##{partyMember.Cid}", false))
                        {
                            MacroItem.Commands[commandIndex].Characters.Add(new Character { Cid = partyMember.Cid, Name = playerInfo });
                        }
                    }
                    ImGui.EndCombo();
                }
                ImGui.EndDisabled();

                ImGui.Spacing();

                if (ImGui.BeginListBox($"##CharactersList_command{commandIndex}", new Vector2(-1, 100)))
                {
                    for (var characterIndex = 0; characterIndex < MacroItem.Commands[commandIndex].Characters.Count; characterIndex++)
                    {
                        if (ImGui.Selectable($"{MacroItem.Commands[commandIndex].Characters[characterIndex].Name}##command{commandIndex}_character{characterIndex}", false, ImGuiSelectableFlags.AllowDoubleClick))
                        {
                            if (ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left))
                                MacroItem.Commands[commandIndex].Characters.RemoveAll(c => c.Cid == MacroItem.Commands[commandIndex].Characters[characterIndex].Cid);
                        }
                        ImGuiUtil.ToolTip("Doubleclick to remove");
                    }
                    ImGui.EndListBox();
                }

                ImGui.Spacing();
                ImGui.Spacing();
                ImGui.TextUnformatted("Actions");
                if (ImGui.InputTextMultiline($"Action##InputAction_command{commandIndex}", ref MacroItem.Commands[commandIndex].Actions, 65535, new Vector2(-1, 100)))
                {
                    // Plugin.Config.MarkTargeted = macroText;
                }

                ImGui.Spacing();
                ImGui.Spacing();
                ImGui.Spacing();
                ImGui.Separator();
                ImGui.Unindent();
            }

            ImGui.Spacing();
            ImGui.Spacing();
            ImGui.Spacing();
            ImGui.Spacing();
        }
    }

    public void ChangeCommmandOrder(int itemIndex, int moveBy)
    {
        var isEmptyList = MacroItem.Commands == null || MacroItem.Commands.Count == 0;
        var isValidIndex = itemIndex >= 0 && itemIndex < MacroItem.Commands.Count;

        if (isEmptyList || !isValidIndex)
            return;

        int targetIndex = itemIndex + moveBy;
        targetIndex = Math.Clamp(targetIndex, 0, MacroItem.Commands.Count);

        if (targetIndex == itemIndex)
            return;

        var item = MacroItem.Commands[itemIndex];
        MacroItem.Commands.RemoveAt(itemIndex);

        targetIndex = Math.Clamp(targetIndex, 0, MacroItem.Commands.Count);

        MacroItem.Commands.Insert(targetIndex, item);
    }
}
