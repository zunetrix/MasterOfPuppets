using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;

using MasterOfPuppets.Util.ImGuiExt;

namespace MasterOfPuppets;

public enum AetheryteOverlayDock {
    Left,
    Right,
    Free,
}

/// <summary>
/// Based on Lifestream.
/// Vertical category bar docked to the screen side while standing at an aetheryte.
/// Hovering a category opens a submenu next to the bar (towards the screen center) with its destinations.
/// Categories: Aethernet / Residential (main cities) / World Visit (Gridania, Limsa, Ul'dah).
/// </summary>
public class AetheryteOverlayWindow : Window {
    private enum Category {
        None,
        Aethernet,
        Residential,
        WorldVisit,
    }

    private sealed record CategoryDef(Category Id, FontAwesomeIcon Icon, string Label, Vector4 IconColor);

    private static readonly CategoryDef[] Categories = [
        new(Category.Aethernet, FontAwesomeIcon.MapMarkedAlt, "Aethernet", Style.Colors.White),
        new(Category.Residential, FontAwesomeIcon.Home, "Residential", Style.Colors.White),
        new(Category.WorldVisit, FontAwesomeIcon.Globe, "World Visit", Style.Colors.White),
    ];

    private const string SubmenuWindowName = "##MopAetheryteOverlaySubmenu";
    private const long SubmenuCloseDelayMs = 350;

    private const ImGuiWindowFlags BaseFlags =
        ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.AlwaysAutoResize |
        ImGuiWindowFlags.NoFocusOnAppearing | ImGuiWindowFlags.NoScrollbar;

    private const ImGuiWindowFlags SubmenuFlags =
        ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.AlwaysAutoResize |
        ImGuiWindowFlags.NoFocusOnAppearing | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoMove |
        ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.NoResize;

    private Plugin Plugin { get; }

    private AethernetCatalog.AetheryteContext? _context;

    private Category _openCategory = Category.None;
    private float _submenuAnchorY;
    private long _lastHoverMs;
    private Vector2 _submenuSize;

    private static float Scale => ImGuiHelpers.GlobalScale;
    private float CategoryButtonWidth => 150f * Scale;
    private float CategoryIconButtonSize => 44f * Scale;
    private float AethernetButtonWidth => 170f * Scale;
    private float WardButtonWidth => 48f * Scale;
    private float WorldButtonWidth => 110f * Scale;
    private float ScreenMargin => 8f * Scale;
    private float SubmenuGap => 4f * Scale;

    public AetheryteOverlayWindow(Plugin plugin) : base("Aetheryte Travel###MopAetheryteOverlay", BaseFlags) {
        Plugin = plugin;
        IsOpen = true;
        RespectCloseHotkey = false;
        ShowCloseButton = false;
    }

    public override bool DrawConditions() {
        _context = null;
        var config = Plugin.Config;
        if (!config.ShowAetheryteOverlay) return CloseSubmenu();
        if (!DalamudApi.PlayerState.IsLoaded) return CloseSubmenu();

        // per-character toggle (unknown characters default to shown, like the other per-character options)
        var charConfig = config.Characters.FirstOrDefault(c => c.Cid == DalamudApi.PlayerState.ContentId);
        if (charConfig != null && !charConfig.ShowAetheryteOverlay) return CloseSubmenu();

        var condition = DalamudApi.Condition;
        if (condition[ConditionFlag.InCombat]
            || condition[ConditionFlag.BetweenAreas]
            || condition[ConditionFlag.BetweenAreas51]
            || condition[ConditionFlag.OccupiedInCutSceneEvent]
            || condition[ConditionFlag.WatchingCutscene]) return CloseSubmenu();

        if (!AethernetCatalog.TryGetContext(out var ctx)) return CloseSubmenu();
        _context = ctx;

        return GetVisibleCategories(ctx).Count > 0 || CloseSubmenu();
    }

    private bool CloseSubmenu() {
        _openCategory = Category.None;
        return false;
    }

    public override void PreDraw() {
        var dock = Plugin.Config.AetheryteOverlayDock;
        Flags = dock == AetheryteOverlayDock.Free ? BaseFlags : BaseFlags | ImGuiWindowFlags.NoMove;
        if (dock == AetheryteOverlayDock.Free) return;

        var viewport = ImGui.GetMainViewport();
        var y = viewport.Pos.Y + viewport.Size.Y * (0.5f + Math.Clamp(Plugin.Config.AetheryteOverlayOffsetY, -0.5f, 0.5f));
        var (x, pivotX) = dock == AetheryteOverlayDock.Left
            ? (viewport.Pos.X + ScreenMargin, 0f)
            : (viewport.Pos.X + viewport.Size.X - ScreenMargin, 1f);
        ImGui.SetNextWindowPos(new Vector2(x, y), ImGuiCond.Always, new Vector2(pivotX, 0.5f));
    }

    public override void Draw() {
        if (_context is not { } ctx) return;
        var now = Environment.TickCount64;

        //  category bar
        var iconsOnly = Plugin.Config.AetheryteOverlayIconsOnly;
        foreach (var def in GetVisibleCategories(ctx)) {
            var clicked = iconsOnly
                ? DrawIconOnlyButton(def)
                : ImGuiUtil.DrawCmdButton(def.Icon, def.Label, null, def.IconColor, CategoryButtonWidth);
            if (clicked)
                OpenSubmenu(def.Id, now);
            if (ImGui.IsItemHovered())
                OpenSubmenu(def.Id, now);
        }

        if (_openCategory != Category.None && ImGui.IsWindowHovered(ImGuiHoveredFlags.RootAndChildWindows))
            _lastHoverMs = now;

        DrawSubmenu(ctx, now);
    }

