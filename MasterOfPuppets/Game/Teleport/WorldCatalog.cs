using System;
using System.Collections.Generic;
using System.Linq;

using Lumina.Excel.Sheets;

namespace MasterOfPuppets;

/// <summary>
/// World / datacenter lookups for world travel.
/// <para>
/// <c>World.DataCenter</c> is the <b>logical</b> DC (what the game lets you visit). Since the JP merge,
/// every JP world points to Elemental (1), while <c>World.Unknown0</c> still holds the original
/// <b>display</b> DC (Elemental / Gaia / Mana / Meteor). For other regions both columns are equal.
/// </para>
/// </summary>
internal static class WorldCatalog {

    // JP = 1, NA = 2, EU = 3, OC = 4 (global client). CN / KR / TW regions are their own clients and only see their own worlds.
    private static bool IsGlobalRegion(uint region) => region is >= 1 and <= 4;

    // World.UserType of CN / KR worlds
    private const int UserTypeChina = 101;
    private const int UserTypeKorea = 201;

    internal sealed record DataCenterGroup(uint Id, string Name, IReadOnlyList<World> Worlds);

    public static uint GetLogicalDataCenterId(World world) => world.DataCenter.RowId;

    public static uint GetDisplayDataCenterId(World world) {
        var display = (uint)world.Unknown0;
        return display != 0 ? display : world.DataCenter.RowId;
    }

    public static string GetDataCenterName(uint dcId) =>
        DalamudApi.DataManager.GetExcelSheet<WorldDCGroupType>().GetRowOrDefault(dcId)?.Name.ExtractText() ?? $"DC {dcId}";

    public static uint GetRegion(World world) => world.DataCenter.ValueNullable?.Region.RowId ?? 0;

    /// <summary>
    /// Public, playable worlds of every region (JP/NA/EU/OC, CN, KR, TW).
    /// CN / KR / TW worlds are not flagged <c>IsPublic</c> in the sheet: CN / KR detected by UserType
    /// <c>ExcelWorldHelper.IsPublic</c>, TW by InternalName.
    /// Cloud / beta DCs are excluded.
    /// </summary>
    public static IEnumerable<World> GetPublicWorlds() =>
        DalamudApi.DataManager.GetExcelSheet<World>().Where(IsPublicWorld);

    private static bool IsPublicWorld(World w) {
        if (w.DataCenter.ValueNullable is not { } dc || dc.RowId == 0 || dc.IsCloud) return false;
        if (dc.Region.RowId == 0) return false; // Unknown / Beta
        if (w.IsPublic) return true;
        // CN
        if (w.RowId > 1000 && w.UserType == UserTypeChina) return true;
        var internalName = w.InternalName.ExtractText();
        // KR (KrOmega is a test world)
        if (w.RowId > 2000 && w.UserType == UserTypeKorea)
            return internalName.StartsWith("Kr", StringComparison.Ordinal) && internalName != "KrOmega";
        // TW (DC 151 陸行鳥, region 8): TcIfrit..TcTitan. TcTest*, t-*, td-* are test worlds.
        // Matched by InternalName: TW UserType values are not reliable to tell public worlds apart.
        if (w.RowId > 4000)
            return internalName.StartsWith("Tc", StringComparison.Ordinal) && !internalName.StartsWith("TcTest", StringComparison.Ordinal);
        return false;
    }

    private static readonly Dictionary<uint, List<DataCenterGroup>> WorldVisitCache = new();

