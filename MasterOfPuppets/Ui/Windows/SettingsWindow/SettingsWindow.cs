using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;

using MasterOfPuppets.Camera;
using MasterOfPuppets.Extensions;
using MasterOfPuppets.Extensions.Dalamud;
using MasterOfPuppets.Resources;
using MasterOfPuppets.Util;
using MasterOfPuppets.Util.ImGuiExt;

namespace MasterOfPuppets;

public class SettingsWindow : Window {
    private Plugin Plugin { get; }
    private string _characterName = string.Empty;
    private float _cameraYOffset = GameCameraManager.MaxYOffset;

    //  keyboard filter popup
    private const string KbFilterPopupId = "Key Filter##KbFilter";

    private readonly record struct KeyDef(int Vk, string Label, float W);
    private static KeyDef K(int v, string l, float w = 1f) => new(v, l, w);
    private static KeyDef Gap(float w) => new(0, string.Empty, w);

    private static readonly KeyDef[][] MainKeyRows = [
        // Function row
        [K(0x1B,"Esc"), Gap(.5f), K(0x70,"F1"),K(0x71,"F2"),K(0x72,"F3"),K(0x73,"F4"),
         Gap(.25f), K(0x74,"F5"),K(0x75,"F6"),K(0x76,"F7"),K(0x77,"F8"),
         Gap(.25f), K(0x78,"F9"),K(0x79,"F10"),K(0x7A,"F11"),K(0x7B,"F12")],
        // Number row
        [K(0xC0,"`"),K(0x31,"1"),K(0x32,"2"),K(0x33,"3"),K(0x34,"4"),K(0x35,"5"),K(0x36,"6"),
         K(0x37,"7"),K(0x38,"8"),K(0x39,"9"),K(0x30,"0"),K(0xBD,"-"),K(0xBB,"="),K(0x08,"Back",2f)],
        // QWERTY row
        [K(0x09,"Tab",1.5f),K(0x51,"Q"),K(0x57,"W"),K(0x45,"E"),K(0x52,"R"),K(0x54,"T"),
         K(0x59,"Y"),K(0x55,"U"),K(0x49,"I"),K(0x4F,"O"),K(0x50,"P"),K(0xDB,"["),K(0xDD,"]"),K(0xDC,"\\",1.5f)],
        // Home row
        [K(0x14,"Caps",1.75f),K(0x41,"A"),K(0x53,"S"),K(0x44,"D"),K(0x46,"F"),K(0x47,"G"),
         K(0x48,"H"),K(0x4A,"J"),K(0x4B,"K"),K(0x4C,"L"),K(0xBA,";"),K(0xDE,"'"),K(0x0D,"Enter",2.25f)],
        // Shift row
        [K(0xA0,"LShift",2.25f),K(0x5A,"Z"),K(0x58,"X"),K(0x43,"C"),K(0x56,"V"),K(0x42,"B"),
         K(0x4E,"N"),K(0x4D,"M"),K(0xBC,","),K(0xBE,"."),K(0xBF,"/"),K(0xA1,"RShift",2.75f)],
        // Bottom row
        [K(0xA2,"LCtrl",1.5f),K(0x5B,"Win",1.25f),K(0xA4,"LAlt",1.25f),K(0x20,"Space",6.25f),
         K(0xA5,"RAlt",1.25f),K(0x5C,"Win",1.25f),K(0x5D,"Menu",1.25f),K(0xA3,"RCtrl",1.5f)],
    ];

    public SettingsWindow(Plugin plugin) : base($"{Plugin.Name} {Language.SettingsTitle}###SettingsWindowMop") {
        Plugin = plugin;

        Size = ImGuiHelpers.ScaledVector2(400, 300);
        SizeCondition = ImGuiCond.FirstUseEver;
        // SizeCondition = ImGuiCond.Always;
        // Flags = ImGuiWindowFlags.NoResize;
    }

    public override void Draw() {
        {
            using var tabBar = ImRaii.TabBar("##SettingsTabs");
            if (tabBar) {
                DrawGeneralTab();
                DrawChatSyncTab();
                DrawCommandsTab();
            }
        }
    }

