using System;
using System.Linq;
using System.Collections.Generic;

using Dalamud.Configuration;
using Dalamud.Plugin;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Reflection;
using MasterOfPuppets.Ipc;

namespace MasterOfPuppets;

[Serializable]
internal class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 1;
    private IDalamudPluginInterface Interface { get; set; } = null!;

    public bool SyncClients { get; set; } = true;
    // for individual Config file accounts
    public bool SaveConfigAfterSync { get; set; } = false;

    // Macros
    public List<Macro> Macros { get; set; } = new();
    public int DelayBetweenActions { get; set; } = 2;
    public List<Character> Characters { get; set; } = new();
    public List<CidGroup> CidsGroups { get; set; } = new();

    // Interface
    public bool OpenOnStartup { get; set; } = false;

    // public Vector4 ThemeColour { get; set; } = new(0f, 1f, 0f, 1f);
    public bool OpenOnLogin { get; set; }
    public bool AllowMovement { get; set; } = true;
    public bool AllowResize { get; set; } = true;
    public bool ShowSettingsButton { get; set; } = true;
    public bool AllowCloseWithEscape { get; set; }

    public void Initialize(IDalamudPluginInterface pluginInterface)
    {
        Interface = pluginInterface;
    }

    public void Save()
    {
        Interface.SavePluginConfig(this);
    }

    public void UpdateFromJson(string cofigurationJson)
    {
        if (string.IsNullOrWhiteSpace(cofigurationJson)) return;

        var newPluginConfig = cofigurationJson.JsonDeserialize<Configuration>();

        SyncClients = newPluginConfig.SyncClients;
        SaveConfigAfterSync = newPluginConfig.SaveConfigAfterSync;
        Macros = newPluginConfig.Macros;
        DelayBetweenActions = newPluginConfig.DelayBetweenActions;
        Characters = newPluginConfig.Characters;
        CidsGroups = newPluginConfig.CidsGroups;
        OpenOnStartup = newPluginConfig.OpenOnStartup;
        OpenOnLogin = newPluginConfig.OpenOnLogin;
    }


    public void MoveMacroToIndex(int itemIndex, int targetIndex)
    {
        Macros.MoveItemToIndex(itemIndex, targetIndex);
        this.Save();
    }

    public void RemoveMacroItem(int itemIndex)
    {
        var isEmptyList = Macros == null || Macros.Count == 0;
        var isValidIndex = itemIndex >= 0 && itemIndex < Macros.Count;

        if (isEmptyList || !isValidIndex)
            return;

        Macros.RemoveAt(itemIndex);

        this.Save();
    }

    public void CloneMacroItem(int itemIndex)
    {
        var isEmptyList = Macros == null || Macros.Count == 0;
        var isValidIndex = itemIndex >= 0 && itemIndex < Macros.Count;


        if (isEmptyList || !isValidIndex)
            return;

        var cloenedMacro = Macros[itemIndex].Clone();
        cloenedMacro.Name += " (copy)";
        Macros.Add(cloenedMacro);

        this.Save();
    }

    public void RemoveCharacter(ulong cid)
    {
        var isEmptyList = Characters == null || Characters.Count == 0;

        if (isEmptyList)
            return;

        var existingIndex = Characters.FindIndex(character => character.Cid == cid);
        if (existingIndex != -1)
        {
            Characters.RemoveAt(existingIndex);
        }

        this.Save();
    }

    public void MoveCharacterToIndex(int itemIndex, int targetIndex)
    {
        Characters.MoveItemToIndex(itemIndex, targetIndex);
        this.Save();
    }

    public void AddCharacter(Character character)
    {
        if (!Characters.Any(c => c.Cid == character.Cid))
        {
            Characters.Add(new Character { Cid = character.Cid, Name = character.Name });
        }

        this.Save();
    }
}

