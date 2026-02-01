using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

using Dalamud.Configuration;
using Dalamud.Game.Text;
using Dalamud.Plugin;

using MasterOfPuppets.Extensions;

namespace MasterOfPuppets;

internal class Configuration : IPluginConfiguration {
    public int Version { get; set; } = 1;
    private IDalamudPluginInterface PluginInterface { get; set; } = null;

    public bool SyncClients { get; set; } = true;
    // individual account Config file
    public bool SaveConfigAfterSync { get; set; } = false;

    // Macros
    public List<Macro> Macros { get; set; } = new();
    public List<Character> Characters { get; set; } = new();
    public List<CidGroup> CidsGroups { get; set; } = new();
    public double DelayBetweenActions { get; set; } = 1.00;
    public bool AutoSaveMacro { get; set; } = false;
    public string MacroExportPath { get; set; } = DalamudApi.PluginInterface.ConfigDirectory.FullName ?? string.Empty;
    public MacroImportMode MacroImportMode { get; set; } = MacroImportMode.AppendAll;
    public bool IncludeCidOnExport { get; set; } = false;
    public bool IncludeCidOnImport { get; set; } = true;
    public bool BackupBeforeImport { get; set; } = true;

    // chat commands
    public bool UseChatSync { get; set; } = false;
    public HashSet<XivChatType> ListenedChatTypes { get; set; } = new();
    public bool UseChatCommandSenderWhitelist { get; set; } = false;
    public List<string> ChatCommandSenderWhitelist { get; set; } = new();
    public string DefaultChatSyncPrefix { get; set; } = "/p";

    // Interface
    public bool OpenOnStartup { get; set; } = false;

    // public Vector4 ThemeColour { get; set; } = new(0f, 1f, 0f, 1f);
    public bool OpenOnLogin { get; set; } = false;
    public bool AllowMovement { get; set; } = true;
    public bool AllowResize { get; set; } = true;
    public bool ShowSettingsButton { get; set; } = true;
    public bool AllowCloseWithEscape { get; set; } = false;
    public bool ShowPanelActionsBroadcast { get; set; } = true;
    public bool ShowPanelMacroTags { get; set; } = true;
    public float ActionIconSize { get; set; } = 48;

    // Movement
    [Newtonsoft.Json.JsonIgnore]
    public bool AlignCameraToMovement { get; set; } = true;
    [Newtonsoft.Json.JsonIgnore]
    public float AlignCameraHeight { get; set; } = -15;
    [Newtonsoft.Json.JsonIgnore]
    public float StuckTolerance { get; set; } = 0.05f;
    [Newtonsoft.Json.JsonIgnore]
    public bool StopOnStuck { get; set; } = false;
    [Newtonsoft.Json.JsonIgnore]
    public bool RetryOnStuck { get; set; } = false;
    [Newtonsoft.Json.JsonIgnore]
    public int StuckTimeoutMs { get; set; } = 500;
    [Newtonsoft.Json.JsonIgnore]
    public bool CancelMoveOnUserInput { get; set; } = false;

    public void Initialize(IDalamudPluginInterface pluginInterface) {
        PluginInterface = pluginInterface;
    }

    public void Save() {
        PluginInterface.SavePluginConfig(this);
    }

    public void ResetData() {
        Macros = new();
        Characters = new();
        CidsGroups = new();
        ListenedChatTypes = new();
    }

    // public void UpdateFromJson(string cofigurationJson) {
    //     if (string.IsNullOrWhiteSpace(cofigurationJson)) return;

    //     var newPluginConfig = cofigurationJson.JsonDeserialize<Configuration>();
    //     if (newPluginConfig == null) return;

    //     // macro
    //     Macros = newPluginConfig.Macros;
    //     Characters = newPluginConfig.Characters;
    //     CidsGroups = newPluginConfig.CidsGroups;
    //     AutoSaveMacro = newPluginConfig.AutoSaveMacro;

    //     // import export
    //     MacroExportPath = newPluginConfig.MacroExportPath;
    //     MacroImportMode = newPluginConfig.MacroImportMode;
    //     IncludeCidOnExport = newPluginConfig.IncludeCidOnExport;
    //     IncludeCidOnImport = newPluginConfig.IncludeCidOnImport;
    //     BackupBeforeImport = newPluginConfig.BackupBeforeImport;

