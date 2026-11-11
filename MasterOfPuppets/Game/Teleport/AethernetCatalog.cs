using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.Types;

using Lumina.Excel.Sheets;

namespace MasterOfPuppets;

/// <summary>
/// Aetheryte / aethernet lookups.
/// <para>Cities: <c>Aetheryte</c> sheet grouped by <c>AethernetGroup</c>.</para>
/// <para>Residential areas: <c>HousingAethernet</c> sheet per territory.</para>
/// </summary>
internal static class AethernetCatalog {

    // 2 = New Gridania, 8 = Limsa Lominsa, 9 = Ul'dah, 70 = Foundation (Ishgard), 111 = Kugane
    private static readonly uint[] WorldVisitAetherytes = [2, 8, 9];
    private static readonly uint[] ResidentialAetherytes = [2, 8, 9, 70, 111];

    // GetValidAetheryte: 2D < 11 && 3D < 15
    private const float MaxDistance2D = 11f;
    private const float MaxDistance3D = 15f;

    // residential shards sit close to each other: tighter range so the right one is used (Lifestream: 3D > 4.6 skipped)
    private const float MaxResidentialDistance3D = 4.6f;

    /// <summary>
    /// <paramref name="RowId"/> is an <c>Aetheryte</c> row, or a <c>HousingAethernet</c> row when <paramref name="IsHousing"/>.
    /// </summary>
    internal readonly record struct AethernetEntry(uint RowId, string Name, bool IsMaster, bool IsHousing = false, bool IsSubdivision = false);

    /// <summary>Information about the aetheryte the player is standing at.</summary>
    internal readonly record struct AetheryteContext(
        IGameObject Object,
        uint CurrentRowId,
        uint MasterRowId,
        bool CanWorldVisit,
        bool CanResidential,
        IReadOnlyList<AethernetEntry> Aethernet);

    public static string GetName(Aetheryte row) {
        var name = row.AethernetName.ValueNullable?.Name.ExtractText();
        if (string.IsNullOrWhiteSpace(name)) name = row.PlaceName.ValueNullable?.Name.ExtractText();
        return name?.Trim() ?? $"#{row.RowId}";
    }

    private static readonly Dictionary<byte, List<AethernetEntry>> GroupCache = new();

    /// <summary>All aetherytes sharing <paramref name="group"/>, master first. Cached per group.</summary>
    public static List<AethernetEntry> GetGroup(byte group) {
        if (group == 0) return [];
        if (GroupCache.TryGetValue(group, out var cached)) return cached;
        return GroupCache[group] = DalamudApi.DataManager.GetExcelSheet<Aetheryte>()
            .Where(a => a.AethernetGroup == group)
            .OrderByDescending(a => a.IsAetheryte)
            .ThenBy(a => a.RowId)
            .Select(a => new AethernetEntry(a.RowId, GetName(a), a.IsAetheryte))
            .Where(e => !string.IsNullOrEmpty(e.Name))
            .ToList();
    }

    /// <summary>Aethernet of the current territory's city (used when not standing at an aetheryte).</summary>
    public static List<AethernetEntry> GetGroupForCurrentTerritory() {
        var territoryId = DalamudApi.ClientState.TerritoryType;
        if (IsResidentialZone(territoryId)) return GetResidentialAethernet(territoryId);
        var territory = DalamudApi.DataManager.GetExcelSheet<TerritoryType>().GetRowOrDefault(territoryId);
        return GetGroup(territory?.Aetheryte.ValueNullable?.AethernetGroup ?? 0);
    }

    // residential aethernet
    private static readonly Dictionary<uint, List<AethernetEntry>> ResidentialCache = new();