    private void OpenSubmenu(Category category, long now) {
        _openCategory = category;
        _submenuAnchorY = ImGui.GetItemRectMin().Y;
        _lastHoverMs = now;
    }

    //  submenu
    //  A separate ImGui window, not a popup: an open popup blocks hovering the bar,
    //  which would break switching category by hover.

    private void DrawSubmenu(AethernetCatalog.AetheryteContext ctx, long now) {
        if (_openCategory == Category.None) return;

        var barPos = ImGui.GetWindowPos();
        var barSize = ImGui.GetWindowSize();
        var viewport = ImGui.GetMainViewport();

        // open towards the screen center
        var opensLeft = barPos.X + barSize.X * 0.5f > viewport.Pos.X + viewport.Size.X * 0.5f;
        var x = opensLeft ? barPos.X - SubmenuGap : barPos.X + barSize.X + SubmenuGap;

        // keep inside the screen vertically (submenu size from the previous frame)
        var maxY = viewport.Pos.Y + viewport.Size.Y - _submenuSize.Y - ScreenMargin;
        var y = Math.Max(viewport.Pos.Y + ScreenMargin, Math.Min(_submenuAnchorY, maxY));

        ImGui.SetNextWindowPos(new Vector2(x, y), ImGuiCond.Always, new Vector2(opensLeft ? 1f : 0f, 0f));

        if (ImGui.Begin(SubmenuWindowName, SubmenuFlags)) {
            ImGui.TextUnformatted(GetLabel(_openCategory));
            ImGui.Separator();
            ImGui.Spacing();

            try {
                switch (_openCategory) {
                    case Category.Aethernet:
                        AetheryteTravelPanel.DrawAethernet(Plugin, ctx, AethernetButtonWidth);
                        break;
                    case Category.Residential:
                        AetheryteTravelPanel.DrawResidential(Plugin, 5, WardButtonWidth);
                        break;
                    case Category.WorldVisit:
                        AetheryteTravelPanel.DrawWorldVisit(Plugin, WorldButtonWidth);
                        break;
                }
            } catch (Exception ex) {
                DalamudApi.PluginLog.Error(ex, $"[AetheryteOverlay] failed drawing {_openCategory}");
            }

            if (ImGui.IsWindowHovered(ImGuiHoveredFlags.RootAndChildWindows | ImGuiHoveredFlags.AllowWhenBlockedByActiveItem))
                _lastHoverMs = now;

            _submenuSize = ImGui.GetWindowSize();
        }
        ImGui.End();

        if (AetheryteTravelPanel.ActionTriggeredThisFrame || now - _lastHoverMs > SubmenuCloseDelayMs)
            _openCategory = Category.None;
    }

    //  helpers

    /// <summary>Square icon-only category button, same look as <see cref="ImGuiUtil.DrawCmdButton"/>.</summary>
    private bool DrawIconOnlyButton(CategoryDef def) {
        var size = new Vector2(CategoryIconButtonSize, CategoryIconButtonSize);
        var rounding = 6f * Scale;
        var pos = ImGui.GetCursorScreenPos();

        bool clicked;
        using (ImRaii.PushColor(ImGuiCol.Button, Style.Components.ButtonCmdNormal)
                   .Push(ImGuiCol.ButtonHovered, Style.Components.ButtonCmdHovered)
                   .Push(ImGuiCol.ButtonActive, Style.Components.ButtonCmdActive))
        using (ImRaii.PushStyle(ImGuiStyleVar.FrameRounding, rounding)) {
            clicked = ImGui.Button($"##overlayCategory_{def.Id}", size);
        }

        var hovered = ImGui.IsItemHovered();
        var drawList = ImGui.GetWindowDrawList();
        var borderCol = ImGui.ColorConvertFloat4ToU32(hovered ? new Vector4(0.45f, 0.60f, 1.00f, 0.55f) : new Vector4(1f, 1f, 1f, 0.08f));
        drawList.AddRect(pos, pos + size, borderCol, rounding, ImDrawFlags.None, hovered ? 1.2f : 0.5f);

        using (ImRaii.PushFont(UiBuilder.IconFont)) {
            var iconStr = def.Icon.ToIconString();
            var glyphSize = ImGui.CalcTextSize(iconStr);
            drawList.AddText(pos + (size - glyphSize) * 0.5f, ImGui.ColorConvertFloat4ToU32(def.IconColor), iconStr);
        }

        return clicked;
    }

    private List<CategoryDef> GetVisibleCategories(AethernetCatalog.AetheryteContext ctx) {
        var config = Plugin.Config;
        var list = new List<CategoryDef>(Categories.Length);
        foreach (var def in Categories) {
            var visible = def.Id switch {
                Category.Aethernet => config.AetheryteOverlayShowAethernet && ctx.Aethernet.Count > 0,
                Category.Residential => config.AetheryteOverlayShowResidential && ctx.CanResidential,
                Category.WorldVisit => config.AetheryteOverlayShowWorldVisit && ctx.CanWorldVisit,
                _ => false,
            };
            if (visible) list.Add(def);
        }
        return list;
    }

    private static string GetLabel(Category category) {
        foreach (var def in Categories)
            if (def.Id == category) return def.Label;
        return string.Empty;
    }
}
