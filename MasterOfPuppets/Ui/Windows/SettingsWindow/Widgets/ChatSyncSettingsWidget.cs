using System.Linq;
using System.Numerics;

using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;

using MasterOfPuppets.Resources;
using MasterOfPuppets.Util.ImGuiExt;
using MasterOfPuppets.Extensions;
using MasterOfPuppets.Extensions.Dalamud;

namespace MasterOfPuppets;

public class ChatSyncSettingsWidget : Widget {
    public override string Title => Language.SettingsChatSyncTab;
    public override FontAwesomeIcon Icon => FontAwesomeIcon.Comments;

    private string _characterName = string.Empty;

    public ChatSyncSettingsWidget(WidgetContext ctx) : base(ctx) {
    }

    public override void Draw() {
        var Plugin = Context.Plugin;

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
                foreach (var chatType in Plugin.ChatWatcher.AllowedChatTypes.Except(Plugin.Config.ListenedChatTypes)) {
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
}
