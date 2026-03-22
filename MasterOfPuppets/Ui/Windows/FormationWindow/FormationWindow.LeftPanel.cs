using System;
using System.Numerics;

using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

using MasterOfPuppets.Util.ImGuiExt;
using MasterOfPuppets.Resources;
using Dalamud.Interface.Utility.Raii;

namespace MasterOfPuppets;

public partial class FormationWindow {

    // =========================================================================
    // Left panel - formation list
    // =========================================================================

    private void DrawLeftPanel() {
        using (ImRaii.Group()) {
            if (ImGuiUtil.PrimaryIconButton(FontAwesomeIcon.Plus, "##fiadd_s", "New formation"))
                ImGui.OpenPopup("##finew");
            if (ImGui.BeginPopup("##finew")) {
                ImGui.Text("Name:");
                ImGui.SameLine();
                ImGui.SetNextItemWidth(150);
                bool enter = ImGui.InputText("##finewname", ref _newFmName, 64, ImGuiInputTextFlags.EnterReturnsTrue);
                if ((enter || ImGui.Button("Create")) && !string.IsNullOrWhiteSpace(_newFmName)) {
                    AddFormation();
                    ImGui.CloseCurrentPopup();
                }
                ImGui.EndPopup();
            }
            ImGui.SameLine();
            ImGui.SetNextItemWidth(-1);
            ImGui.InputTextWithHint("##filsrch", Language.SearchInputLabel, ref _searchFilter, 64);
            ImGui.Separator();
        }

        var formations = Plugin.Config.Formations;
        ImGui.BeginChild("##fillist", new Vector2(-1, -1), false);
        for (int i = 0; i < formations.Count; i++) {
            var f = formations[i];
            if (!string.IsNullOrEmpty(_searchFilter) &&
                !f.Name.Contains(_searchFilter, StringComparison.OrdinalIgnoreCase))
                continue;

            if (ImGui.Selectable(f.Name.Length > 0 ? f.Name : "(unnamed)", i == _selFormation) &&
                _selFormation != i) {
                _selFormation = i;
                _selPoint = -1;
                _needsAxisReset = true;
            }
        }
        ImGui.EndChild();
    }
}
