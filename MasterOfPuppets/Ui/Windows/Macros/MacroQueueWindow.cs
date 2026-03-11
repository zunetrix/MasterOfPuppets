using System;
using System.Collections.Generic;
using System.Linq;

using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.ImGuiNotification;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
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
    }

    public override void Draw() {
        bool isPaused = Plugin.MacroHandler.IsPaused;

        if (isPaused) {
            if (ImGuiUtil.IconButton(FontAwesomeIcon.Play, "##ResumeMacroExecutionQueueBtn", "Resume"))
                Plugin.IpcProvider.ResumeMacroExecution();
        } else {
            if (ImGuiUtil.IconButton(FontAwesomeIcon.Pause, "##PauseMacroExecutionQueueBtn", "Pause"))
                Plugin.IpcProvider.PauseMacroExecution();
        }

        ImGui.SameLine();
        using (ImRaii.PushColor(ImGuiCol.Button, Style.Components.ButtonDangerNormal)
            .Push(ImGuiCol.ButtonHovered, Style.Components.ButtonDangerHovered)
            .Push(ImGuiCol.ButtonActive, Style.Components.ButtonDangerActive)) {
            if (ImGuiUtil.IconButton(FontAwesomeIcon.Stop, "##StopMacroExecutionQueueBtn", Language.StopMacroExecutionBtn)) {
                Plugin.IpcProvider.StopMacroExecution();
                DalamudApi.ShowNotification("Macro execution queue stopped", NotificationType.Info, 3000);
            }
        }

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        // Snapshot for thread safety — lists are modified by the worker thread
        var macroList = Plugin.MacroHandler.CurrentActionsExecutionList.ToList();
        var macroIndex = Plugin.MacroHandler.CurrentActionExecutionIndex;
        var loopList = Plugin.MacroHandler.CurrentActionsLoopExecutionList.ToList();
        var loopIndex = Plugin.MacroHandler.CurrentActionLoopExecutionIndex;

        DrawQueuePanel("Macro Queue", macroList, macroIndex, "##MacroQueuePane", "##MacroQueueList");
        ImGui.SameLine();
        DrawQueuePanel("Loop Queue", loopList, loopIndex, "##LoopQueuePane", "##LoopQueueList");
    }

    private static void DrawQueuePanel(string label, List<string> list, int currentIndex, string childId, string listId) {
        ImGui.BeginChild(childId, ImGuiHelpers.ScaledVector2(260, 250));

        string header = list.Count > 0
            ? $"{label} [{Math.Max(currentIndex + 1, 0)} / {list.Count}]"
            : label;
        ImGui.Text(header);

        if (ImGui.BeginListBox(listId, ImGuiHelpers.ScaledVector2(260, 220))) {
            if (list.Count == 0) {
                ImGui.TextDisabled("No actions queued");
            } else {
                for (int i = 0; i < list.Count; i++) {
                    bool isActive = i == currentIndex;
                    using (ImRaii.PushColor(ImGuiCol.Text, Style.Colors.Green, isActive)) {
                        ImGui.Selectable($"[{i + 1:000}] {list[i]}{listId}_{i}", isActive);
                    }
                    if (isActive)
                        ImGui.SetScrollHereY(0.5f);
                }
            }
            ImGui.EndListBox();
        }

        ImGui.EndChild();
    }
}
