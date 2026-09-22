using System;

using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;

using MasterOfPuppets.Camera;
using MasterOfPuppets.Resources;
using MasterOfPuppets.Util.ImGuiExt;

namespace MasterOfPuppets;

public class MultiboxSettingsWidget : Widget {
    public override string Title => "Multibox";
    public override FontAwesomeIcon Icon => FontAwesomeIcon.Clone;

    private float _cameraYOffset = GameCameraManager.MaxYOffset;

    public MultiboxSettingsWidget(WidgetContext ctx) : base(ctx) {
    }

    public override void Draw() {
        var Plugin = Context.Plugin;

        using (ImGuiGroupPanel.BeginGroupPanel("Multibox")) {
            var multiboxEnabled = Plugin.Config.MultiboxEnabled;
            if (ImGui.Checkbox("Enable Multibox (Remove client mutex on startup)", ref multiboxEnabled)) {
                Plugin.Config.MultiboxEnabled = multiboxEnabled;
                Plugin.IpcProvider.SyncConfiguration();
                MultiboxManager.RemoveMutexes();
            }
            ImGuiUtil.HelpMarker("Removes the FFXIV mutex to allow opening more than 2 game instances");
        }

        ImGui.Spacing();
        ImGui.Spacing();

        using (ImGuiGroupPanel.BeginGroupPanel("Render Hack")) {
            bool enabled = Plugin.GameRenderManager.Enabled;
            if (ImGui.Checkbox("Render Hack", ref enabled)) {
                if (enabled) {
                    Plugin.GameRenderManager.DisableRendering(true);
                } else
                    Plugin.GameRenderManager.DisableRendering(false);
            }
            ImGuiUtil.HelpMarker("Disables game render, turns windows black to save GPU resources");
        }

        ImGui.Spacing();
        ImGui.Spacing();

        using (ImGuiGroupPanel.BeginGroupPanel("Cam Hack")) {
            bool enabled = GameCameraManager.Enabled;
            if (ImGui.Checkbox("Cam Hack", ref enabled)) {
                if (enabled) {
                    _cameraYOffset = GameCameraManager.MaxYOffset;
                    GameCameraManager.EnableCamHighHeight();
                } else
                    GameCameraManager.Disable();
            }
            ImGuiUtil.HelpMarker("Sets game camera to a high altitude in order to save GPU resources");

            ImGui.Text($"Camera Height Offset: {GameCameraManager.YOffset}");
            ImGui.SetNextItemWidth(150 * ImGuiHelpers.GlobalScale);
            if (ImGui.DragFloat("##CameraYOffset", ref _cameraYOffset, 1f, 0f, GameCameraManager.MaxYOffset, "%.0f")) {
                float YOffset = Math.Clamp(_cameraYOffset, 0f, GameCameraManager.MaxYOffset);
                GameCameraManager.SetHeight(YOffset, true);
            }
            if (ImGui.IsItemClicked(ImGuiMouseButton.Right)) {
                _cameraYOffset = 0;
                GameCameraManager.SetHeight(0, false);
            }
            ImGui.SameLine();
            if (ImGuiUtil.IconButton(FontAwesomeIcon.Undo, "##ResetCameraOffsetBtn", "Reset")) {
                _cameraYOffset = GameCameraManager.MaxYOffset;
                GameCameraManager.SetHeight(GameCameraManager.MaxYOffset, true);
            }
        }

        ImGui.Spacing();
        ImGui.Spacing();

        using (ImGuiGroupPanel.BeginGroupPanel("Game Window")) {
            var showCharacterNameInTitle = Plugin.Config.ShowCharacterNameInWindowTitle;
            if (ImGui.Checkbox("Show Character Name In Title Bar", ref showCharacterNameInTitle)) {
                Plugin.Config.ShowCharacterNameInWindowTitle = showCharacterNameInTitle;
                Plugin.IpcProvider.SyncConfiguration();
                Plugin.IpcProvider.SetWindowTitle(showCharacterNameInTitle);
            }

            bool enabled = Plugin.Config.AllowFreeGameWindowResize;
            if (ImGui.Checkbox("Allow Free Window Resize", ref enabled)) {
                Plugin.Config.AllowFreeGameWindowResize = !Plugin.Config.AllowFreeGameWindowResize;
                Plugin.IpcProvider.SyncConfiguration();
                Plugin.IpcProvider.SetWindowResize(enabled);
            }
        }

        ImGui.Spacing();
        ImGui.Spacing();

        using (ImGuiGroupPanel.BeginGroupPanel("Auto Accept")) {
            var acceptParty = Plugin.Config.AutoAcceptPartyInvite;
            if (ImGui.Checkbox("Auto-accept party invites", ref acceptParty)) {
                Plugin.Config.AutoAcceptPartyInvite = acceptParty;
                Plugin.Config.Save();
                Plugin.IpcProvider.SyncConfiguration();
            }
            ImGuiUtil.HelpMarker("When enabled, SelectYesno dialogs for party invites are automatically confirmed. Per-character toggle available in Characters window.");
            ImGui.SameLine();
            if (ImGuiUtil.IconButton(FontAwesomeIcon.Users, "##ShowCharactersBtnParty", Language.ShowCharactersBtn))
                Plugin.Ui.CharactersWindow.Toggle();

            using (ImRaii.PushIndent()) {
                var onlyFromCharacters = Plugin.Config.AutoAcceptPartyInviteOnlyFromCharacters;
                if (ImGui.Checkbox("Only from characters list##AutoAcceptPartyInviteOnlyFromCharacters", ref onlyFromCharacters)) {
                    Plugin.Config.AutoAcceptPartyInviteOnlyFromCharacters = onlyFromCharacters;
                    Plugin.Config.Save();
                    Plugin.IpcProvider.SyncConfiguration();
                }
                ImGuiUtil.HelpMarker("When enabled, party invites are accepted only if the inviter matches a character in the Characters window.");
            }

            ImGui.Spacing();
            var acceptTeleport = Plugin.Config.AutoAcceptTeleport;
            if (ImGui.Checkbox("Auto-accept teleport requests", ref acceptTeleport)) {
                Plugin.Config.AutoAcceptTeleport = acceptTeleport;
                Plugin.Config.Save();
                Plugin.IpcProvider.SyncConfiguration();
            }
            ImGuiUtil.HelpMarker("When enabled, SelectYesno dialogs for teleport requests are automatically confirmed. Per-character toggle available in Characters window.");
            ImGui.SameLine();
            if (ImGuiUtil.IconButton(FontAwesomeIcon.Users, "##ShowCharactersBtnTP", Language.ShowCharactersBtn))
                Plugin.Ui.CharactersWindow.Toggle();
        }

        DrawPreferredMountGroup(Plugin);
    }

