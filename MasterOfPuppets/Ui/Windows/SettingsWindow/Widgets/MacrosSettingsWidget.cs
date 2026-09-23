using System;
using System.Linq;

using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;

using MasterOfPuppets.Resources;
using MasterOfPuppets.Util.ImGuiExt;

namespace MasterOfPuppets;

public class MacrosSettingsWidget : Widget {
    private string _globalDelayExclusionInput = string.Empty;
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
            Be careful when disabling global delay along with loops to avoid spamming server actions
            """);

            if (ImGui.CollapsingHeader("Global delay exclusions")) {
                DrawGlobalDelayExclusions(Plugin);
            }
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
    private void DrawGlobalDelayExclusions(Plugin Plugin) {
        ImGui.SetNextItemWidth(180 * ImGuiHelpers.GlobalScale);
        ImGui.InputTextWithHint("##GlobalDelayExclusionInput", "Command name", ref _globalDelayExclusionInput, 64);
        ImGuiUtil.HelpMarker("Commands in this list skip the global delay");

        ImGui.SameLine();
        if (ImGui.Button("Add##GlobalDelayExclusion")) {
            var command = MacroHandler.NormalizeGlobalDelayCommand(_globalDelayExclusionInput);
            if (command.Length > 0 && !Plugin.Config.GlobalDelayExcludedCommands.Contains(command, StringComparer.OrdinalIgnoreCase)) {
                Plugin.Config.GlobalDelayExcludedCommands.Add(command.ToLowerInvariant());
                _globalDelayExclusionInput = string.Empty;
                Plugin.IpcProvider.SyncConfiguration();
            }
        }

        var excludedCommands = Plugin.Config.GlobalDelayExcludedCommands.ToList();
        if (excludedCommands.Count > 0) {
            ImGui.Spacing();

            float actionsColWidth = ImGui.GetFrameHeight() + ImGui.GetStyle().ItemSpacing.X;
            if (ImGui.BeginTable("##GlobalDelayExclusionsTable", 3,
                ImGuiTableFlags.RowBg | ImGuiTableFlags.PadOuterX |
                ImGuiTableFlags.NoSavedSettings | ImGuiTableFlags.BordersInnerV)) {

                ImGui.TableSetupColumn("#", ImGuiTableColumnFlags.WidthFixed, 28 * ImGuiHelpers.GlobalScale);
                ImGui.TableSetupColumn("Command name", ImGuiTableColumnFlags.WidthStretch);
                ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthFixed, actionsColWidth);
                ImGui.TableHeadersRow();

                for (int i = 0; i < excludedCommands.Count; i++) {
                    var command = excludedCommands[i];
                    ImGui.PushID(i);
                    ImGui.TableNextRow();

                    ImGui.TableNextColumn();
                    ImGui.Text($"{i + 1:00}");

                    ImGui.TableNextColumn();
                    ImGui.TextUnformatted(command);

                    ImGui.TableNextColumn();
                    if (ImGuiUtil.IconButton(FontAwesomeIcon.Trash, $"##RemoveExclusion_{i}", Language.DeleteInstructionTooltip)) {
                        if (ImGui.GetIO().KeyCtrl) {
                            Plugin.Config.GlobalDelayExcludedCommands.Remove(command);
                            Plugin.IpcProvider.SyncConfiguration();
                        }
                    }

                    ImGui.PopID();
                }
                ImGui.EndTable();
            }
        }
    }
}
