using System.Linq;
using System.Numerics;

using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;

using MasterOfPuppets.Util.ImGuiExt;

namespace MasterOfPuppets;

public partial class MainWindow : Window {

    //  Commands
    private void DrawCommandsSection() {
        using (ImRaii.Child("##CommandsSectionScroll", new Vector2(-1, -1), false)) {

            using (ImGuiGroupPanel.BeginGroupPanel("Party Actions")) {
                DrawCmdRow(FontAwesomeIcon.UserPlus, "Invite All To Party", "/mop invite", () => Plugin.IpcProvider.RequestInviteAllToParty(), isSuccess: true);
                DrawCmdRow(FontAwesomeIcon.UserMinus, "Disband Party", "/mop disband", () => Plugin.IpcProvider.RequestDisbandParty(), isDanger: true);
                DrawCmdRow(FontAwesomeIcon.PersonRays, "Get Party Leader", "/mop getleader", () => Plugin.IpcProvider.RequestPartyLeader());
            }

            ImGui.Spacing();

            using (ImGuiGroupPanel.BeginGroupPanel("Commands")) {
                DrawCmdRow(FontAwesomeIcon.Crosshairs, "Target My Target", "/mop targetmytarget", () => Plugin.IpcProvider.ExecuteTargetMyTarget());
                DrawCmdRow(FontAwesomeIcon.PersonChalkboard, "Interact With My Target", "/mop interactwithmytarget", () => Plugin.IpcProvider.ExecuteInteractWithMyTarget());
                DrawCmdRow(FontAwesomeIcon.Times, "Target Clear", "/mop targetclear", () => Plugin.IpcProvider.ExecuteTargetClear(), isDanger: true);
                DrawCmdRow(FontAwesomeIcon.PersonWalkingArrowRight, "Move To My Target", "/mop movetomytarget", () => Plugin.IpcProvider.ExecuteMoveToMyTarget());
                DrawCmdRow(FontAwesomeIcon.PersonArrowDownToLine, "Stack On Me", "/mop stackonme", () => Plugin.IpcProvider.ExecuteStackOnMe());
                DrawCmdRow(FontAwesomeIcon.PersonWalkingDashedLineArrowRight, "Toggle Walking", "/mop togglewalk", () => Plugin.IpcProvider.ExecuteToggleWalking());
                DrawCmdRow(FontAwesomeIcon.PersonWalkingArrowLoopLeft, "Abandon Duty", "/mop ad", () => Plugin.IpcProvider.ExecuteAbandonDuty(), isDanger: true);
            }

            ImGui.Spacing();

            using (ImGuiGroupPanel.BeginGroupPanel("Follow")) {
                DrawCmdRow(FontAwesomeIcon.PeopleArrows, "Start Follow", "/mop follow", () => Plugin.IpcProvider.Follow(DalamudApi.PlayerState.EntityId), isSuccess: true);
                DrawCmdRow(FontAwesomeIcon.HandPaper, "Stop Follow", "/mop stopfollow", () => Plugin.IpcProvider.StopFollow(), isDanger: true);
            }

            ImGui.Spacing();

            using (ImGuiGroupPanel.BeginGroupPanel("Key Broadcast")) {
                DrawCmdRow(FontAwesomeIcon.Keyboard, "Enable Key Broadcast", "/mop keybroadcast on", () => Plugin.IpcProvider.EnableKeyboardBroadcast(), isSuccess: true);
                DrawCmdRow(FontAwesomeIcon.Keyboard, "Disable Key Broadcast", "/mop keybroadcast off", () => Plugin.IpcProvider.DisableKeyboardBroadcast(), isDanger: true);
            }

            ImGui.Spacing();

            using (ImGuiGroupPanel.BeginGroupPanel("Render")) {
                DrawCmdRow(FontAwesomeIcon.Desktop, "Enable RenderHack", "/mop renderhack on", () => Plugin.IpcProvider.EnableRenderHack(), isSuccess: true);
                DrawCmdRow(FontAwesomeIcon.Desktop, "Disable RenderHack", "/mop renderhack off", () => Plugin.IpcProvider.DisableRenderHack(), isDanger: true);
            }

            ImGui.Spacing();

            using (ImGuiGroupPanel.BeginGroupPanel("Camera")) {
                DrawCmdRow(FontAwesomeIcon.Tv, "Enable CamHack", "/mop camhack on", () => Plugin.IpcProvider.EnableCamHack(), isSuccess: true);
                DrawCmdRow(FontAwesomeIcon.Tv, "Disable CamHack", "/mop camhack off", () => Plugin.IpcProvider.DisableCamHack(), isDanger: true);
            }

            ImGui.Spacing();

            using (ImGuiGroupPanel.BeginGroupPanel("Exit Actions")) {
                DrawCmdRow(FontAwesomeIcon.UsersSlash, "Logout All", "/mop logout", () => Plugin.IpcProvider.ExecuteLogout(includeSelf: true), isWarning: true);
                DrawCmdRow(FontAwesomeIcon.UserAltSlash, "Logout Others", "/mop logout", () => Plugin.IpcProvider.ExecuteLogout(), isWarning: true);
                ImGui.Separator();
                DrawCmdRow(FontAwesomeIcon.UsersSlash, "Shutdown All", "/mop shutdown", () => Plugin.IpcProvider.ExecuteShutdown(includeSelf: true), isDanger: true);
                DrawCmdRow(FontAwesomeIcon.UserAltSlash, "Shutdown Others", "/mop shutdown", () => Plugin.IpcProvider.ExecuteShutdown(), isDanger: true);
            }
        }
    }