    private void DrawPreferredMountGroup(Plugin Plugin) {
        using (ImGuiGroupPanel.BeginGroupPanel("Preferred Multi Rider Mount")) {
            var preferredMultiRiderMountId = Plugin.Config.PreferredMultiRiderMountId;

            using (ImRaii.PushIndent()) {
                ImGui.Text("Mount:");
                ImGui.SameLine();
                ImGui.SetNextItemWidth(150 * ImGuiHelpers.GlobalScale);
                string currentMount = Plugin.Config.PreferredMultiRiderMountId == 0 ? "Select..." : MountHelper.GetExecutableAction(Plugin.Config.PreferredMultiRiderMountId).ActionName;

                using (ImRaii.PushColor(ImGuiCol.Border, Style.Components.TooltipBorderColor, true))
                using (ImRaii.PushStyle(ImGuiStyleVar.PopupBorderSize, 1, true))
                using (ImRaii.PushFont(UiBuilder.DefaultFont)) {
                    if (ImGui.BeginCombo("##PreferredMultiRiderMountId", currentMount)) {
                        foreach (var multiRiderMount in MountHelper.GetAllowedMultiRiderMounts()) {
                            bool isSelected = Plugin.Config.PreferredMultiRiderMountId == multiRiderMount.ActionId;
                            if (ImGui.Selectable(multiRiderMount.ActionName, isSelected)) {
                                Plugin.Config.PreferredMultiRiderMountId = multiRiderMount.ActionId;
                                Plugin.Config.Save();
                                Plugin.IpcProvider.SyncConfiguration();
                            }
                            if (isSelected) {
                                ImGui.SetItemDefaultFocus();
                            }
                        }
                        ImGui.EndCombo();
                    }
                }
            }

            ImGui.SameLine();
            if (ImGuiUtil.IconButton(FontAwesomeIcon.Undo, "##ResetMountId", "Reset")) {
                Plugin.Config.PreferredMultiRiderMountId = 0;
                Plugin.Config.Save();
                Plugin.IpcProvider.SyncConfiguration();
            }
        }
    }
}
