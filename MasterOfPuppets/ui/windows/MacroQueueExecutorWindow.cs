using System.Numerics;

using Dalamud.Interface;
using Dalamud.Interface.Windowing;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.ImGuiNotification;
using Dalamud.Interface.Utility;

using MasterOfPuppets.Resources;
using MasterOfPuppets.Ipc;

namespace MasterOfPuppets;

public class MacroQueueExecutorWindow : Window
{
    private Plugin Plugin { get; }

    public MacroQueueExecutorWindow(Plugin plugin) : base($"{Language.MacroExecutionQueueTitle}###MacroQueueExecutorWindow")
    {
        Plugin = plugin;

        Size = ImGuiHelpers.ScaledVector2(310, 250);
        SizeCondition = ImGuiCond.FirstUseEver;
        // SizeCondition = ImGuiCond.Always;
        // Flags = ImGuiWindowFlags.NoResize;
    }

    public override void PreDraw()
    {
        base.PreDraw();
    }

    public override void Draw()
    {
        ImGui.TextUnformatted("Queu Actions");
        ImGui.SameLine();
        ImGui.SameLine();
        if (ImGuiUtil.IconButton(FontAwesomeIcon.Stop, $"##StopMacroExecutionQueueBtn", Language.StopMacroExecutionBtn))
        {
            Plugin.IpcProvider.StopMacroExecution();
            DalamudApi.ShowNotification($"Macro execution queue stoped", NotificationType.Info, 3000);
        }

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        if (ImGui.BeginListBox($"##CurrentActionsExecutionList", new Vector2(300, 200)))
        {
            for (int i = 0; i < MacroQueueExecutor.CurrentActionsExecutionList.Count; i++)
            {
                var isCurrentItemActive = i == MacroQueueExecutor.CurrentActionExecutionIndex;
                if (isCurrentItemActive)
                {
                    ImGui.PushStyleColor(ImGuiCol.Text, Style.Colors.Green);
                }
                ImGui.Selectable($"[{i:000}] {MacroQueueExecutor.CurrentActionsExecutionList[i]}##CurrentActionsExecutionList{i}", isCurrentItemActive, ImGuiSelectableFlags.None);

                if (isCurrentItemActive)
                {
                    ImGui.PopStyleColor();
                }
            }

            ImGui.EndListBox();
        }
    }
}
