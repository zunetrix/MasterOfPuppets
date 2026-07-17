using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;

using MasterOfPuppets.Resources;
using MasterOfPuppets.Util.ImGuiExt;

namespace MasterOfPuppets;

public partial class GameSettingsWindow {

    private void DrawProfileConfigKeysTab() {
        ImGui.TextWrapped(
            "Select which game settings should be stored in profiles. Unchecked keys will be ignored.");
        ImGui.Spacing();

        // Search + filter controls
        ImGui.SetNextItemWidth(240 * ImGuiHelpers.GlobalScale);
        ImGui.InputTextWithHint("##GscPKSearch", Language.SearchInputLabel,
            ref _profileKeySearchFilter, 64);

        ImGui.SameLine();
        ImGui.Checkbox("Show Selected Only##GscPKShowSelected",
            ref _showOnlyEnabledProfileKeys);

        ImGui.SameLine();
        if (ImGuiUtil.IconButton(FontAwesomeIcon.List, "##GscPKDefault", "Select Default")) {
            Plugin.Config.GameSettingsProfileKeys.Clear();
            foreach (var key in GameSettingsManager.DefaultGameSettingsProfileKeys)
                Plugin.Config.GameSettingsProfileKeys.Add(key);
            Plugin.Config.Save();
            Plugin.IpcProvider.SyncConfiguration();
        }

        ImGui.SameLine();
        if (ImGuiUtil.IconButton(FontAwesomeIcon.Check, "##GscPKAll", "Select All")) {
            foreach (var k in GameSettingsManager.GetAllGameSettingsKeys())
                Plugin.Config.GameSettingsProfileKeys.Add(k);
            Plugin.Config.Save();
            Plugin.IpcProvider.SyncConfiguration();
        }

        ImGui.SameLine();
        if (ImGuiUtil.IconButton(FontAwesomeIcon.Times, "##GscPKNone", "Select None")) {
            Plugin.Config.GameSettingsProfileKeys.Clear();
            Plugin.Config.Save();
            Plugin.IpcProvider.SyncConfiguration();
        }

        ImGui.Spacing();

        float listH = ImGui.GetContentRegionAvail().Y;
        using (ImRaii.Child("##GscPKList", new System.Numerics.Vector2(-1, listH), true)) {
            var keys = GameSettingsManager.GetAllGameSettingsKeys();
            foreach (var key in keys) {
                if (!string.IsNullOrEmpty(_profileKeySearchFilter) &&
                    !key.Contains(_profileKeySearchFilter,
                        System.StringComparison.OrdinalIgnoreCase))
                    continue;

                bool enabled = Plugin.Config.GameSettingsProfileKeys.Contains(key);
                if (_showOnlyEnabledProfileKeys && !enabled) continue;

                if (ImGui.Checkbox(key, ref enabled)) {
                    if (enabled) Plugin.Config.GameSettingsProfileKeys.Add(key);
                    else Plugin.Config.GameSettingsProfileKeys.Remove(key);
                    Plugin.Config.Save();
                    Plugin.IpcProvider.SyncConfiguration();
                }
            }
        }
    }
}
