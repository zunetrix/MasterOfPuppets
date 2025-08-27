using System.Numerics;

using Dalamud.Interface.Windowing;
using Dalamud.Bindings.ImGui;

using MasterOfPuppets.Resources;

namespace MasterOfPuppets;

public class GameCatalogWindow : Window
{
    private Plugin Plugin { get; }

    public GameCatalogWindow(Plugin plugin) : base($"{Language.GameCatalogTitle}###GameCatalogWindow")
    {
        Plugin = plugin;

        Size = new Vector2(500, 300);
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
        ImGui.TextUnformatted("Game catalog for emotes and usable items");
    }
}
