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
    private SettingsDisplayObjectLimitType _objectQuantityType;
    // commandKey → { defaultAlias → current input text }
    private readonly Dictionary<string, Dictionary<string, string>> _aliasInputs = new();

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

    public SettingsWindow(Plugin plugin) : base($"{Plugin.Name} {Language.SettingsTitle}###SettingsWindow") {
        Plugin = plugin;

        Size = ImGuiHelpers.ScaledVector2(400, 300);
        SizeCondition = ImGuiCond.FirstUseEver;
        // SizeCondition = ImGuiCond.Always;
        // Flags = ImGuiWindowFlags.NoResize;

        _objectQuantityType = GameSettingsManager.GetDisplayObjectLimit();
    }

    public override void OnOpen() {
        _objectQuantityType = GameSettingsManager.GetDisplayObjectLimit();
        foreach (var def in PluginCommandManager.Definitions) {
            _aliasInputs[def.Key] = new();
            foreach (var alias in def.DefaultAliases) {
                if (alias.Equals(def.DefaultCommand, StringComparison.OrdinalIgnoreCase)) continue;
                _aliasInputs[def.Key][alias] = GetEffectiveAliasName(def.Key, alias);
            }
        }
        base.OnOpen();
    }

    public override void Draw() {
        {
            using var tabBar = ImRaii.TabBar("##SettingsTabs");
            if (tabBar) {
                DrawGeneralTab();
                DrawChatSyncTab();
                DrawGameSettingsTab();
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

            ImGui.Spacing();
            ImGui.Spacing();

            var saveConfigAfterSync = Plugin.Config.SaveConfigAfterSync;
            if (ImGui.Checkbox(Language.SettingsWindowSaveConfigAfterSync, ref saveConfigAfterSync)) {
                Plugin.Config.SaveConfigAfterSync = saveConfigAfterSync;
                Plugin.Config.Save();
                Plugin.IpcProvider.SyncConfiguration();
            }
            ImGuiUtil.HelpMarker("Enable for accounts with individual config file");

            ImGui.Spacing();
            ImGui.Spacing();

            var autoSaveMacro = Plugin.Config.AutoSaveMacro;
            if (ImGui.Checkbox(Language.SettingsWindowAutoSaveMacro, ref autoSaveMacro)) {
                Plugin.Config.AutoSaveMacro = autoSaveMacro;
                Plugin.IpcProvider.SyncConfiguration();
            }
            ImGuiUtil.HelpMarker("Auto save macro on close editor");

            ImGui.Spacing();
            ImGui.Spacing();
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
            }
            ImGuiUtil.HelpMarker("Removes the FFXIV mutex to allow opening more than 2 game instances");
        }

        ImGui.Spacing();
        ImGui.Spacing();

        using (ImGuiGroupPanel.BeginGroupPanel("Cam Hack")) {
            bool enabled = GameCameraManager.Enabled;
            if (ImGui.Checkbox("Cam Hack", ref enabled)) {
                if (enabled)
                    GameCameraManager.EnableCamHighHeight();
                else
                    GameCameraManager.Disable();
            }

            ImGui.Text($"Camera Height Offset: {GameCameraManager.YOffset}");
            ImGui.SetNextItemWidth(150 * ImGuiHelpers.GlobalScale);
            if (ImGui.DragFloat("##CameraYOffset", ref _cameraYOffset, 1f, 0f, GameCameraManager.MaxYOffset, "%.0f")) {
                float YOffset = Math.Clamp(_cameraYOffset, 0f, GameCameraManager.MaxYOffset);
                GameCameraManager.SetHeight(YOffset, true);
            }
            ImGui.SameLine();
            if (ImGuiUtil.IconButton(FontAwesomeIcon.Undo, "##ResetCameraOffsetBtn", "Reset")) {
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
            ImGui.Spacing();

            bool isCapturing = Plugin.KeyboardBroadcastManager.IsCapturing;
            if (ImGui.Checkbox("Broadcast my keyboard input", ref isCapturing))
                Plugin.IpcProvider.ToggleKeyboardBroadcast();
            ImGuiUtil.HelpMarker("When enabled, key presses on this client are broadcast to all other clients (master mode).");

            ImGui.SameLine();
            if (ImGui.Button("Key Filter##KbFilterBtn"))
                ImGui.OpenPopup(KbFilterPopupId);

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
                mopstop
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

    private void DrawGameSettingsTab() {
        using var tabItem = ImRaii.TabItem($"{Language.SettingsGameSettingsTab}###GameSettingsTab");
        if (!tabItem) return;

        ImGui.Text("Object Quantity Limit");
        ImGuiUtil.HelpMarker("Change for all clients");
        if (ImGuiUtil.EnumCombo("##SettingsObjectQuantity", ref _objectQuantityType)) {
            Plugin.IpcProvider.SetGameSettingsObjectQuantity(_objectQuantityType);
        }
        ImGuiUtil.ToolTip("Change object quantity limit");

        // var MoveMode = DalamudApi.GameConfig.UiControl.GetUInt("MoveMode");
        // var PadMode = DalamudApi.GameConfig.UiConfig.GetUInt("PadMode");
        // DalamudApi.GameConfig.UiControl.Set("MoveMode", 0);
        // DalamudApi.GameConfig.UiConfig.Set("PadMode", 0);
    }

    private void DrawCommandsTab() {
        using var tabItem = ImRaii.TabItem("Commands###CommandsTab");
        if (!tabItem) return;

        ImGui.TextWrapped("Enable and rename aliases for built-in commands. Changes are synced to all clients.");
        ImGuiUtil.HelpMarker("Example: enable '/br' as alias for '/mopbr', or rename it to '/broadcast'.");

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        foreach (var def in PluginCommandManager.Definitions) {
            var visibleAliases = def.DefaultAliases
                .Where(a => !a.Equals(def.DefaultCommand, StringComparison.OrdinalIgnoreCase))
                .ToArray();
            if (visibleAliases.Length == 0) continue;

            ImGui.TextDisabled(def.DefaultCommand);

            Plugin.Config.EnabledCommandAliases.TryGetValue(def.Key, out var enabledAliases);
            if (!_aliasInputs.ContainsKey(def.Key)) _aliasInputs[def.Key] = new();

            foreach (var alias in visibleAliases) {
                ImGui.Indent(16);

                bool enabled = enabledAliases != null && enabledAliases.Contains(alias, StringComparer.OrdinalIgnoreCase);
                if (ImGui.Checkbox($"##chk_{def.Key}_{alias}", ref enabled)) {
                    if (!Plugin.Config.EnabledCommandAliases.ContainsKey(def.Key))
                        Plugin.Config.EnabledCommandAliases[def.Key] = new();
                    if (enabled)
                        Plugin.Config.EnabledCommandAliases[def.Key].Add(alias);
                    else
                        Plugin.Config.EnabledCommandAliases[def.Key].Remove(alias);
                    SaveAndRefreshCommands();
                }

                ImGui.SameLine();

                if (!_aliasInputs[def.Key].TryGetValue(alias, out var aliasInput))
                    aliasInput = _aliasInputs[def.Key][alias] = GetEffectiveAliasName(def.Key, alias);

                string stored = GetEffectiveAliasName(def.Key, alias);
                bool inputDiffers = !aliasInput.Equals(stored, StringComparison.OrdinalIgnoreCase);
                bool isValid = aliasInput.StartsWith('/') && aliasInput.Length >= 2 && !aliasInput.Contains(' ');

                ImGui.SetNextItemWidth(120 * ImGuiHelpers.GlobalScale);
                if (ImGui.InputText($"##inp_{def.Key}_{alias}", ref aliasInput, 64))
                    _aliasInputs[def.Key][alias] = aliasInput;

                ImGui.SameLine();
                using (ImRaii.Disabled(!inputDiffers || !isValid)) {
                    if (ImGui.Button($"Apply##aliasApply_{def.Key}_{alias}")) {
                        if (!Plugin.Config.CustomAliasNames.ContainsKey(def.Key))
                            Plugin.Config.CustomAliasNames[def.Key] = new();
                        Plugin.Config.CustomAliasNames[def.Key][alias] = aliasInput.Trim().ToLowerInvariant();
                        SaveAndRefreshCommands();
                    }
                }

                bool hasCustom = Plugin.Config.CustomAliasNames.TryGetValue(def.Key, out var cn) && cn.ContainsKey(alias);
                ImGui.SameLine();
                using (ImRaii.Disabled(!hasCustom)) {
                    if (ImGui.Button($"Reset##aliasReset_{def.Key}_{alias}")) {
                        Plugin.Config.CustomAliasNames.TryGetValue(def.Key, out var rn);
                        rn?.Remove(alias);
                        _aliasInputs[def.Key][alias] = alias;
                        SaveAndRefreshCommands();
                    }
                }

                ImGui.Unindent(16);
            }

            ImGui.Spacing();
        }
    }

    private string GetEffectiveAliasName(string key, string defaultAlias) {
        if (Plugin.Config.CustomAliasNames.TryGetValue(key, out var names) &&
            names.TryGetValue(defaultAlias, out var custom) &&
            !string.IsNullOrWhiteSpace(custom))
            return custom;
        return defaultAlias;
    }

    private void SaveAndRefreshCommands() {
        Plugin.Config.Save();
        Plugin.IpcProvider.SyncConfiguration();
        Plugin.IpcProvider.RefreshCommands();
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
