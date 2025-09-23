using System;

using Dalamud.Interface.ImGuiNotification;

using MasterOfPuppets.Util;

namespace MasterOfPuppets;

// TODO: move macros from config to individual json file
public class MacroManager
{
    private Plugin Plugin { get; }
    // public List<Macro> Macros { get; set; } = new();

    public MacroManager(Plugin plugin)
    {
        Plugin = plugin;
    }

    public int GetTotalMacros()
    {
        return Plugin.Config.Macros.Count;
    }

    public void AddMacro(Macro macro)
    {
        macro.SanitizeActions();
        Plugin.Config.Macros.Add(macro);
    }

    public void UpdateMacro(int macroIdx, Macro macro)
    {
        macro.SanitizeActions();
        Plugin.Config.Macros[macroIdx] = macro;
    }

    public Macro GetMacroByIndex(int macroIndex)
    {
        if (!Plugin.Config.Macros.IndexExists(macroIndex))
        {
            throw new ArgumentException("Invalid macro index");
        }

        return Plugin.Config.Macros[macroIndex];
    }

    public void RemoveMacro(int itemIndex)
    {
        var isEmptyList = Plugin.Config.Macros == null || Plugin.Config.Macros.Count == 0;
        var isValidIndex = itemIndex >= 0 && itemIndex < Plugin.Config.Macros.Count;

        if (isEmptyList || !isValidIndex)
            return;

        Plugin.Config.Macros.RemoveAt(itemIndex);

        Plugin.Config.Save();
    }

    public void CloneMacro(int itemIndex)
    {
        var isEmptyList = Plugin.Config.Macros == null || Plugin.Config.Macros.Count == 0;
        var isValidIndex = itemIndex >= 0 && itemIndex < Plugin.Config.Macros.Count;

        if (isEmptyList || !isValidIndex)
            return;

        var clonedMacro = Plugin.Config.Macros[itemIndex].Clone();
        clonedMacro.Name += " (copy)";
        Plugin.Config.Macros.Add(clonedMacro);

        Plugin.Config.Save();
    }
    public void MoveMacroToIndex(int itemIndex, int targetIndex)
    {
        Plugin.Config.Macros.MoveItemToIndex(itemIndex, targetIndex);
        Plugin.Config.Save();
    }

    public string ExportMacroToString(int itemIndex, bool includeCids = false)
    {
        var isEmptyList = Plugin.Config.Macros == null || Plugin.Config.Macros.Count == 0;
        var isValidIndex = itemIndex >= 0 && itemIndex < Plugin.Config.Macros.Count;

        if (isEmptyList || !isValidIndex)
        {
            DalamudApi.PluginLog.Error("Invalid macro index");
            throw new ArgumentException("Invalid macro index");
        }

        var clonedMacro = includeCids ? Plugin.Config.Macros[itemIndex].Clone() : Plugin.Config.Macros[itemIndex].CloneWithoutCharacters();
        string macroJson = clonedMacro.JsonSerialize();
        return Compressor.CompressString(macroJson);
    }

    public void ImportMacroFromString(string compressedMacroString)
    {
        string macroString = Compressor.DecompressString(compressedMacroString);
        var newMacro = macroString.JsonDeserialize<Macro>();
        Plugin.Config.Macros.Add(newMacro);
    }

    public int FindMacroIndex(string macroNameOrNumber)
    {
        int macroIndexByName = Plugin.Config.Macros.FindIndex(m => string.Equals(m.Name, macroNameOrNumber, StringComparison.OrdinalIgnoreCase));

        if (!int.TryParse(macroNameOrNumber, out var macroIndexArg) && macroIndexByName == -1)
        {
            DalamudApi.PluginLog.Error($"Invalid macro name or number {macroNameOrNumber}");
            DalamudApi.ShowNotification($"Invalid macro name or number", NotificationType.Error, 5000);
            throw new ArgumentException($"Invalid macro name or numnber {macroNameOrNumber}");
        }

        // user input 1 index based
        int macroIndex = macroIndexByName != -1 ? macroIndexByName : macroIndexArg - 1;
        var isValidMacroIndex = Plugin.Config.Macros.IndexExists(macroIndex);
        if (!isValidMacroIndex)
        {
            throw new ArgumentException($"Invalid macro index");
        }

        return macroIndex;
    }

    // public static string SerializeObject(object o, bool saveAllValues) => !saveAllValues
    //  ? JsonConvert.SerializeObject(o, new JsonSerializerSettings
    //  {
    //      TypeNameHandling = TypeNameHandling.Objects,
    //      NullValueHandling = NullValueHandling.Ignore,
    //      DefaultValueHandling = DefaultValueHandling.Ignore,
    //  })
    //  : JsonConvert.SerializeObject(o, new JsonSerializerSettings
    //  {
    //      TypeNameHandling = TypeNameHandling.Objects
    //  });

    // public static T DeserializeObject<T>(string o) => JsonConvert.DeserializeObject<T>(o, new JsonSerializerSettings
    // {
    //     TypeNameHandling = TypeNameHandling.Objects,
    // });

    // public static string ExportObject(object o, bool saveAllValues) => CompressString(SerializeObject(o, saveAllValues));

    // public static T ImportObject<T>(string import) => DeserializeObject<T>(DecompressString(import));
}
