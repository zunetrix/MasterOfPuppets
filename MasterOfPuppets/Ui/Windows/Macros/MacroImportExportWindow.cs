using System;
using System.IO;

using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.ImGuiFileDialog;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Windowing;
using Dalamud.Utility;

using MasterOfPuppets.Extensions;
using MasterOfPuppets.Resources;
using MasterOfPuppets.Util;
using MasterOfPuppets.Util.ImGuiExt;

namespace MasterOfPuppets;

public class MacroImportExportWindow : Window {
    private Plugin Plugin { get; }
    private FileDialogManager FileDialogManager { get; }

    public MacroImportExportWindow(Plugin plugin) : base($"{Language.MacroImportExportTitle}###MacroImportExportWindow") {
        Plugin = plugin;
        FileDialogManager = new FileDialogManager();

        Size = ImGuiHelpers.ScaledVector2(350, 320);
        SizeCondition = ImGuiCond.FirstUseEver;
        // SizeCondition = ImGuiCond.Always;
        // Flags = ImGuiWindowFlags.NoResize;
    }

    public override void PreDraw() {
        FileDialogManager.Draw();
        base.PreDraw();
    }

    public override void Draw() {
        ImGuiGroupPanel.BeginGroupPanel("Macro Export");
        ImGui.Text("Export Directory:");
        ImGui.Text(Plugin.Config.MacroExportPath.EllipsisPath(50));

        ImGui.SameLine();
        ImGuiHelpers.ScaledDummy(5);

        ImGui.SameLine();
        if (ImGuiUtil.IconButton(FontAwesomeIcon.Folder, "##SetExportFolderBtn", "Set Export Folder")) {
            FileDialogManager.OpenFolderDialog(
                title: "Select Folder",
                startPath: Plugin.Config.MacroExportPath,
                callback: (result, selectedPath) => {
                    if (!result) return;
                    if (!Path.Exists(Plugin.Config.MacroExportPath)) return;
                    Plugin.Config.MacroExportPath = selectedPath;
                    Plugin.IpcProvider.SyncConfiguration();
                });
        }

        ImGui.SameLine();
        if (ImGuiUtil.IconButton(FontAwesomeIcon.Undo, "##ReseExportFolderBtn", "Reset")) {
            Plugin.Config.MacroExportPath = DalamudApi.PluginInterface.ConfigDirectory.FullName;
            Plugin.IpcProvider.SyncConfiguration();
        }

        ImGui.SameLine();
        if (ImGuiUtil.IconButton(FontAwesomeIcon.FolderOpen, "##OpenExportFolderBtn", "Open Export Folder")) {
            WindowsApi.OpenFolder(Plugin.Config.MacroExportPath);
        }

        var includeCidOnExport = Plugin.Config.IncludeCidOnExport;
        if (ImGui.Checkbox("Include CIDs##IncludeCidOnExport", ref includeCidOnExport)) {
            Plugin.Config.IncludeCidOnExport = includeCidOnExport;
            Plugin.Config.Save();
            Plugin.IpcProvider.SyncConfiguration();
        }
        ImGuiUtil.HelpMarker("""
        Include assigned characters cids data in the exported file
        (disable if you want to share the macro with someone without your character data)
        """);

        ImGui.Spacing();
        ImGui.Spacing();
        ImGui.Spacing();
        ImGui.Spacing();

        ImGui.PushStyleColor(ImGuiCol.Button, Style.Components.ButtonBlueNormal);
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, Style.Components.ButtonBlueHovered);
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, Style.Components.ButtonBlueActive);
        if (ImGui.Button("Export All Macros")) {
            var exportFolder = Plugin.Config.MacroExportPath.IsNullOrEmpty() ?
                        DalamudApi.PluginInterface.ConfigDirectory.FullName : Plugin.Config.MacroExportPath;

            var dateNow = DateTime.Now.ToString("yyyy-MM-dd_HHmmss");
            var exportFileName = $"mop-macro-export-{dateNow}.json";

            FileDialogManager.SaveFileDialog("Export", ".json", exportFileName, ".json", (result, selectedPath) => {
                if (!result) return;

                Plugin.MacroManager.ExportMacrosToFile(selectedPath, Plugin.Config.IncludeCidOnExport);

                Plugin.Config.MacroExportPath = Path.GetDirectoryName(selectedPath);
                Plugin.Config.Save();
                Plugin.IpcProvider.SyncConfiguration();
            }, exportFolder);
            // ImGui.SetNextWindowFocus();
        }
        ImGui.PopStyleColor(3);

        ImGui.SameLine();
        ImGuiHelpers.ScaledDummy(10);

        ImGui.SameLine();
        ImGui.PushStyleColor(ImGuiCol.Button, Style.Components.ButtonBlueNormal);
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, Style.Components.ButtonBlueHovered);
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, Style.Components.ButtonBlueActive);
        if (ImGui.Button("Export Selected Macros")) {
            if (Plugin.MacroManager.SelectedMacrosIndexes.Count == 0) {
                Chat.PrintError("No macros selected to export");
                return;
            }

            var exportFolder = Plugin.Config.MacroExportPath.IsNullOrEmpty() ?
                        DalamudApi.PluginInterface.ConfigDirectory.FullName : Plugin.Config.MacroExportPath;

            var dateNow = DateTime.Now.ToString("yyyy-MM-dd_HHmmss");
            var exportFileName = $"mop-macro-export-{dateNow}.json";

            FileDialogManager.SaveFileDialog("Export", ".json", exportFileName, ".json", (result, selectedPath) => {
                if (!result) return;

                Plugin.MacroManager.ExportSelectedMacrosToFile(selectedPath, Plugin.Config.IncludeCidOnExport);

                Plugin.Config.MacroExportPath = Path.GetDirectoryName(selectedPath);
                Plugin.Config.Save();
                Plugin.IpcProvider.SyncConfiguration();
            }, exportFolder);
        }
        ImGui.PopStyleColor(3);
        ImGuiGroupPanel.EndGroupPanel();

        ImGui.Spacing();
        ImGui.Spacing();
        ImGui.Spacing();
        ImGui.Spacing();

        ImGuiGroupPanel.BeginGroupPanel("Macro Import");

        ImGui.Text("Import Mode:");
        var macroImportMode = Plugin.Config.MacroImportMode;
        if (ImGuiUtil.EnumCombo("##MacroImportMode", ref macroImportMode)) {
            Plugin.Config.MacroImportMode = macroImportMode;
            Plugin.Config.Save();
            Plugin.IpcProvider.SyncConfiguration();
        }
        ImGuiUtil.HelpMarker("""
        AppendAll:
            Imports all macros from the source.
            Macros with the same name will be duplicated with appended (copy) in the name.

        AppendNew:
            Imports only macros that don't already exist in your list.
            Macros with the same name are ignored.

        Merge:
            Adds new macros (those with names not in your list) and replaces existing ones with the same name.

        ReplaceExisting:
            Replaces macros in your list that have the same name, but ignores any new ones from the import source.

        OverwriteAll:
            Deletes all your current macros and imports everything from the import source.
        """);

        ImGui.Spacing();
        ImGui.Spacing();

        var includeCidOnImport = Plugin.Config.IncludeCidOnImport;
        if (ImGui.Checkbox("Use CIDs##IncludeCidOnImport", ref includeCidOnImport)) {
            Plugin.Config.IncludeCidOnImport = includeCidOnImport;
            Plugin.Config.Save();
            Plugin.IpcProvider.SyncConfiguration();
        }
        ImGuiUtil.HelpMarker("""
        Import macros with assigned characters cids data
        (if disabled only commands and actions will be imported)
        """);

        ImGui.Spacing();
        ImGui.Spacing();

        var backupBeforeImport = Plugin.Config.BackupBeforeImport;
        if (ImGui.Checkbox("Backup before import", ref backupBeforeImport)) {
            Plugin.Config.BackupBeforeImport = backupBeforeImport;
            Plugin.Config.Save();
            Plugin.IpcProvider.SyncConfiguration();
        }
        ImGuiUtil.HelpMarker("""
        Auto create a backup file with your current macros before import process
        """);

        ImGui.Spacing();
        ImGui.Spacing();
        ImGui.Spacing();
        ImGui.Spacing();

        ImGui.PushStyleColor(ImGuiCol.Button, Style.Components.ButtonBlueNormal);
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, Style.Components.ButtonBlueHovered);
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, Style.Components.ButtonBlueActive);
        if (ImGui.Button("Import File")) {
            FileDialogManager.OpenFileDialog(
                title: "Import",
                filters: ".json",
                startPath: Plugin.Config.MacroExportPath,
                selectionCountMax: 1,
                callback: (result, selectedPaths) => {
                    if (!result || selectedPaths.Count == 0) return;
                    if (!File.Exists(selectedPaths[0])) return;

                    Plugin.MacroManager.ImportMacrosFromFile(selectedPaths[0], Plugin.Config.MacroImportMode, Plugin.Config.IncludeCidOnImport, backupBeforeImport);
                }
            );
        }
        ImGui.PopStyleColor(3);
        ImGuiGroupPanel.EndGroupPanel();
    }
}


