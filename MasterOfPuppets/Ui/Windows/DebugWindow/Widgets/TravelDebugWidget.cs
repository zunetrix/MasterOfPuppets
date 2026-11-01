using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Text;

using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Utility;

using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;

using Lumina.Excel.Sheets;
using Lumina.Text.ReadOnly;

using MasterOfPuppets.Util.ImGuiExt;

namespace MasterOfPuppets.Debug;

/// <summary>
/// Collects sheet / addon data needed to build the aetheryte travel UI
/// (world grouping by datacenter, aethernet shards, aetheryte menu entries).
/// Every section can be copied as TSV.
/// </summary>
public sealed unsafe class TravelDebugWidget : Widget {
    public override string Title => "Travel";

    private bool _worldsOnlyPublic = true;
    private bool _worldsOnlyCurrentRegion = false;
    private bool _aetherytesOnlyAethernet = true;

    private static readonly string[] AddonNames = [
        "SelectString", "TelepotTown", "TeleportTown", "WorldTravelSelect", "HousingSelectBlock", "SelectYesno",
    ];

    public TravelDebugWidget(WidgetContext ctx) : base(ctx) {
    }

    public override void Draw() {
        if (ImGui.Button("Copy ALL sections")) {
            var sb = new StringBuilder();
            sb.AppendLine("### Context").AppendLine(BuildContext());
            sb.AppendLine("### WorldDCGroupType").AppendLine(BuildDataCenters());
            sb.AppendLine("### World").AppendLine(BuildWorlds());
            sb.AppendLine("### Aetheryte").AppendLine(BuildAetherytes());
            sb.AppendLine("### Nearby Aetherytes").AppendLine(BuildNearbyAetherytes());
            sb.AppendLine("### Addons").AppendLine(BuildAddons());
            ImGui.SetClipboardText(sb.ToString());
        }
        ImGuiUtil.ToolTip("Copies everything as TSV to the clipboard");

        ImGui.Spacing();

        DrawSection("Context", BuildContext);
        DrawSection("Data Centers (WorldDCGroupType)", BuildDataCenters);

        DrawSection("Worlds (World)", BuildWorlds, () => {
            ImGui.Checkbox("Only public##worldsPublic", ref _worldsOnlyPublic);
            ImGui.SameLine();
            ImGui.Checkbox("Only current region##worldsRegion", ref _worldsOnlyCurrentRegion);
        });

        DrawSection("Aetherytes (Aetheryte)", BuildAetherytes, () => {
            ImGui.Checkbox("Only AethernetGroup != 0##aethOnly", ref _aetherytesOnlyAethernet);
        });

        DrawSection("Nearby Aetheryte Objects", BuildNearbyAetherytes);
        DrawSection("Addons (open aetheryte menu first)", BuildAddons);
    }

    private static void DrawSection(string title, Func<string> build, System.Action? options = null) {
        using var id = ImRaii.PushId(title);
        if (!ImGui.CollapsingHeader(title)) return;

        options?.Invoke();

        string text;
        try {
            text = build();
        } catch (Exception ex) {
            text = $"ERROR: {ex}";
        }

        if (ImGui.Button("Copy")) {
            ImGui.SetClipboardText(text);
        }
        ImGui.SameLine();
        ImGui.TextDisabled($"{text.Count(c => c == '\n')} lines");

        using var child = ImRaii.Child("##text", new Vector2(-1, 200), true, ImGuiWindowFlags.HorizontalScrollbar);
        if (child) ImGui.TextUnformatted(text);
    }

    //  Builders

