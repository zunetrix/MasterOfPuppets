using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

using Dalamud.Interface.ImGuiNotification;

using MasterOfPuppets.Extensions;
using MasterOfPuppets.Util;

namespace MasterOfPuppets;

// TODO: move macros from config to individual json file
public class MacroManager {
    private Plugin Plugin { get; }
    // public List<Macro> Macros { get; set; } = new();
    public HashSet<int> SelectedMacrosIndexes = new();

    public MacroManager(Plugin plugin) {
        Plugin = plugin;
    }

    public int GetMacrosCount() {
        return Plugin.Config.Macros.Count;
    }

    public bool ContainsMacroName(string macroName) {
        int macroIndex = Plugin.Config.Macros.FindIndex(macro => string.Equals(macro.Name, macroName, StringComparison.OrdinalIgnoreCase));
        return macroIndex != -1;
    }

    public void AddMacro(Macro macro) {
        if (this.ContainsMacroName(macro.Name)) {
            throw new ArgumentException($"A macro named \"{macro.Name}\" already exists");
        }

        macro.SanitizeActions();
        Plugin.Config.Macros.Add(macro);

        Plugin.Config.Save();
        Plugin.IpcProvider.SyncConfiguration();
    }

    public void UpdateMacro(int macroIdx, Macro macro) {
        bool hasMacroName = Plugin.Config.Macros
        .Where((m, idx) => idx != macroIdx) // ignore current item name
        .Any(m => string.Equals(m.Name, macro.Name, StringComparison.OrdinalIgnoreCase));

        if (hasMacroName) {
            throw new ArgumentException($"A macro named \"{macro.Name}\" already exists");
        }

        macro.SanitizeActions();
        Plugin.Config.Macros[macroIdx] = macro;

        Plugin.Config.Save();
        Plugin.IpcProvider.SyncConfiguration();
    }

    public Macro GetMacroByIndex(int macroIndex) {
        if (!Plugin.Config.Macros.IndexExists(macroIndex)) {
            throw new ArgumentException("Invalid macro index");
        }

        return Plugin.Config.Macros[macroIndex];
    }

    public void DeleteMacro(int itemIndex) {
        var isEmptyList = Plugin.Config.Macros == null || Plugin.Config.Macros.Count == 0;
        var isValidIndex = Plugin.Config.Macros.IndexExists(itemIndex);
        ;
        if (isEmptyList || !isValidIndex)
            return;

        Plugin.Config.Macros.RemoveAt(itemIndex);

        Plugin.Config.Save();
    }

    public void CloneMacro(int itemIndex) {
        var isEmptyList = Plugin.Config.Macros == null || Plugin.Config.Macros.Count == 0;
        var isValidIndex = Plugin.Config.Macros.IndexExists(itemIndex);

        if (isEmptyList || !isValidIndex)
            return;

        var clonedMacro = Plugin.Config.Macros[itemIndex].Clone();
        clonedMacro.Name += " (copy)";
        Plugin.Config.Macros.Add(clonedMacro);

        Plugin.Config.Save();
    }

    public void MoveMacroToIndex(int itemIndex, int targetIndex) {
        Plugin.Config.Macros.MoveItemToIndex(itemIndex, targetIndex);
        Plugin.Config.Save();
    }

    public int FindMacroIndex(string macroNameOrNumber) {
        int macroIndexByName = Plugin.Config.Macros.FindIndex(macro => string.Equals(macro.Name, macroNameOrNumber, StringComparison.OrdinalIgnoreCase));

        if (!int.TryParse(macroNameOrNumber, out var macroIndexArg) && macroIndexByName == -1) {
            DalamudApi.PluginLog.Error($"Invalid macro name or number {macroNameOrNumber}");
            DalamudApi.ShowNotification($"Invalid macro name or number", NotificationType.Error, 5000);
            throw new ArgumentException($"Invalid macro name or number {macroNameOrNumber}");
        }

        // user input 1 index based
        int macroIndex = macroIndexByName != -1 ? macroIndexByName : macroIndexArg - 1;
        var isValidMacroIndex = Plugin.Config.Macros.IndexExists(macroIndex);
        if (!isValidMacroIndex) {
            throw new ArgumentException($"Invalid macro index");
        }

        return macroIndex;
    }

    public string ExportMacroToString(int itemIndex, bool includeCids = false) {
        var isEmptyList = Plugin.Config.Macros == null || Plugin.Config.Macros.Count == 0;
        var isValidIndex = itemIndex >= 0 && itemIndex < Plugin.Config.Macros.Count;

        if (isEmptyList || !isValidIndex) {
            DalamudApi.PluginLog.Error("Invalid macro index");
            throw new ArgumentException("Invalid macro index");
        }

        var clonedMacro = includeCids ? Plugin.Config.Macros[itemIndex].Clone() : Plugin.Config.Macros[itemIndex].CloneWithoutCharacters();
        string macroJson = clonedMacro.JsonSerialize();
        return Compressor.CompressString(macroJson);
    }

    public void ImportMacroFromString(string compressedMacroString) {
        string macroString = Compressor.DecompressString(compressedMacroString);
        var newMacro = macroString.JsonDeserialize<Macro>();
        Plugin.Config.Macros.Add(newMacro);
    }

