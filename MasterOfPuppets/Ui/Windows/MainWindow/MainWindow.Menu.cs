using System.Linq;
using System.Numerics;

using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;

using MasterOfPuppets.Resources;
using MasterOfPuppets.Util.ImGuiExt;

namespace MasterOfPuppets;

public partial class MainWindow : Window {
    private void DrawMenuBar() {
        using var color = ImRaii.PushColor(ImGuiCol.Border, Style.Components.TooltipBorderColor);
        using var style = ImRaii.PushStyle(ImGuiStyleVar.PopupBorderSize, 1);
        using var menuBar = ImRaii.MenuBar();
        if (!menuBar) return;

        DrawMacroMenu();
        // DrawActionsMenu();

        if (ImGui.MenuItem("Actions"))
            Plugin.Ui.ActionsBroadcastWindow.Toggle();

        DrawCommandsMenu();

        DrawTeleportMenu();

        DrawCharactersMenu();

        if (ImGui.MenuItem("Help"))
            Plugin.Ui.HelpWindow.Toggle();

        var versionText = $"v{Version}";
        var textSize = ImGui.CalcTextSize(versionText);
        var padding = ImGui.GetStyle().FramePadding.X + 5;
        var regionMaxX = ImGui.GetWindowContentRegionMax().X;
        ImGui.SameLine(regionMaxX - textSize.X - (padding * 2));
        ImGui.Text(versionText);
    }

    private void DrawMacroMenu() {
        using var menu = ImRaii.Menu("Macro");
        if (!menu) return;

        if (ImGuiUtil.PrimaryIconButton(FontAwesomeIcon.Plus, $"##AddMacroMenu")) {
            Ui.MacroEditorWindow.AddNewMacro();
        }
        ImGui.SameLine();
        if (ImGui.Selectable(Language.AddMacroBtn)) {
            Ui.MacroEditorWindow.AddNewMacro();
        }

        // -----------------------

        if (ImGuiUtil.DangerIconButton(FontAwesomeIcon.Trash, $"##DeleteSelectedMacrosMenu")) {
            if (ImGui.GetIO().KeyCtrl) {
                Plugin.MacroManager.DeleteSelectedMacros();
                Plugin.IpcProvider.SyncConfiguration();
            }
        }
        ImGui.SameLine();
        if (ImGui.Selectable(Language.DeleteSelectedMacrosBtn)) {
            if (ImGui.GetIO().KeyCtrl) {
                Plugin.MacroManager.DeleteSelectedMacros();
                Plugin.IpcProvider.SyncConfiguration();
            }
        }
        ImGuiUtil.ToolTip(Language.DeleteInstructionTooltip);

        // -----------------------

        if (ImGuiUtil.IconButton(FontAwesomeIcon.ExchangeAlt, $"##MacroImportExportMenu")) {
            Ui.MacroImportExportWindow.Toggle();
        }
        ImGui.SameLine();
        if (ImGui.Selectable("Import Export Macros")) {
            Ui.MacroImportExportWindow.Toggle();
        }

        // -----------------------

        if (ImGuiUtil.IconButton(FontAwesomeIcon.FileImport, $"##ImportFromClipboardMenu")) {
            ImportMacroFromClipboard();
        }
        ImGui.SameLine();
        if (ImGui.Selectable(Language.ImportMacroBtn)) {
            ImportMacroFromClipboard();
        }
        // -----------------------

        if (ImGuiUtil.IconButton(FontAwesomeIcon.FilePen, $"##MacroBatchEditorMenu")) {
            Ui.MacroBatchEditorWindow.Toggle();
        }
        ImGui.SameLine();
        if (ImGui.Selectable(Language.MacroBatchEditorTitle)) {
            Ui.MacroBatchEditorWindow.Toggle();
        }

        // -----------------------

        if (ImGuiUtil.IconButton(FontAwesomeIcon.FileArchive, $"##MacroBackupMenu")) {
            Plugin.MacroManager.BackupMacros();
        }
        ImGui.SameLine();
        if (ImGui.Selectable(Language.MacroBackup)) {
            Plugin.MacroManager.BackupMacros();
        }
    }

