using System.Linq;
using System.Numerics;

using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;

using MasterOfPuppets.Resources;
using MasterOfPuppets.Util.ImGuiExt;
using MasterOfPuppets.Extensions;
using MasterOfPuppets.Extensions.Dalamud;

namespace MasterOfPuppets;

public class BroadcastSettingsWidget : Widget {
    public override string Title => "Broadcast";
    public override FontAwesomeIcon Icon => FontAwesomeIcon.BroadcastTower;

    private string _characterName = string.Empty;

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

    public BroadcastSettingsWidget(WidgetContext ctx) : base(ctx) {
    }

    public override void Draw() {
        var Plugin = Context.Plugin;

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
            if (ImGui.Checkbox("Show key broadcast icon in server bar", ref showKeyBroadcastBarInfo)) {
                Plugin.Config.ShowKeyBroadcastBarInfo = showKeyBroadcastBarInfo;
                Plugin.Config.Save();
                Plugin.IpcProvider.SyncConfiguration();
                Plugin.ServerBarProvider.Update();
            }

            DrawKeyboardFilterPopup(Plugin);
        }

        ImGui.Spacing();
        ImGui.Spacing();

        using (ImGuiGroupPanel.BeginGroupPanel("Chat Sync")) {
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
                if (ImGui.BeginTable("##ListenedChatTypesTable", 2, ImGuiTableFlags.RowBg | ImGuiTableFlags.PadOuterX | ImGuiTableFlags.NoSavedSettings | ImGuiTableFlags.BordersInnerV)) {
                    ImGui.TableSetupColumn("Chat Type", ImGuiTableColumnFlags.WidthStretch);
                    ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthFixed, ImGui.GetFrameHeight() + ImGui.GetStyle().ItemSpacing.X);
                    ImGui.TableHeadersRow();

                    foreach (var chatType in Plugin.Config.ListenedChatTypes.ToList()) {
                        ImGui.PushID($"ChatType_{chatType}");
                        ImGui.TableNextRow();
                        ImGui.TableNextColumn();
                        ImGui.AlignTextToFramePadding();
                        ImGui.Text($"{chatType}");

                        ImGui.TableNextColumn();
                        if (ImGuiUtil.IconButton(FontAwesomeIcon.Trash, "##RemoveChatType", Language.DeleteInstructionTooltip)) {
                            if (ImGui.GetIO().KeyCtrl) {
                                Plugin.Config.ListenedChatTypes.Remove(chatType);
                                Plugin.IpcProvider.SyncConfiguration();
                            }
                        }
                        ImGui.PopID();
                    }
                    ImGui.EndTable();
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
                if (ImGui.BeginTable("##ChatCommandSenderWhitelistTable", 2, ImGuiTableFlags.RowBg | ImGuiTableFlags.PadOuterX | ImGuiTableFlags.NoSavedSettings | ImGuiTableFlags.BordersInnerV)) {
                    ImGui.TableSetupColumn("Sender Name", ImGuiTableColumnFlags.WidthStretch);
                    ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthFixed, ImGui.GetFrameHeight() + ImGui.GetStyle().ItemSpacing.X);
                    ImGui.TableHeadersRow();

                    foreach (var senderName in Plugin.Config.ChatCommandSenderWhitelist.ToList()) {
                        ImGui.PushID($"Sender_{senderName}");
                        ImGui.TableNextRow();
                        ImGui.TableNextColumn();
                        ImGui.AlignTextToFramePadding();
                        ImGui.Text(senderName);

                        ImGui.TableNextColumn();
                        if (ImGuiUtil.IconButton(FontAwesomeIcon.Trash, "##RemoveSender", Language.DeleteInstructionTooltip)) {
                            if (ImGui.GetIO().KeyCtrl) {
                                Plugin.Config.ChatCommandSenderWhitelist.Remove(senderName);
                                Plugin.IpcProvider.SyncConfiguration();
                            }
                        }
                        ImGui.PopID();
                    }
                    ImGui.EndTable();
                }
                ImGui.Unindent();
            }
        }
    }

    private void DrawKeyboardFilterPopup(Plugin Plugin) {
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
