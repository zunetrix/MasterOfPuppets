using System;
using System.Collections.Generic;
using System.Numerics;

using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Interface;
using Dalamud.Interface.Utility;


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
public class AetheryteOverlayWindow : ThemedWindow {
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
    private float AethernetButtonWidth => 170f * Scale;
    private float WardButtonWidth => 48f * Scale;
    private float WorldButtonWidth => 110f * Scale;
    private float SubmenuGap => 4f * Scale;

    public AetheryteOverlayWindow(Plugin plugin) : base("Aetheryte Travel###MopAetheryteOverlay", OverlayBar.BaseFlags) {
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

        if (!OverlayBar.IsEnabledForCurrentCharacter(config)) return CloseSubmenu();

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
        UpdateNativeMapPreview();
        return false;
    }

    // native TelepotTown window follows the Aethernet submenu (local only)
    private void UpdateNativeMapPreview() {
        var active = _openCategory == Category.Aethernet && Plugin.Config.AetheryteOverlayNativeMapPreview;
        if (!active) {
            if (AethernetMapPreview.IsActive) AethernetMapPreview.Close();
            return;
        }

        AethernetMapPreview.Open();
        AethernetMapPreview.Update(AetheryteTravelPanel.HoveredAethernet?.Name);
    }

    public override void PreDraw() {
        Flags = OverlayBar.ApplyDock(Plugin.Config.AetheryteOverlayDock, Plugin.Config.AetheryteOverlayOffsetY);
        base.PreDraw();
    }

    public override void Draw() {
        if (_context is not { } ctx) return;
        var now = Environment.TickCount64;

        // category bar
        var iconsOnly = Plugin.Config.AetheryteOverlayIconsOnly;
        foreach (var def in GetVisibleCategories(ctx)) {
            if (OverlayBar.DrawButton($"category_{def.Id}", def.Icon, def.Label, def.IconColor, iconsOnly))
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

    // submenu
    private void DrawSubmenu(AethernetCatalog.AetheryteContext ctx, long now) {
        if (_openCategory == Category.None) {
            UpdateNativeMapPreview();
            return;
        }

        var barPos = ImGui.GetWindowPos();
        var barSize = ImGui.GetWindowSize();
        var viewport = ImGui.GetMainViewport();

        // open towards the screen center
        var opensLeft = barPos.X + barSize.X * 0.5f > viewport.Pos.X + viewport.Size.X * 0.5f;
        var x = opensLeft ? barPos.X - SubmenuGap : barPos.X + barSize.X + SubmenuGap;

        // keep inside the screen vertically (submenu size from the previous frame)
        var maxY = viewport.Pos.Y + viewport.Size.Y - _submenuSize.Y - OverlayBar.ScreenMargin;
        var y = Math.Max(viewport.Pos.Y + OverlayBar.ScreenMargin, Math.Min(_submenuAnchorY, maxY));

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

        // a destination was clicked: the travel flow reuses the open native window
        if (AetheryteTravelPanel.ActionTriggeredThisFrame && _openCategory == Category.Aethernet)
            AethernetMapPreview.HandOff();

        if (AetheryteTravelPanel.ActionTriggeredThisFrame || now - _lastHoverMs > SubmenuCloseDelayMs)
            _openCategory = Category.None;

        UpdateNativeMapPreview();
    }

    //  helpers
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
