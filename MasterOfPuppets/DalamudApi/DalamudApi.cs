using System;

using Dalamud.Game;
using Dalamud.Game.ClientState.Objects;
using Dalamud.Interface.ImGuiNotification;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;

namespace MasterOfPuppets;

public class DalamudApi {
    [PluginService] public static IDalamudPluginInterface PluginInterface { get; private set; } = null;
    [PluginService] public static IPluginLog PluginLog { get; private set; } = null;
    [PluginService] public static ICommandManager CommandManager { get; private set; } = null;
    [PluginService] public static IClientState ClientState { get; private set; } = null;
    [PluginService] public static IPlayerState Player { get; private set; } = null;
    [PluginService] public static IPartyList PartyList { get; private set; } = null;
    [PluginService] public static INotificationManager NotificationManager { get; private set; } = null;
    [PluginService] public static IFramework Framework { get; private set; } = null;
    [PluginService] public static IDataManager DataManager { get; private set; } = null;
    [PluginService] public static ITextureProvider TextureProvider { get; private set; } = null;
    [PluginService] public static IChatGui ChatGui { get; private set; } = null;
    [PluginService] public static IGameConfig GameConfig { get; private set; } = null;
    [PluginService] public static IObjectTable Objects { get; private set; }
    [PluginService] public static ITargetManager Targets { get; private set; }
    [PluginService] public static ICondition Condition { get; private set; } = null;
    // hook
    [PluginService] public static IGameInteropProvider GameInteropProvider { get; private set; } = null;
    // [PluginService] public static IKeyState KeyState { get; private set; }
    // [PluginService] public static IGameGui GameGui { get; private set; } = null;
    // [PluginService] public static IToastGui ToastGui { get; private set; } = null;
    // Chat
    [PluginService] public static ISigScanner SigScanner { get; private set; } = null;
    [PluginService] public static ISeStringEvaluator SeStringEvaluator { get; private set; } = null;

    private const string PluginPrefixName = $"[MoP] ";

    public static void ShowNotification(string message, NotificationType type = NotificationType.None, uint msDelay = 3_000u) => NotificationManager.AddNotification(new Notification { Type = type, Title = PluginPrefixName, Content = message, InitialDuration = TimeSpan.FromMilliseconds(msDelay) });

    // private IExposedPlugin? FindInstalledPlugin(PluginInfo pluginInfo) {
    //     return PluginInterface.InstalledPlugins.FirstOrDefault(x =>
    //         x.InternalName == pluginInfo.InternalName && x.IsLoaded);
    // }

    // private sealed record PluginInfo(
    // string DisplayName,
    // string InternalName,
    // string Details,
    // Uri WebsiteUri,
    // Uri? DalamudRepositoryUri,
    // string? ConfigCommand = null,
    // List<PluginDetailInfo>? DetailsToCheck = null);

    // private sealed record PluginDetailInfo(string DisplayName, string Details, Func<bool> Predicate);
}
