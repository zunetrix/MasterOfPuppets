using System.Collections.Generic;
using System.Linq;

using Lumina.Excel.Sheets;

namespace MasterOfPuppets;

public static class EmoteHelper {
    private static ExecutableAction GetExecutableAction(Emote emote) {
        return new ExecutableAction {
            ActionId = emote.RowId,
            ActionName = emote.Name.ToString(),
            IconId = emote.Icon,
            TextCommand = $"{emote.TextCommand.Value.Command}",
            // TextCommandAlias = emote.TextCommand.Value.Alias.ToList(),
            Category = emote.EmoteCategory.ValueNullable?.Name.ToString() ?? null,
            // SortOrder = emote.Order
        };
    }

    public static List<ExecutableAction> GetAllowedItems() {
        var unlockedEmotes = DalamudApi.DataManager.GetExcelSheet<Emote>()
            .Where(e => e.IsUnlocked())
            .Select(GetExecutableAction);

        var internalEmotes = GenerateInternalEmotesList();

        return unlockedEmotes
            .Concat(internalEmotes)
            .ToList();
    }

    private static Emote? GetEmoteById(uint id) {
        return DalamudApi.DataManager.Excel.GetSheet<Emote>().GetRowOrDefault(id);
    }

    public static ExecutableAction? GetExecutableActionById(uint slotId) {
        var emote = GetEmoteById(slotId);
        return emote == null ? null : GetExecutableAction(emote.Value);
    }

    public static uint GetIconId(uint item) {
        uint undefinedIcon = 60042;
        return GetEmoteById(item)?.Icon ?? undefinedIcon;
    }

    private static readonly (string Name, uint Id, uint IconId)[] InternalEmoteData =
    {
        ("Sleep / wake", 88, 64013),
        ("Sleep / wake (Change Pose)", 99, 64013),

        ("Change <Pose 1>", 91, 64068),
        ("Change <Pose 2>", 92, 64068),
        ("Change <Pose 3>", 107, 64068),
        ("Change <Pose 4>", 108, 64068),
        ("Change <Pose 5>", 218, 64068),
        ("Change <Pose 6>", 219, 64068),

        ("Sit on ground <pose 1>", 97, 64054),
        ("Sit on ground <pose 2>", 98, 64054),
        ("Sit on ground <pose 3>", 117, 64054),
        ("Stand up from groundsit", 53, 64061),

        ("Chair Sit <pose 1>", 95, 64056),
        ("Chair Sit <pose 2>", 96, 64056),
        ("Chair Sit <pose 3>", 254, 64056),
        ("Chair Sit <pose 4>", 255, 64056),
        ("Stand up from chairsit", 51, 64061),

        ("Umbrella <Pose 1>", 243, 64277),
        ("Umbrella <Pose 2>", 244, 64277),
        ("Umbrella <Pose 3>", 253, 64277),
    };

    private static List<ExecutableAction> GenerateInternalEmotesList() {
        return InternalEmoteData
            .Select(e => new ExecutableAction {
                ActionId = e.Id,
                ActionName = e.Name,
                IconId = e.IconId,
                TextCommand = $"/mopbr /mophotbaremote {e.Id}",
                Category = "InternalEmote",
            })
            .ToList();
    }
}
