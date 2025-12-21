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

    private static Emote? GetEmote(uint id) {
        return DalamudApi.DataManager.Excel.GetSheet<Emote>().GetRowOrDefault(id);
    }

    public static ExecutableAction? GetExecutableAction(uint slotId) {
        var emote = GetEmote(slotId);
        return emote == null ? null : GetExecutableAction(emote.Value);
    }

    public static uint GetIconId(uint item) {
        uint undefinedIcon = 60042;
        return GetEmote(item)?.Icon ?? undefinedIcon;
    }

    private static readonly (string Name, uint Id, uint IconId)[] InternalEmoteData =
    {
        ("Sleep / wake", 88, 246213),
        ("Sleep / wake (Change Pose)", 99, 246213),

        ("Change <Pose 1>", 91, 246268),
        ("Change <Pose 2>", 92, 246268),
        ("Change <Pose 3>", 107, 246268),
        ("Change <Pose 4>", 108, 246268),
        ("Change <Pose 5>", 218, 246268),
        ("Change <Pose 6>", 219, 246268),

        ("Sit on ground <pose 1>", 97, 246254),
        ("Sit on ground <pose 2>", 98, 246254),
        ("Sit on ground <pose 3>", 117, 246254),
        ("Stand up from groundsit", 53, 246261),

        ("Chair Sit <pose 1>", 95, 246256),
        ("Chair Sit <pose 2>", 96, 246256),
        ("Chair Sit <pose 3>", 254, 246256),
        ("Chair Sit <pose 4>", 255, 246256),
        ("Stand up from chairsit", 51, 246261),

        ("Umbrella <Pose 1>", 243, 246477),
        ("Umbrella <Pose 2>", 244, 246477),
        ("Umbrella <Pose 3>", 253, 246477),
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
