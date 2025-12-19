using Dalamud.Bindings.ImGui;
using Dalamud.Interface.ImGuiNotification;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;

using MasterOfPuppets.Resources;
using MasterOfPuppets.Util.ImGuiExt;

namespace MasterOfPuppets;

public class MacroBatchEditorWindow : Window {
    private Plugin Plugin { get; }
    private string _findActionInput = string.Empty;
    private string _replaceActionInput = string.Empty;
    private bool _ignoreCaseAction = false;
    private int _affectedMacrosActions = 0;

    private string _findTagInput = string.Empty;
    private string _replaceTagInput = string.Empty;
    private bool _ignoreCaseTag = false;
    private int _affectedMacrosTags = 0;

    private string _removeTagInput = string.Empty;
    private bool _ignoreCaseTagRemove = false;
    private int _affectedMacrosTagRemove = 0;

    public MacroBatchEditorWindow(Plugin plugin) : base($"{Language.MacroBatchEditorTitle}###MacroBatchEditorWindow") {
        Plugin = plugin;

        Size = ImGuiHelpers.ScaledVector2(350, 320);
        SizeCondition = ImGuiCond.FirstUseEver;
        // SizeCondition = ImGuiCond.Always;
        // Flags = ImGuiWindowFlags.NoResize;
    }

    public override void Draw() {
        DrawMacroActionBatchEditor();

        ImGui.Spacing();
        ImGui.Spacing();

        DrawMacroTagBatchEditor();
    }

    private void DrawMacroActionBatchEditor() {
        ImGuiGroupPanel.BeginGroupPanel("Macro Batch Action Editor");
        ImGui.Text("Replace in the selected macros actions");
        ImGui.SameLine();
        ImGuiUtil.HelpMarker("""
        Pay attention when replacing commands to avoid false positives.
        For example, replacing /target with /moptarget: it's better to include the slash in both the find and replace.
        Using just the word target caused it to be replaced in places where it shouldn't have been.
        """);

        ImGui.InputTextWithHint("##FindActionInput", "Find", ref _findActionInput, 255, ImGuiInputTextFlags.AutoSelectAll);
        ImGui.SameLine();
        ImGui.Checkbox($"Ignore Case##IgnoreCaseActionCheckbox", ref _ignoreCaseAction);

        ImGui.Spacing();
        ImGui.InputTextWithHint("##ReplaceActionInput", "Replace", ref _replaceActionInput, 255, ImGuiInputTextFlags.AutoSelectAll);

        ImGui.Spacing();
        ImGui.Spacing();
        // ImGui.PushStyleColor(ImGuiCol.Button, Style.Components.ButtonBlueNormal);
        // ImGui.PushStyleColor(ImGuiCol.ButtonHovered, Style.Components.ButtonBlueHovered);
        // ImGui.PushStyleColor(ImGuiCol.ButtonActive, Style.Components.ButtonBlueActive);
        if (ImGui.Button($"Replace Actions In All Selected Macros ({Plugin.MacroManager.SelectedMacrosIndexes.Count})")) {
            if (Plugin.MacroManager.SelectedMacrosIndexes.Count == 0) {
                DalamudApi.ShowNotification($"Select at least one macro to replace", NotificationType.Warning, 5000);
                return;
            }

            _affectedMacrosActions = Plugin.MacroManager.ReplaceInSelectedMacrosActions(_findActionInput, _replaceActionInput, _ignoreCaseAction);
            Plugin.IpcProvider.SyncConfiguration();
        }
        // ImGui.PopStyleColor(3);

        ImGui.Spacing();
        if (_affectedMacrosActions > 0) {
            ImGui.Text($"Affected Macros: {_affectedMacrosActions}");
        }

        ImGui.Spacing();
        ImGui.Spacing();
        ImGuiGroupPanel.EndGroupPanel();
    }

    private void DrawMacroTagBatchEditor() {
        ImGuiGroupPanel.BeginGroupPanel("Macro Batch Tag Editor");
        ImGui.Text("Replace in the selected macros tags");

        ImGui.InputTextWithHint("##FindTagInput", "Find", ref _findTagInput, 255, ImGuiInputTextFlags.AutoSelectAll);
        ImGui.SameLine();
        ImGui.Checkbox($"Ignore Case##IgnoreCaseTagCheckbox", ref _ignoreCaseTag);

        ImGui.Spacing();
        ImGui.InputTextWithHint("##ReplaceTagInput", "Replace", ref _replaceTagInput, 255, ImGuiInputTextFlags.AutoSelectAll);

        ImGui.Spacing();
        ImGui.Spacing();
        if (ImGui.Button($"Replace Tags In All Selected Macros ({Plugin.MacroManager.SelectedMacrosIndexes.Count})")) {
            if (Plugin.MacroManager.SelectedMacrosIndexes.Count == 0) {
                DalamudApi.ShowNotification($"Select at least one macro to replace", NotificationType.Warning, 5000);
                return;
            }

            _affectedMacrosTags = Plugin.MacroManager.ReplaceInSelectedMacrosTags(_findTagInput, _replaceTagInput, _ignoreCaseTag);
            Plugin.IpcProvider.SyncConfiguration();
        }

        ImGui.Spacing();
        if (_affectedMacrosTags > 0) {
            ImGui.Text($"Affected Macros: {_affectedMacrosTags}");
        }
        ImGui.Spacing();
        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();
        ImGui.Spacing();

        ImGui.InputTextWithHint("##RemoveTagInput", "Replace", ref _removeTagInput, 255, ImGuiInputTextFlags.AutoSelectAll);
        ImGui.SameLine();
        ImGui.Checkbox($"Ignore Case##IgnoreCaseTagRemoveCheckbox", ref _ignoreCaseTagRemove);

        ImGui.Spacing();
        ImGui.Spacing();
        if (ImGui.Button($"Delete Tag In All Selected Macros ({Plugin.MacroManager.SelectedMacrosIndexes.Count})")) {
            if (Plugin.MacroManager.SelectedMacrosIndexes.Count == 0) {
                DalamudApi.ShowNotification($"Select at least one macro to remove tag", NotificationType.Warning, 5000);
                return;
            }

            _affectedMacrosTagRemove = Plugin.MacroManager.RemoveTagFromSelectedMacros(_removeTagInput, _ignoreCaseTagRemove);
            Plugin.IpcProvider.SyncConfiguration();
        }

        ImGui.Spacing();
        if (_affectedMacrosTagRemove > 0) {
            ImGui.Text($"Affected Macros: {_affectedMacrosTagRemove}");
        }
        ImGuiGroupPanel.EndGroupPanel();
    }
}