    // private void DrawActionsMenu() {
    //     if (ImGui.BeginMenu("Actions")) {
    //         if (ImGuiUtil.IconButton(FontAwesomeIcon.SmileWink, $"##ShowEmotesMenu")) {
    //             Ui.EmotesWindow.Toggle();
    //         }
    //         ImGui.SameLine();
    //         if (ImGui.Selectable(Language.ShowEmotesBtn)) {
    //             Ui.EmotesWindow.Toggle();
    //         }

    //         // -----------------------

    //         if (ImGuiUtil.IconButton(FontAwesomeIcon.Umbrella, $"##ShowFashionAccessoriesMenu")) {
    //             Ui.FashionAccessoriesWindow.Toggle();
    //         }
    //         ImGui.SameLine();
    //         if (ImGui.Selectable(Language.ShowFashionAccessoriesBtn)) {
    //             Ui.FashionAccessoriesWindow.Toggle();
    //         }

    //         // -----------------------

    //         if (ImGuiUtil.IconButton(FontAwesomeIcon.Glasses, $"##ShowFacewearMenu")) {
    //             Ui.FacewearWindow.Toggle();
    //         }
    //         ImGui.SameLine();
    //         if (ImGui.Selectable(Language.ShowFacewearBtn)) {
    //             Ui.FacewearWindow.Toggle();
    //         }

    //         // -----------------------

    //         if (ImGuiUtil.IconButton(FontAwesomeIcon.Horse, $"##ShowMountMenu")) {
    //             Ui.MountWindow.Toggle();
    //         }
    //         ImGui.SameLine();
    //         if (ImGui.Selectable(Language.ShowMountBtn)) {
    //             Ui.MountWindow.Toggle();
    //         }

    //         // -----------------------

    //         if (ImGuiUtil.IconButton(FontAwesomeIcon.Cat, $"##ShowMinionMenu")) {
    //             Ui.MinionWindow.Toggle();
    //         }
    //         ImGui.SameLine();
    //         if (ImGui.Selectable(Language.ShowMinionBtn)) {
    //             Ui.MinionWindow.Toggle();
    //         }

    //         // -----------------------

    //         if (ImGuiUtil.IconButton(FontAwesomeIcon.ShoppingBag, $"##ShowItemMenu")) {
    //             Ui.ItemWindow.Toggle();
    //         }
    //         ImGui.SameLine();
    //         if (ImGui.Selectable(Language.ShowItemBtn)) {
    //             Ui.ItemWindow.Toggle();
    //         }

    //         // -----------------------

    //         if (ImGuiUtil.IconButton(FontAwesomeIcon.Briefcase, $"##ShowGearsetMenu")) {
    //             Ui.GearSetWindow.Toggle();
    //         }
    //         ImGui.SameLine();
    //         if (ImGui.Selectable(Language.GearSetTitle)) {
    //             Ui.GearSetWindow.Toggle();
    //         }
    //         ImGui.EndMenu();
    //     }
    // }

