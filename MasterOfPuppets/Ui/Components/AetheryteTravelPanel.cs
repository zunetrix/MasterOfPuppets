using System;
using System.Collections.Generic;
using System.Numerics;

using Dalamud.Bindings.ImGui;
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

        foreach (var entry in entries) {
            var isCurrent = ctx?.CurrentRowId == entry.RowId;
            using (ImRaii.Disabled(isCurrent)) {
                var label = $"{GetAethernetLabel(entry)}##Aethernet{entry.RowId}";
                if (ImGui.Button(label, new Vector2(buttonWidth, 0))) {
                    plugin.IpcProvider.ExecuteTeleportToAethernet(entry.Name);
                    MarkActionTriggered();
                }
            }
            ImGuiUtil.ToolTip($"/mop aethernet \"{entry.Name}\"");
        }
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
        if (ImGui.Button($"W{ward:D2}##Ward{ward}", new Vector2(buttonWidth, 0))) {
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
                    buttonWidth = Math.Max(buttonWidth, ImGui.CalcTextSize(GetWorldLabel(world, current.Value.RowId, homeWorldId)).X + padding);

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

    private static string GetWorldLabel(Lumina.Excel.Sheets.World world, uint currentWorldId, uint homeWorldId) =>
        $"{(world.RowId == currentWorldId ? "> " : "")}{world.Name.ExtractText()}{(world.RowId == homeWorldId ? " *" : "")}";

    private static void DrawWorldButtons(Plugin plugin, IReadOnlyList<Lumina.Excel.Sheets.World> worlds, uint currentWorldId, uint homeWorldId, float buttonWidth) {
        foreach (var world in worlds) {
            var name = world.Name.ExtractText();
            var isCurrent = world.RowId == currentWorldId;
            var isHome = world.RowId == homeWorldId;

            using (ImRaii.Disabled(isCurrent)) {
                var label = $"{GetWorldLabel(world, currentWorldId, homeWorldId)}##World{world.RowId}";
                if (ImGui.Button(label, new Vector2(buttonWidth, 0))) {
                    plugin.IpcProvider.ExecuteTravelToWorld(name);
                    MarkActionTriggered();
                }
            }
            ImGuiUtil.ToolTip(isHome ? $"Home world\n/mop world {name}" : $"/mop world {name}");
        }
    }
}
