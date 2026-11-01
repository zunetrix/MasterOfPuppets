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

    private string _selectedSection = "Commands";
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
        _widgetManager.Add(() => new DalamudPluginsWidget(_widgetContext));             // idx 7 → Plugins
        // _widgetManager.Add(() => new GearSetListWidget(_widgetContext));         // idx 7 → GearSetList

        UpdateWindowConfig();
    }

    public override void PreDraw() {
        Flags = ImGuiWindowFlags.None;
        if (!Plugin.Config.AllowMovement) Flags |= ImGuiWindowFlags.NoMove;
        if (!Plugin.Config.AllowResize) Flags |= ImGuiWindowFlags.NoResize;
        EnsureValidSelectedSection();
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

        if (MainMenuRegistry.TryGetDef(_selectedSection, out var def) && def.Kind == MenuItemKind.View) {
            if (def.WidgetIndex.HasValue) {
                _widgetManager.Show(def.WidgetIndex.Value);
                _widgetManager.Draw();
            } else {
                switch (def.Id) {
                    case "Commands": DrawCommandsSection(); break;
                    case "Teleport": DrawTeleportSection(); break;
                }
            }
        }

        ImGui.EndChild();
    }

    private void EnsureValidSelectedSection() {
        var items = Plugin.Config.MainMenuItems;
        var currentItem = items.Find(i => i.Id.Equals(_selectedSection, StringComparison.OrdinalIgnoreCase));
        if (currentItem == null || !currentItem.Enabled) {
            var firstEnabledView = items.Find(i => i.Enabled && MainMenuRegistry.TryGetDef(i.Id, out var d) && d.Kind == MenuItemKind.View);
            if (firstEnabledView != null) {
                SelectSection(firstEnabledView.Id);
            }
        }
    }

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
