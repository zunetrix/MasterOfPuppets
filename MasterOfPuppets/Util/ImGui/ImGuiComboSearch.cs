using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;

namespace MasterOfPuppets.Util.ImGuiExt;

/// <summary>
/// Self-contained combo widget with an integrated search filter.
/// Each instance manages its own filter state independently.
/// </summary>
public class ImGuiComboSearch {
    private string _filter = string.Empty;

    public bool Draw(string label, IList<string> options, ref string selected, int maxVisible = 8) {
        bool changed = false;
        ImGui.PushID(label);
        if (ImGui.BeginCombo(label, selected, ImGuiComboFlags.HeightLargest)) {
            ImGui.SetNextItemWidth(-1);
            ImGui.InputTextWithHint("##search", "Search...", ref _filter, 64);

            var filter = _filter;
            var filtered = string.IsNullOrEmpty(filter)
                ? options
                : options.Where(x => x.Contains(filter, StringComparison.OrdinalIgnoreCase)).ToList();

            var itemHeight = ImGui.GetTextLineHeightWithSpacing();
            var visibleRows = Math.Max(3, Math.Min(filtered.Count, maxVisible));
            bool shouldClose = false;
            {
                using var child = ImRaii.Child("##cs_list", new Vector2(-1, visibleRows * itemHeight), false);
                if (child) {
                    foreach (var option in filtered) {
                        if (ImGui.Selectable(option, option == selected)) {
                            selected = option;
                            changed = true;
                            shouldClose = true;
                        }
                    }
                }
            }
            if (shouldClose) ImGui.CloseCurrentPopup();
            ImGui.EndCombo();
        } else {
            _filter = string.Empty;
        }
        ImGui.PopID();
        return changed;
    }
}
