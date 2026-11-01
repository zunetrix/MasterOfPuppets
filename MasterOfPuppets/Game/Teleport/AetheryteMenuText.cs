using Lumina.Excel;

namespace MasterOfPuppets;

/// <summary>
/// Localized texts of the aetheryte SelectString menu, read from the quest dialogue sheet
/// <c>transport/Aetheryte</c> (Aethernet = 1, ResidentialDistrict = 2, VisitAnotherWorld = 3).
/// </summary>
internal static class AetheryteMenuText {
    private const string Sheet = "transport/Aetheryte";

    // index fallbacks when the text cannot be matched (main city aetheryte menu order)
    public const int IndexAethernet = 0;
    public const int IndexResidentialDistrict = 1;
    public const int IndexVisitAnotherWorld = 2;

    public static string? Aethernet => Read(1);
    public static string? ResidentialDistrict => Read(2);
    public static string? VisitAnotherWorld => Read(3);

    private static string? Read(uint row) {
        try {
            var sheet = DalamudApi.DataManager.GetExcelSheet<RawRow>(DalamudApi.ClientState.ClientLanguage, Sheet);
            var text = sheet?.GetRowOrDefault(row)?.ReadStringColumn(1).ExtractText().Trim();
            return string.IsNullOrEmpty(text) ? null : text;
        } catch (System.Exception ex) {
            DalamudApi.PluginLog.Debug($"[AetheryteMenuText] failed to read {Sheet}#{row}: {ex.Message}");
            return null;
        }
    }
}
