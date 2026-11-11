using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;

using MasterOfPuppets.Util.ImGuiExt;

namespace MasterOfPuppets;

/// <summary>
/// Shared drawing of the three aetheryte travel categories (Aethernet / Residential / World Visit).
/// Used by the main window teleport section and by <see cref="AetheryteOverlayWindow"/>.
/// Every button broadcasts through IPC so all clients run the same travel.
/// </summary>
internal static class AetheryteTravelPanel {

    /// <summary>Width argument meaning "fill available width".</summary>
    public const float FillWidth = -1f;

    /// <summary>ImGui frame of the last travel button click (lets hosts such as the overlay submenu close itself).</summary>
    public static int LastActionFrame { get; private set; } = -1;

    public static bool ActionTriggeredThisFrame => LastActionFrame == ImGui.GetFrameCount();

    private static void MarkActionTriggered() => LastActionFrame = ImGui.GetFrameCount();

    //  Aethernet

    /// <summary>
    /// Draws the aethernet destinations. With a <paramref name="ctx"/> the current shard is disabled;
    /// without it, the current territory's city aethernet is listed.
    /// </summary>
    public static void DrawAethernet(Plugin plugin, AethernetCatalog.AetheryteContext? ctx, float buttonWidth = FillWidth) {
        var entries = ctx?.Aethernet ?? AethernetCatalog.GetGroupForCurrentTerritory();
        if (entries.Count == 0) {
            ImGui.TextDisabled("No aethernet in this zone");
            return;
        }

        if (buttonWidth > 0) {
            var padding = ImGui.GetStyle().FramePadding.X * 2;
            foreach (var entry in entries)
                buttonWidth = Math.Max(buttonWidth, ImGui.CalcTextSize(GetAethernetLabel(entry)).X + padding);
        }

        // residential: main district and subdivision
        if (entries.Any(e => e.IsSubdivision)) {
            var spacing = ImGui.GetStyle().ItemSpacing.X;
            var columnWidth = buttonWidth > 0 ? buttonWidth : (ImGui.GetContentRegionAvail().X - spacing) / 2f;

            ImGui.BeginGroup();
            DrawAethernetColumn(plugin, ctx, entries.Where(e => !e.IsSubdivision), columnWidth, "Main");
            ImGui.EndGroup();

            ImGui.SameLine();

            ImGui.BeginGroup();
            DrawAethernetColumn(plugin, ctx, entries.Where(e => e.IsSubdivision), columnWidth, "Subdivision");
            ImGui.EndGroup();
            return;
        }

        foreach (var entry in entries)
            DrawAethernetButton(plugin, ctx, entry, buttonWidth);
    }

    private static void DrawAethernetColumn(Plugin plugin, AethernetCatalog.AetheryteContext? ctx, IEnumerable<AethernetCatalog.AethernetEntry> entries, float width, string header) {
        ImGui.TextDisabled(header);
        foreach (var entry in entries)
            DrawAethernetButton(plugin, ctx, entry, width);
    }

    private static void DrawAethernetButton(Plugin plugin, AethernetCatalog.AetheryteContext? ctx, AethernetCatalog.AethernetEntry entry, float width) {
        // never disabled: the click is broadcast and other clients may be elsewhere;
        // AethernetTeleportManager skips the travel on clients already at the destination
        var isCurrent = ctx?.CurrentRowId == entry.RowId && !entry.IsHousing;
        using (PushCurrentLocationColors(isCurrent)) {
            var label = $"{GetAethernetLabel(entry)}##Aethernet{(entry.IsHousing ? "H" : "")}{entry.RowId}";
            if (ImGui.Button(label, new Vector2(width, 0))) {
                plugin.IpcProvider.ExecuteTeleportToAethernet(entry.Name);
                MarkActionTriggered();
            }
        }
        var command = $"/mop aethernet \"{entry.Name}\"";
        ImGuiUtil.ToolTip(isCurrent ? $"Current location\n{command}" : command);
    }

    private static string GetAethernetLabel(AethernetCatalog.AethernetEntry entry) =>
        $"{(entry.IsMaster ? "* " : "")}{entry.Name}";

    //  Residential

    public static void DrawResidential(Plugin plugin, int columns, float buttonWidth = FillWidth) {
        columns = Math.Max(1, columns);

        // fixed width (overlay): plain grid, no table (nested tables clip inside auto-fit parents)
        if (buttonWidth > 0) {
            for (var ward = 1; ward <= 30; ward++) {
                DrawWardButton(plugin, ward, buttonWidth);
                if (ward % columns != 0) ImGui.SameLine();
            }
            return;
        }

        using var table = ImRaii.Table("##AetheryteWardTable", columns, ImGuiTableFlags.SizingStretchSame | ImGuiTableFlags.NoSavedSettings);
        if (!table) return;

        for (var ward = 1; ward <= 30; ward++) {
            ImGui.TableNextColumn();
            DrawWardButton(plugin, ward, buttonWidth);
        }
    }

    private static void DrawWardButton(Plugin plugin, int ward, float buttonWidth) {
        if (ImGui.Button($"{ward:D2}##Ward{ward}", new Vector2(buttonWidth, 0))) {
            plugin.IpcProvider.ExecuteTeleportToWard(ward);
            MarkActionTriggered();
        }
        ImGuiUtil.ToolTip($"/mop ward {ward}");
    }

