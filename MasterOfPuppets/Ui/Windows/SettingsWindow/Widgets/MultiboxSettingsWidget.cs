using System;
using System.Collections.Generic;

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
    private string _vfxPathInput = string.Empty;

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

            bool vfxBlocked = Plugin.GameVfxManager.Enabled;
            using (ImRaii.Disabled(!Plugin.GameVfxManager.Available)) {
                if (ImGui.Checkbox("Block All VFX", ref vfxBlocked))
                    Plugin.GameVfxManager.SetBlocking(vfxBlocked);
            }
            ImGuiUtil.HelpMarker("Blocks every VFX (spells, abilities, environment effects) to save GPU resources (turns Block VFX List off)");

            bool listBlocked = Plugin.GameVfxManager.ListEnabled;
            using (ImRaii.Disabled(!Plugin.GameVfxManager.Available)) {
                if (ImGui.Checkbox("Block VFX List", ref listBlocked))
                    Plugin.GameVfxManager.SetListBlocking(listBlocked);
            }
            ImGuiUtil.HelpMarker("Blocks only the VFX paths in the list below (turns Block All VFX off). Entries ending in .avfx must match the full path, anything else blocks every path starting with it (e.g. vfx/common/eff/emt_played).");

            if (!ImGui.CollapsingHeader($"VFX Block List###VfxBlockListHeader")) {
                DrawVfxBlockList(Plugin);
            }
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
            ImGuiUtil.HelpMarker("When enabled, dialogs for party invites are automatically confirmed. Per-character toggle available in Characters window.");
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
            ImGuiUtil.HelpMarker("When enabled, dialogs for teleport requests are automatically confirmed. Per-character toggle available in Characters window.");
            ImGui.SameLine();
            if (ImGuiUtil.IconButton(FontAwesomeIcon.Users, "##ShowCharactersBtnTP", Language.ShowCharactersBtn))
                Plugin.Ui.CharactersWindow.Toggle();
        }

        DrawPreferredMountGroup(Plugin);
    }

    private void DrawVfxBlockList(Plugin Plugin) {
        var vfx = Plugin.GameVfxManager;
        var paths = Plugin.Config.BlockedVfxPaths;

        ImGui.SetNextItemWidth(300 * ImGuiHelpers.GlobalScale);
        var submit = ImGui.InputTextWithHint("##VfxPathInput", "full path (.avfx) or partial: vfx/common/eff/emt_", ref _vfxPathInput, 256, ImGuiInputTextFlags.EnterReturnsTrue);
        ImGui.SameLine();
        if ((ImGuiUtil.IconButton(FontAwesomeIcon.Plus, "##AddVfxPath", "Add") || submit) && AddVfxPath(Plugin, _vfxPathInput))
            _vfxPathInput = string.Empty;
        ImGui.SameLine();
        if (ImGuiUtil.IconButton(FontAwesomeIcon.Music, "##AddPerformancePreset", "Add preset: Performance music notes"))
            AddVfxPaths(Plugin, GameVfxManager.PerformanceNotesPreset);

        string? remove = null;
        using (var child = ImRaii.Child("##VfxBlockList", new(0, 120 * ImGuiHelpers.GlobalScale), true)) {
            if (child) {
                foreach (var path in paths) {
                    if (ImGuiUtil.IconButton(FontAwesomeIcon.Trash, $"##RemoveVfx{path}", "Remove"))
                        remove = path;
                    ImGui.SameLine();
                    ImGui.TextUnformatted(path);
                }
            }
        }
        if (remove != null) {
            paths.Remove(remove);
            SaveVfxList(Plugin);
        }

        // recorder: lists the .avfx paths requested by the game so they can be added
        var recording = vfx.Recording;
        using (ImRaii.Disabled(!vfx.Available)) {
            if (ImGui.Checkbox("Record requested VFX##VfxRecord", ref recording))
                vfx.Recording = recording;
        }
        ImGuiUtil.HelpMarker("Logs the VFX paths loaded by the game (most recent first, each path once). Paths already covered by the list are hidden. Use the + button to add one to the list");
        ImGui.SameLine();
        if (ImGuiUtil.IconButton(FontAwesomeIcon.Eraser, "##ClearRecentVfx", "Clear"))
            vfx.ClearRecentPaths();

        using (var child = ImRaii.Child("##VfxRecentList", new(0, 150 * ImGuiHelpers.GlobalScale), true)) {
            if (child) {
                string? add = null;
                foreach (var path in vfx.RecentPaths) {
                    if (ImGuiUtil.IconButton(FontAwesomeIcon.Plus, $"##AddRecentVfx{path}", "Add to list"))
                        add = path;
                    ImGui.SameLine();
                    ImGui.TextUnformatted(path);
                }
                if (add != null) AddVfxPath(Plugin, add);
            }
        }
    }

    private static bool AddVfxPath(Plugin Plugin, string path) => AddVfxPaths(Plugin, [path]);

    /// <summary>Adds the paths missing from the list (existing entries are kept), saves and syncs once.</summary>
    private static bool AddVfxPaths(Plugin Plugin, IEnumerable<string> paths) {
        var added = false;
        foreach (var path in paths) {
            var normalized = GameVfxManager.NormalizePath(path);
            if (normalized.Length == 0 || Plugin.Config.BlockedVfxPaths.Contains(normalized)) continue;
            Plugin.Config.BlockedVfxPaths.Add(normalized);
            added = true;
        }
        if (added) SaveVfxList(Plugin);
        return added;
    }

    private static void SaveVfxList(Plugin Plugin) {
        Plugin.GameVfxManager.RefreshBlockList();
        Plugin.Config.Save();
        Plugin.IpcProvider.SyncConfiguration();
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