    private void DrawCommandsMenu() {
        using var menu = ImRaii.Menu("Commands");
        if (!menu) return;

        if (ImGuiUtil.PrimaryIconButton(FontAwesomeIcon.Crosshairs, $"##ExecuteTargetMyTargetCommand")) {
            Plugin.IpcProvider.ExecuteTargetMyTarget();
        }
        ImGui.SameLine();
        if (ImGui.Selectable("Target My Target")) {
            Plugin.IpcProvider.ExecuteTargetMyTarget();
        }
        ImGuiUtil.ToolTip("/mop targetmytarget");

        // -----------------------

        if (ImGuiUtil.PrimaryIconButton(FontAwesomeIcon.PersonChalkboard, $"##ExecuteInteractWIthMyTargetCommand")) {
            Plugin.IpcProvider.ExecuteInteractWithMyTarget();
        }
        ImGui.SameLine();
        if (ImGui.Selectable("Interact With My Target")) {
            Plugin.IpcProvider.ExecuteInteractWithMyTarget();
        }
        ImGuiUtil.ToolTip("/mop interactwithmytarget");

        // -----------------------

        if (ImGuiUtil.DangerIconButton(FontAwesomeIcon.Times, $"##ExecuteTargetClearCommand")) {
            Plugin.IpcProvider.ExecuteTargetClear();
        }
        ImGui.SameLine();
        if (ImGui.Selectable("Target Clear")) {
            Plugin.IpcProvider.ExecuteTargetClear();
        }
        ImGuiUtil.ToolTip("/mop targetclear");

        // -----------------------

        if (ImGuiUtil.PrimaryIconButton(FontAwesomeIcon.PersonWalkingArrowRight, $"##ExecuteMoveToMyTargetCommand")) {
            Plugin.IpcProvider.ExecuteMoveToMyTarget();
        }
        ImGui.SameLine();
        if (ImGui.Selectable("Move To My Target")) {
            Plugin.IpcProvider.ExecuteMoveToMyTarget();
        }
        ImGuiUtil.ToolTip("/mop movetotarget");

        // -----------------------

        if (ImGuiUtil.PrimaryIconButton(FontAwesomeIcon.PersonArrowDownToLine, $"##ExecuteStackOnMeCommand")) {
            Plugin.IpcProvider.ExecuteStackOnMe();
        }
        ImGui.SameLine();
        if (ImGui.Selectable("Stack On Me")) {
            Plugin.IpcProvider.ExecuteStackOnMe();
        }
        ImGuiUtil.ToolTip("/mop stackonme");

        // -----------------------

        if (ImGuiUtil.PrimaryIconButton(FontAwesomeIcon.PersonWalkingDashedLineArrowRight, $"##ExecuteToggleWalkingCommand")) {
            Plugin.IpcProvider.ExecuteToggleWalking();
        }
        ImGui.SameLine();
        if (ImGui.Selectable("Toggle Walking")) {
            Plugin.IpcProvider.ExecuteToggleWalking();
        }
        ImGuiUtil.ToolTip("/mop togglewalk");

        // -----------------------

        if (ImGuiUtil.DangerIconButton(FontAwesomeIcon.PersonWalkingArrowLoopLeft, $"##ExecuteAbandonDutyCommand")) {
            Plugin.IpcProvider.ExecuteAbandonDuty();
        }
        ImGui.SameLine();
        if (ImGui.Selectable("Abandon Duty")) {
            Plugin.IpcProvider.ExecuteAbandonDuty();
        }

        DrawFollowSubMenu();
        DrawKeyBroadcastSubMenu();
        DrawCameraSubMenu();
    }

    private void DrawFollowSubMenu() {
        using var submenu = ImRaii.Menu("Follow");
        if (!submenu) return;

        if (ImGuiUtil.SuccessIconButton(FontAwesomeIcon.PeopleArrows, $"##StartFollow")) {
            Plugin.IpcProvider.StartFollow(DalamudApi.PlayerState.EntityId);
        }
        ImGui.SameLine();
        if (ImGui.Selectable("Start Follow")) {
            Plugin.IpcProvider.StartFollow(DalamudApi.PlayerState.EntityId);
        }
        ImGuiUtil.ToolTip("/mop follow on");

        // -----------------------

        if (ImGuiUtil.DangerIconButton(FontAwesomeIcon.HandPaper, $"##StopFollow")) {
            Plugin.IpcProvider.StopFollow();
        }
        ImGui.SameLine();
        if (ImGui.Selectable("Stop Follow")) {
            Plugin.IpcProvider.StopFollow();
        }
        ImGuiUtil.ToolTip("/mop follow off");
    }

    private void DrawKeyBroadcastSubMenu() {
        using var submenu = ImRaii.Menu("Key Broadcast");
        if (!submenu) return;

        if (ImGuiUtil.SuccessIconButton(FontAwesomeIcon.Keyboard, $"##ExecuteEnableKB")) {
            Plugin.IpcProvider.EnableKeyboardBroadcast();
        }
        ImGui.SameLine();
        if (ImGui.Selectable("Enable Key Broadcast")) {
            Plugin.IpcProvider.EnableKeyboardBroadcast();
        }
        ImGuiUtil.ToolTip("/mop keybroadcast on");

        // -----------------------

        if (ImGuiUtil.DangerIconButton(FontAwesomeIcon.Keyboard, $"##ExecuteDisableKB")) {
            Plugin.IpcProvider.DisableKeyboardBroadcast();
        }
        ImGui.SameLine();
        if (ImGui.Selectable("Disable Key Broadcast")) {
            Plugin.IpcProvider.DisableKeyboardBroadcast();
        }
        ImGuiUtil.ToolTip("/mop keybroadcast off");
    }

