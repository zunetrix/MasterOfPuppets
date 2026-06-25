using System.IO;

using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;

using MasterOfPuppets.Extensions;
using MasterOfPuppets.Util;
using MasterOfPuppets.Util.ImGuiExt;

namespace MasterOfPuppets;

public partial class XivLauncherWindow {
    private void DrawRightPanel() {
        var config = Plugin.Config;

        if (_selEntry == -1 || SelectedEntry == null) {
            ImGui.TextDisabled("No account selected.");
            return;
        }

        var entry = SelectedEntry;

        ImGui.TextDisabled($"Account: {entry.Name}");
        ImGui.Separator();
        ImGui.Spacing();

        if (ImGui.BeginTable("##xlentrytbl", 2, ImGuiTableFlags.NoSavedSettings)) {
            ImGui.TableSetupColumn("Label", ImGuiTableColumnFlags.WidthFixed, 100f * ImGuiHelpers.GlobalScale);
            ImGui.TableSetupColumn("Input", ImGuiTableColumnFlags.WidthStretch);

            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            ImGui.AlignTextToFramePadding();
            ImGui.Text("Username");
            ImGui.TableNextColumn();
            var userName = entry.UserName ?? string.Empty;
            ImGui.SetNextItemWidth(-1);
            if (ImGui.InputText("##xlusername", ref userName, 256)) {
                entry.UserName = userName;
            }
            if (ImGui.IsItemDeactivatedAfterEdit()) {
                Plugin.Config.Save();
                Plugin.IpcProvider.SyncConfiguration();
            }

            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            ImGui.AlignTextToFramePadding();
            ImGui.Text("Options");
            ImGui.TableNextColumn();

            var isEnabled = entry.Enabled;
            if (ImGui.Checkbox("Enabled", ref isEnabled)) {
                entry.Enabled = isEnabled;
                Plugin.Config.Save();
                Plugin.IpcProvider.SyncConfiguration();
            }

            var autoLogin = entry.AutoLogin;
            if (ImGui.Checkbox("Auto-login", ref autoLogin)) {
                entry.AutoLogin = autoLogin;
                Plugin.Config.Save();
                Plugin.IpcProvider.SyncConfiguration();
            }
            ImGui.SameLine();
            ImGuiUtil.HelpMarker("In order for it to work properly, you need to enable Auto Login in the launcher and have your password saved");

            var steamAccount = entry.UseSteamServiceAccount;
            if (ImGui.Checkbox("Steam account", ref steamAccount)) {
                entry.UseSteamServiceAccount = steamAccount;
                Plugin.Config.Save();
                Plugin.IpcProvider.SyncConfiguration();
            }

            var useOtp = entry.UseOtp;
            if (ImGui.Checkbox("OTP", ref useOtp)) {
                entry.UseOtp = useOtp;
                Plugin.Config.Save();
                Plugin.IpcProvider.SyncConfiguration();
            }

            ImGui.Text("Roaming Folder:");
            ImGui.Text(entry.RoamingPath.EllipsisPath(50));

            ImGui.SameLine();
            ImGuiHelpers.ScaledDummy(5);

            ImGui.SameLine();
            if (ImGuiUtil.IconButton(FontAwesomeIcon.Folder, "##SetXivEntryRoamingPathBtn", "Select Roaming Folder")) {
                _fileDialogManager.OpenFolderDialog(
                    title: "Select Roaming Folder",
                    startPath: DalamudApi.PluginInterface.ConfigDirectory.FullName,
                    callback: (result, selectedPath) => {
                        if (!result) return;
                        if (!Path.Exists(selectedPath)) return;
                        entry.RoamingPath = selectedPath;
                        Plugin.IpcProvider.SyncConfiguration();
                    });
            }

            ImGui.SameLine();
            if (ImGuiUtil.IconButton(FontAwesomeIcon.Undo, "##ResetRoamingFolderBtn", "Reset")) {
                entry.RoamingPath = string.Empty;
                Plugin.Config.Save();
                Plugin.IpcProvider.SyncConfiguration();
            }

            ImGui.SameLine();
            if (ImGuiUtil.IconButton(FontAwesomeIcon.FolderOpen, "##OpenRoamingDirectoryBtn", "Open Roaming Directory")) {
                WindowsApi.OpenFolder(entry.RoamingPath);
            }

            ImGui.EndTable();
        }

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();
    }
}