    public void DeleteSelectedMacros() {
        if (SelectedMacrosIndexes == null || SelectedMacrosIndexes.Count == 0)
            return;

        // remove in reverse order
        var indexesToRemove = SelectedMacrosIndexes.OrderByDescending(i => i).ToList();

        foreach (var index in indexesToRemove) {
            if (Plugin.Config.Macros.IndexExists(index)) {
                Plugin.Config.Macros.RemoveAt(index);
            }
        }

        SelectedMacrosIndexes.Clear();
        Plugin.Config.Save();
    }

    public void ExportSelectedMacrosToFile(string filePath, bool includeCids = false) {
        try {
            if (SelectedMacrosIndexes == null || SelectedMacrosIndexes.Count == 0) return;

            var selectedMacros = Plugin.Config.Macros
            .Select((macro, index) => new { macro, index })
            .Where(x => SelectedMacrosIndexes.Contains(x.index))
            .Select(x => includeCids ? x.macro : x.macro.CloneWithoutCharacters())
            .ToList();

            string macroJson = selectedMacros.JsonSerialize();

            File.WriteAllText(filePath, macroJson, Encoding.UTF8);

            DalamudApi.ShowNotification("Macros Exported!", NotificationType.Success, 5000);
        } catch (Exception e) {
            DalamudApi.PluginLog.Warning(e, "Error while exporting macros");
            DalamudApi.ShowNotification("Error while exporting macros", NotificationType.Error, 5000);
        }
    }

    public void ExportMacrosToFile(string filePath, bool includeCids = false) {
        try {
            var selectedMacros = Plugin.Config.Macros
            .Select((macro, index) => new { macro, index })
            .Select(x => includeCids ? x.macro : x.macro.CloneWithoutCharacters())
            .ToList();

            string macroJson = selectedMacros.JsonSerialize();

            File.WriteAllText(filePath, macroJson, Encoding.UTF8);

            DalamudApi.ShowNotification("Macros Exported!", NotificationType.Success, 5000);
        } catch (Exception e) {
            DalamudApi.PluginLog.Warning(e, "Error while exporting macros");
            DalamudApi.ShowNotification("Error while exporting macros", NotificationType.Error, 5000);
        }
    }

    public void BackupMacros() {
        try {
            string macroJson = Plugin.Config.Macros.JsonSerialize();
            var dateNow = DateTime.Now.ToString("yyyy-MM-dd_HHmmss");
            var exportFileName = $"mop-macro-backup-{dateNow}.json";
            string filePath = Path.Combine(DalamudApi.PluginInterface.ConfigDirectory.FullName, exportFileName);

            File.WriteAllText(filePath, macroJson, Encoding.UTF8);
        } catch (Exception e) {
            DalamudApi.PluginLog.Warning(e, "Error while backuping macros");
            DalamudApi.ShowNotification("Error while backuping macros", NotificationType.Error, 5000);
        }
    }

    public void ImportMacrosFromFile(string filePath, MacroImportMode importMode, bool includeCids, bool backupBeforeImport) {
        try {
            if (backupBeforeImport) {
                this.BackupMacros();
            }

            var macrosData = File.ReadAllText(filePath, Encoding.UTF8);
            var macrosImport = macrosData.JsonDeserialize<List<Macro>>() ?? new List<Macro>();

            if (!includeCids) {
                macrosImport = macrosImport
                   .Select(macro => macro.CloneWithoutCharacters())
                   .ToList();
            }

            var macroIndexMap = Plugin.Config.Macros
                          .Select((m, i) => new { m.Name, Index = i })
                          .ToDictionary(x => x.Name, x => x.Index, StringComparer.OrdinalIgnoreCase);

            switch (importMode) {
                case MacroImportMode.AppendAll:
                    Plugin.Config.Macros.AddRange(macrosImport);
                    break;

                case MacroImportMode.AppendNew:
                    foreach (var macroImport in macrosImport) {
                        if (macroIndexMap.TryGetValue(macroImport.Name, out var idx))
                            continue;

                        Plugin.Config.Macros.Add(macroImport);
                        macroIndexMap[macroImport.Name] = Plugin.Config.Macros.Count - 1;

                    }
                    break;

                case MacroImportMode.Merge: {
                        foreach (var macroImport in macrosImport) {
                            if (macroIndexMap.TryGetValue(macroImport.Name, out var idx)) {
                                Plugin.Config.Macros[idx] = macroImport;
                            } else {
                                Plugin.Config.Macros.Add(macroImport);
                                macroIndexMap[macroImport.Name] = Plugin.Config.Macros.Count - 1;
                            }
                        }
                    }
                    break;

                case MacroImportMode.ReplaceExisting: {
                        foreach (var macroImport in macrosImport) {
                            if (macroIndexMap.TryGetValue(macroImport.Name, out var idx)) {
                                Plugin.Config.Macros[idx] = macroImport;
                            }
                        }
                    }
                    break;

                case MacroImportMode.OverwriteAll:
                    Plugin.Config.Macros = macrosImport;
                    break;
            }

            Plugin.Config.Save();
            Plugin.IpcProvider.SyncConfiguration();
            DalamudApi.ShowNotification("Macros imported!", NotificationType.Success, 5000);
        } catch (Exception e) {
            DalamudApi.PluginLog.Error(e, "Error while importing macros");
            DalamudApi.ShowNotification("Error while importing macros", NotificationType.Error, 5000);
        }
    }
}

public enum MacroImportMode {
    AppendAll,
    AppendNew,
    Merge,
    ReplaceExisting,
    OverwriteAll
}