    /// <summary>
    /// Aethernet shards of a residential territory (Mist, Lavender Beds, Goblet, Shirogane, Empyreum), by <c>Order</c>.
    /// The second half of the list is the subdivision (Order >= count / 2). Empty for other territories. Cached.
    /// </summary>
    public static List<AethernetEntry> GetResidentialAethernet(uint territoryId) {
        if (ResidentialCache.TryGetValue(territoryId, out var cached)) return cached;
        var rows = DalamudApi.DataManager.GetExcelSheet<HousingAethernet>()
            .Where(a => a.TerritoryType.RowId == territoryId)
            .OrderBy(a => a.Order)
            .ToList();
        var half = rows.Count / 2;
        return ResidentialCache[territoryId] = rows
            .Select(a => new AethernetEntry(a.RowId, a.PlaceName.ValueNullable?.Name.ExtractText().Trim() ?? string.Empty, false, IsHousing: true, IsSubdivision: a.Order >= half))
            .Where(e => !string.IsNullOrEmpty(e.Name))
            .ToList();
    }

    public static bool IsResidentialZone(uint territoryId) => GetResidentialAethernet(territoryId).Count > 0;

    public static bool IsInResidentialZone() => IsResidentialZone(DalamudApi.ClientState.TerritoryType);

    // shard objects
    // EObjName rows sharing the singular name of: 2000151 "Aethernet shard", 2014665 "aetheryte shard", 2003395 housing-area shard (KR)
    private static readonly uint[] ShardNameRows = [2000151, 2014665, 2003395];
    private static HashSet<uint>? _shardObjectIds;

    private static HashSet<uint> ShardObjectIds {
        get {
            if (_shardObjectIds != null) return _shardObjectIds;
            var sheet = DalamudApi.DataManager.GetExcelSheet<EObjName>();
            var names = ShardNameRows
                .Select(id => sheet.GetRowOrDefault(id)?.Singular.ExtractText())
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            return _shardObjectIds = sheet
                .Where(r => names.Contains(r.Singular.ExtractText()))
                .Select(r => r.RowId)
                .ToHashSet();
        }
    }

    /// <summary>Aetheryte, or an aethernet shard event object (residential shards may not be <c>ObjectKind.Aetheryte</c>).</summary>
    public static bool IsAethernetObject(IGameObject? obj) =>
        obj != null && (obj.ObjectKind == ObjectKind.Aetheryte
            || (obj.ObjectKind == ObjectKind.EventObj && ShardObjectIds.Contains(obj.BaseId)));

    /// <summary>
    /// True when <paramref name="aetheryte"/> can be interacted with from <paramref name="playerPosition"/>.
    /// Shared by the overlay and the travel managers (the aetheryte crystal sits high, so a plain 3D
    /// distance check overestimates the range). Tighter in residential areas.
    /// </summary>
    public static bool IsInInteractRange(Vector3 playerPosition, IGameObject aetheryte) {
        var d3d = Vector3.Distance(playerPosition, aetheryte.Position);
        if (IsInResidentialZone() && d3d > MaxResidentialDistance3D) return false;
        return Vector2.Distance(new(playerPosition.X, playerPosition.Z), new(aetheryte.Position.X, aetheryte.Position.Z)) < MaxDistance2D
            && d3d < MaxDistance3D;
    }

    /// <summary>Nearest aetheryte / aethernet shard object, or null.</summary>
    public static IGameObject? GetNearestAethernetObject() {
        var player = DalamudApi.ObjectTable.LocalPlayer;
        if (player == null) return null;

        return DalamudApi.ObjectTable
            .Where(o => IsAethernetObject(o) && o.IsTargetable)
            .OrderBy(o => Vector3.DistanceSquared(player.Position, o.Position))
            .FirstOrDefault();
    }

    /// <summary>Nearest targetable aetheryte object within interaction range, or null.</summary>
    public static IGameObject? GetNearbyAetheryteObject() {
        var player = DalamudApi.ObjectTable.LocalPlayer;
        if (player == null) return null;

        return DalamudApi.ObjectTable
            .Where(o => IsAethernetObject(o) && o.IsTargetable)
            .Where(o => IsInInteractRange(player.Position, o))
            .OrderBy(o => Vector3.DistanceSquared(player.Position, o.Position))
            .FirstOrDefault();
    }