    private static string BuildContext() {
        var sb = new StringBuilder();
        var ps = DalamudApi.PlayerState;
        sb.AppendLine($"ClientLanguage\t{DalamudApi.ClientState.ClientLanguage}");
        sb.AppendLine($"TerritoryType\t{DalamudApi.ClientState.TerritoryType}");
        sb.AppendLine($"PlayerLoaded\t{ps.IsLoaded}");
        if (!ps.IsLoaded) return sb.ToString();

        var cw = ps.CurrentWorld.ValueNullable;
        var hw = ps.HomeWorld.ValueNullable;
        sb.AppendLine($"CurrentWorld\t{cw?.RowId}\t{cw?.Name}\tDC={cw?.DataCenter.RowId}\t{cw?.DataCenter.ValueNullable?.Name}\tRegion={cw?.DataCenter.ValueNullable?.Region.RowId}");
        sb.AppendLine($"HomeWorld\t{hw?.RowId}\t{hw?.Name}\tDC={hw?.DataCenter.RowId}\t{hw?.DataCenter.ValueNullable?.Name}\tRegion={hw?.DataCenter.ValueNullable?.Region.RowId}");

        var territory = DalamudApi.DataManager.GetExcelSheet<TerritoryType>().GetRowOrDefault(DalamudApi.ClientState.TerritoryType);
        sb.AppendLine($"Territory\t{territory?.RowId}\t{territory?.PlaceName.ValueNullable?.Name}\tAetheryte={territory?.Aetheryte.RowId}");
        return sb.ToString();
    }

    private static string BuildDataCenters() {
        var rows = DalamudApi.DataManager.GetExcelSheet<WorldDCGroupType>();
        return DumpRows(rows, _ => true);
    }

    private string BuildWorlds() {
        var regionId = DalamudApi.PlayerState.IsLoaded
            ? DalamudApi.PlayerState.CurrentWorld.ValueNullable?.DataCenter.ValueNullable?.Region.RowId
            : null;

        var rows = DalamudApi.DataManager.GetExcelSheet<World>();
        return DumpRows(rows, w =>
            (!_worldsOnlyPublic || w.IsPublic) &&
            (!_worldsOnlyCurrentRegion || regionId == null || w.DataCenter.ValueNullable?.Region.RowId == regionId),
            extra: w => [("DC.Name", w.DataCenter.ValueNullable?.Name.ExtractText() ?? ""), ("DC.Region", w.DataCenter.ValueNullable?.Region.RowId.ToString() ?? "")]);
    }

    private string BuildAetherytes() {
        var rows = DalamudApi.DataManager.GetExcelSheet<Aetheryte>();
        return DumpRows(rows, a => !_aetherytesOnlyAethernet || a.AethernetGroup != 0,
            extra: a => [
                ("PlaceName.Name", a.PlaceName.ValueNullable?.Name.ExtractText() ?? ""),
                ("AethernetName.Name", a.AethernetName.ValueNullable?.Name.ExtractText() ?? ""),
                ("Territory.Name", a.Territory.ValueNullable?.PlaceName.ValueNullable?.Name.ExtractText() ?? ""),
            ]);
    }

    private static string BuildNearbyAetherytes() {
        var player = DalamudApi.ObjectTable.LocalPlayer;
        if (player == null) return "no local player";

        var sb = new StringBuilder();
        sb.AppendLine("Name\tBaseId\tEntityId\tDistance\tPosition\tSheet.AethernetGroup\tSheet.IsAetheryte\tSheet.PlaceName\tSheet.AethernetName");
        var sheet = DalamudApi.DataManager.GetExcelSheet<Aetheryte>();
        foreach (var obj in DalamudApi.ObjectTable
                     .Where(o => o != null && o.ObjectKind == ObjectKind.Aetheryte)
                     .OrderBy(o => Vector3.Distance(player.Position, o.Position))) {
            var row = sheet.GetRowOrDefault(obj.BaseId);
            sb.AppendLine(string.Join('\t',
                obj.Name.TextValue,
                obj.BaseId,
                obj.EntityId,
                Vector3.Distance(player.Position, obj.Position).ToString("0.00"),
                obj.Position.ToString("0.00"),
                row?.AethernetGroup,
                row?.IsAetheryte,
                row?.PlaceName.ValueNullable?.Name.ExtractText(),
                row?.AethernetName.ValueNullable?.Name.ExtractText()));
        }
        return sb.ToString();
    }