    //  Teleport
    private void DrawTeleportSection() {
        using (ImRaii.Child("##TeleportSectionScroll", new Vector2(-1, -1), false)) {

            using (ImGuiGroupPanel.BeginGroupPanel("House")) {
                DrawCmdRow(FontAwesomeIcon.HouseCircleCheck, "Enter House", "/mop enterhouse", () => Plugin.IpcProvider.ExecuteEnterHouse(), isSuccess: true);
                DrawCmdRow(FontAwesomeIcon.HouseCircleXmark, "Exit House", "/mop exithouse", () => Plugin.IpcProvider.ExecuteExitHouse(), isDanger: true);
                DrawCmdRow(FontAwesomeIcon.DoorClosed, "Move To Front Door", "/mop movefrontdoor", () => Plugin.IpcProvider.ExecuteMoveToFrontDoor());
            }

            ImGui.Spacing();

            using (ImGuiGroupPanel.BeginGroupPanel("Estate Teleport")) {
                DrawCmdRow(FontAwesomeIcon.Building, "Free Company Estate", string.Empty, () => Plugin.IpcProvider.ExecuteTeleportToEstate(DalamudApi.PlayerState.CharacterName, "fc"));
                DrawCmdRow(FontAwesomeIcon.Home, "Private Estate", string.Empty, () => Plugin.IpcProvider.ExecuteTeleportToEstate(DalamudApi.PlayerState.CharacterName, "pe"));
                DrawCmdRow(FontAwesomeIcon.City, "Apartments", string.Empty, () => Plugin.IpcProvider.ExecuteTeleportToEstate(DalamudApi.PlayerState.CharacterName, "ap"));
            }

            ImGui.Spacing();

            using (ImGuiGroupPanel.BeginGroupPanel("Teleport to Ward")) {
                ImGuiUtil.ToolTip("/mop ward <1-30>");
                if (ImGui.BeginTable("##WardTable", 6, ImGuiTableFlags.SizingStretchSame)) {
                    for (var ward = 1; ward <= 30; ward++) {
                        ImGui.TableNextColumn();
                        if (ImGui.Button($"W{ward:D2}##Ward{ward}", new Vector2(-1, 0))) {
                            Plugin.IpcProvider.ExecuteTeleportToWard(ward);
                        }
                    }
                    ImGui.EndTable();
                }
            }

            ImGui.Spacing();

            using (ImGuiGroupPanel.BeginGroupPanel("Travel to World")) {
                ImGuiUtil.ToolTip("/mop world <WorldName>");
                var localPlayer = DalamudApi.PlayerState;
                if (localPlayer.IsLoaded) {
                    var dcId = localPlayer.CurrentWorld.Value.DataCenter.RowId;
                    var worlds = DalamudApi.DataManager.GetExcelSheet<Lumina.Excel.Sheets.World>()
                        .Where(w => w.IsPublic && w.DataCenter.RowId == dcId)
                        .OrderBy(w => w.Name.ToString())
                        .Select(w => w.Name.ToString())
                        .ToList();

                    if (ImGui.BeginTable("##WorldTable", 2, ImGuiTableFlags.SizingStretchSame)) {
                        foreach (var world in worlds) {
                            ImGui.TableNextColumn();
                            float btnWidth = ImGui.GetContentRegionAvail().X;
                            if (ImGui.Button($"{world}##World{world}", new Vector2(btnWidth, 0))) {
                                Plugin.IpcProvider.ExecuteTravelToWorld(world);
                            }
                        }
                        ImGui.EndTable();
                    }
                } else {
                    ImGui.TextDisabled("Not logged in");
                }
            }
        }
    }

