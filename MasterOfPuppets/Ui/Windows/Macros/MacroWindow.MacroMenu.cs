using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;

using MasterOfPuppets.Resources;
using MasterOfPuppets.Util.ImGuiExt;

namespace MasterOfPuppets;

public partial class MacroWindow : Window {
    private void DrawMacroToolbar() {
        using var color = ImRaii.PushColor(ImGuiCol.Border, Style.Components.TooltipBorderColor);
        using var style = ImRaii.PushStyle(ImGuiStyleVar.PopupBorderSize, 1);

        if (ImGuiUtil.IconButton(FontAwesomeIcon.Plus, "##MacroToolbarAdd", Language.AddMacroBtn)) {
            Ui.MacroEditorWindow.AddNewMacro();
        }

        ImGui.SameLine();
        using (ImRaii.PushColor(ImGuiCol.Button, Style.Components.ButtonDangerNormal)
            .Push(ImGuiCol.ButtonHovered, Style.Components.ButtonDangerHovered)
            .Push(ImGuiCol.ButtonActive, Style.Components.ButtonDangerActive)) {
            if (ImGuiUtil.IconButton(FontAwesomeIcon.Trash, "##MacroToolbarDeleteSelected", Language.DeleteSelectedMacrosBtn)) {
                if (ImGui.GetIO().KeyCtrl) {
                    Plugin.MacroManager.DeleteSelectedMacros();
                    Plugin.IpcProvider.SyncConfiguration();
                }
            }
        }
        ImGuiUtil.ToolTip(Language.DeleteInstructionTooltip);

        ImGui.SameLine();
        if (ImGuiUtil.IconButton(FontAwesomeIcon.ExchangeAlt, "##MacroToolbarImportExport", "Import / Export Macros")) {
            Ui.MacroImportExportWindow.Toggle();
        }

        ImGui.SameLine();
        if (ImGuiUtil.IconButton(FontAwesomeIcon.FileImport, "##MacroToolbarImportClipboard", Language.ImportMacroBtn)) {
            ImportMacroFromClipboard();
        }

        ImGui.SameLine();
        if (ImGuiUtil.IconButton(FontAwesomeIcon.FilePen, "##MacroToolbarBatchEditor", Language.MacroBatchEditorTitle)) {
            Ui.MacroBatchEditorWindow.Toggle();
        }

        ImGui.SameLine();
        if (ImGuiUtil.IconButton(FontAwesomeIcon.FileArchive, "##MacroToolbarBackup", Language.MacroBackup)) {
            Plugin.MacroManager.BackupMacros();
        }

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();
    }
}