    private void DrawCameraSubMenu() {
        using var submenu = ImRaii.Menu("Camera");
        if (!submenu) return;

        if (ImGuiUtil.SuccessIconButton(FontAwesomeIcon.Tv, $"##ExecuteEnableCamHack")) {
            Plugin.IpcProvider.EnableCamHack();
        }
        ImGui.SameLine();
        if (ImGui.Selectable("Enable CamHack")) {
            Plugin.IpcProvider.EnableCamHack();
        }
        ImGuiUtil.ToolTip("/mop camhack on");

        // -----------------------

        if (ImGuiUtil.DangerIconButton(FontAwesomeIcon.Tv, $"##ExecuteDisableCamHack")) {
            Plugin.IpcProvider.DisableCamHack();
        }
        ImGui.SameLine();
        if (ImGui.Selectable("Disable CamHack")) {
            Plugin.IpcProvider.DisableCamHack();
        }
        ImGuiUtil.ToolTip("/mop camhack off");
    }

    private void DrawTeleportMenu() {
        using var menu = ImRaii.Menu("Teleport");
        if (!menu) return;

        ImGui.Separator();
        if (ImGuiUtil.SuccessIconButton(FontAwesomeIcon.HouseCircleCheck, $"##ExecuteEnterHouse")) {
            Plugin.IpcProvider.ExecuteEnterHouse();
        }
        ImGui.SameLine();
        if (ImGui.Selectable("Enter House")) {
            Plugin.IpcProvider.ExecuteEnterHouse();
        }
        ImGuiUtil.ToolTip("/mop enterhouse");

        // -----------------------

        if (ImGuiUtil.DangerIconButton(FontAwesomeIcon.HouseCircleXmark, $"##ExecuteExitHouse")) {
            Plugin.IpcProvider.ExecuteExitHouse();
        }
        ImGui.SameLine();
        if (ImGui.Selectable("Exit House")) {
            Plugin.IpcProvider.ExecuteExitHouse();
        }
        ImGuiUtil.ToolTip("/mop exithouse");

        // -----------------------

        if (ImGuiUtil.PrimaryIconButton(FontAwesomeIcon.DoorClosed, $"##ExecuteMoveToFrontDoot")) {
            Plugin.IpcProvider.ExecuteMoveToFrontDoor();
        }
        ImGui.SameLine();
        if (ImGui.Selectable("Move To Front Door")) {
            Plugin.IpcProvider.ExecuteMoveToFrontDoor();
        }
        ImGuiUtil.ToolTip("/mop movefrontdoor");

        // -----------------------

        ImGui.Separator();
        ImGui.Text("Teleport to Ward");
        ImGuiUtil.ToolTip("/mop ward <1-30>");
        if (ImGui.BeginTable("WardTable", 3, ImGuiTableFlags.None)) {
            for (var ward = 1; ward <= 30; ward++) {
                ImGui.TableNextColumn();
                if (ImGui.SmallButton($"Ward {ward:D2}##Ward{ward}")) {
                    Plugin.IpcProvider.ExecuteTeleportToWard(ward);
                    ImGui.CloseCurrentPopup();
                }
            }
            ImGui.EndTable();
        }

        // -----------------------

        ImGui.Separator();
        ImGui.Text("Travel to World");
        ImGuiUtil.ToolTip("/mop world <WorldName>");
        var localPlayer = DalamudApi.PlayerState;
        if (localPlayer.IsLoaded) {
            var dcId = localPlayer.CurrentWorld.Value.DataCenter.RowId;
            var worlds = DalamudApi.DataManager.GetExcelSheet<Lumina.Excel.Sheets.World>()
                .Where(w => w.IsPublic && w.DataCenter.RowId == dcId)
                .OrderBy(w => w.Name.ToString())
                .Select(w => w.Name.ToString())
                .ToList();
            if (ImGui.BeginTable("WorldTable", 2, ImGuiTableFlags.SizingStretchSame)) {
                foreach (var world in worlds) {
                    ImGui.TableNextColumn();
                    float width = ImGui.GetContentRegionAvail().X;
                    if (ImGui.Button($"{world}##World{world}", new Vector2(width, 0))) {
                        Plugin.IpcProvider.ExecuteTravelToWorld(world);
                        ImGui.CloseCurrentPopup();
                    }
                }

                ImGui.EndTable();
            }
        }
    }

