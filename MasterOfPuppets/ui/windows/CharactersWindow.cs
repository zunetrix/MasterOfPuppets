using System;
using System.Linq;
using System.Numerics;
using System.Collections.Generic;

using Dalamud.Interface;
using Dalamud.Interface.Windowing;
using Dalamud.Bindings.ImGui;

using MasterOfPuppets.Resources;

namespace MasterOfPuppets;

public class CharactersWindow : Window
{
    private Plugin Plugin { get; }

    public CharactersWindow(Plugin plugin) : base($"{Plugin.Name} Characters###CharactersWindow")
    {
        Plugin = plugin;

        Size = new Vector2(500, 300);
        SizeCondition = ImGuiCond.FirstUseEver;
    }

    public override void PreDraw()
    {
        base.PreDraw();
    }

    public override void Draw()
    {
        var availablePartyMembers = GetAvailablePartyMembers();

        ImGui.BeginDisabled(availablePartyMembers.Count == 0);
        ImGui.TextUnformatted(Language.CharactersLabel);

        if (ImGui.BeginCombo($"##partyMemberSelectList", "Select a party character to add"))
        {
            foreach (var partyMember in availablePartyMembers)
            {
                if (ImGui.Selectable($"{partyMember.Name}##{partyMember.Cid}", false))
                {
                    Plugin.Config.AddCharacter(partyMember);
                }
            }
            ImGui.EndCombo();
        }
        ImGui.EndDisabled();
        ImGuiUtil.HelpMarker("""
        Added characters are used to assign macro actions

        Drag to reorder
        """);

        ImGui.Spacing();
        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();
        ImGui.Spacing();
        ImGui.Spacing();

        var characters = Plugin.Config.Characters;
        if (ImGui.BeginTable("##CharactersTable", 3, ImGuiTableFlags.RowBg | ImGuiTableFlags.PadOuterX |
        ImGuiTableFlags.NoSavedSettings | ImGuiTableFlags.BordersInnerV))
        {
            ImGui.TableSetupColumn("#", ImGuiTableColumnFlags.WidthFixed);
            ImGui.TableSetupColumn("Name", ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableSetupColumn("Options", ImGuiTableColumnFlags.WidthFixed);

            for (int i = 0; i < characters.Count; i++)
            {
                ImGui.PushID(i);
                ImGui.TableNextRow();
                ImGui.TableSetColumnIndex(0);
                ImGui.TextUnformatted($"{i + 1:00}");

                ImGui.TableNextColumn();
                ImGui.Selectable($"{characters[i].Name}");

                if (ImGui.BeginDragDropSource())
                {
                    unsafe
                    {
                        ImGui.SetDragDropPayload("DND_CHARACTERS_LIST", new ReadOnlySpan<byte>(&i, sizeof(int)), ImGuiCond.None);
                        ImGui.Button($"({i + 1}) {characters[i].Name}");
                    }

                    // PluginLog.Warning($"Drag start [{i}]: {characters[i].Name}");
                    ImGui.EndDragDropSource();
                }

                ImGui.PushStyleColor(ImGuiCol.DragDropTarget, Style.Components.DragDropTarget);
                if (ImGui.BeginDragDropTarget())
                {
                    ImGuiPayloadPtr dragDropPayload = ImGui.AcceptDragDropPayload("DND_CHARACTERS_LIST");

                    bool isDropping = false;
                    unsafe
                    {
                        isDropping = !dragDropPayload.IsNull;
                    }

                    if (isDropping && dragDropPayload.IsDelivery())
                    {
                        unsafe
                        {
                            int originalIndex = *(int*)dragDropPayload.Data;

                            int offset = i - originalIndex;
                            if (offset != 0 && originalIndex + offset >= 0)
                            {
                                int targetIndex = originalIndex + offset;
                                // PluginLog.Warning($"Drag end [{i}]: [{originalIndex}, {targetIndex}] {offset}");
                                Plugin.Config.MoveCharacterToIndex(originalIndex, targetIndex);
                            }
                        }
                    }
                    ImGui.EndDragDropTarget();
                }
                ImGui.PopStyleColor();

                ImGui.TableNextColumn();
                ImGui.BeginDisabled(i == 0);
                if (ImGuiUtil.IconButton(FontAwesomeIcon.ArrowUp, $"##MoveUpCharacter_{i}", "Move Up"))
                    Plugin.Config.MoveCharacterToIndex(i, i - 1);
                ImGui.EndDisabled();


                ImGui.SameLine();
                ImGui.BeginDisabled(i == characters.Count - 1);
                if (ImGuiUtil.IconButton(FontAwesomeIcon.ArrowDown, $"##MoveDownCharacter_{i}", "Move Down"))
                    Plugin.Config.MoveCharacterToIndex(i, i + 1);
                ImGui.EndDisabled();

                ImGui.SameLine();
                if (ImGuiUtil.IconButton(FontAwesomeIcon.Trash, $"##RemoveCharacter_{i}", "Remove"))
                    Plugin.Config.RemoveCharacter(characters[i].Cid);

                ImGui.PopID();
            }

            ImGui.EndTable();
        }
    }

    private List<Character> GetAvailablePartyMembers()
    {
        var usedCids = Plugin.Config.Characters
       .Select(c => c.Cid)
       .ToHashSet() ?? new HashSet<ulong>();

        var availablePartyMembers = DalamudApi.PartyList
       .Select((partyMember) => partyMember.GetPartyMemberData())
       .Where(partyMember => !usedCids.Contains(partyMember.Cid))
       .Select(partyMember => new Character { Cid = partyMember.Cid, Name = $"{partyMember.Name}@{partyMember.World}" })
       .ToList();

        return availablePartyMembers;
    }
}
