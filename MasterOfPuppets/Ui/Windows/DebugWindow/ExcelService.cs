using System;
using System.Collections.Generic;
using System.Linq;

using Dalamud.Game;
using Dalamud.Utility;

using Lumina.Excel;
using Lumina.Extensions;

namespace MasterOfPuppets.Debug;

public static class ExcelService {
    public static bool HasSheet(string name)
        => DalamudApi.DataManager.Excel.SheetNames.Contains(name);

    public static int GetRowCount<T>() where T : struct, IExcelRow<T>
        => GetSheet<T>().Count;

    // Normal Sheets

    public static RowRef<T> CreateRowRef<T>(uint rowId, ClientLanguage? language = null) where T : struct, IExcelRow<T>
        => new(DalamudApi.DataManager.Excel, rowId, (language ?? DalamudApi.ClientState.ClientLanguage).ToLumina());

    public static ExcelSheet<T> GetSheet<T>(ClientLanguage? language = null) where T : struct, IExcelRow<T>
        => DalamudApi.DataManager.GetExcelSheet<T>(language ?? DalamudApi.ClientState.ClientLanguage)!;

    public static ExcelSheet<T> GetSheet<T>(string sheetName, ClientLanguage? language = null) where T : struct, IExcelRow<T>
        => DalamudApi.DataManager.GetExcelSheet<T>(language ?? DalamudApi.ClientState.ClientLanguage, sheetName)!;

    public static bool TryGetRow<T>(uint rowId, out T row) where T : struct, IExcelRow<T>
        => TryGetRow(rowId, null, out row);

    public static bool TryGetRow<T>(uint rowId, ClientLanguage? language, out T row) where T : struct, IExcelRow<T>
        => GetSheet<T>(language ?? DalamudApi.ClientState.ClientLanguage).TryGetRow(rowId, out row);

    public static bool TryGetRow<T>(string sheetName, uint rowId, out T row) where T : struct, IExcelRow<T>
        => TryGetRow(sheetName, rowId, null, out row);

    public static bool TryGetRow<T>(string sheetName, uint rowId, ClientLanguage? language, out T row) where T : struct, IExcelRow<T>
        => GetSheet<T>(sheetName, language ?? DalamudApi.ClientState.ClientLanguage).TryGetRow(rowId, out row);

    public static bool TryFindRow<T>(string sheetName, Predicate<T> predicate, out T row) where T : struct, IExcelRow<T>
        => TryFindRow(sheetName, predicate, null, out row);

    public static bool TryFindRow<T>(string sheetName, Predicate<T> predicate, ClientLanguage? language, out T row) where T : struct, IExcelRow<T>
        => GetSheet<T>(sheetName, language ?? DalamudApi.ClientState.ClientLanguage).TryGetFirst(predicate, out row);

    public static bool TryFindRow<T>(Predicate<T> predicate, out T row) where T : struct, IExcelRow<T>
        => TryFindRow(predicate, null, out row);

    public static bool TryFindRow<T>(Predicate<T> predicate, ClientLanguage? language, out T row) where T : struct, IExcelRow<T>
        => GetSheet<T>(language ?? DalamudApi.ClientState.ClientLanguage).TryGetFirst(predicate, out row);

    public static IReadOnlyList<T> FindRows<T>(Predicate<T> predicate, ClientLanguage? language = null) where T : struct, IExcelRow<T>
        => [.. GetSheet<T>(language ?? DalamudApi.ClientState.ClientLanguage).Where(row => predicate(row))];

    public static bool TryFindRows<T>(Predicate<T> predicate, out IReadOnlyList<T> rows) where T : struct, IExcelRow<T>
        => TryFindRows(predicate, null, out rows);

    public static bool TryFindRows<T>(Predicate<T> predicate, ClientLanguage? language, out IReadOnlyList<T> rows) where T : struct, IExcelRow<T> {
        rows = [.. GetSheet<T>(language).Where(row => predicate(row))];
        return rows.Count != 0;
    }

    // Subrow Sheets

    public static SubrowRef<T> CreateSubrowRef<T>(uint rowId, ClientLanguage? language = null) where T : struct, IExcelSubrow<T>
        => new(DalamudApi.DataManager.Excel, rowId, (language ?? DalamudApi.ClientState.ClientLanguage).ToLumina());

    public static SubrowExcelSheet<T> GetSubrowSheet<T>(ClientLanguage? language = null) where T : struct, IExcelSubrow<T>
        => DalamudApi.DataManager.GetSubrowExcelSheet<T>(language ?? DalamudApi.ClientState.ClientLanguage)!;

    public static SubrowExcelSheet<T> GetSubrowSheet<T>(string sheetName, ClientLanguage? language = null) where T : struct, IExcelSubrow<T>
        => DalamudApi.DataManager.GetSubrowExcelSheet<T>(language ?? DalamudApi.ClientState.ClientLanguage, sheetName)!;

    public static bool TryGetSubrows<T>(uint rowId, out SubrowCollection<T> rows) where T : struct, IExcelSubrow<T>
        => TryGetSubrows(rowId, null, out rows);

    public static bool TryGetSubrows<T>(uint rowId, ClientLanguage? language, out SubrowCollection<T> rows) where T : struct, IExcelSubrow<T>
        => GetSubrowSheet<T>(language ?? DalamudApi.ClientState.ClientLanguage).TryGetRow(rowId, out rows);

    public static bool TryGetSubrow<T>(uint rowId, int subRowIndex, out T row) where T : struct, IExcelSubrow<T>
        => TryGetSubrow(rowId, subRowIndex, null, out row);

    public static bool TryGetSubrow<T>(uint rowId, int subRowIndex, ClientLanguage? language, out T row) where T : struct, IExcelSubrow<T> {
        if (!GetSubrowSheet<T>(language ?? DalamudApi.ClientState.ClientLanguage).TryGetRow(rowId, out var rows) || subRowIndex < rows.Count) {
            row = default;
            return false;
        }

        row = rows[subRowIndex];
        return true;
    }

    public static bool TryFindSubrow<T>(Predicate<T> predicate, out T subrow) where T : struct, IExcelSubrow<T>
        => TryFindSubrow(predicate, null, out subrow);

    public static bool TryFindSubrow<T>(Predicate<T> predicate, ClientLanguage? language, out T subrow) where T : struct, IExcelSubrow<T> {
        foreach (var irow in GetSubrowSheet<T>(language ?? DalamudApi.ClientState.ClientLanguage)) {
            foreach (var isubrow in irow) {
                if (predicate(isubrow)) {
                    subrow = isubrow;
                    return true;
                }
            }
        }

        subrow = default;
        return false;
    }

    // RawRow
    public static bool TryGetRawRow(string sheetName, uint rowId, out RawRow rawRow)
        => TryGetRow(sheetName, rowId, out rawRow);

    public static bool TryGetRawRow(string sheetName, uint rowId, ClientLanguage? language, out RawRow rawRow)
        => TryGetRow(sheetName, rowId, language, out rawRow);
}
