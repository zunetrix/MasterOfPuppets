using System;
using System.Collections.Generic;

using Dalamud.Game;
using Dalamud.Utility;

using FFXIVClientStructs.FFXIV.Client.Game.Object;

using Lumina.Excel;
using Lumina.Excel.Sheets;
using Lumina.Text.ReadOnly;

using DObjectKind = Dalamud.Game.ClientState.Objects.Enums.ObjectKind;

namespace MasterOfPuppets.Debug;

public static class TextService {
    private static readonly Dictionary<(Type, uint, ClientLanguage), string> _rowNameCache = [];

    public static string GetAddonText(uint id, ClientLanguage? language = null)
        => GetOrCreateCachedText<Addon>(id, language, (row) => row.Text);

    public static string GetLobbyText(uint id, ClientLanguage? language = null)
        => GetOrCreateCachedText<Lobby>(id, language, (row) => row.Text);

    public static string GetLogMessage(uint id, ClientLanguage? language = null)
        => GetOrCreateCachedText<LogMessage>(id, language, (row) => row.Text);

    public static ReadOnlySeString GetItemName(uint itemId, ClientLanguage? language = null)
        => GetItemName(itemId, false, language);

    public static ReadOnlySeString GetItemName(uint itemId, bool includeIcon, ClientLanguage? language = null)
        => ItemUtil.GetItemName(itemId, includeIcon, language ?? DalamudApi.ClientState.ClientLanguage);

    public static string GetStainName(uint id, ClientLanguage? language = null)
        => GetOrCreateCachedText<Stain>(id, language, (row) => row.Name);

    public static string GetQuestName(uint id, ClientLanguage? language = null)
        => GetOrCreateCachedText<Quest>(id, language, (row) => row.Name);

    public static string GetLeveName(uint id, ClientLanguage? language = null)
        => GetOrCreateCachedText<Leve>(id, language, (row) => row.Name);

    public static string GetTraitName(uint id, ClientLanguage? language = null)
        => GetOrCreateCachedText<Trait>(id, language, (row) => row.Name);

    public static string GetActionName(uint id, ClientLanguage? language = null)
        => GetOrCreateCachedText<Lumina.Excel.Sheets.Action>(id, language, (row) => row.Name);

    public static string GetEmoteName(uint id, ClientLanguage? language = null)
        => GetOrCreateCachedText<Emote>(id, language, (row) => row.Name);

    public static string GetEventActionName(uint id, ClientLanguage? language = null)
        => GetOrCreateCachedText<EventAction>(id, language, (row) => row.Name);

    public static string GetGeneralActionName(uint id, ClientLanguage? language = null)
        => GetOrCreateCachedText<GeneralAction>(id, language, (row) => row.Name);

    public static string GetBuddyActionName(uint id, ClientLanguage? language = null)
        => GetOrCreateCachedText<BuddyAction>(id, language, (row) => row.Name);

    public static string GetMainCommandName(uint id, ClientLanguage? language = null)
        => GetOrCreateCachedText<MainCommand>(id, language, (row) => row.Name);

    public static string GetCraftActionName(uint id, ClientLanguage? language = null)
        => GetOrCreateCachedText<CraftAction>(id, language, (row) => row.Name);

    public static string GetPetActionName(uint id, ClientLanguage? language = null)
        => GetOrCreateCachedText<PetAction>(id, language, (row) => row.Name);

    public static string GetCompanyActionName(uint id, ClientLanguage? language = null)
        => GetOrCreateCachedText<CompanyAction>(id, language, (row) => row.Name);

    public static string GetMarkerName(uint id, ClientLanguage? language = null)
        => GetOrCreateCachedText<Marker>(id, language, (row) => row.Name);

    public static string GetFieldMarkerName(uint id, ClientLanguage? language = null)
        => GetOrCreateCachedText<FieldMarker>(id, language, (row) => row.Name);

    public static string GetChocoboRaceAbilityName(uint id, ClientLanguage? language = null)
        => GetOrCreateCachedText<ChocoboRaceAbility>(id, language, (row) => row.Name);

    public static string GetChocoboRaceItemName(uint id, ClientLanguage? language = null)
        => GetOrCreateCachedText<ChocoboRaceItem>(id, language, (row) => row.Name);

