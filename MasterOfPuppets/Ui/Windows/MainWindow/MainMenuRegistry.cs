using System;
using System.Collections.Generic;
using System.Linq;

using Dalamud.Interface;

namespace MasterOfPuppets;

public enum MenuItemKind {
    View,
    Action
}

public record MainMenuItemDef(
    string Id,
    string Label,
    FontAwesomeIcon Icon,
    MenuItemKind Kind,
    int? WidgetIndex = null,
    Action<PluginUi>? OnClick = null
);

public static class MainMenuRegistry {
    public static readonly IReadOnlyList<MainMenuItemDef> AllItems = [
        new("Macros", "Macros", FontAwesomeIcon.Scroll, MenuItemKind.Action, OnClick: ui => ui.MacroWindow.Toggle()),
        new("Commands", "Commands", FontAwesomeIcon.Terminal, MenuItemKind.View),
        new("Teleport", "Teleport", FontAwesomeIcon.MapMarkerAlt, MenuItemKind.View),
        new("Emotes", "Emotes", FontAwesomeIcon.SmileWink, MenuItemKind.View, WidgetIndex: 0),
        new("Mounts", "Mounts", FontAwesomeIcon.Horse, MenuItemKind.View, WidgetIndex: 1),
        new("Minions", "Minions", FontAwesomeIcon.Cat, MenuItemKind.View, WidgetIndex: 2),
        new("Facewear", "Facewear", FontAwesomeIcon.Glasses, MenuItemKind.View, WidgetIndex: 3),
        new("FashionAccessories", "Fashion Accessories", FontAwesomeIcon.Umbrella, MenuItemKind.View, WidgetIndex: 4),
        new("Items", "Items", FontAwesomeIcon.ShoppingBag, MenuItemKind.View, WidgetIndex: 5),
        new("GearSets", "Gear Sets", FontAwesomeIcon.Tshirt, MenuItemKind.View, WidgetIndex: 6),
        new("Characters", "Characters", FontAwesomeIcon.Users, MenuItemKind.Action, OnClick: ui => ui.CharactersWindow.Toggle()),
        new("Formation", "Formation", FontAwesomeIcon.ArrowsDownToPeople, MenuItemKind.Action, OnClick: ui => ui.FormationWindow.Toggle()),
        new("WindowLayout", "Windows Layout", FontAwesomeIcon.Display, MenuItemKind.Action, OnClick: ui => ui.WindowLayoutWindow.Toggle()),
        new("GameSettings", "Game Settings Profile", FontAwesomeIcon.SlidersH, MenuItemKind.Action, OnClick: ui => ui.GameSettingsWindow.Toggle()),
        new("PeerMonitor", "Peer Monitor", FontAwesomeIcon.UsersViewfinder, MenuItemKind.Action, OnClick: ui => ui.PeerMonitorWindow.Toggle()),
        new("XivLauncher", "XIV Launcher", FontAwesomeIcon.Rocket, MenuItemKind.Action, OnClick: ui => ui.XivLauncherWindow.Toggle()),
        new("Settings", "Settings", FontAwesomeIcon.Cog, MenuItemKind.Action, OnClick: ui => ui.SettingsWindow.Toggle()),
        new("Help", "Help", FontAwesomeIcon.QuestionCircle, MenuItemKind.Action, OnClick: ui => ui.HelpWindow.Toggle()),
    ];

    private static readonly Dictionary<string, MainMenuItemDef> _itemsById =
        AllItems.ToDictionary(i => i.Id, StringComparer.OrdinalIgnoreCase);

    public static bool TryGetDef(string id, out MainMenuItemDef def) =>
        _itemsById.TryGetValue(id, out def!);

    public static List<MainMenuItemConfig> CreateDefaultConfigList() =>
        AllItems.Select(item => new MainMenuItemConfig { Id = item.Id, Enabled = true }).ToList();
}

