using Dalamud.Game.Config;

using FFXIVClientStructs.FFXIV.Client.System.Framework;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;

namespace MasterOfPuppets;

public enum SettingsDisplayObjectLimitType {
    Automatic = 0,
    Maximum = 1,
    High = 2,
    Normal = 3,
    Low = 4,
    Minimum = 5
}

public static class GameSettingsManager {
    public static unsafe void GetSettings() {
        var gameConfig = Framework.Instance()->SystemConfig.SystemConfigBase.ConfigBase.ConfigEntry;
        uint displayObjectLimit = gameConfig[(int)ConfigOption.DisplayObjectLimitType].Value.UInt;
        uint displayObjectLimit2 = gameConfig[(int)ConfigOption.DisplayObjectLimitType2].Value.UInt;

        DalamudApi.PluginLog.Warning($"DisplayObjectLimit {displayObjectLimit}");
        DalamudApi.PluginLog.Warning($"DisplayObjectLimit {displayObjectLimit2}");
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

    // public static void GetDisplayObjectLimitType()
    // {
    //     DalamudApi.GameConfig.TryGet(SystemConfigOption.DisplayObjectLimitType, out uint displayObjectLimitType);
    //     DalamudApi.PluginLog.Warning($"displayObjectLimitType {displayObjectLimitType}");
    // }

    // public static void SetDisplayObjectLimitType(uint displayObjectLimitType)
    // {
    //     DalamudApi.GameConfig.Set(SystemConfigOption.DisplayObjectLimitType, displayObjectLimitType);
    // }
}