    private void DrawGeneralTab() {
        using var tabItem = ImRaii.TabItem($"{Language.SettingsGeneralTab}###GeneralTab");
        if (!tabItem) return;

        using (ImGuiGroupPanel.BeginGroupPanel(Language.SettingsGeneralTab)) {
            var syncClients = Plugin.Config.SyncClients;
            if (ImGui.Checkbox(Language.SettingsWindowSyncClients, ref syncClients)) {
                Plugin.Config.SyncClients = syncClients;
                Plugin.Config.Save();
                Plugin.IpcProvider.SyncConfiguration();
            }
            ImGuiUtil.HelpMarker("Allow actions to be executed in broadcast to all clients");

            var saveConfigAfterSync = Plugin.Config.SaveConfigAfterSync;
            if (ImGui.Checkbox(Language.SettingsWindowSaveConfigAfterSync, ref saveConfigAfterSync)) {
                Plugin.Config.SaveConfigAfterSync = saveConfigAfterSync;
                Plugin.Config.Save();
                Plugin.IpcProvider.SyncConfiguration();
            }
            ImGuiUtil.HelpMarker("Enable for accounts with individual config file");

            var autoSaveMacro = Plugin.Config.AutoSaveMacro;
            if (ImGui.Checkbox(Language.SettingsWindowAutoSaveMacro, ref autoSaveMacro)) {
                Plugin.Config.AutoSaveMacro = autoSaveMacro;
                Plugin.IpcProvider.SyncConfiguration();
            }
            ImGuiUtil.HelpMarker("Auto save macro on close editor");

            ImGui.Text("Global delay between actions");
            ImGui.SetNextItemWidth(150);
            var delayBetweenActions = Plugin.Config.DelayBetweenActions;
            if (ImGui.InputDouble("##DelayBetrweenActions", ref delayBetweenActions, 0.1, 1, "%.2f", ImGuiInputTextFlags.AutoSelectAll)) {
                delayBetweenActions = Math.Clamp(Math.Round(delayBetweenActions, 2, MidpointRounding.AwayFromZero), 0, 60);
                Plugin.Config.DelayBetweenActions = delayBetweenActions;
                Plugin.IpcProvider.SyncConfiguration();
            }
            ImGuiUtil.HelpMarker("""
            Set 0 to disable
            Be careful when disabling global delay along with loops to avoid spamming actions
            """);
        }

        ImGui.Spacing();
        ImGui.Spacing();

        using (ImGuiGroupPanel.BeginGroupPanel("Window")) {
            var openOnStartup = Plugin.Config.OpenOnStartup;
            if (ImGui.Checkbox(Language.SettingsWindowOpenOnStartup, ref openOnStartup)) {
                Plugin.Config.OpenOnStartup = openOnStartup;
                Plugin.IpcProvider.SyncConfiguration();
            }

            var openOnLogin = Plugin.Config.OpenOnLogin;
            if (ImGui.Checkbox(Language.SettingsWindowOpenLogin, ref openOnLogin)) {
                Plugin.Config.OpenOnLogin = openOnLogin;
                Plugin.IpcProvider.SyncConfiguration();
            }

            var allowCloseWithEscape = Plugin.Config.AllowCloseWithEscape;
            if (ImGui.Checkbox(Language.SettingsWindowAllowCloseWithEscape, ref allowCloseWithEscape)) {
                Plugin.Config.AllowCloseWithEscape = allowCloseWithEscape;
                Plugin.IpcProvider.SyncConfiguration();
                Plugin.Ui.MainWindow.UpdateWindowConfig();
            }

            // var showSettingsButton = Plugin.Config.ShowSettingsButton;
            // if (ImGui.Checkbox(Language.SettingsWindowShowConfigButton, ref showSettingsButton))
            // {
            //     Plugin.Config.ShowSettingsButton = showSettingsButton;
            //     Plugin.Config.Save();
            //     Plugin.Ui.MainWindow.UpdateConfig();
            // }

            // var allowMovement = Plugin.Config.AllowMovement;
            // if (ImGui.Checkbox(Language.SettingsWindowAllowMovement, ref allowMovement))
            // {
            //     Plugin.Config.AllowMovement = allowMovement;
            //     Plugin.Config.Save();
            // }

            // var allowResizing = Plugin.Config.AllowResize;
            // if (ImGui.Checkbox(Language.SettingsWindowAllowResize, ref allowResizing))
            // {
            //     Plugin.Config.AllowResize = allowResizing;
            //     Plugin.Config.Save();
            // }
        }

        ImGui.Spacing();
        ImGui.Spacing();

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

        using (ImGuiGroupPanel.BeginGroupPanel("Keyboard Broadcast")) {
            var kbEnabled = Plugin.Config.KeyboardBroadcastEnabled;
            if (ImGui.Checkbox("Enabled for all clients", ref kbEnabled)) {
                Plugin.Config.KeyboardBroadcastEnabled = kbEnabled;
                Plugin.Config.Save();
                Plugin.IpcProvider.SyncConfiguration();
                if (!kbEnabled) Plugin.KeyboardBroadcastManager.IsReceiving = false;
            }
            ImGuiUtil.HelpMarker("Global feature toggle synced to all clients. When disabled, no client receives key broadcasts.");

            ImGui.SameLine();
            if (ImGuiUtil.IconButton(FontAwesomeIcon.Users, $"##ShowCharactersBtn", Language.ShowCharactersBtn)) {
                Plugin.Ui.CharactersWindow.Toggle();
            }

            bool isCapturing = Plugin.KeyboardBroadcastManager.IsCapturing;
            if (ImGui.Checkbox("Broadcast my keyboard input", ref isCapturing))
                Plugin.IpcProvider.ToggleKeyboardBroadcast();
            ImGuiUtil.HelpMarker("When enabled, key presses on this client are broadcast to all other clients (master mode).");

            ImGui.SameLine();
            if (ImGui.Button("Key Filter##KbFilterBtn"))
                ImGui.OpenPopup(KbFilterPopupId);

            var showKeyBroadcastBarInfo = Plugin.Config.ShowKeyBroadcastBarInfo;
            if (ImGui.Checkbox("Show key broadcast in server bar", ref showKeyBroadcastBarInfo)) {
                Plugin.Config.ShowKeyBroadcastBarInfo = showKeyBroadcastBarInfo;
                Plugin.Config.Save();
                Plugin.IpcProvider.SyncConfiguration();
                Plugin.ServerBarProvider.Update();
            }

            DrawKeyboardFilterPopup();
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

        DrawLoginMacroGroup();

        DrawPreferredMountGroup();

        ImGui.Spacing();
        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();
        ImGui.Spacing();

        using (ImRaii.PushColor(ImGuiCol.Button, Style.Components.ButtonPurpleNormal)
            .Push(ImGuiCol.ButtonHovered, Style.Components.ButtonPurpleHovered)
            .Push(ImGuiCol.ButtonActive, Style.Components.ButtonPurpleActive)) {

            if (ImGui.Button(Language.OpenPluginFolder)) {
                WindowsApi.OpenFolder(DalamudApi.PluginInterface.ConfigDirectory.FullName);
            }

            ImGui.SameLine();
            ImGuiHelpers.ScaledDummy(0, 20);
            ImGui.SameLine();

            if (ImGui.Button(Language.OpenPluginConfigFile)) {
                WindowsApi.OpenFile(DalamudApi.PluginInterface.ConfigFile.FullName);
            }
        }
    }

    private void DrawChatSyncTab() {
        using var tabItem = ImRaii.TabItem($"{Language.SettingsChatSyncTab}###ChatSyncTabTab");
        if (!tabItem) return;

        var useChatSync = Plugin.Config.UseChatSync;
        if (ImGui.Checkbox(Language.SettingsWindowUseChatSync, ref useChatSync)) {
            Plugin.Config.UseChatSync = useChatSync;
            Plugin.IpcProvider.SyncConfiguration();
        }
        ImGuiUtil.HelpMarker("""
            Enable chat synchronization to run actions across multiple devices.
            This turns on the chat watcher for the moprun and mopstop commands.
            Set the same macro on both devices and trigger it via chat(party / linkshell etc).
            You can define which chats are listened to and limit yourself to responding only to commands from certain senders

            Chat commands
                moprun number
                moprun macro_name
                moprun "macro name with spaces"
                mopformation "formation name"
                mopstop

            Formation chat sync uses the chat sender as the default live anchor.
            Use the default anchor argument to use point 1's assigned character instead.
            All clients need the same formation and the anchor character must be visible.
            """);

        ImGui.Spacing();
        ImGui.Spacing();

        var useChatCommandSenderWhitelist = Plugin.Config.UseChatCommandSenderWhitelist;
        if (ImGui.Checkbox(Language.SettingsWindowUseChatCommandSenderWhitelist, ref useChatCommandSenderWhitelist)) {
            Plugin.Config.UseChatCommandSenderWhitelist = useChatCommandSenderWhitelist;
            Plugin.Config.Save();
            Plugin.IpcProvider.SyncConfiguration();
        }

        ImGui.Spacing();
        ImGui.Spacing();

        var selectedPrefix = Plugin.Config.DefaultChatSyncPrefix;
        ImGui.Text(Language.SettingsWindowDefaultChatSyncPrefix);
        if (ImGui.BeginCombo("##DefaultChatPrefix", selectedPrefix)) {
            foreach (var chatType in Plugin.ChatWatcher.AllowedChatTypes) {
                string prefix = chatType.ToChatPrefix();

                bool isSelected = selectedPrefix == prefix;
                if (ImGui.Selectable(prefix, isSelected)) {
                    Plugin.Config.DefaultChatSyncPrefix = prefix;
                    Plugin.Config.Save();
                    Plugin.IpcProvider.SyncConfiguration();
                }
            }

            ImGui.EndCombo();
        }
        ImGuiUtil.HelpMarker("Default chat prefix used when running macros from the list");

        ImGui.Spacing();
        ImGui.Spacing();

        ImGui.Separator();

        ImGui.Spacing();
        ImGui.Spacing();

        if (ImGui.CollapsingHeader("Allowed Chats")) {
            ImGui.Indent();
            if (ImGui.BeginCombo("##ListenedChatTypesSelectList", "Select Chat to Listen")) {
                // foreach (XivChatType chatType in Enum.GetValues(typeof(XivChatType)))
                foreach (var chatType in Plugin.ChatWatcher.AllowedChatTypes.Except(Plugin.Config.ListenedChatTypes)) {
                    // var displayName = $"{chatType} ({(int)chatType})";
                    if (ImGui.Selectable($"{chatType}", false)) {
                        Plugin.Config.ListenedChatTypes.Add(chatType);
                        Plugin.IpcProvider.SyncConfiguration();
                    }
                }
                ImGui.EndCombo();
            }

            ImGui.Spacing();
            ImGui.Spacing();

            ImGui.Text("Listened Chats");
            if (ImGui.BeginListBox("##ListenedChatTypes", new Vector2(-1, 100))) {
                foreach (var chatType in Plugin.Config.ListenedChatTypes.ToList()) {
                    var displayName = $"{chatType}";
                    if (ImGui.Selectable(displayName, false)) {
                        if (ImGui.GetIO().KeyCtrl) {
                            Plugin.Config.ListenedChatTypes.Remove(chatType);
                            Plugin.IpcProvider.SyncConfiguration();
                        }
                    }
                    ImGuiUtil.ToolTip(Language.DeleteInstructionTooltip);
                }
                ImGui.EndListBox();
            }

            ImGui.Unindent();
        }

        ImGui.Spacing();
        ImGui.Spacing();

        if (ImGui.CollapsingHeader($"Allowed Chat Command Senders")) {
            ImGui.Indent();
            ImGui.Text("Sender Name");
            ImGui.InputTextWithHint("##CommandSenderNameInput", "Sender name", ref _characterName, 255, ImGuiInputTextFlags.AutoSelectAll);

            ImGui.SameLine();
            ImGuiHelpers.ScaledDummy(0, 20);
            ImGui.SameLine();

            if (ImGuiUtil.IconButton(FontAwesomeIcon.Crosshairs, $"##AddSenderNameFromTarget", "Add From Target")) {
                _characterName = GameTargetManager.GetTargetName();
            }

            ImGui.SameLine();
            ImGuiHelpers.ScaledDummy(0, 20);
            ImGui.SameLine();

            if (ImGui.Button($"Add##AddCommandSenderBtn")) {
                if (string.IsNullOrEmpty(_characterName.Trim())) return;

                Plugin.Config.ChatCommandSenderWhitelist.AddUnique(_characterName.Trim());
                _characterName = string.Empty;
                Plugin.IpcProvider.SyncConfiguration();
            }

            ImGui.Spacing();
            ImGui.Spacing();

            ImGui.Text("Chat Command Sender Whitelist");
            if (ImGui.BeginListBox("##ChatCommandSenderWhitelist", new Vector2(-1, 100))) {
                foreach (var senderName in Plugin.Config.ChatCommandSenderWhitelist.ToList()) {
                    if (ImGui.Selectable(senderName, false)) {
                        if (ImGui.GetIO().KeyCtrl) {
                            Plugin.Config.ChatCommandSenderWhitelist.Remove(senderName);
                            Plugin.IpcProvider.SyncConfiguration();
                        }
                    }
                    ImGuiUtil.ToolTip(Language.DeleteInstructionTooltip);
                }
                ImGui.EndListBox();
            }
            ImGui.Unindent();
        }

    }

    private void DrawLoginMacroGroup() {
        using (ImGuiGroupPanel.BeginGroupPanel("Login Macro")) {
            var runLoginMacro = Plugin.Config.RunLoginMacro;
            if (ImGui.Checkbox("Run macro on login", ref runLoginMacro)) {
                Plugin.Config.RunLoginMacro = runLoginMacro;
                Plugin.Config.Save();
                Plugin.IpcProvider.SyncConfiguration();
            }
            ImGuiUtil.HelpMarker("""
            Enable this option to run a macro on login and configure settings like sound, window layout, renderhack, settings profile, and getting the leader. You can set up different commands for each case.

            Main character:
                /mop settingsprofile "normal"
                /mop getleader
                /mop layout "bard"
                /pvis HidePlayer off


            Other characters:
                /mop settingsprofile "low"
                /mop layout "bard"
                /mop renderhack on
                /pvis HidePlayer on
            """);

            ImGui.Spacing();
            if (runLoginMacro) {
                using (ImRaii.PushIndent()) {
                    ImGui.Text("Macro:");
                    ImGui.SameLine();
                    ImGui.SetNextItemWidth(150 * ImGuiHelpers.GlobalScale);
                    string currentMacro = string.IsNullOrEmpty(Plugin.Config.LoginMacro) ? "Select..." : Plugin.Config.LoginMacro;

                    using (ImRaii.PushColor(ImGuiCol.Border, Style.Components.TooltipBorderColor, true))
                    using (ImRaii.PushStyle(ImGuiStyleVar.PopupBorderSize, 1, true))
                    using (ImRaii.PushFont(UiBuilder.DefaultFont)) {
                        if (ImGui.BeginCombo("##OnLoginMacroCombo", currentMacro)) {
                            foreach (var macro in Plugin.Config.Macros) {
                                bool isSelected = Plugin.Config.LoginMacro == macro.Name;
                                if (ImGui.Selectable(macro.Name, isSelected)) {
                                    Plugin.Config.LoginMacro = macro.Name;
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
            }
        }
    }

    private void DrawPreferredMountGroup() {
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

    private void DrawCommandsTab() {
        using var tabItem = ImRaii.TabItem("Commands###CommandsTab");
        if (!tabItem) return;

        ImGui.TextWrapped("Built-in plugin slash commands.");

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        if (ImGui.BeginTable("##SettingsCommandsTable", 2, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.SizingStretchProp)) {
            ImGui.TableSetupColumn("Command", ImGuiTableColumnFlags.WidthFixed, 100 * ImGuiHelpers.GlobalScale);
            ImGui.TableSetupColumn("Description", ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableHeadersRow();

            foreach (var def in PluginCommandManager.Definitions) {
                ImGui.TableNextRow();
                ImGui.TableNextColumn();
                ImGui.TextUnformatted(def.DefaultCommand);
                ImGui.TableNextColumn();
                ImGui.TextUnformatted(def.HelpMessage);
            }

            ImGui.EndTable();
        }
    }


    private void DrawKeyboardFilterPopup() {
        ImGui.SetNextWindowSize(ImGuiHelpers.ScaledVector2(720, 360), ImGuiCond.Appearing);
        using var borderColor = ImRaii.PushColor(ImGuiCol.Border, Style.Components.TooltipBorderColor);
        using var popupBorder = ImRaii.PushStyle(ImGuiStyleVar.PopupBorderSize, 1);
        using var popUp = ImRaii.Popup(KbFilterPopupId, ImGuiWindowFlags.NoResize);
        if (!popUp) return;

        float unit = 29f * ImGuiHelpers.GlobalScale;
        float keyH = 24f * ImGuiHelpers.GlobalScale;
        float gap = 2f * ImGuiHelpers.GlobalScale;
        var ignoredKeys = Plugin.Config.KeyboardBroadcastIgnoredKeys;

        void DrawRow(KeyDef[] row) {
            bool first = true;
            foreach (var key in row) {
                if (!first) ImGui.SameLine(0, gap);
                first = false;
                float w = key.W * unit - gap;
                if (key.Vk == 0) { ImGui.Dummy(new Vector2(w, keyH)); continue; }
                bool isIgnored = ignoredKeys.Contains(key.Vk);
                using (ImRaii.PushColor(ImGuiCol.Button, Style.Components.ButtonDangerNormal, isIgnored)
                    .Push(ImGuiCol.ButtonHovered, Style.Components.ButtonDangerHovered, isIgnored)
                    .Push(ImGuiCol.ButtonActive, Style.Components.ButtonDangerActive, isIgnored)) {
                    if (ImGui.Button($"{key.Label}##{key.Vk}", new Vector2(w, keyH))) {
                        if (isIgnored) {
                            ignoredKeys.Remove(key.Vk);
                        } else {
                            ignoredKeys.Add(key.Vk);
                        }
                        Plugin.Config.Save();
                        Plugin.IpcProvider.SyncConfiguration();
                    }
                }
            }
        }

        ImGui.TextDisabled("Click a key to toggle filtering.  Red = not broadcast.");
        ImGui.Spacing();

        // Main keyboard block
        ImGui.BeginGroup();
        foreach (var row in MainKeyRows) {
            DrawRow(row);
            ImGui.Dummy(new Vector2(0, gap));
        }
        ImGui.EndGroup();

        // Navigation cluster + arrow keys (to the right)
        ImGui.SameLine(0, 18f * ImGuiHelpers.GlobalScale);
        ImGui.BeginGroup();
        KeyDef[] sysRow = [K(0x2C, "Prt"), K(0x91, "Scr"), K(0x13, "Brk")];
        KeyDef[] navRow1 = [K(0x2D, "Ins"), K(0x24, "Home"), K(0x21, "PgUp")];
        KeyDef[] navRow2 = [K(0x2E, "Del"), K(0x23, "End"), K(0x22, "PgDn")];
        KeyDef[] arrowUp = [Gap(1f), K(0x26, "↑")];
        KeyDef[] arrowLDR = [K(0x25, "←"), K(0x28, "↓"), K(0x27, "→")];
        DrawRow(sysRow); ImGui.Dummy(new Vector2(0, gap * 4f)); // extra gap separates sys/nav
        DrawRow(navRow1); ImGui.Dummy(new Vector2(0, gap));
        DrawRow(navRow2); ImGui.Dummy(new Vector2(0, gap * 4f));
        DrawRow(arrowUp); ImGui.Dummy(new Vector2(0, gap));
        DrawRow(arrowLDR);
        ImGui.EndGroup();

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();
        if (ImGui.Button("Clear All##KbClearAll")) {
            ignoredKeys.Clear();
            Plugin.Config.Save();
            Plugin.IpcProvider.SyncConfiguration();
        }
        ImGui.SameLine();
        if (ImGui.Button("Close##KbClose")) ImGui.CloseCurrentPopup();
    }
}
