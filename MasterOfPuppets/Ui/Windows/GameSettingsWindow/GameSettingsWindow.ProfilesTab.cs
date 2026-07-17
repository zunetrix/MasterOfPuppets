using System;
using System.Numerics;

using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.ImGuiNotification;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;

using MasterOfPuppets.Resources;
using MasterOfPuppets.Util.ImGuiExt;

namespace MasterOfPuppets;

public partial class GameSettingsWindow {

    private void DrawProfilesTab() {
        float splitterW = 10f * ImGuiHelpers.GlobalScale;
        float minLeftW = 350f * ImGuiHelpers.GlobalScale;
        float minRightW = 300f * ImGuiHelpers.GlobalScale;

        var avail = ImGui.GetContentRegionAvail();

        if (_leftPanelWidth <= 0f) _leftPanelWidth = 270f * ImGuiHelpers.GlobalScale;
        if (_rightPanelWidth <= 0f) _rightPanelWidth = avail.X - _leftPanelWidth - splitterW;

        float maxLeftW = MathF.Max(avail.X - splitterW - minRightW, minLeftW);
        _leftPanelWidth = Math.Clamp(_leftPanelWidth, minLeftW, maxLeftW);
        _rightPanelWidth = MathF.Max(avail.X - _leftPanelWidth - splitterW, minRightW);

        float h = avail.Y;

        // Left: profile list
        using (ImRaii.Child("##GscLeft", new Vector2(_leftPanelWidth, h), true)) {
            DrawProfileListPanel();
        }

        ImGui.SameLine(0, 0);

        // Splitter
        using (ImRaii.PushStyle(ImGuiStyleVar.FramePadding, Vector2.Zero)) {
            ImGui.InvisibleButton("##GscSplit", new Vector2(splitterW, h));
            if (ImGui.IsItemHovered()) ImGui.SetMouseCursor(ImGuiMouseCursor.ResizeEw);
            if (ImGui.IsItemActive() && ImGui.IsMouseDragging(ImGuiMouseButton.Left)) {
                _leftPanelWidth += ImGui.GetIO().MouseDelta.X;
                _leftPanelWidth = Math.Clamp(_leftPanelWidth, minLeftW, maxLeftW);
            }
        }

        ImGui.SameLine(0, 0);

        // Right: keys view for selected profile
        using (ImRaii.Child("##GscRight", new Vector2(_rightPanelWidth, h), true)) {
            DrawProfileKeysPanel();
        }
    }