    //     SyncClients = newPluginConfig.SyncClients;

    //     UseChatSync = newPluginConfig.UseChatSync;
    //     ListenedChatTypes = newPluginConfig.ListenedChatTypes;
    //     UseChatCommandSenderWhitelist = newPluginConfig.UseChatCommandSenderWhitelist;
    //     ChatCommandSenderWhitelist = newPluginConfig.ChatCommandSenderWhitelist;
    //     DefaultChatSyncPrefix = newPluginConfig.DefaultChatSyncPrefix;

    //     DelayBetweenActions = newPluginConfig.DelayBetweenActions;
    //     SaveConfigAfterSync = newPluginConfig.SaveConfigAfterSync;

    //     // UI
    //     ShowPanelActionsBroadcast = newPluginConfig.ShowPanelActionsBroadcast;
    //     ShowPanelMacroTags = newPluginConfig.ShowPanelMacroTags;
    //     ActionIconSize = newPluginConfig.ActionIconSize;

    //     OpenOnStartup = newPluginConfig.OpenOnStartup;
    //     OpenOnLogin = newPluginConfig.OpenOnLogin;
    //     AllowCloseWithEscape = newPluginConfig.AllowCloseWithEscape;
    // }

    private void UpdateFrom(Configuration other) {
        var type = typeof(Configuration);

        foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance)) {
            if (!prop.CanRead || !prop.CanWrite)
                continue;

            if (Attribute.IsDefined(prop, typeof(NoSyncAttribute)))
                continue;

            if (Attribute.IsDefined(prop, typeof(Newtonsoft.Json.JsonIgnoreAttribute)))
                continue;

            var oldValue = prop.GetValue(this);
            var newValue = prop.GetValue(other);

#if DEBUG
            if (!AreEqual(oldValue, newValue)) {
                LogChange(prop.Name, oldValue, newValue);
            }
#endif
            prop.SetValue(this, newValue);
        }

        foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.Instance)) {
            if (Attribute.IsDefined(field, typeof(NoSyncAttribute)))
                continue;

            if (Attribute.IsDefined(field, typeof(Newtonsoft.Json.JsonIgnoreAttribute)))
                continue;

            var oldValue = field.GetValue(this);
            var newValue = field.GetValue(other);

#if DEBUG
            if (!AreEqual(oldValue, newValue)) {
                LogChange(field.Name, oldValue, newValue);
            }
#endif
            field.SetValue(this, newValue);
        }
    }

    public void UpdateFromJson(string configurationJson) {
        if (string.IsNullOrWhiteSpace(configurationJson))
            return;

        var incoming = configurationJson.JsonDeserialize<Configuration>();
        if (incoming == null)
            return;

        UpdateFrom(incoming);
    }

    static bool AreEqual(object? a, object? b) {
        if (ReferenceEquals(a, b))
            return true;

        if (a == null || b == null)
            return false;

        if (a is IList listA && b is IList listB)
            return listA.Count == listB.Count;

        return a.Equals(b);
    }

    static void LogChange(string name, object? oldVal, object? newVal) {
        static string Format(object? v) {
            if (v == null) return "null";
            if (v is IList list) return $"List(count={list.Count})";
            return v.ToString() ?? "?";
        }

        DalamudApi.PluginLog.Debug($"[ConfigSync] {name}: {Format(oldVal)} → {Format(newVal)}");
    }

    public void AddCharacter(Character character) {
        if (!Characters.Any(c => c.Cid == character.Cid)) {
            Characters.Add(new Character { Cid = character.Cid, Name = character.Name });
        }

        this.Save();
    }

    public void RemoveCharacter(ulong cid) {
        var isEmptyList = Characters == null || Characters.Count == 0;

        if (isEmptyList)
            return;

        var existingIndex = Characters.FindIndex(character => character.Cid == cid);
        if (existingIndex != -1) {
            Characters.RemoveAt(existingIndex);
        }

        this.Save();
    }

    public void MoveCharacterToIndex(int itemIndex, int targetIndex) {
        Characters.MoveItemToIndex(itemIndex, targetIndex);
        this.Save();
    }
}

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
public sealed class NoSyncAttribute : Attribute { }