    //  Helpers
    /// <summary>Draws a command row: [icon button] Label  (tooltip: command)</summary>
    private void DrawCmdRow(
        FontAwesomeIcon icon,
        string label,
        string command,
        System.Action action,
        bool isSuccess = false,
        bool isDanger = false,
        bool isWarning = false) {

        string btnId = $"##CmdBtn_{label}";

        if (isDanger) {
            using var _ = ImRaii.PushColor(ImGuiCol.Button, Style.Components.ButtonDangerNormal)
                                 .Push(ImGuiCol.ButtonHovered, Style.Components.ButtonDangerHovered)
                                 .Push(ImGuiCol.ButtonActive, Style.Components.ButtonDangerActive);
            ImGuiUtil.IconButton(icon, btnId, label);
        } else if (isWarning) {
            using var _ = ImRaii.PushColor(ImGuiCol.Button, Style.Components.ButtonWarningNormal)
                                 .Push(ImGuiCol.ButtonHovered, Style.Components.ButtonWarningHovered)
                                 .Push(ImGuiCol.ButtonActive, Style.Components.ButtonWarningActive);
            ImGuiUtil.IconButton(icon, btnId, label);
        } else if (isSuccess) {
            using var _ = ImRaii.PushColor(ImGuiCol.Button, Style.Components.ButtonSuccessNormal)
                                 .Push(ImGuiCol.ButtonHovered, Style.Components.ButtonSuccessHovered)
                                 .Push(ImGuiCol.ButtonActive, Style.Components.ButtonSuccessActive);
            ImGuiUtil.IconButton(icon, btnId, label);
        } else {
            ImGuiUtil.IconButton(icon, btnId, label);
        }

        if (ImGui.IsItemClicked()) action();

        ImGui.SameLine();

        if (ImGui.Selectable($"{label}##CmdSel_{label}")) {
            action();
        }

        if (!string.IsNullOrEmpty(command))
            ImGuiUtil.ToolTip(command);
    }

    /// <summary>Draws a launcher row that opens a window: [arrow-right icon] Label</summary>
    private void DrawWindowLaunchRow(
        FontAwesomeIcon icon,
        string label,
        string tooltip,
        System.Action action,
        Vector4? iconColor = null) {

        string btnId = $"##LaunchBtn_{label}";

        if (iconColor.HasValue) {
            using var _ = ImRaii.PushColor(ImGuiCol.Text, iconColor.Value);
            ImGuiUtil.IconButton(icon, btnId, tooltip);
        } else {
            ImGuiUtil.IconButton(icon, btnId, tooltip);
        }

        if (ImGui.IsItemClicked()) action();

        ImGui.SameLine();

        if (ImGui.Selectable($"{label}##LaunchSel_{label}")) {
            action();
        }
        ImGuiUtil.ToolTip(tooltip);
    }
}
