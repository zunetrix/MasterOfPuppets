using System;

using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;

using MasterOfPuppets.Resources;
using MasterOfPuppets.Util.ImGuiExt;

namespace MasterOfPuppets;

public class MacrosSettingsWidget : Widget {
    public override string Title => "Macros";
    public override FontAwesomeIcon Icon => FontAwesomeIcon.Scroll;

    public MacrosSettingsWidget(WidgetContext ctx) : base(ctx) {
    }

    public override void Draw() {
        var Plugin = Context.Plugin;

        using (ImGuiGroupPanel.BeginGroupPanel("General Macro Settings")) {
            var autoSaveMacro = Plugin.Config.AutoSaveMacro;
            if (ImGui.Checkbox(Language.SettingsWindowAutoSaveMacro, ref autoSaveMacro)) {
                Plugin.Config.AutoSaveMacro = autoSaveMacro;
                Plugin.IpcProvider.SyncConfiguration();
            }
            ImGuiUtil.HelpMarker("Auto save macro on close editor");

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

        DrawLoginMacroGroup(Plugin);
    }

    private void DrawLoginMacroGroup(Plugin Plugin) {
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
}