    /// <summary>
    /// Builds the travel context for the aetheryte in range. The sheet row is resolved by the object's BaseId;
    /// if that fails it falls back to the territory's aetheryte (same aethernet group).
    /// </summary>
    public static bool TryGetContext(out AetheryteContext context) {
        context = default;
        var obj = GetNearbyAetheryteObject();
        if (obj == null) return false;

        // residential area: HousingAethernet list, no world visit / ward menus from shards (current shard not resolved)
        var territoryId = DalamudApi.ClientState.TerritoryType;
        if (IsResidentialZone(territoryId)) {
            context = new AetheryteContext(obj, 0, 0, CanWorldVisit: false, CanResidential: false, Aethernet: GetResidentialAethernet(territoryId));
            return true;
        }

        var sheet = DalamudApi.DataManager.GetExcelSheet<Aetheryte>();
        var row = sheet.GetRowOrDefault(obj.BaseId);
        var currentRowId = row?.RowId ?? 0;

        if (row == null || row.Value.AethernetGroup == 0) {
            var territory = DalamudApi.DataManager.GetExcelSheet<TerritoryType>().GetRowOrDefault(DalamudApi.ClientState.TerritoryType);
            row = territory?.Aetheryte.ValueNullable;
        }

        var group = row?.AethernetGroup ?? 0;
        var aethernet = GetGroup(group);
        var master = aethernet.FirstOrDefault(e => e.IsMaster);
        var masterRowId = master.RowId != 0 ? master.RowId : row?.RowId ?? 0;

        // world visit / residential menus are only offered by the main city aetheryte itself
        var atMaster = currentRowId == 0 ? row?.IsAetheryte == true : currentRowId == masterRowId;

        context = new AetheryteContext(
            obj,
            currentRowId,
            masterRowId,
            CanWorldVisit: atMaster && WorldVisitAetherytes.Contains(masterRowId),
            CanResidential: atMaster && ResidentialAetherytes.Contains(masterRowId),
            Aethernet: aethernet);
        return true;
    }

    /// <summary>
    /// Resolves an aethernet destination name (exact, then contains): residential shards of the current territory first, then city aethernet.
    /// <paramref name="aetheryteRowId"/> is the <c>Aetheryte</c> row, 0 for residential shards.
    /// </summary>
    public static bool TryResolveDestination(string name, out string resolvedName, out uint aetheryteRowId) {
        resolvedName = name?.Trim() ?? string.Empty;
        aetheryteRowId = 0;
        if (string.IsNullOrWhiteSpace(name)) return false;

        var needle = resolvedName;
        var housing = GetResidentialAethernet(DalamudApi.ClientState.TerritoryType);
        var match = housing.FirstOrDefault(e => e.Name.Equals(needle, StringComparison.OrdinalIgnoreCase));
        if (match.Name == null) match = housing.FirstOrDefault(e => e.Name.Contains(needle, StringComparison.OrdinalIgnoreCase));
        if (match.Name != null) {
            resolvedName = match.Name;
            return true;
        }

        if (!TryResolve(name, out var row)) return false;
        resolvedName = GetName(row);
        aetheryteRowId = row.RowId;
        return true;
    }

    /// <summary>Resolves an aethernet destination by name in the current client language (exact, then contains).</summary>
    public static bool TryResolve(string name, out Aetheryte aetheryte) {
        aetheryte = default;
        if (string.IsNullOrWhiteSpace(name)) return false;
        var needle = name.Trim();
        var rows = DalamudApi.DataManager.GetExcelSheet<Aetheryte>().Where(a => a.AethernetGroup != 0).ToList();

        foreach (var a in rows) {
            if (!GetName(a).Equals(needle, StringComparison.OrdinalIgnoreCase)) continue;
            aetheryte = a;
            return true;
        }
        foreach (var a in rows) {
            if (!GetName(a).Contains(needle, StringComparison.OrdinalIgnoreCase)) continue;
            aetheryte = a;
            return true;
        }
        return false;
    }
}
