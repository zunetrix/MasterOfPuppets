using System;
using System.Linq;
using System.Numerics;

using Dalamud.Interface.Windowing;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;

using MasterOfPuppets.Resources;

namespace MasterOfPuppets;

public class SettingsWindow : Window
{
    private Plugin Plugin { get; }
    private string _characterName = string.Empty;
    private SettingsDisplayObjectLimitType _objectQuantityType;

    public SettingsWindow(Plugin plugin) : base($"{Plugin.Name} {Language.SettingsTitle}###SettingsWindow")
    {
        Plugin = plugin;

        Size = ImGuiHelpers.ScaledVector2(400, 300);
        SizeCondition = ImGuiCond.FirstUseEver;
        // SizeCondition = ImGuiCond.Always;
        // Flags = ImGuiWindowFlags.NoResize;

        _objectQuantityType = GameSettingsManager.GetDisplayObjectLimit();
    }

    public override void PreDraw()
    {
        base.PreDraw();
    }

    public override void Draw()
    {
        if (!ImGui.BeginTabBar("##SettingsTabs")) return;
        DrawGeneralTab();
        DrawChatSyncTab();
        DrawGameSettingsTab();
        ImGui.EndTabBar();
    }

    private void DrawGeneralTab()
    {
        if (ImGui.BeginTabItem($"{Language.SettingsGeneralTab}###GeneralTab"))
        {

            ImGuiGroupPanel.BeginGroupPanel(Language.SettingsGeneralTab);
            var syncClients = Plugin.Config.SyncClients;
            if (ImGui.Checkbox(Language.SettingsWindowSyncClients, ref syncClients))
            {
                Plugin.Config.SyncClients = syncClients;
                Plugin.Config.Save();
                Plugin.IpcProvider.SyncConfiguration();
            }
            ImGuiUtil.HelpMarker("Allow actions to be executed in broadcast to all clients");

            ImGui.Spacing();
            ImGui.Spacing();

            var saveConfigAfterSync = Plugin.Config.SaveConfigAfterSync;
            if (ImGui.Checkbox(Language.SettingsWindowSaveConfigAfterSync, ref saveConfigAfterSync))
            {
                Plugin.Config.SaveConfigAfterSync = saveConfigAfterSync;
                Plugin.Config.Save();
                Plugin.IpcProvider.SyncConfiguration();
            }
            ImGuiUtil.HelpMarker("Enable for accounts with individual config file");

            ImGui.Spacing();
            ImGui.Spacing();

            var autoSaveMacro = Plugin.Config.AutoSaveMacro;
            if (ImGui.Checkbox(Language.SettingsWindowAutoSaveMacro, ref autoSaveMacro))
            {
                Plugin.Config.AutoSaveMacro = autoSaveMacro;
                Plugin.IpcProvider.SyncConfiguration();
            }
            ImGuiUtil.HelpMarker("Auto save macro on close editor");

            ImGui.Spacing();
            ImGui.Spacing();
            ImGui.TextUnformatted("Global delay between actions");
            ImGui.SetNextItemWidth(150);
            var delayBetweenActions = Plugin.Config.DelayBetweenActions;
            if (ImGui.InputDouble("##DelayBetrweenActions", ref delayBetweenActions, 0.1, 1, "%.2f", ImGuiInputTextFlags.AutoSelectAll))
            {
                delayBetweenActions = Math.Clamp(Math.Round(delayBetweenActions, 2, MidpointRounding.AwayFromZero), 0, 60);
                Plugin.Config.DelayBetweenActions = delayBetweenActions;
                Plugin.Config.Save();
                Plugin.IpcProvider.SyncConfiguration();
            }
            ImGuiUtil.HelpMarker("Set 0 to disable");
            ImGuiGroupPanel.EndGroupPanel();

            ImGui.Spacing();
            ImGui.Spacing();


            ImGuiGroupPanel.BeginGroupPanel("Window");
            var openOnStartup = Plugin.Config.OpenOnStartup;
            if (ImGui.Checkbox(Language.SettingsWindowOpenOnStartup, ref openOnStartup))
            {
                Plugin.Config.OpenOnStartup = openOnStartup;
                Plugin.Config.Save();
                Plugin.IpcProvider.SyncConfiguration();
            }

            var openOnLogin = Plugin.Config.OpenOnLogin;
            if (ImGui.Checkbox(Language.SettingsWindowOpenLogin, ref openOnLogin))
            {
                Plugin.Config.OpenOnLogin = openOnLogin;
                Plugin.Config.Save();
                Plugin.IpcProvider.SyncConfiguration();
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

            // var allowCloseWithEscape = Plugin.Config.AllowCloseWithEscape;
            // if (ImGui.Checkbox(Language.SettingsWindowAllowCloseWithEscape, ref allowCloseWithEscape))
            // {
            //     Plugin.Config.AllowCloseWithEscape = allowCloseWithEscape;
            //     Plugin.Config.Save();
            //     Plugin.Ui.MainWindow.UpdateConfig();
            // }
            ImGuiGroupPanel.EndGroupPanel();

            ImGui.Spacing();
            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();
            ImGui.Spacing();

            ImGui.PushStyleColor(ImGuiCol.Button, Style.Components.ButtonPurpleNormal);
            ImGui.PushStyleColor(ImGuiCol.ButtonHovered, Style.Components.ButtonPurpleHovered);
            ImGui.PushStyleColor(ImGuiCol.ButtonActive, Style.Components.ButtonPurpleActive);
            if (ImGui.Button(Language.OpenPluginFolder))
            {
                WindowsApi.OpenFolder(DalamudApi.PluginInterface.ConfigDirectory.FullName);
            }

            ImGui.SameLine();
            ImGui.Dummy(ImGuiHelpers.ScaledVector2(0, 20));
            ImGui.SameLine();

            if (ImGui.Button(Language.OpenPluginConfigFile))
            {
                WindowsApi.OpenFile(DalamudApi.PluginInterface.ConfigFile.FullName);
            }
            ImGui.PopStyleColor(3);

            // ImGui.Spacing();
            // var targetedColour = Plugin.Config.TargetedColour;
            // if (ImGui.ColorEdit4(Language.SettingsMarkersMarkTargetColour, ref targetedColour))
            // {
            //     Plugin.Config.TargetedColour = targetedColour;
            //     Plugin.Config.Save();
            // }

            // var targetedSize = Plugin.Config.TargetedSize;
            // if (ImGui.DragFloat(Language.SettingsMarkersMarkTargetSize, ref targetedSize, 0.01f, 0f, 15f))
            // {
            //     targetedSize = Math.Max(0f, targetedSize);
            //     Plugin.Config.TargetedSize = targetedSize;
            //     Plugin.Config.Save();
            // }

            ImGui.EndTabItem();
        }
    }

    private void DrawChatSyncTab()
    {
        if (ImGui.BeginTabItem($"{Language.SettingsChatSyncTab}###ChatSyncTabTab"))
        {
            var useChatSync = Plugin.Config.UseChatSync;
            if (ImGui.Checkbox(Language.SettingsWindowUseChatSync, ref useChatSync))
            {
                Plugin.Config.UseChatSync = useChatSync;
                Plugin.Config.Save();
                Plugin.IpcProvider.SyncConfiguration();
            }
            ImGuiUtil.HelpMarker("""
            Enable chat synchronization to run actions across multiple devices.
            This turns on the chat watcher for the moprun and mopstop commands.
            Set the same macro on both devices and trigger it via chat (party / linkshell etc).
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
            if (ImGui.Checkbox(Language.SettingsWindowUseChatCommandSenderWhitelist, ref useChatCommandSenderWhitelist))
            {
                Plugin.Config.UseChatCommandSenderWhitelist = useChatCommandSenderWhitelist;
                Plugin.Config.Save();
                Plugin.IpcProvider.SyncConfiguration();
            }

            ImGui.Spacing();
            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();
            ImGui.Spacing();

            if (ImGui.CollapsingHeader("Allowed Chats"))
            {
                ImGui.Indent();
                if (ImGui.BeginCombo("##ListenedChatTypesSelectList", "Select Chat to Listen"))
                {
                    // foreach (XivChatType chatType in Enum.GetValues(typeof(XivChatType)))
                    foreach (var chatType in Plugin.ChatWatcher.AllowedChatTypes.Except(Plugin.Config.ListenedChatTypes))
                    {
                        // var displayName = $"{chatType} ({(int)chatType})";
                        var displayName = $"{chatType}";
                        if (ImGui.Selectable(displayName, false))
                        {
                            Plugin.Config.ListenedChatTypes.Add(chatType);
                            Plugin.IpcProvider.SyncConfiguration();
                        }
                    }
                    ImGui.EndCombo();
                }

                ImGui.Spacing();
                ImGui.Spacing();

                ImGui.TextUnformatted("Listened Chats");
                if (ImGui.BeginListBox("##ListenedChatTypes", new Vector2(-1, 100)))
                {
                    foreach (var chatType in Plugin.Config.ListenedChatTypes.ToList())
                    {
                        var displayName = $"{chatType}";
                        if (ImGui.Selectable(displayName, false, ImGuiSelectableFlags.AllowDoubleClick))
                        {
                            if (ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left))
                            {
                                Plugin.Config.ListenedChatTypes.Remove(chatType);
                                Plugin.IpcProvider.SyncConfiguration();
                            }
                        }
                        ImGuiUtil.ToolTip("Doubleclick to remove");
                    }
                    ImGui.EndListBox();
                }

                ImGui.Unindent();
            }

            ImGui.Spacing();
            ImGui.Spacing();

            if (ImGui.CollapsingHeader($"Allowed Chat Command Senders"))
            {
                ImGui.Indent();
                ImGui.TextUnformatted("Sender Name");
                ImGui.InputTextWithHint("##CommandSenderNameInput", "Sender name", ref _characterName, 255, ImGuiInputTextFlags.AutoSelectAll);

                ImGui.SameLine();
                ImGui.Dummy(ImGuiHelpers.ScaledVector2(0, 20));
                ImGui.SameLine();

                if (ImGui.Button($"Add##AddCommandSenderBtn"))
                {
                    if (string.IsNullOrEmpty(_characterName.Trim())) return;

                    Plugin.Config.ChatCommandSenderWhitelist.AddUnique(_characterName.Trim());
                    _characterName = string.Empty;
                    Plugin.IpcProvider.SyncConfiguration();
                }

                ImGui.Spacing();
                ImGui.Spacing();

                ImGui.TextUnformatted("Chat Command Sender Whitelist");
                if (ImGui.BeginListBox("##ChatCommandSenderWhitelist", new Vector2(-1, 100)))
                {
                    foreach (var senderName in Plugin.Config.ChatCommandSenderWhitelist)
                    {
                        if (ImGui.Selectable(senderName, false, ImGuiSelectableFlags.AllowDoubleClick))
                        {
                            if (ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left))
                            {
                                Plugin.Config.ChatCommandSenderWhitelist.Remove(senderName);
                                Plugin.IpcProvider.SyncConfiguration();
                            }
                        }
                        ImGuiUtil.ToolTip("Doubleclick to remove");
                    }
                    ImGui.EndListBox();
                }

                ImGui.Unindent();
            }

            ImGui.EndTabItem();
        }
    }

    private void DrawGameSettingsTab()
    {
        if (ImGui.BeginTabItem($"{Language.SettingsGameSettingsTab}###GameSettingsTab"))
        {
            ImGui.TextUnformatted("Object Quantity Limit");
            ImGuiUtil.HelpMarker("Change for all clients");
            if (ImGuiUtil.EnumCombo("##SettingsObjectQuantity", ref _objectQuantityType))
            {
                Plugin.IpcProvider.SetGameSettingsObjectQuantity(_objectQuantityType);
            }
            ImGuiUtil.ToolTip("Change object quantity limit");

            ImGui.EndTabItem();
        }
    }
}
