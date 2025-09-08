using System;
using System.Linq;
using System.Collections.Generic;

using Dalamud.Configuration;
using Dalamud.Plugin;
using Dalamud.Game.Text;

using MasterOfPuppets.Util;

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
    public double DelayBetweenActions { get; set; } = 1;
    public List<Character> Characters { get; set; } = new();
    public List<CidGroup> CidsGroups { get; set; } = new();

    // chat commands
    public bool UseChatSync { get; set; } = false;
    public HashSet<XivChatType> ListenedChatTypes { get; set; } = new();
    public bool UseChatCommandSenderWhitelist { get; set; } = false;
    public List<string> ChatCommandSenderWhitelist { get; set; } = new();


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

    public void ResetData()
    {
        Macros = new();
        Characters = new();
        CidsGroups = new();
        ListenedChatTypes = new();
    }

    public void UpdateFromJson(string cofigurationJson)
    {
        if (string.IsNullOrWhiteSpace(cofigurationJson)) return;

        var newPluginConfig = cofigurationJson.JsonDeserialize<Configuration>();

        Macros = newPluginConfig.Macros;
        Characters = newPluginConfig.Characters;
        CidsGroups = newPluginConfig.CidsGroups;

        SyncClients = newPluginConfig.SyncClients;

        UseChatSync = newPluginConfig.UseChatSync;
        ListenedChatTypes = newPluginConfig.ListenedChatTypes;

        UseChatCommandSenderWhitelist = newPluginConfig.UseChatCommandSenderWhitelist;
        ChatCommandSenderWhitelist = newPluginConfig.ChatCommandSenderWhitelist;

        DelayBetweenActions = newPluginConfig.DelayBetweenActions;
        SaveConfigAfterSync = newPluginConfig.SaveConfigAfterSync;
        OpenOnStartup = newPluginConfig.OpenOnStartup;
        OpenOnLogin = newPluginConfig.OpenOnLogin;
    }

    public void ImportMacro(Macro macro)
    {
        Macros.Add(macro);
    }

    public void MoveMacroToIndex(int itemIndex, int targetIndex)
    {
        Macros.MoveItemToIndex(itemIndex, targetIndex);
        this.Save();
    }

    public void RemoveMacro(int itemIndex)
    {
        var isEmptyList = Macros == null || Macros.Count == 0;
        var isValidIndex = itemIndex >= 0 && itemIndex < Macros.Count;

        if (isEmptyList || !isValidIndex)
            return;

        Macros.RemoveAt(itemIndex);

        this.Save();
    }

    public void CloneMacro(int itemIndex)
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

