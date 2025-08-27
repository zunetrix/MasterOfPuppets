using System;
using System.Numerics;
using System.Collections.Generic;

using Dalamud.Configuration;
using Dalamud.Plugin;

namespace MasterOfPuppets;

[Serializable]
internal class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 1;

    private IDalamudPluginInterface Interface { get; set; } = null!;

    public bool SyncClients { get; set; } = false;

    public List<Macro> Macros = new();

    public int DelayBetweenActions { get; set; } = 2;

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

    public void MoveMacroToIndex(int itemIndex, int targetIndex)
    {
        var isEmptyList = Macros == null || Macros.Count == 0;
        var isValidIndex = itemIndex >= 0 && itemIndex < Macros.Count;

        if (isEmptyList || !isValidIndex)
            return;

        // clamp index
        targetIndex = Math.Clamp(targetIndex, 0, Macros.Count);

        var item = Macros[itemIndex];
        Macros.RemoveAt(itemIndex);
        Macros.Insert(targetIndex, item);
    }

    public void RemoveMacroItem(int itemIndex)
    {
        var isEmptyList = Macros == null || Macros.Count == 0;
        var isValidIndex = itemIndex >= 0 && itemIndex < Macros.Count;

        if (isEmptyList || !isValidIndex)
            return;

        Macros.RemoveAt(itemIndex);
    }

    public void DuplicateMacroItem(int itemIndex)
    {
        var isEmptyList = Macros == null || Macros.Count == 0;
        var isValidIndex = itemIndex >= 0 && itemIndex < Macros.Count;


        if (isEmptyList || !isValidIndex)
            return;

        var duplicatedMacro = Macros[itemIndex].Clone();
        duplicatedMacro.Name += " (copy)";
        Macros.Add(duplicatedMacro);
    }
}

