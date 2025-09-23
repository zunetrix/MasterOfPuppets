using System.Numerics;

using Dalamud.Interface;
using Dalamud.Interface.Windowing;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.ImGuiNotification;
using Dalamud.Interface.Utility;

using MasterOfPuppets.Resources;

namespace MasterOfPuppets;

public class MacroQueueWindow : Window
{
    private Plugin Plugin { get; }

    public MacroQueueWindow(Plugin plugin) : base($"{Language.MacroQueueTitle}###MacroQueueWindow")
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
        ImGui.TextUnformatted("Action Queue");
        ImGui.SameLine();
        ImGui.SameLine();
        ImGui.PushStyleColor(ImGuiCol.Button, Style.Components.ButtonDangerNormal);
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, Style.Components.ButtonDangerHovered);
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, Style.Components.ButtonDangerActive);
        if (ImGuiUtil.IconButton(FontAwesomeIcon.Stop, $"##StopMacroExecutionQueueBtn", Language.StopMacroExecutionBtn))
        {
            Plugin.IpcProvider.StopMacroExecution();
            DalamudApi.ShowNotification($"Macro execution queue stoped", NotificationType.Info, 3000);
        }
        ImGui.PopStyleColor(3);

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        if (ImGui.BeginListBox($"##CurrentActionsExecutionList", new Vector2(300, 200)))
        {
            for (int i = 0; i < Plugin.MacroHandler.CurrentActionsExecutionList.Count; i++)
            {
                var isCurrentItemActive = i == Plugin.MacroHandler.CurrentActionExecutionIndex;
                if (isCurrentItemActive)
                {
                    ImGui.PushStyleColor(ImGuiCol.Text, Style.Colors.Green);
                }
                ImGui.Selectable($"[{i + 1:000}] {Plugin.MacroHandler.CurrentActionsExecutionList[i]}##CurrentActionsExecutionList{i}", isCurrentItemActive, ImGuiSelectableFlags.None);
                // var windowWidth = ImGui.GetWindowContentRegionMax().X - ImGui.GetWindowContentRegionMin().X;
                // ImGui.SameLine(windowWidth - ImGui.CalcTextSize(time).X);

                if (isCurrentItemActive)
                {
                    ImGui.PopStyleColor();
                }
            }

            ImGui.EndListBox();
        }
    }
}
