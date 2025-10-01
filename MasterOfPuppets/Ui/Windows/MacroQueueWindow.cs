using System.Numerics;

using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.ImGuiNotification;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Windowing;

using MasterOfPuppets.Resources;
using MasterOfPuppets.Util.ImGuiExt;

namespace MasterOfPuppets;

public class MacroQueueWindow : Window {
    private Plugin Plugin { get; }

    public MacroQueueWindow(Plugin plugin) : base($"{Language.MacroQueueTitle}###MacroQueueWindow") {
        Plugin = plugin;

        Size = ImGuiHelpers.ScaledVector2(550, 300);
        SizeCondition = ImGuiCond.FirstUseEver;
        // SizeCondition = ImGuiCond.Always;
        // Flags = ImGuiWindowFlags.NoResize;
    }

    public override void PreDraw() {
        base.PreDraw();
    }

    public override void Draw() {
        ImGui.BeginDisabled(true);
        if (ImGuiUtil.IconButton(FontAwesomeIcon.Play, $"##StartMacroExecutionQueueBtn", "Start")) {
            // Plugin.IpcProvider.StartMacroExecution();
        }

        ImGui.SameLine();
        if (ImGuiUtil.IconButton(FontAwesomeIcon.Pause, $"##PauseMacroExecutionQueueBtn", "Pause")) {
            // Plugin.IpcProvider.PauseMacroExecution();
        }
        ImGui.EndDisabled();

        ImGui.SameLine();
        ImGui.PushStyleColor(ImGuiCol.Button, Style.Components.ButtonDangerNormal);
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, Style.Components.ButtonDangerHovered);
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, Style.Components.ButtonDangerActive);
        if (ImGuiUtil.IconButton(FontAwesomeIcon.Stop, $"##StopMacroExecutionQueueBtn", Language.StopMacroExecutionBtn)) {
            Plugin.IpcProvider.StopMacroExecution();
            DalamudApi.ShowNotification($"Macro execution queue stoped", NotificationType.Info, 3000);
        }
        ImGui.PopStyleColor(3);

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();


        ImGui.BeginChild("##CurrentActionsExecutionListLeftPane", ImGuiHelpers.ScaledVector2(300, 250));
        ImGui.TextUnformatted("Macro Queue");
        if (ImGui.BeginListBox($"##CurrentActionsExecutionList", ImGuiHelpers.ScaledVector2(300, 220))) {
            for (int i = 0; i < Plugin.MacroHandler.CurrentActionsExecutionList.Count; i++) {
                var isCurrentItemActive = i == Plugin.MacroHandler.CurrentActionExecutionIndex;
                if (isCurrentItemActive) {
                    ImGui.PushStyleColor(ImGuiCol.Text, Style.Colors.Green);
                }
                ImGui.Selectable($"[{i + 1:000}] {Plugin.MacroHandler.CurrentActionsExecutionList[i]}##CurrentActionsExecutionList_{i}", isCurrentItemActive, ImGuiSelectableFlags.None);
                // var windowWidth = ImGui.GetWindowContentRegionMax().X - ImGui.GetWindowContentRegionMin().X;
                // ImGui.SameLine(windowWidth - ImGui.CalcTextSize(time).X);

                if (isCurrentItemActive) {
                    ImGui.PopStyleColor();
                }
            }

            ImGui.EndListBox();
        }
        ImGui.EndChild();

        ImGui.SameLine();
        ImGui.BeginChild("##CurrentActionsExecutionListRightPane", ImGuiHelpers.ScaledVector2(300, 250));
        ImGui.TextUnformatted($"Loop Queue");
        if (ImGui.BeginListBox($"##CurrentActionsLoopExecutionList", ImGuiHelpers.ScaledVector2(300, 220))) {
            for (int i = 0; i < Plugin.MacroHandler.CurrentActionsLoopExecutionList.Count; i++) {
                var isCurrentItemActive = i == Plugin.MacroHandler.CurrentActionLoopExecutionIndex;
                if (isCurrentItemActive) {
                    ImGui.PushStyleColor(ImGuiCol.Text, Style.Colors.Green);
                }
                ImGui.Selectable($"[{i + 1:000}] {Plugin.MacroHandler.CurrentActionsLoopExecutionList[i]}##CurrentActionsLoopExecutionList_{i}", isCurrentItemActive, ImGuiSelectableFlags.None);

                if (isCurrentItemActive) {
                    ImGui.PopStyleColor();
                }
            }
            ImGui.EndListBox();
        }
        ImGui.EndChild();
    }
}