    //  Left panel — profile list
    private void DrawProfileListPanel() {
        var profiles = Plugin.Config.GameSettingsProfiles;

        // Toolbar
        using (ImRaii.Group()) {
            if (ImGuiUtil.IconButtonStyled(FontAwesomeIcon.Plus, ImGuiUtil.IconButtonStyle.Primary,
                    "##GscPAdd", "New profile"))
                ImGui.OpenPopup("##GscPNew");

            DrawNewProfilePopup(profiles);

            ImGui.SameLine();
            ImGuiUtil.HelpMarker("""
            Use this option to create Game Settings profiles. Simply adjust your settings in the native game's settings menu and create a new profile. The current settings will be saved as a snapshot that can be reapplied at any time.

            To edit an existing profile, apply it locally, make the desired changes in the game's settings menu, and select Update Snapshot to overwrite the profile with the new settings.

            This is especially useful for maintaining a Low Settings profile for your alts and a High Settings profile for your main character. You can also combine it with the Login Macro feature to automatically apply a specific settings profile whenever a particular character logs in.
            """);

            ImGui.SameLine();
            ImGui.SetNextItemWidth(-1);
            ImGui.InputTextWithHint("##GscPSearch", Language.SearchInputLabel,
                ref _profileSearchFilter, 64);
        }

        ImGui.Separator();

        float btnW = ImGui.GetFrameHeight();
        float spc = ImGui.GetStyle().ItemSpacing.X + 3 * ImGuiHelpers.GlobalScale;
        float actColW = btnW * 5 + spc * 4;

        float listH = ImGui.GetContentRegionAvail().Y;

        if (!ImGui.BeginTable("##GscPTbl", 3,
                ImGuiTableFlags.RowBg | ImGuiTableFlags.NoSavedSettings |
                ImGuiTableFlags.BordersInnerV | ImGuiTableFlags.ScrollY,
                new Vector2(-1, listH)))
            return;

        ImGui.TableSetupScrollFreeze(0, 1);
        ImGui.TableSetupColumn("#", ImGuiTableColumnFlags.WidthFixed, 28f);
        ImGui.TableSetupColumn("Profile", ImGuiTableColumnFlags.WidthStretch);
        ImGui.TableSetupColumn("##GscPActs", ImGuiTableColumnFlags.WidthFixed, actColW);
        ImGui.TableHeadersRow();

        for (int i = 0; i < profiles.Count; i++) {
            var profile = profiles[i];
            if (!string.IsNullOrEmpty(_profileSearchFilter) &&
                !profile.Name.Contains(_profileSearchFilter, StringComparison.OrdinalIgnoreCase))
                continue;

            ImGui.PushID($"gsc_p_{i}");
            ImGui.TableNextRow();

            // Col 0: index
            ImGui.TableNextColumn();
            ImGui.TextDisabled($"{i + 1}");

            // Col 1: name / rename input
            ImGui.TableNextColumn();
            if (_renamingProfileIdx == i) {
                if (_renamingProfileFocusPending) {
                    ImGui.SetKeyboardFocusHere();
                    _renamingProfileFocusPending = false;
                }
                ImGui.SetNextItemWidth(-1);
                if (ImGui.InputText("##GscPRenIn", ref _renameProfileBuffer, 64,
                        ImGuiInputTextFlags.EnterReturnsTrue | ImGuiInputTextFlags.AutoSelectAll))
                    CommitProfileRename(i);
                if (ImGui.IsItemDeactivated() && _renamingProfileIdx == i)
                    CommitProfileRename(i);
            } else {
                bool isSelected = _selectedProfileIdx == i;
                using (ImRaii.PushColor(ImGuiCol.Header, Style.Components.ButtonBlueHovered, isSelected)
                            .Push(ImGuiCol.HeaderHovered, Style.Components.ButtonBlueHovered)
                            .Push(ImGuiCol.HeaderActive, Style.Components.ButtonBlueHovered)) {
                    if (ImGui.Selectable(profile.Name, isSelected)) {
                        _selectedProfileIdx = isSelected ? -1 : i;
                    }
                }

                // Drag-and-drop reorder
                if (ImGui.BeginDragDropSource()) {
                    unsafe {
                        ImGui.SetDragDropPayload("DND_GSC_PROFILE",
                            new ReadOnlySpan<byte>(&i, sizeof(int)), ImGuiCond.None);
                        ImGui.Text(profile.Name);
                    }
                    ImGui.EndDragDropSource();
                }
                using (ImRaii.PushColor(ImGuiCol.DragDropTarget, Style.Components.DragDropTarget)) {
                    if (ImGui.BeginDragDropTarget()) {
                        var payload = ImGui.AcceptDragDropPayload("DND_GSC_PROFILE");
                        bool dropping = false;
                        unsafe { dropping = !payload.IsNull; }
                        if (dropping && payload.IsDelivery()) {
                            unsafe {
                                int from = *(int*)payload.Data;
                                if (from != i) {
                                    var tmp = profiles[from];
                                    profiles.RemoveAt(from);
                                    profiles.Insert(i, tmp);
                                    if (_selectedProfileIdx == from) _selectedProfileIdx = i;
                                    else if (_selectedProfileIdx > from && _selectedProfileIdx <= i) _selectedProfileIdx--;
                                    else if (_selectedProfileIdx < from && _selectedProfileIdx >= i) _selectedProfileIdx++;
                                    Plugin.Config.Save();
                                    Plugin.IpcProvider.SyncConfiguration();
                                }
                            }
                        }
                        ImGui.EndDragDropTarget();
                    }
                }
            }

            // Col 2: action buttons
            ImGui.TableNextColumn();

            if (ImGuiUtil.IconButtonStyled(FontAwesomeIcon.Trash, ImGuiUtil.IconButtonStyle.Danger,
                    "##GscPDel", Language.DeleteInstructionTooltip) && ImGui.GetIO().KeyCtrl) {
                profiles.RemoveAt(i);
                if (_renamingProfileIdx == i) _renamingProfileIdx = -1;
                else if (_renamingProfileIdx > i) _renamingProfileIdx--;
                if (_selectedProfileIdx == i) _selectedProfileIdx = -1;
                else if (_selectedProfileIdx > i) _selectedProfileIdx--;
                Plugin.Config.Save();
                Plugin.IpcProvider.SyncConfiguration();
                ImGui.PopID();
                break;
            }

            ImGui.SameLine();
            if (ImGuiUtil.IconButton(FontAwesomeIcon.Pen, "##GscPRnm", "Rename")) {
                _renamingProfileIdx = i;
                _renameProfileBuffer = profile.Name;
                _renamingProfileFocusPending = true;
            }

            ImGui.SameLine();
            if (ImGuiUtil.IconButton(FontAwesomeIcon.Save, "##GscPSave",
                    "Update snapshot from current game settings configuration")) {
                var updated = GameSettingsManager.CreateProfile(profile.Name,
                    Plugin.Config.GameSettingsProfileKeys);
                Plugin.Config.GameSettingsProfiles[i] = updated;
                Plugin.Config.Save();
                Plugin.IpcProvider.SyncConfiguration();
                DalamudApi.ShowNotification("Game Settings Profile Updated",
                    NotificationType.Info, 5000);
            }

            ImGui.SameLine();
            if (ImGuiUtil.IconButtonStyled(FontAwesomeIcon.Play, ImGuiUtil.IconButtonStyle.Success,
                    "##GscPApp", "Apply locally")) {
                GameSettingsManager.ApplyProfile(profile, Plugin.Config.GameSettingsProfileKeys);
            }

            ImGui.SameLine();
            if (ImGuiUtil.IconButtonStyled(FontAwesomeIcon.BroadcastTower,
                    ImGuiUtil.IconButtonStyle.Success, "##GscPBr",
                    "Broadcast apply to all clients")) {
                Plugin.IpcProvider.BroadcastApplyGameSettingsProfile(profile.Name);
            }

            ImGui.PopID();
        }

        ImGui.EndTable();
    }

