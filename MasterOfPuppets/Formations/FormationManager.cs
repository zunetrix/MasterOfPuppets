using System;
using System.IO;
using System.Text;

using Dalamud.Interface.ImGuiNotification;

using MasterOfPuppets.Extensions;

namespace MasterOfPuppets.Formations;

public sealed class FormationManager {
    private Plugin Plugin { get; }

    public FormationManager(Plugin plugin) {
        Plugin = plugin;
    }

    public void BackupFormations() {
        try {
            string json = Plugin.Config.Formations.JsonSerialize();
            var dateNow = DateTime.Now.ToString("yyyy-MM-dd_HHmmss");
            var exportFileName = $"mop-formation-backup-{dateNow}.json";
            string filePath = Path.Combine(DalamudApi.PluginInterface.ConfigDirectory.FullName, exportFileName);

            File.WriteAllText(filePath, json, Encoding.UTF8);
            DalamudApi.ShowNotification($"Formation backup success: {exportFileName}", NotificationType.Success, 5000);
        } catch (Exception e) {
            DalamudApi.PluginLog.Warning(e, "Error while backing up formations");
            DalamudApi.ShowNotification("Error while backing up formations", NotificationType.Error, 5000);
        }
    }

    public BardToolboxFormationImportResult ImportBardToolboxConfigFromFile(
        string filePath,
        MacroImportMode importMode,
        bool includeCharacters,
        bool backupBeforeImport) {
        try {
            if (backupBeforeImport)
                BackupFormations();

            var json = File.ReadAllText(filePath, Encoding.UTF8);
            var import = BardToolboxFormationImporter.ParseConfigJson(json);
            var result = BardToolboxFormationImporter.ImportInto(
                Plugin.Config.Formations,
                Plugin.Config.Characters,
                import,
                importMode,
                includeCharacters);

            Plugin.Config.Save();
            Plugin.IpcProvider.SyncConfiguration();
            DalamudApi.ShowNotification(
                $"Imported {result.FormationsImported} formations, {result.PointsImported} points",
                NotificationType.Success,
                5000);
            return result;
        } catch (Exception e) {
            DalamudApi.PluginLog.Error(e, "Error while importing BardToolbox formations");
            DalamudApi.ShowNotification("Error while importing BardToolbox formations", NotificationType.Error, 5000);
            return new BardToolboxFormationImportResult(0, 0, 0, 0);
        }
    }
}