    public static string GetExtraCommandName(uint id, ClientLanguage? language = null)
        => GetOrCreateCachedText<ExtraCommand>(id, language, (row) => row.Name);

    public static string GetQuickChatName(uint id, ClientLanguage? language = null)
        => GetOrCreateCachedText<QuickChat>(id, language, (row) => row.NameAction);

    public static string GetActionComboRouteName(uint id, ClientLanguage? language = null)
        => GetOrCreateCachedText<ActionComboRoute>(id, language, (row) => row.Name);

    public static string GetBgcArmyActionName(uint id, ClientLanguage? language = null)
        => GetOrCreateCachedText<BgcArmyAction>(id, language, (row) => row.Name);

    public static string GetPerformanceInstrumentName(uint id, ClientLanguage? language = null)
        => GetOrCreateCachedText<Perform>(id, language, (row) => row.Instrument);

    public static string GetMcGuffinName(uint id, ClientLanguage? language = null) {
        return GetOrCreateCachedText<McGuffin>(id, language, GetMcGuffinUIName);

        static ReadOnlySeString GetMcGuffinUIName(McGuffin mcGuffinRow)
            => ExcelService.TryGetRow<McGuffinUIData>(mcGuffinRow.UIData.RowId, out var mcGuffinUIDataRow)
                ? mcGuffinUIDataRow.Name
                : default;
    }

    public static string GetGlassesName(uint id, ClientLanguage? language = null)
        => GetOrCreateCachedText<Glasses>(id, language, (row) => row.Name);

    public static string GetOrnamentName(uint id, ClientLanguage? language = null)
        => GetOrCreateCachedText<Ornament>(id, language, (row) => row.Singular);

    public static string GetMountName(uint id, ClientLanguage? language = null)
        => GetOrCreateCachedText<Mount>(id, language, (row) => row.Singular);

    public static string GetPlaceName(uint id, ClientLanguage? language = null)
        => GetOrCreateCachedText<PlaceName>(id, language, (row) => row.Name);

    public static string GetFateName(uint id, ClientLanguage? language = null)
        => GetOrCreateCachedText<Fate>(id, language, (row) => DalamudApi.SeStringEvaluator.Evaluate(row.Name, default, language));

    public static string GetBNpcName(uint id, ClientLanguage? language = null)
        => FromObjStr(ObjectKind.BattleNpc, id, language);

    public static string GetENpcResidentName(uint id, ClientLanguage? language = null)
        => FromObjStr(ObjectKind.EventNpc, id, language);

    public static string GetTreasureName(uint id, ClientLanguage? language = null)
        => FromObjStr(ObjectKind.Treasure, id, language);

    public static string GetGatheringPointName(uint id, ClientLanguage? language = null)
        => FromObjStr(ObjectKind.GatheringPoint, id, language);

    public static string GetEObjName(uint id, ClientLanguage? language = null)
        => FromObjStr(ObjectKind.EventObj, id, language);

    public static string GetCompanionName(uint id, ClientLanguage? language = null)
        => FromObjStr(ObjectKind.Companion, id, language);

    private static string FromObjStr(ObjectKind objectKind, uint id, ClientLanguage? language = null)
        => DalamudApi.SeStringEvaluator.EvaluateFromAddon(2025, [((DObjectKind)objectKind).GetObjStrId(id)], language).ToString();

    private static string GetOrCreateCachedText<T>(uint rowId, ClientLanguage? language, Func<T, ReadOnlySeString> getText) where T : struct, IExcelRow<T> {
        var lang = language ?? DalamudApi.ClientState.ClientLanguage;
        var key = (typeof(T), rowId, lang);

        if (_rowNameCache.TryGetValue(key, out var text))
            return text;

        if (!ExcelService.TryGetRow<T>(rowId, lang, out var row)) {
            _rowNameCache.Add(key, text = $"{typeof(T).Name}#{rowId}");
            return text;
        }

        var tempText = getText(row);
        _rowNameCache.Add(key, text = tempText.IsEmpty ? $"{typeof(T).Name}#{rowId}" : tempText.ToString().FirstCharToUpper());
        return text;
    }
}