    private void DrawNewProfilePopup(System.Collections.Generic.List<GameSettingsProfile> profiles) {
        using (ImRaii.PushColor(ImGuiCol.Border, Style.Components.TooltipBorderColor))
        using (ImRaii.PushStyle(ImGuiStyleVar.PopupBorderSize, 1)) {
            if (!ImGui.BeginPopup("##GscPNew")) return;

            ImGui.Text("Name:");
            ImGui.SameLine();
            ImGui.SetNextItemWidth(180);
            bool enter = ImGui.InputText("##GscPNewName", ref _newProfileName, 64,
                ImGuiInputTextFlags.EnterReturnsTrue);

            bool dupName = !string.IsNullOrWhiteSpace(_newProfileName) &&
                profiles.Exists(p => p.Name.Equals(_newProfileName.Trim(),
                    StringComparison.OrdinalIgnoreCase));
            if (dupName) {
                ImGui.SameLine();
                ImGui.TextColored(Style.Colors.Yellow, "already exists");
            }

            if ((enter || ImGui.Button("Create")) &&
                !string.IsNullOrWhiteSpace(_newProfileName) && !dupName) {
                var profile = GameSettingsManager.CreateProfile(_newProfileName.Trim(),
                    Plugin.Config.GameSettingsProfileKeys);
                profiles.Add(profile);
                _newProfileName = string.Empty;
                Plugin.Config.Save();
                Plugin.IpcProvider.SyncConfiguration();
                ImGui.CloseCurrentPopup();
            }

            ImGui.EndPopup();
        }
    }

