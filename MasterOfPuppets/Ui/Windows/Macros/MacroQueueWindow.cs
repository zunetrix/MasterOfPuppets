using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

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

    private readonly record struct QueueControls(bool IsPaused, Action OnPause, Action OnResume, Action OnStop);

    public MacroQueueWindow(Plugin plugin) : base($"{Language.MacroQueueTitle}###MacroQueueWindow") {
        Plugin = plugin;

        Size = ImGuiHelpers.ScaledVector2(660, 360);
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
        if (ImGuiUtil.DangerIconButton(FontAwesomeIcon.Stop, "##StopMacroExecutionQueueBtn", Language.StopMacroExecutionBtn)) {
            Plugin.IpcProvider.StopMacroExecution();
            DalamudApi.ShowNotification("Macro execution queue stopped", NotificationType.Info, 3000);
        }

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        // Snapshot for thread safety
        var macroPending = Plugin.MacroHandler.MacroPendingQueue.ToList();
        var macroCurrentId = Plugin.MacroHandler.MacroCurrentId;
        var macroActions = Plugin.MacroHandler.MacroCurrentActions.ToList();
        var macroIndex = Plugin.MacroHandler.MacroCurrentIndex;

        var loopPending = Plugin.MacroHandler.LoopPendingQueue.ToList();
        var loopCurrentId = Plugin.MacroHandler.LoopCurrentId;
        var loopActions = Plugin.MacroHandler.LoopCurrentActions.ToList();
        var loopIndex = Plugin.MacroHandler.LoopCurrentIndex;

        DrawQueuePanel(
            label: "Macro Queue",
            pendingMacros: macroPending, currentMacroId: macroCurrentId,
            currentActions: macroActions, currentIndex: macroIndex,
            controls: new QueueControls(
                Plugin.MacroHandler.IsMacroQueuePaused,
                Plugin.MacroHandler.PauseMacroQueue,
                Plugin.MacroHandler.ResumeMacroQueue,
                Plugin.MacroHandler.StopMacroQueue),
            childId: "##MacroQueuePane");

        ImGui.SameLine();

        DrawQueuePanel(
            label: "Loop Queue",
            pendingMacros: loopPending, currentMacroId: loopCurrentId,
            currentActions: loopActions, currentIndex: loopIndex,
            controls: new QueueControls(
                Plugin.MacroHandler.IsLoopQueuePaused,
                Plugin.MacroHandler.PauseLoopQueue,
                Plugin.MacroHandler.ResumeLoopQueue,
                Plugin.MacroHandler.StopLoopQueue),
            childId: "##LoopQueuePane");
    }

    private static void DrawQueuePanel(
        string label,
        List<string> pendingMacros,
        string? currentMacroId,
        List<string> currentActions,
        int currentIndex,
        QueueControls controls,
        string childId) {

        ImGui.BeginChild(childId, ImGuiHelpers.ScaledVector2(316, 280));

        // Controls row
        if (controls.IsPaused) {
            if (ImGuiUtil.IconButton(FontAwesomeIcon.Play, $"##Resume{childId}", "Resume"))
                controls.OnResume();
        } else {
            if (ImGuiUtil.IconButton(FontAwesomeIcon.Pause, $"##Pause{childId}", "Pause"))
                controls.OnPause();
        }
        ImGui.SameLine();
        if (ImGuiUtil.DangerIconButton(FontAwesomeIcon.Stop, $"##Stop{childId}", "Stop"))
            controls.OnStop();
        ImGui.SameLine();
        ImGui.Text(label);

        float subHeight = ImGuiHelpers.GlobalScale * 235f;

        // Left: pending macro names
        ImGui.BeginChild($"##Pending{childId}", new Vector2(ImGuiHelpers.GlobalScale * 110f, subHeight));
        ImGui.TextDisabled($"Queue ({pendingMacros.Count})");
        if (ImGui.BeginListBox($"##PendingList{childId}", new Vector2(-1, -1))) {
            if (pendingMacros.Count == 0) {
                ImGui.TextDisabled("–");
            } else {
                for (int i = 0; i < pendingMacros.Count; i++)
                    ImGui.Selectable($"{pendingMacros[i]}##{childId}_pm{i}");
            }
            ImGui.EndListBox();
        }
        ImGui.EndChild();

        ImGui.SameLine(0, ImGuiHelpers.GlobalScale * 4f);

        // Right: current macro actions
        ImGui.BeginChild($"##Actions{childId}", new Vector2(0, subHeight));
        string rightHeader = currentMacroId != null
            ? $"{currentMacroId} [{Math.Max(currentIndex + 1, 0)}/{currentActions.Count}]"
            : "Idle";
        ImGui.TextDisabled(rightHeader);
        if (ImGui.BeginListBox($"##ActionsList{childId}", new Vector2(-1, -1))) {
            if (currentActions.Count == 0) {
                ImGui.TextDisabled("No actions");
            } else {
                for (int i = 0; i < currentActions.Count; i++) {
                    bool isActive = i == currentIndex;
                    using (ImRaii.PushColor(ImGuiCol.Text, Style.Colors.Green, isActive))
                        ImGui.Selectable($"[{i + 1:000}] {currentActions[i]}##{childId}_a{i}", isActive);
                    if (isActive)
                        ImGui.SetScrollHereY(0.5f);
                }
            }
            ImGui.EndListBox();
        }
        ImGui.EndChild();

        ImGui.EndChild();
    }
}
