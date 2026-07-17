using Dalamud.Bindings.ImGui;

using MasterOfPuppets.Util.ImGuiExt;

namespace MasterOfPuppets;

public partial class GameSettingsWindow {

    private void DrawBroadcastTab() {
        ImGui.Text("Game Settings Broadcast");
        ImGuiUtil.HelpMarker("Broadcast changes for all clients");
        ImGui.Separator();
        ImGui.Spacing();

        ImGui.Text("Object Quantity Limit");
        if (ImGuiUtil.EnumCombo("##GscBcastObjectQuantity", ref _objectQuantityType)) {
            Plugin.IpcProvider.SetGameSettingsObjectQuantity(_objectQuantityType);
        }

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        ImGui.Text("Keep game pad enabled when client is inactive");
        ImGui.Spacing();
        if (ImGuiUtil.ButtonStyled("Enable##GscAlwaysInputEnable",
                ImGuiUtil.ButtonStyle.Success)) {
            Plugin.IpcProvider.SetGameSettingsAlwaysInput(1);
        }
        ImGui.SameLine();
        if (ImGuiUtil.ButtonStyled("Disable##GscAlwaysInputDisable",
                ImGuiUtil.ButtonStyle.Danger)) {
            Plugin.IpcProvider.SetGameSettingsAlwaysInput(0);
        }
    }
}