    //  Right panel — keys read-only view

    private void DrawProfileKeysPanel() {
        if (_selectedProfileIdx < 0 ||
            _selectedProfileIdx >= Plugin.Config.GameSettingsProfiles.Count) {
            var sz = ImGui.GetContentRegionAvail();
            ImGui.SetCursorPos(new Vector2(
                sz.X * 0.5f - ImGui.CalcTextSize("Select a profile").X * 0.5f,
                sz.Y * 0.5f));
            ImGui.TextDisabled("Select a profile");
            return;
        }

        var profile = Plugin.Config.GameSettingsProfiles[_selectedProfileIdx];

        ImGui.Text($"Profile: {profile.Name}");
        ImGui.Separator();
        ImGui.Spacing();

        // Search
        ImGui.SetNextItemWidth(-1);
        ImGui.InputTextWithHint("##GscKVSearch", Language.SearchInputLabel,
            ref _keysViewSearchFilter, 64);

        ImGui.Spacing();

        float tableH = ImGui.GetContentRegionAvail().Y;
        if (!ImGui.BeginTable("##GscKVTbl", 3,
                ImGuiTableFlags.RowBg | ImGuiTableFlags.BordersInnerV |
                ImGuiTableFlags.ScrollY | ImGuiTableFlags.NoSavedSettings,
                new Vector2(-1, tableH)))
            return;

        ImGui.TableSetupScrollFreeze(0, 1);
        ImGui.TableSetupColumn("#", ImGuiTableColumnFlags.WidthFixed, 34f);
        ImGui.TableSetupColumn("Key", ImGuiTableColumnFlags.WidthStretch);
        ImGui.TableSetupColumn("Value", ImGuiTableColumnFlags.WidthFixed, 100f * ImGuiHelpers.GlobalScale);
        ImGui.TableHeadersRow();

        int rowIdx = 0;

        // Combine all snapshots into one display list
        void RenderSnapshot(GameSettingsSnapshot snap) {
            foreach (var kv in snap.UIntSettings) {
                if (!string.IsNullOrEmpty(_keysViewSearchFilter) &&
                    !kv.Key.Contains(_keysViewSearchFilter, StringComparison.OrdinalIgnoreCase))
                    continue;
                ImGui.TableNextRow();
                ImGui.TableNextColumn(); ImGui.TextDisabled($"{++rowIdx}");
                ImGui.TableNextColumn(); ImGui.TextUnformatted(kv.Key);
                ImGui.TableNextColumn(); ImGui.TextUnformatted(kv.Value.ToString());
            }
            foreach (var kv in snap.FloatSettings) {
                if (!string.IsNullOrEmpty(_keysViewSearchFilter) &&
                    !kv.Key.Contains(_keysViewSearchFilter, StringComparison.OrdinalIgnoreCase))
                    continue;
                ImGui.TableNextRow();
                ImGui.TableNextColumn(); ImGui.TextDisabled($"{++rowIdx}");
                ImGui.TableNextColumn(); ImGui.TextUnformatted(kv.Key);
                ImGui.TableNextColumn(); ImGui.TextUnformatted(kv.Value.ToString("F4"));
            }
            foreach (var kv in snap.StringSettings) {
                if (!string.IsNullOrEmpty(_keysViewSearchFilter) &&
                    !kv.Key.Contains(_keysViewSearchFilter, StringComparison.OrdinalIgnoreCase))
                    continue;
                ImGui.TableNextRow();
                ImGui.TableNextColumn(); ImGui.TextDisabled($"{++rowIdx}");
                ImGui.TableNextColumn(); ImGui.TextUnformatted(kv.Key);
                ImGui.TableNextColumn(); ImGui.TextUnformatted(kv.Value);
            }
        }

        RenderSnapshot(profile.System);
        RenderSnapshot(profile.Ui);
        RenderSnapshot(profile.UiControl);

        if (rowIdx == 0) {
            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            ImGui.TableNextColumn();
            ImGui.TextDisabled("No keys stored in this profile");
        }

        ImGui.EndTable();
    }
}
