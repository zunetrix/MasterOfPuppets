using System.Numerics;

using Dalamud.Interface;
using Dalamud.Interface.Windowing;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.ImGuiNotification;
using Dalamud.Interface.Utility;

using MasterOfPuppets.Resources;

namespace MasterOfPuppets;

public class MacroImportExportWindow : Window
{
    private Plugin Plugin { get; }

    public MacroImportExportWindow(Plugin plugin) : base($"{Language.MacroImportExportTitle}###MacroImportExportWindow")
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
        ImGui.TextUnformatted("Macro Import Export");
        ImGui.SameLine();
        ImGui.SameLine();

    }
}