    private void DrawCharactersMenu() {
        using var menu = ImRaii.Menu("Characters");
        if (!menu) return;

        if (ImGuiUtil.SuccessIconButton(FontAwesomeIcon.UserPlus, $"##RequestInviteAllToParty")) {
            Plugin.IpcProvider.RequestInviteAllToParty();
        }
        ImGui.SameLine();
        if (ImGui.Selectable("Invite To Party")) {
            Plugin.IpcProvider.RequestInviteAllToParty();
        }
        ImGuiUtil.ToolTip("/mop invite");

        // -----------------------

        if (ImGuiUtil.DangerIconButton(FontAwesomeIcon.UserMinus, $"##RequestDisbandParty")) {
            Plugin.IpcProvider.RequestDisbandParty();
        }
        ImGui.SameLine();
        if (ImGui.Selectable("Disband Party")) {
            Plugin.IpcProvider.RequestDisbandParty();
        }
        ImGuiUtil.ToolTip("/mop disband");

        // -----------------------

        if (ImGuiUtil.PrimaryIconButton(FontAwesomeIcon.PersonRays, $"##RequestPartyLeader")) {
            Plugin.IpcProvider.RequestPartyLeader();
        }
        ImGui.SameLine();
        if (ImGui.Selectable("Get Party Leader")) {
            Plugin.IpcProvider.RequestPartyLeader();
        }
        ImGuiUtil.ToolTip("/mop getleader");

        // -----------------------

        // if (ImGui.MenuItem("Formations"))
        //     Plugin.Ui.FormationWindow.Toggle();

        // if (ImGui.MenuItem("Formations2"))
        //     Plugin.Ui.FormationImPlotWindow.Toggle();

        // -----------------------

        ImGui.Separator();
        if (ImGuiUtil.PrimaryIconButton(FontAwesomeIcon.Users, $"##CharactersMenu")) {
            Ui.CharactersWindow.Toggle();
        }
        ImGui.SameLine();
        if (ImGui.Selectable(Language.ShowCharactersBtn)) {
            Ui.CharactersWindow.Toggle();
        }

        if (ImGuiUtil.PrimaryIconButton(FontAwesomeIcon.UsersViewfinder, $"##Peer Monitor")) {
            Plugin.Ui.PeerMonitorWindow.Toggle();
        }
        ImGui.SameLine();
        if (ImGui.MenuItem("Peer Monitor"))
            Plugin.Ui.PeerMonitorWindow.Toggle();

        DrawGameExitSubMenu();

    }

    private void DrawGameExitSubMenu() {
        using var submenu = ImRaii.Menu("Exit Actions");
        if (!submenu) return;

        if (ImGuiUtil.WarningIconButton(FontAwesomeIcon.UsersSlash, $"##ExecuteLogoutAll")) {
            Plugin.IpcProvider.ExecuteLogout(includeSelf: true);
        }
        ImGui.SameLine();
        if (ImGui.Selectable("Logout All")) {
            Plugin.IpcProvider.ExecuteLogout(includeSelf: true);
        }
        ImGuiUtil.ToolTip("/mop logout");

        if (ImGuiUtil.WarningIconButton(FontAwesomeIcon.UserAltSlash, $"##ExecuteLogoutOthers")) {
            Plugin.IpcProvider.ExecuteLogout();
        }
        ImGui.SameLine();
        if (ImGui.Selectable("Logout Others")) {
            Plugin.IpcProvider.ExecuteLogout();
        }
        ImGuiUtil.ToolTip("/mop logout");

        // -----------------------

        ImGui.Separator();

        if (ImGuiUtil.DangerIconButton(FontAwesomeIcon.UsersSlash, $"##ExecuteShutdownAll")) {
            Plugin.IpcProvider.ExecuteShutdown(includeSelf: true);
        }
        ImGui.SameLine();
        if (ImGui.Selectable("Shutdown All")) {
            Plugin.IpcProvider.ExecuteShutdown(includeSelf: true);
        }
        ImGuiUtil.ToolTip("/mop shutdown");

        if (ImGuiUtil.DangerIconButton(FontAwesomeIcon.UserAltSlash, $"##ExecuteShutDownOthers")) {
            Plugin.IpcProvider.ExecuteShutdown();
        }
        ImGui.SameLine();
        if (ImGui.Selectable("Shutdown Others")) {
            Plugin.IpcProvider.ExecuteShutdown();
        }
        ImGuiUtil.ToolTip("/mop shutdown");
    }
}
