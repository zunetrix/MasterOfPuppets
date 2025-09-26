using System.IO;

using Dalamud.Interface;
using Dalamud.Interface.Windowing;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.ImGuiFileDialog;

using MasterOfPuppets.Resources;
using Dalamud.Utility;
using System;

namespace MasterOfPuppets;

public class MacroImportExportWindow : Window
{
    private Plugin Plugin { get; }
    private FileDialogManager FileDialogManager { get; }
    private bool _includeCids = false;
    private MacroImportMode _importMode = MacroImportMode.Add;

    public MacroImportExportWindow(Plugin plugin) : base($"{Language.MacroImportExportTitle}###MacroImportExportWindow")
    {
        Plugin = plugin;
        FileDialogManager = new FileDialogManager();

        Size = ImGuiHelpers.ScaledVector2(310, 250);
        SizeCondition = ImGuiCond.FirstUseEver;
        // SizeCondition = ImGuiCond.Always;
        // Flags = ImGuiWindowFlags.NoResize;
    }

    public override void PreDraw()
    {
        FileDialogManager.Draw();
        base.PreDraw();
    }

    public override void Draw()
    {
        ImGui.TextUnformatted("Macro Export");
        ImGui.Separator();

        var macroExportPath = Plugin.Config.MacroExportPath;
        ImGui.InputText("Exporting Directory", ref macroExportPath);
        ImGui.SameLine();

        if (ImGuiUtil.IconButton(FontAwesomeIcon.FileExport))
        {
            var exportFolder = Plugin.Config.MacroExportPath.IsNullOrEmpty() ?
                        DalamudApi.PluginInterface.ConfigDirectory.FullName : Plugin.Config.MacroExportPath;

            var dateNow = DateTime.Now.ToString("yyyy-MM-dd_HHmm");
            var exportFileName = $"mop-macro-export-{dateNow}.json";

            FileDialogManager.SaveFileDialog("Export", ".json", exportFileName, ".json", (result, selectedPath) =>
            {
                if (!result) return;

                Plugin.MacroManager.ExportMacrosToFile(Path.Combine(exportFolder, exportFileName), _includeCids);
                Plugin.Config.MacroExportPath = exportFolder;
            }, exportFolder);
        }

        ImGui.SameLine();
        if (ImGui.Button("Open Export Folder"))
        {
            WindowsApi.OpenFolder(Plugin.Config.MacroExportPath);
        }

        ImGui.Checkbox("Include CIDs", ref _includeCids);

        ImGui.Spacing();
        ImGui.Spacing();
        ImGui.Spacing();

        ImGui.TextUnformatted("Macro Import");
        ImGui.Separator();

        if (ImGuiUtil.EnumCombo("##MacroImportMode", ref _importMode))
        {
            //
        }

        if (ImGui.Button("Import File"))
        {
            FileDialogManager.OpenFileDialog("Import File", ".json", (result, selectedPaths) =>
            {
                if (!result) return;

                Plugin.MacroManager.ImportMacrosFromFile(selectedPaths, _importMode);
            });
        }
    }
}


