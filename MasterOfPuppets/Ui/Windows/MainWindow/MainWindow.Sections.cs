using System;
using System.Linq;
using System.Numerics;

using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;

using MasterOfPuppets.Util.ImGuiExt;

namespace MasterOfPuppets;

public partial class MainWindow : Window {

    private record CmdEntry(
       FontAwesomeIcon Icon,
       string Label,
       string Tooltip,
       Vector4 IconColor,
       Action OnClick);

    private float BtnMinWidth => 154f * ImGuiHelpers.GlobalScale;

    private void DrawCommandsSection() {
        using var scroll = ImRaii.Child("##CommandsSectionScroll", new Vector2(-1, -1), false);

        float availW = ImGui.GetContentRegionAvail().X;
        int cols = Math.Max(1, (int)(availW / BtnMinWidth));
        float btnW = (availW - (cols - 1) * ImGui.GetStyle().ItemSpacing.X) / cols;

        DrawCmdSection("PARTY ACTIONS", cols, btnW, [
            new(FontAwesomeIcon.UserPlus,                      "Invite To Party", "/mop invite",            Style.Components.ButtonSuccessNormal, () => Plugin.IpcProvider.RequestInviteAllToParty()),
            new(FontAwesomeIcon.UserMinus,                     "Disband Party",       "/mop disband",           Style.Components.ButtonDangerNormal,  () => Plugin.IpcProvider.RequestDisbandParty()),
            new(FontAwesomeIcon.PersonRays,                    "Get Party Leader",    "/mop getleader",         Style.Components.ButtonBlueNormal, () => Plugin.IpcProvider.RequestPartyLeader()),
            new(FontAwesomeIcon.PersonWalkingArrowLoopLeft,    "Abandon Duty",        "/mop abandonduty",       Style.Components.ButtonDangerNormal,  () => Plugin.IpcProvider.ExecuteAbandonDuty()),
        ]);

        ImGui.Spacing();

        DrawCmdSection("FOLLOW", cols, btnW, [
            new(FontAwesomeIcon.PeopleArrows, "Start Follow", "/mop follow",     Style.Components.ButtonSuccessNormal, () => Plugin.IpcProvider.Follow(DalamudApi.PlayerState.EntityId)),
            new(FontAwesomeIcon.HandPaper,    "Stop Follow",  "/mop stopfollow", Style.Components.ButtonDangerNormal,  () => Plugin.IpcProvider.StopFollow()),
        ]);

        ImGui.Spacing();

        DrawCmdSection("TARGET", cols, btnW, [
            new(FontAwesomeIcon.Crosshairs,       "Target My Target",     "/mop targetmytarget",        Style.Components.ButtonBlueNormal, () => Plugin.IpcProvider.ExecuteTargetMyTarget()),
            new(FontAwesomeIcon.PersonChalkboard, "Interact With My Target", "/mop interactwithmytarget",  Style.Components.ButtonBlueNormal, () => Plugin.IpcProvider.ExecuteInteractWithMyTarget()),
            new(FontAwesomeIcon.Times,            "Clear Target",         "/mop targetclear",           Style.Components.ButtonDangerNormal,  () => Plugin.IpcProvider.ExecuteTargetClear()),
        ]);

        ImGui.Spacing();

        DrawCmdSection("MOVEMENT", cols, btnW, [
            new(FontAwesomeIcon.PersonWalkingArrowRight,           "Move To My Target", "/mop movetomytarget", Style.Components.ButtonBlueNormal, () => Plugin.IpcProvider.ExecuteMoveToMyTarget()),
            new(FontAwesomeIcon.PersonArrowDownToLine,             "Stack On Me",       "/mop stackonme",      Style.Components.ButtonBlueNormal, () => Plugin.IpcProvider.ExecuteStackOnMe()),
            new(FontAwesomeIcon.PersonWalkingDashedLineArrowRight, "Toggle Walking",    "/mop togglewalk",     Style.Components.ButtonBlueNormal, () => Plugin.IpcProvider.ExecuteToggleWalking()),
        ]);

        ImGui.Spacing();

        DrawCmdSection("KEY BROADCAST", cols, btnW, [
            new(FontAwesomeIcon.Keyboard, "Enable Key Broadcast",  "/mop keybroadcast on",  Style.Components.ButtonSuccessNormal, () => Plugin.IpcProvider.EnableKeyboardBroadcast()),
            new(FontAwesomeIcon.Keyboard, "Disable Key Broadcast", "/mop keybroadcast off", Style.Components.ButtonDangerNormal,  () => Plugin.IpcProvider.DisableKeyboardBroadcast()),
        ]);

        ImGui.Spacing();

        DrawCmdSection("RENDER & CAMERA", cols, btnW, [
            new(FontAwesomeIcon.Desktop, "Enable RenderHack",  "/mop renderhack on",  Style.Components.ButtonSuccessNormal, () => Plugin.IpcProvider.EnableRenderHack()),
            new(FontAwesomeIcon.Desktop, "Disable RenderHack", "/mop renderhack off", Style.Components.ButtonDangerNormal,  () => Plugin.IpcProvider.DisableRenderHack()),
            new(FontAwesomeIcon.Tv,      "Enable CamHack",     "/mop camhack on",     Style.Components.ButtonSuccessNormal, () => Plugin.IpcProvider.EnableCamHack()),
            new(FontAwesomeIcon.Tv,      "Disable CamHack",    "/mop camhack off",    Style.Components.ButtonDangerNormal,  () => Plugin.IpcProvider.DisableCamHack()),
        ]);

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        DrawCmdSection("EXIT ACTIONS", cols, btnW, [
            new(FontAwesomeIcon.UsersSlash,   "Logout All",      "/mop logout",   Style.Components.ButtonWarningNormal, () => Plugin.IpcProvider.ExecuteLogout(includeSelf: true)),
            new(FontAwesomeIcon.UserAltSlash, "Logout Others",   "/mop logout",   Style.Components.ButtonWarningNormal, () => Plugin.IpcProvider.ExecuteLogout()),
            new(FontAwesomeIcon.UsersSlash,   "Shutdown All",    "/mop shutdown", Style.Components.ButtonDangerNormal,  () => Plugin.IpcProvider.ExecuteShutdown(includeSelf: true)),
            new(FontAwesomeIcon.UserAltSlash, "Shutdown Others", "/mop shutdown", Style.Components.ButtonDangerNormal,  () => Plugin.IpcProvider.ExecuteShutdown()),
        ]);
    }