    private static string BuildAddons() {
        var sb = new StringBuilder();

        sb.AppendLine("[WorldTravelSelect StringArray]");
        foreach (var w in GameDialogManager.GetAvailableWorldDestinations()) sb.AppendLine($"\t{w}");

        sb.AppendLine($"[WorldTranslate StringArray raw] mergedDC={WorldCatalog.IsCurrentDataCenterMerged()}");
        var worldTranslate = RaptureAtkModule.Instance()->AtkArrayDataHolder.StringArrays[(int)StringArrayType.WorldTranslate];
        if (worldTranslate != null) {
            for (var i = 0; i < worldTranslate->AtkArrayData.Size; i++) {
                var str = worldTranslate->StringArray[i];
                if (!str.HasValue) continue;
                var text = Dalamud.Memory.MemoryHelper.ReadStringNullTerminated((nint)str.Value).Trim();
                if (text.Length > 0) sb.AppendLine($"	{i}	{text}");
            }
        }

        sb.AppendLine("[TelepotTown NodeList destinations]");
        foreach (var w in GameDialogManager.GetAvailableAethernetDestinations()) sb.AppendLine($"\t{w}");

        foreach (var name in AddonNames) {
            if (!GameDialogManager.TryGetAddonByName<AtkUnitBase>(name, out var addon) || addon == null) {
                sb.AppendLine($"[{name}] not open");
                continue;
            }
            sb.AppendLine($"[{name}] visible={addon->IsVisible} AtkValuesCount={addon->AtkValuesCount}");
            for (var i = 0; i < addon->AtkValuesCount; i++) {
                var v = addon->AtkValues[i];
                sb.AppendLine($"\t{i}\t{v.Type}\t{FormatAtkValue(v)}");
            }
        }
        return sb.ToString();
    }

    //  Helpers

    private static string FormatAtkValue(AtkValue v) {
        return v.Type switch {
            AtkValueType.Int => v.Int.ToString(),
            AtkValueType.UInt => v.UInt.ToString(),
            AtkValueType.Bool => (v.Byte != 0).ToString(),
            AtkValueType.Float => v.Float.ToString("0.###"),
            AtkValueType.String or
            AtkValueType.ConstString or
            AtkValueType.ManagedString or
            AtkValueType.WideString =>
                v.String.Value == null ? "" : new ReadOnlySeStringSpan(v.String.Value).ExtractText().Replace("\n", "\\n"),
            _ => "",
        };
    }

    /// <summary>Dumps every scalar property of a Lumina sheet row as TSV (RowRef → RowId, SeString → text).</summary>
    private static string DumpRows<T>(IEnumerable<T> rows, Func<T, bool> filter, Func<T, (string, string)[]>? extra = null) where T : struct {
        var props = typeof(T)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.GetIndexParameters().Length == 0 && IsDumpable(p.PropertyType))
            .ToArray();

        var sb = new StringBuilder();
        var header = props.Select(p => p.Name).ToList();
        var headerWritten = false;

        foreach (var row in rows) {
            if (!filter(row)) continue;
            var extras = extra?.Invoke(row) ?? [];
            if (!headerWritten) {
                header.AddRange(extras.Select(e => e.Item1));
                sb.AppendLine(string.Join('\t', header));
                headerWritten = true;
            }
            var values = props.Select(p => FormatValue(p.GetValue(row))).Concat(extras.Select(e => e.Item2));
            sb.AppendLine(string.Join('\t', values));
        }
        return sb.ToString();
    }

    private static bool IsDumpable(Type t) {
        if (t.IsPrimitive || t.IsEnum || t == typeof(string) || t == typeof(ReadOnlySeString)) return true;
        return t.IsGenericType && t.Name.StartsWith("RowRef", StringComparison.Ordinal);
    }

    private static string FormatValue(object? value) {
        switch (value) {
            case null: return "";
            case ReadOnlySeString s: return s.ExtractText().Replace("\t", " ").Replace("\n", "\\n");
        }
        var rowIdProp = value.GetType().GetProperty("RowId");
        if (rowIdProp != null) return rowIdProp.GetValue(value)?.ToString() ?? "";
        return value.ToString() ?? "";
    }
}
