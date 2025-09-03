using System;

using Dalamud.Game;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Dalamud.Interface.ImGuiNotification;
using Dalamud.Game.ClientState.Objects;

namespace MasterOfPuppets;

public class DalamudApi
{
    [PluginService] public static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] public static IPluginLog PluginLog { get; private set; } = null!;
    [PluginService] public static ICommandManager CommandManager { get; private set; } = null!;
    [PluginService] public static IClientState ClientState { get; private set; } = null!;
    [PluginService] public static IPartyList PartyList { get; private set; } = null!;
    [PluginService] public static INotificationManager NotificationManager { get; private set; } = null!;
    [PluginService] public static IFramework Framework { get; private set; } = null!;
    [PluginService] public static IDataManager DataManager { get; private set; } = null!;
    [PluginService] public static ITextureProvider TextureProvider { get; private set; } = null!;

    [PluginService] public static IObjectTable Objects { get; private set; }
    [PluginService] public static ITargetManager Targets { get; private set; }
    // [PluginService] public static ICondition Condition { get; private set; } = null!;
    // [PluginService] public static IGameConfig GameConfig { get; private set; } = null!;
    // [PluginService] public static IGameInteropProvider GameInteropProvider { get; private set; } = null!;
    // [PluginService] public static IGameGui GameGui { get; private set; } = null!;
    // [PluginService] public static IChatGui ChatGui { get; private set; } = null!;
    // [PluginService] public static IToastGui ToastGui { get; private set; } = null!;
    // Chat
    [PluginService] public static ISigScanner SigScanner { get; private set; } = null!;

    private const string printName = "MoP";
    private const string printHeader = $"[{printName}] ";

    public static void ShowNotification(string message, NotificationType type = NotificationType.None, uint msDelay = 3_000u) => NotificationManager.AddNotification(new Notification { Type = type, Title = printHeader, Content = message, InitialDuration = TimeSpan.FromMilliseconds(msDelay) });
}