    private void DrawCmdSection(string title, int cols, float btnW, CmdEntry[] entries) {
        ImGui.SetWindowFontScale(0.82f);
        ImGui.Text(title);
        ImGui.SetWindowFontScale(1.00f);
        ImGui.Spacing();

        using var table = ImRaii.Table($"##grid_{title}", cols);
        if (!table) return;

        for (int i = 0; i < cols; i++)
            ImGui.TableSetupColumn($"##c{i}_{title}", ImGuiTableColumnFlags.WidthFixed, btnW);

        for (int i = 0; i < entries.Length; i++) {
            if (i % cols == 0) ImGui.TableNextRow();
            ImGui.TableSetColumnIndex(i % cols);

            var entry = entries[i];
            if (ImGuiUtil.DrawCmdButton(entry.Icon, entry.Label, entry.Tooltip, entry.IconColor, btnW))
                entry.OnClick();
        }
    }


    //  Teleport
    private void DrawTeleportSection() {
        using var scroll = ImRaii.Child("##TeleportSectionScroll", new Vector2(-1, -1), false);

        float availW = ImGui.GetContentRegionAvail().X;
        int cols = Math.Max(1, (int)(availW / BtnMinWidth));
        float btnW = (availW - (cols - 1) * ImGui.GetStyle().ItemSpacing.X) / cols;

        DrawCmdSection("HOUSE", cols, btnW, [
            new(FontAwesomeIcon.HouseCircleCheck, "Enter House", "/mop enterhouse",     Style.Components.ButtonSuccessNormal, () => Plugin.IpcProvider.ExecuteEnterHouse()),
            new(FontAwesomeIcon.HouseCircleXmark, "Exit House", "/mop exithouse", Style.Components.ButtonDangerNormal,  () => Plugin.IpcProvider.ExecuteExitHouse()),
            new(FontAwesomeIcon.DoorClosed, "Move To Front Door", "/mop movefrontdoor", Style.Components.ButtonBlueNormal,  () => Plugin.IpcProvider.ExecuteMoveToFrontDoor()),
        ]);

        DrawCmdSection("ESTATE", cols, btnW, [
            new(FontAwesomeIcon.City, "Free Company Estate", "/mop estate \"Name\" fc",            Style.Components.ButtonBlueNormal, () => Plugin.IpcProvider.ExecuteTeleportToEstate(DalamudApi.PlayerState.CharacterName, "fc")),
            new(FontAwesomeIcon.Home, "Private Estate",       "/mop estate \"Name\" pe",           Style.Components.ButtonBlueNormal,  () => Plugin.IpcProvider.ExecuteTeleportToEstate(DalamudApi.PlayerState.CharacterName, "pe")),
            new(FontAwesomeIcon.Building, "Apartments",    "/mop estate \"Name\" ap",         Style.Components.ButtonBlueNormal, () => Plugin.IpcProvider.ExecuteTeleportToEstate(DalamudApi.PlayerState.CharacterName, "ap")),
        ]);

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
