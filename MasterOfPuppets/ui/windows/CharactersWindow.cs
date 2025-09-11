using System;
using System.Linq;
using System.Numerics;
using System.Collections.Generic;

using Dalamud.Interface;
using Dalamud.Interface.Windowing;
using Dalamud.Bindings.ImGui;

using MasterOfPuppets.Resources;
using Dalamud.Utility;
using Dalamud.Interface.Utility;

namespace MasterOfPuppets;

public class CharactersWindow : Window
{
    private Plugin Plugin { get; }

    private string _tmpGroupName = "";
    private int _selectedCidGroupIndex { get; set; } = 0;

    public CharactersWindow(Plugin plugin) : base($"{Plugin.Name} Characters###CharactersWindow")
    {
        Plugin = plugin;

        Size = ImGuiHelpers.ScaledVector2(500, 300);
        SizeCondition = ImGuiCond.FirstUseEver;
    }

    public override void PreDraw()
    {
        base.PreDraw();
    }

    private bool IsValidGroup()
    {
        var isValidGroup = Plugin.Config.CidsGroups.IndexExists(_selectedCidGroupIndex) && Plugin.Config.CidsGroups.Count > 0;
        return isValidGroup;
    }

    public override void Draw()
    {
        if (!ImGui.BeginTabBar("##CharactersManagerTabs")) return;

        if (ImGui.BeginTabItem($"Characters List###CharactersTab"))
        {
            DrawPartyMemberSelector();
            DrawCharactersTable();

            ImGui.EndTabItem();
        }

        if (ImGui.BeginTabItem($"Characters Groups###CidsGroupsTab"))
        {
            DrawCidsGroupsHeader();
            DrawCidsGroupsSelector();
            DrawGroupAvailableCharacterSelector();
            DrawCidGroupCharactersList();

            ImGui.EndTabItem();
        }

        ImGui.EndTabBar();
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

    private void DrawPartyMemberSelector()
    {
        var availablePartyMembers = GetAvailablePartyMembers();

        ImGui.BeginDisabled(availablePartyMembers.Count == 0);
        ImGui.TextUnformatted(Language.CharactersLabel);

        if (ImGui.BeginCombo($"##PartyMemberSelectList", "Select a party character to add"))
        {
            foreach (var partyMember in availablePartyMembers)
            {
                if (ImGui.Selectable($"{partyMember.Name}##{partyMember.Cid}", false))
                {
                    Plugin.Config.AddCharacter(partyMember);
                    Plugin.IpcProvider.SyncConfiguration();
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
    }

    private void DrawCharactersTable()
    {
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
                        ImGui.SetDragDropPayload("DND_CHARACTER_LIST", new ReadOnlySpan<byte>(&i, sizeof(int)), ImGuiCond.None);
                        ImGui.Button($"({i + 1}) {characters[i].Name}");
                    }

                    // DalamudApi.PluginLog.Warning($"Drag start [{i}]: {characters[i].Name}");
                    ImGui.EndDragDropSource();
                }

                ImGui.PushStyleColor(ImGuiCol.DragDropTarget, Style.Components.DragDropTarget);
                if (ImGui.BeginDragDropTarget())
                {
                    ImGuiPayloadPtr dragDropPayload = ImGui.AcceptDragDropPayload("DND_CHARACTER_LIST");

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
                                // DalamudApi.PluginLog.Warning($"Drag end [{i}]: [{originalIndex}, {targetIndex}] {offset}");
                                Plugin.Config.MoveCharacterToIndex(originalIndex, targetIndex);
                                Plugin.IpcProvider.SyncConfiguration();
                            }
                        }
                    }
                    ImGui.EndDragDropTarget();
                }
                ImGui.PopStyleColor();

                ImGui.TableNextColumn();
                ImGui.BeginDisabled(i == 0);
                if (ImGuiUtil.IconButton(FontAwesomeIcon.ArrowUp, $"##MoveUpCharacter_{i}", "Move Up"))
                {
                    Plugin.Config.MoveCharacterToIndex(i, i - 1);
                    Plugin.IpcProvider.SyncConfiguration();
                }
                ImGui.EndDisabled();


                ImGui.SameLine();
                ImGui.BeginDisabled(i == characters.Count - 1);
                if (ImGuiUtil.IconButton(FontAwesomeIcon.ArrowDown, $"##MoveDownCharacter_{i}", "Move Down"))
                {
                    Plugin.Config.MoveCharacterToIndex(i, i + 1);
                    Plugin.IpcProvider.SyncConfiguration();
                }
                ImGui.EndDisabled();

                ImGui.SameLine();
                if (ImGuiUtil.IconButton(FontAwesomeIcon.Trash, $"##RemoveCharacter_{i}", "Remove"))
                {
                    Plugin.Config.RemoveCharacter(characters[i].Cid);
                    Plugin.IpcProvider.SyncConfiguration();
                }