    /// <summary>Worlds reachable through "Visit Another World" from <paramref name="logicalDcId"/>, grouped by display DC. Cached per DC.</summary>
    public static List<DataCenterGroup> GetWorldVisitGroups(uint logicalDcId) {
        if (WorldVisitCache.TryGetValue(logicalDcId, out var cached)) return cached;
        return WorldVisitCache[logicalDcId] = GetPublicWorlds()
            .Where(w => GetLogicalDataCenterId(w) == logicalDcId)
            .GroupBy(GetDisplayDataCenterId)
            .Select(g => new DataCenterGroup(
                g.Key,
                GetDataCenterName(g.Key),
                g.OrderBy(w => w.Name.ExtractText(), StringComparer.OrdinalIgnoreCase).ToList()))
            .OrderBy(g => g.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private const uint RegionOceania = 4;

    private static readonly Dictionary<uint, List<uint>> MergedTabsCache = new();

    /// <summary>
    /// Display DCs of a logical DC in <c>WorldTravelSelect</c> tab order (display DC name, alphabetical).
    /// JP: Elemental, Gaia, Mana, Meteor → tabs 0..3 (same as RowId order)
    /// NA: Aether, Crystal, Dynamis, Primal → tabs 0..3 (RowId order Aether, Primal, Crystal, Dynamis)
    /// </summary>
    private static List<uint> GetWorldTravelTabs(uint logicalDcId) {
        if (MergedTabsCache.TryGetValue(logicalDcId, out var cached)) return cached;
        return MergedTabsCache[logicalDcId] = GetPublicWorlds()
            .Where(w => GetLogicalDataCenterId(w) == logicalDcId)
            .Select(GetDisplayDataCenterId)
            .Distinct()
            .OrderBy(GetDataCenterName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>Number of <c>WorldTravelSelect</c> tabs of the current logical DC (0 when not merged).</summary>
    public static int GetCurrentWorldTravelTabCount() {
        var current = DalamudApi.PlayerState.IsLoaded ? DalamudApi.PlayerState.CurrentWorld.ValueNullable : null;
        if (current == null) return 0;
        var tabs = GetWorldTravelTabs(GetLogicalDataCenterId(current.Value));
        return tabs.Count > 1 ? tabs.Count : 0;
    }

    /// <summary>
    /// True when the logical DC is a merge of several display DCs (WorldTravelSelect shows one tab per display DC).
    /// Region-agnostic: detected from the World sheet, so future NA/EU/OC merges work without code changes.
    /// </summary>
    public static bool IsMergedDataCenter(uint logicalDcId) => GetWorldTravelTabs(logicalDcId).Count > 1;

    /// <summary>True when the group's display DC belongs to a merged logical DC (its worlds are reachable via World Visit).</summary>
    public static bool IsMergedGroup(DataCenterGroup group) =>
        group.Worlds.Count > 0 && IsMergedDataCenter(GetLogicalDataCenterId(group.Worlds[0]));

    public static bool IsCurrentDataCenterMerged() {
        var current = DalamudApi.PlayerState.IsLoaded ? DalamudApi.PlayerState.CurrentWorld.ValueNullable : null;
        return current != null && IsMergedDataCenter(GetLogicalDataCenterId(current.Value));
    }

    /// <summary>WorldTravelSelect tab index of <paramref name="world"/> in a merged DC, or -1 when the DC has no tabs.</summary>
    public static int GetWorldTravelTab(World world) {
        var tabs = GetWorldTravelTabs(GetLogicalDataCenterId(world));
        return tabs.Count > 1 ? tabs.IndexOf(GetDisplayDataCenterId(world)) : -1;
    }

    private static readonly Dictionary<uint, List<DataCenterGroup>> RegionCache = new();

    /// <summary>
    /// Worlds of <paramref name="region"/> plus Oceania (cross-DC travel targets), grouped by display DC.
    /// Oceania is only added for global regions (CN / KR / TE clients cannot travel there). Region DCs first, Oceania last. Cached per region.
    /// </summary>
    public static List<DataCenterGroup> GetDataCenterTravelGroups(uint region) {
        if (RegionCache.TryGetValue(region, out var cached)) return cached;
        var withOceania = IsGlobalRegion(region);
        return RegionCache[region] = GetPublicWorlds()
            .Where(w => GetRegion(w) is var r && (r == region || (withOceania && r == RegionOceania)))
            .GroupBy(GetDisplayDataCenterId)
            .Select(g => new DataCenterGroup(
                g.Key,
                GetDataCenterName(g.Key),
                g.OrderBy(w => w.Name.ExtractText(), StringComparer.OrdinalIgnoreCase).ToList()))
            .OrderBy(g => g.Worlds[0].DataCenter.ValueNullable?.Region.RowId == RegionOceania && region != RegionOceania ? 1 : 0)
            .ThenBy(g => g.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>Resolves a world by name (exact, then prefix). Case-insensitive.</summary>
    public static bool TryResolve(string name, out World world) {
        world = default;
        if (string.IsNullOrWhiteSpace(name)) return false;
        var needle = name.Trim();
        var worlds = GetPublicWorlds().ToList();

        foreach (var w in worlds) {
            if (!w.Name.ExtractText().Equals(needle, StringComparison.OrdinalIgnoreCase)) continue;
            world = w;
            return true;
        }
        foreach (var w in worlds) {
            if (!w.Name.ExtractText().StartsWith(needle, StringComparison.OrdinalIgnoreCase)) continue;
            world = w;
            return true;
        }
        return false;
    }
}
