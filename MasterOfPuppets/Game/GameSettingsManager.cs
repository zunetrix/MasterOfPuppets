using Dalamud.Game.Config;

using FFXIVClientStructs.FFXIV.Client.System.Framework;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using FFXIVClientStructs.FFXIV.Common.Configuration;

// using CSConfigType = FFXIVClientStructs.FFXIV.Common.Configuration.ConfigType;

namespace MasterOfPuppets;

public static class GameSettingsManager {
    public static unsafe void GetSettings() {
        var gameConfig = Framework.Instance()->SystemConfig.SystemConfigBase.ConfigBase.ConfigEntry;
        if (gameConfig == null) return;

        uint displayObjectLimit = gameConfig[(int)ConfigOption.DisplayObjectLimitType].Value.UInt;
        uint displayObjectLimit2 = gameConfig[(int)ConfigOption.DisplayObjectLimitType2].Value.UInt;
        // keep game pad enabled when client is inactive
        var alwaysInput = gameConfig[(int)ConfigOption.AlwaysInput].Value.UInt;

        DalamudApi.PluginLog.Warning($"DisplayObjectLimit {displayObjectLimit}");
        DalamudApi.PluginLog.Warning($"DisplayObjectLimit2 {displayObjectLimit2}");
        DalamudApi.PluginLog.Warning($"alwaysInput {alwaysInput}");
        // gameConfig[(int)ConfigOption.DisplayObjectLimitType].SetValueUInt((uint)DisplayObjectLimit.Maximum);
    }

    public static SettingsDisplayObjectLimitType GetDisplayObjectLimit() {
        DalamudApi.GameConfig.TryGet(SystemConfigOption.DisplayObjectLimitType2, out uint displayObjectLimitType2);
        // DalamudApi.PluginLog.Debug($"displayObjectLimitType2 {displayObjectLimitType2}");
        return (SettingsDisplayObjectLimitType)displayObjectLimitType2;
    }

    public static void SetDisplayObjectLimit(SettingsDisplayObjectLimitType displayObjectLimitType) {
        DalamudApi.GameConfig.Set(SystemConfigOption.DisplayObjectLimitType2, (uint)displayObjectLimitType);
    }

    public static void SetAlwaysInput(uint value) {
        DalamudApi.GameConfig.Set(SystemConfigOption.AlwaysInput, value);
    }

    // public static void GetDisplayObjectLimitType()
    // {
    //     DalamudApi.GameConfig.TryGet(SystemConfigOption.DisplayObjectLimitType, out uint displayObjectLimitType);
    //     DalamudApi.PluginLog.Warning($"displayObjectLimitType {displayObjectLimitType}");
    // }

    // public static void SetDisplayObjectLimitType(uint displayObjectLimitType)
    // {
    //     DalamudApi.GameConfig.Set(SystemConfigOption.DisplayObjectLimitType, displayObjectLimitType);
    // }


    // DalamudApi.GameConfig.UiConfigChanged += OnConfigChange;
    //     DalamudApi.GameConfig.UiControlChanged += OnConfigChange;
    //     var MoveMode = DalamudApi.GameConfig.UiControl.GetUInt("MoveMode");
    // var PadMode = DalamudApi.GameConfig.UiConfig.GetUInt("PadMode");
    // DalamudApi.GameConfig.UiControl.Set("MoveMode", 0);
    //     DalamudApi.GameConfig.UiConfig.Set("PadMode", 0);
    //     DalamudApi.GameConfig.UiConfig.Set("PadMode", PadMode);
    //     DalamudApi.GameConfig.UiControl.Set("MoveMode", MoveMode);

    public static void EnableDebug() {
        DalamudApi.GameConfig.UiConfigChanged += OnUiConfigChanged;
        DalamudApi.GameConfig.UiControlChanged += OnUiControlChanged;
        DalamudApi.GameConfig.SystemChanged += OnSystemConfigChange;
    }

    public static void DisableDebug() {
        DalamudApi.GameConfig.UiConfigChanged -= OnUiConfigChanged;
        DalamudApi.GameConfig.UiControlChanged -= OnUiControlChanged;
        DalamudApi.GameConfig.SystemChanged -= OnSystemConfigChange;
    }

    private static void OnUiConfigChanged(object? sender, ConfigChangeEvent e) {
        var option = e.Option;
        var optionName = e.Option.ToString();

        DalamudApi.PluginLog.Warning($"UiConfigChanged: {option}");

        try {
            var value = DalamudApi.GameConfig.UiConfig.GetUInt(optionName);
            DalamudApi.PluginLog.Warning($"{optionName} [{option}] (UInt) = {value}");
            return;
        } catch { }

        try {
            var value = DalamudApi.GameConfig.UiConfig.GetFloat(optionName);
            DalamudApi.PluginLog.Warning($"{optionName} [{option}] (Float) = {value}");
            return;
        } catch { }

        try {
            var value = DalamudApi.GameConfig.UiConfig.GetString(optionName);
            DalamudApi.PluginLog.Warning($"{optionName} [{option}] (String) = {value}");
            return;
        } catch { }
    }

    private static void OnUiControlChanged(object? sender, ConfigChangeEvent e) {
        var option = e.Option;
        var optionName = e.Option.ToString();

        DalamudApi.PluginLog.Warning($"UiControlChanged: {option}");

        try {
            var value = DalamudApi.GameConfig.UiControl.GetUInt(optionName);
            DalamudApi.PluginLog.Warning($"{optionName} [{option}] (UInt - Control) = {value}");
            return;
        } catch { }
    }

    private static void OnSystemConfigChange(object? sender, ConfigChangeEvent e) {
        var option = e.Option;
        var optionName = e.Option.ToString();

        DalamudApi.PluginLog.Warning($"SystemChanged: {optionName} [{option}]");

        // try {
        //     DalamudApi.GameConfig.TryGet(SystemConfigOption.?, out uint value);
        //     DalamudApi.PluginLog.Warning($"{optionName} [{option}] (UInt - Control) = {value}");
        //     return;
        // } catch { }
    }
}

public enum SettingsDisplayObjectLimitType {
    Automatic = 0,
    Maximum = 1,
    High = 2,
    Normal = 3,
    Low = 4,
    Minimum = 5
}