                ImGui.PopID();
            }

            ImGui.EndTable();
        }
    }

    private void DrawCidsGroupsHeader()
    {
        ImGui.InputTextWithHint("##GroupNameInput", "Group name", ref _tmpGroupName, 255, ImGuiInputTextFlags.AutoSelectAll);

        ImGui.SameLine();
        ImGui.Dummy(ImGuiHelpers.ScaledVector2(0, 20));
        ImGui.SameLine();

        if (ImGui.Button($"Add new group##AddNewGroupBtn"))
        {
            if (_tmpGroupName.IsNullOrEmpty()) return;

            var newGroup = new CidGroup { Name = _tmpGroupName, Cids = new() };
            Plugin.Config.CidsGroups.Add(newGroup);
            _selectedCidGroupIndex = Plugin.Config.CidsGroups.Count - 1;
            _tmpGroupName = string.Empty;
            Plugin.Config.Save();
            Plugin.IpcProvider.SyncConfiguration();
        }

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();
    }

    private void DrawCidsGroupsSelector()
    {
        if (Plugin.Config.CidsGroups.Count == 0) return;

        var cidsGroups = Plugin.Config.CidsGroups;

        ImGui.TextUnformatted(Language.GroupsLabel);

        string previewGroupValue = cidsGroups.Count > 0
        && Plugin.Config.CidsGroups.IndexExists(_selectedCidGroupIndex)
        ? cidsGroups[_selectedCidGroupIndex].Name
        : "Select a group";

        if (ImGui.BeginCombo($"##CidsGroupsSelectList", previewGroupValue))
        {
            for (var groupIndex = 0; groupIndex < cidsGroups.Count; groupIndex++)
            {
                bool isGroupSelected = _selectedCidGroupIndex == groupIndex;
                if (ImGui.Selectable($"{cidsGroups[groupIndex].Name}##group_{groupIndex}", isGroupSelected))
                {
                    _selectedCidGroupIndex = groupIndex;

                    if (isGroupSelected)
                        ImGui.SetItemDefaultFocus();
                }
            }
            ImGui.EndCombo();
        }

        ImGui.SameLine();
        ImGui.Dummy(ImGuiHelpers.ScaledVector2(0, 20));
        ImGui.SameLine();

        ImGui.BeginDisabled(!Plugin.Config.CidsGroups.IndexExists(_selectedCidGroupIndex));
        if (ImGui.Button($"Delete group"))
        {
            Plugin.Config.CidsGroups.RemoveAt(_selectedCidGroupIndex);
            Plugin.Config.Save();
            Plugin.IpcProvider.SyncConfiguration();
        }
        ImGui.EndDisabled();
    }

    private void DrawGroupAvailableCharacterSelector()
    {
        if (!IsValidGroup()) return;

        var characters = Plugin.Config.Characters;
        var cidGroup = Plugin.Config.CidsGroups[_selectedCidGroupIndex];

        var availableCharacters = Plugin.Config.Characters
                .Where(character => !cidGroup.Cids.Contains(character.Cid))
                .ToList();

        ImGui.BeginDisabled(availableCharacters.Count == 0);
        ImGui.TextUnformatted(Language.CharactersLabel);

        if (ImGui.BeginCombo($"##CidGroupCharactersSelectList_cidGroup_{_selectedCidGroupIndex}", "Select a character to add to the group"))
        {
            foreach (var character in availableCharacters)
            {
                if (ImGui.Selectable($"{character.Name}##{character.Cid}", false))
                {
                    cidGroup.Cids.Add(character.Cid);
                    Plugin.Config.Save();
                    Plugin.IpcProvider.SyncConfiguration();
                }
            }
            ImGui.EndCombo();
        }
        ImGui.EndDisabled();
    }

    private void DrawCidGroupCharactersList()
    {
        if (!IsValidGroup()) return;

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        if (ImGui.BeginListBox($"##CidsGroupsCharactersList_group{_selectedCidGroupIndex}", new Vector2(-1, 200)))
        {
            for (var characterIndex = 0; characterIndex < Plugin.Config.CidsGroups[_selectedCidGroupIndex].Cids.Count; characterIndex++)
            {
                var targetCid = Plugin.Config.CidsGroups[_selectedCidGroupIndex].Cids[characterIndex];
                // find cid name
                var character = Plugin.Config.Characters.FirstOrDefault(c => c.Cid == targetCid)
                    ?? new Character { Cid = targetCid, Name = $"Unknown ({targetCid})" };

                if (ImGui.Selectable($"{character.Name}##CidsGroups{_selectedCidGroupIndex}_character{characterIndex}", false, ImGuiSelectableFlags.AllowDoubleClick))
                {
                    if (ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left))
                    {
                        Plugin.Config.CidsGroups[_selectedCidGroupIndex].Cids.RemoveAll(cid => cid == targetCid);
                        Plugin.Config.Save();
                        Plugin.IpcProvider.SyncConfiguration();
                    }

                }
                ImGuiUtil.ToolTip("Doubleclick to remove");
            }
            ImGui.EndListBox();
        }
    }
}
