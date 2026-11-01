using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.Types;

using Lumina.Excel.Sheets;

namespace MasterOfPuppets;

/// <summary>
/// Aetheryte / aethernet lookups (Aetheryte sheet grouped by <c>AethernetGroup</c>).
/// </summary>
internal static class AethernetCatalog {

    // 2 = New Gridania, 8 = Limsa Lominsa, 9 = Ul'dah, 70 = Foundation (Ishgard), 111 = Kugane
    private static readonly uint[] WorldVisitAetherytes = [2, 8, 9];
    private static readonly uint[] ResidentialAetherytes = [2, 8, 9, 70, 111];

    // GetValidAetheryte: 2D < 11 && 3D < 15
    private const float MaxDistance2D = 11f;
    private const float MaxDistance3D = 15f;

    internal readonly record struct AethernetEntry(uint RowId, string Name, bool IsMaster);

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
        var territory = DalamudApi.DataManager.GetExcelSheet<TerritoryType>().GetRowOrDefault(DalamudApi.ClientState.TerritoryType);
        return GetGroup(territory?.Aetheryte.ValueNullable?.AethernetGroup ?? 0);
    }

    /// <summary>
    /// True when <paramref name="aetheryte"/> can be interacted with from <paramref name="playerPosition"/>.
    /// Shared by the overlay and the travel managers (the aetheryte crystal sits high, so a plain 3D
    /// distance check overestimates the range).
    /// </summary>
    public static bool IsInInteractRange(Vector3 playerPosition, IGameObject aetheryte) =>
        Vector2.Distance(new(playerPosition.X, playerPosition.Z), new(aetheryte.Position.X, aetheryte.Position.Z)) < MaxDistance2D
        && Vector3.Distance(playerPosition, aetheryte.Position) < MaxDistance3D;

    /// <summary>Nearest targetable aetheryte object within interaction range, or null.</summary>
    public static IGameObject? GetNearbyAetheryteObject() {
        var player = DalamudApi.ObjectTable.LocalPlayer;
        if (player == null) return null;

        return DalamudApi.ObjectTable
            .Where(o => o != null && o.ObjectKind == ObjectKind.Aetheryte && o.IsTargetable)
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