    //  World Visit

    /// <summary>
    /// Draws the worlds reachable from the current logical DC, one column per display DC
    /// (keeps Elemental / Gaia / Mana / Meteor apart even though JP is a single logical DC).
    /// </summary>
    public static void DrawWorldVisit(Plugin plugin, float buttonWidth = FillWidth) {
        var ps = DalamudApi.PlayerState;
        var current = ps.IsLoaded ? ps.CurrentWorld.ValueNullable : null;
        if (current == null) {
            ImGui.TextDisabled("Not logged in");
            return;
        }

        var homeWorldId = ps.HomeWorld.RowId;
        var groups = WorldCatalog.GetWorldVisitGroups(WorldCatalog.GetLogicalDataCenterId(current.Value));
        if (groups.Count == 0) {
            ImGui.TextDisabled("No worlds available");
            return;
        }

        var currentDisplayDc = WorldCatalog.GetDisplayDataCenterId(current.Value);

        // fixed width (overlay): one ImGui group per DC side by side, no table
        if (buttonWidth > 0) {
            // grow to the longest label so names are never clipped
            var padding = ImGui.GetStyle().FramePadding.X * 2;
            foreach (var group in groups)
                foreach (var world in group.Worlds)
                    buttonWidth = Math.Max(buttonWidth, ImGui.CalcTextSize(GetWorldLabel(world)).X + padding);

            for (var i = 0; i < groups.Count; i++) {
                var group = groups[i];
                if (i > 0) ImGui.SameLine();
                using (ImRaii.Group()) {
                    if (group.Id == currentDisplayDc)
                        ImGui.TextUnformatted($"> {group.Name}");
                    else
                        ImGui.TextDisabled(group.Name);
                    DrawWorldButtons(plugin, group.Worlds, current.Value.RowId, homeWorldId, buttonWidth);
                }
            }
            return;
        }

        using var table = ImRaii.Table("##AetheryteWorldVisitTable", groups.Count,
            ImGuiTableFlags.SizingStretchSame | ImGuiTableFlags.BordersInnerV | ImGuiTableFlags.NoSavedSettings);
        if (!table) return;

        foreach (var group in groups)
            ImGui.TableSetupColumn($"{(group.Id == currentDisplayDc ? "> " : "")}{group.Name}##dc{group.Id}");
        ImGui.TableHeadersRow();
        ImGui.TableNextRow();

        foreach (var group in groups) {
            ImGui.TableNextColumn();
            DrawWorldButtons(plugin, group.Worlds, current.Value.RowId, homeWorldId, buttonWidth);
        }
    }

    // current world = button color, home world = border (no text markers)
    private static string GetWorldLabel(Lumina.Excel.Sheets.World world) => world.Name.ExtractText();

    private static void DrawWorldButtons(Plugin plugin, IReadOnlyList<Lumina.Excel.Sheets.World> worlds, uint currentWorldId, uint homeWorldId, float buttonWidth) {
        foreach (var world in worlds) {
            var name = world.Name.ExtractText();
            var isCurrent = world.RowId == currentWorldId;
            var isHome = world.RowId == homeWorldId;

            // never disabled: the click is broadcast and other clients may be on another world;
            // WorldTravelManager skips the travel on clients already on the destination
            using (PushCurrentLocationColors(isCurrent))
            using (PushHomeWorldBorder(isHome)) {
                var label = $"{GetWorldLabel(world)}##World{world.RowId}";
                if (ImGui.Button(label, new Vector2(buttonWidth, 0))) {
                    plugin.IpcProvider.ExecuteTravelToWorld(name);
                    MarkActionTriggered();
                }
            }

            var tooltip = $"/mop world {name}";
            if (isHome) tooltip = $"Home world\n{tooltip}";
            if (isCurrent) tooltip = $"Current world\n{tooltip}";
            ImGuiUtil.ToolTip(tooltip);
        }
    }

    //  Location styles

    private static IDisposable PushCurrentLocationColors(bool isCurrent) =>
        ImRaii.PushColor(ImGuiCol.Button, Style.Components.ButtonSuccessNormal, isCurrent)
            .Push(ImGuiCol.ButtonHovered, Style.Components.ButtonSuccessHovered, isCurrent)
            .Push(ImGuiCol.ButtonActive, Style.Components.ButtonSuccessActive, isCurrent);

    private static HomeWorldBorder PushHomeWorldBorder(bool isHome) => new(isHome);

    private readonly struct HomeWorldBorder : IDisposable {
        private readonly IDisposable _color;
        private readonly IDisposable _style;

        public HomeWorldBorder(bool isHome) {
            _color = ImRaii.PushColor(ImGuiCol.Border, Style.Components.TooltipBorderColor, isHome);
            _style = ImRaii.PushStyle(ImGuiStyleVar.FrameBorderSize, 1.5f * ImGuiHelpers.GlobalScale, isHome);
        }

        public void Dispose() {
            _style.Dispose();
            _color.Dispose();
        }
    }
}
