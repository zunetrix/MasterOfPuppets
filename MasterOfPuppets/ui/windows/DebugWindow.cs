using System;
using System.Linq;
using System.Numerics;

using Dalamud.Interface.ImGuiFileDialog;
using Dalamud.Interface.Windowing;
using Dalamud.Interface.ImGuiNotification;
using Dalamud.Bindings.ImGui;

using FFXIVClientStructs.FFXIV.Client.Game;
using MasterOfPuppets.Resources;

namespace MasterOfPuppets;

public class DebugWindow : Window
{
    private Plugin Plugin { get; }
    private FileDialogManager FileDialogManager { get; }

    public DebugWindow(Plugin plugin) : base($"{Plugin.Name} Debug###DebugWindow")
    {
        Plugin = plugin;

        Size = new Vector2(500, 300);
        SizeCondition = ImGuiCond.FirstUseEver;
        // Flags = ImGuiWindowFlags.NoResize;

        FileDialogManager = new FileDialogManager();
    }

    public override void PreDraw()
    {
        FileDialogManager.Draw();
        base.PreDraw();
    }

    public override void Draw()
    {
        if (!ImGui.BeginTabBar("##DebugTabs")) return;

        if (ImGui.BeginTabItem($"{Language.SettingsGeneralTab}###GeneralDebugTab"))
        {
            ImGui.TextUnformatted("Actions Test");
            if (ImGui.Button("Execute Umbrella Dance"))
            {
                Plugin.IpcProvider.BroadcastActionCommand(GameActionManager.CustomActions["UmbrellaDance"].ActionId);
                // GameActionManager.UseAction(30868);
                DalamudApi.ShowNotification($"Execute Macro", NotificationType.Info, 5000);
            }
        }

        ImGui.EndTabItem();

        ImGui.EndTabBar();
    }
}
