using System;
using System.Numerics;

using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Windowing;

using MasterOfPuppets.Actions;
using MasterOfPuppets.Util;
using MasterOfPuppets.Util.ImGuiExt;

namespace MasterOfPuppets;

public partial class MainWindow : Window {
    private Plugin Plugin { get; }
    private PluginUi Ui { get; }

    private static readonly Version Version = typeof(MainWindow).Assembly.GetName().Version;

    internal enum NavSection {
        Emotes,
        Mounts,
        Minions,
        Facewear,
        FashionAccessories,
        Items,
        GearSets,
        Commands,
        Teleport,
    }

    private static readonly (NavSection Section, FontAwesomeIcon Icon, string Label, bool IsSeparatorBefore)[] NavItems = [
        (NavSection.Commands,           FontAwesomeIcon.Terminal,          "Commands",             false),
        (NavSection.Teleport,           FontAwesomeIcon.MapMarkerAlt,      "Teleport",             false),
        (NavSection.Emotes,             FontAwesomeIcon.SmileWink,         "Emotes",               true),
        (NavSection.Mounts,             FontAwesomeIcon.Horse,             "Mounts",               false),
        (NavSection.Minions,            FontAwesomeIcon.Cat,               "Minions",              false),
        (NavSection.Facewear,           FontAwesomeIcon.Glasses,           "Facewear",             false),
        (NavSection.FashionAccessories, FontAwesomeIcon.Umbrella,          "Fashion Accessories",  false),
        (NavSection.Items,              FontAwesomeIcon.ShoppingBag,       "Items",                false),
        (NavSection.GearSets,           FontAwesomeIcon.Briefcase,         "Gear Sets",            false),

    ];

    private NavSection _selectedSection = NavSection.Commands;
    private string _sidebarSearch = string.Empty;
    private bool _sidebarCollapsed = false;
    private float _sidebarWidth = 200f;

    // Widget system for action sections
    private readonly WidgetContext _widgetContext;
    private readonly WidgetManager _widgetManager = new();

    internal MainWindow(Plugin plugin, PluginUi ui) : base($"{Plugin.Name} {Version}###MopMainWindow") {
        Plugin = plugin;
        Ui = ui;

        Size = ImGuiHelpers.ScaledVector2(750, 500);
        SizeCondition = ImGuiCond.FirstUseEver;

        _widgetContext = new WidgetContext(plugin);

        // Register action widgets in sidebar order
        _widgetManager.Add(() => new EmotesWidget(_widgetContext));              // idx 0 → Emotes
        _widgetManager.Add(() => new MountsWidget(_widgetContext));              // idx 1 → Mounts
        _widgetManager.Add(() => new MinionsWidget(_widgetContext));             // idx 2 → Minions
        _widgetManager.Add(() => new FacewearWidget(_widgetContext));            // idx 3 → Facewear
        _widgetManager.Add(() => new FashionAccessoriesWidget(_widgetContext));  // idx 4 → FashionAccessories
        _widgetManager.Add(() => new ItemsWidget(_widgetContext));               // idx 5 → Items
        _widgetManager.Add(() => new GearSetWidget(_widgetContext));             // idx 6 → GearSets

        UpdateWindowConfig();
    }

    public override void PreDraw() {
        Flags = ImGuiWindowFlags.None;
        if (!Plugin.Config.AllowMovement) Flags |= ImGuiWindowFlags.NoMove;
        if (!Plugin.Config.AllowResize) Flags |= ImGuiWindowFlags.NoResize;
        base.PreDraw();
    }

    public override void Draw() {
        DrawSidebar();
        ImGui.SameLine();
        DrawSidebarSplitter();
        ImGui.SameLine();
        DrawContent();
    }

    private void DrawContent() {
        ImGui.BeginChild("##MopHubContent", new Vector2(0, -1), false);

        int widgetIndex = GetWidgetIndex(_selectedSection);
        if (widgetIndex >= 0) {
            // Show/switch widget when section changes
            _widgetManager.Show(widgetIndex);
            _widgetManager.Draw();
        } else {
            switch (_selectedSection) {
                case NavSection.Commands: DrawCommandsSection(); break;
                case NavSection.Teleport: DrawTeleportSection(); break;
            }
        }

        ImGui.EndChild();
    }

    /// <summary>Maps NavSection to widget index (0-based). Returns -1 for non-widget sections.</summary>
    private static int GetWidgetIndex(NavSection section) => section switch {
        NavSection.Emotes => 0,
        NavSection.Mounts => 1,
        NavSection.Minions => 2,
        NavSection.Facewear => 3,
        NavSection.FashionAccessories => 4,
        NavSection.Items => 5,
        NavSection.GearSets => 6,
        _ => -1,
    };

    internal void UpdateWindowConfig() {
        RespectCloseHotkey = Plugin.Config.AllowCloseWithEscape;

        TitleBarButtons.Clear();
        if (Plugin.Config.ShowSettingsButton) {
            TitleBarButtons.Add(new TitleBarButton() {
                AvailableClickthrough = false,
                Icon = FontAwesomeIcon.Cog,
                ShowTooltip = () => ImGuiUtil.ToolTip("Settings"),
                Click = _ => Ui.SettingsWindow.Toggle()
            });

            TitleBarButtons.Add(new TitleBarButton() {
                AvailableClickthrough = false,
                Icon = FontAwesomeIcon.Heart,
                IconColor = Style.Colors.Red,
                ShowTooltip = () => ImGuiUtil.ToolTip("Discord"),
                Click = _ => WindowsApi.OpenUrl("https://discord.gg/BTsHyBzGsN")
            });

#if DEBUG
            TitleBarButtons.Add(new TitleBarButton() {
                AvailableClickthrough = false,
                Icon = FontAwesomeIcon.Bug,
                IconColor = Style.Colors.Green,
                ShowTooltip = () => ImGuiUtil.ToolTip("Debug"),
                Click = _ => Ui.DebugWindow.Toggle()
            });
#endif
        }
    }
}
