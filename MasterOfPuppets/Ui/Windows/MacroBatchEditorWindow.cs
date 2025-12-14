using Dalamud.Bindings.ImGui;
using Dalamud.Interface.ImGuiNotification;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Windowing;

using MasterOfPuppets.Resources;
using MasterOfPuppets.Util.ImGuiExt;

namespace MasterOfPuppets;

public class MacroBatchEditorWindow : Window {
    private Plugin Plugin { get; }
    private string _findInput = string.Empty;
    private string _replaceInput = string.Empty;
    private bool _ignoreCase = false;
    private int _affectedMacros = 0;

    public MacroBatchEditorWindow(Plugin plugin) : base($"{Language.MacroBatchEditorTitle}###MacroBatchEditorWindow") {
        Plugin = plugin;

        Size = ImGuiHelpers.ScaledVector2(350, 320);
        SizeCondition = ImGuiCond.FirstUseEver;
        // SizeCondition = ImGuiCond.Always;
        // Flags = ImGuiWindowFlags.NoResize;
    }

    public override void Draw() {
        ImGuiGroupPanel.BeginGroupPanel("Macro Batch Replace");
        ImGui.TextUnformatted("Replace in the selected macros actions");
        ImGui.SameLine();
        ImGuiUtil.HelpMarker("""
        Pay attention when replacing commands to avoid false positives.
        For example, replacing /target with /moptarget: it's better to include the slash in both the find and replace.
        Using just the word target caused it to be replaced in places where it shouldn't have been.
        """);

        ImGui.InputTextWithHint("##FindInput", "Find", ref _findInput, 255, ImGuiInputTextFlags.AutoSelectAll);
        ImGui.SameLine();
        ImGui.Checkbox($"Ignore Case##IgnoreCaseCheckbox", ref _ignoreCase);

        ImGui.Spacing();
        ImGui.InputTextWithHint("##ReplaceInput", "Replace", ref _replaceInput, 255, ImGuiInputTextFlags.AutoSelectAll);

        ImGui.Spacing();
        ImGui.Spacing();
        ImGui.PushStyleColor(ImGuiCol.Button, Style.Components.ButtonBlueNormal);
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, Style.Components.ButtonBlueHovered);
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, Style.Components.ButtonBlueActive);
        if (ImGui.Button($"Replace In All Selected Macros ({Plugin.MacroManager.SelectedMacrosIndexes.Count})")) {
            if (Plugin.MacroManager.SelectedMacrosIndexes.Count == 0) {
                DalamudApi.ShowNotification($"Select at least one macro to replace", NotificationType.Warning, 5000);
                return;
            }

            _affectedMacros = Plugin.MacroManager.ReplaceInSelectedMacros(_findInput, _replaceInput, _ignoreCase);
            Plugin.IpcProvider.SyncConfiguration();
        }
        ImGui.PopStyleColor(3);

        ImGui.Spacing();
        if (_affectedMacros > 0) {
            ImGui.TextUnformatted($"Affected Macros: {_affectedMacros}");
        }
        ImGui.Spacing();
        ImGui.Spacing();
        ImGuiGroupPanel.EndGroupPanel();
    }
}


