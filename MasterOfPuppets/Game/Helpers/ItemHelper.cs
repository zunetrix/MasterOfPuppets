using System;
using System.Collections.Generic;
using System.Linq;

using Lumina.Excel.Sheets;

namespace MasterOfPuppets;

public static class ItemHelper {
    private static ExecutableAction GetExecutableAction(Item item) {
        return new ExecutableAction {
            ActionId = item.RowId,
            ActionName = item.Name.ToString(),
            // ActionName = item.Singular.ToString(),
            IconId = item.Icon,
            // TextCommand = $"/mopitem {item.RowId}",
            TextCommand = $"/mopitem \"{item.Name}\"",
            // item.ItemSearchCategory.Value.Category // category ID
            Category = $"{item.ItemUICategory.Value.Name} ({item.ItemUICategory.Value.RowId})",
            // SortOrder = emote.Order
        };
    }

    public static List<ExecutableAction> GetAllowedItems() {
        List<uint> allowedCategories = [
            (uint)ItemUICategoryEnum.Miscellany,
            (uint)ItemUICategoryEnum.SeasonalMiscellany
        ];

        return DalamudApi.DataManager.GetExcelSheet<Item>()
        .Where(i => i.IsUnlocked() && allowedCategories.Contains(i.ItemUICategory.Value.RowId) && i.Cooldowns > 0)
        .Select(GetExecutableAction)
        .ToList();
    }

    public static Item? GetItem(string itemName) {
        // returns RowId = 0 for invalid names
        var item = DalamudApi.DataManager.GetExcelSheet<Item>(DalamudApi.ClientState.ClientLanguage)
        .FirstOrDefault(i => string.Equals(i.Name.ToString(), itemName, StringComparison.OrdinalIgnoreCase));

        var isItemFound = item.RowId > 0;
        return isItemFound ? item : null;
    }

    public static ExecutableAction? GetExecutableAction(string itemName) {
        var item = GetItem(itemName);
        return item == null ? null : GetExecutableAction(item.Value);
    }

    private static Item? GetItem(uint id) {
        return DalamudApi.DataManager.Excel.GetSheet<Item>().GetRowOrDefault(id);
    }

    public static ExecutableAction? GetExecutableAction(uint slotId) {
        var item = GetItem(slotId);
        return item == null ? null : GetExecutableAction(item.Value);
    }

    public static uint GetIconId(uint item) {
        uint undefinedIcon = 60042;
        return GetItem(item)?.Icon ?? undefinedIcon;
    }

    public enum ItemUICategoryEnum {
        None = 0,
        PugilistsArm = 1,
        GladiatorsArm = 2,
        MaraudersArm = 3,
        ArchersArm = 4,
        LancersArm = 5,
        OneHandedThaumaturgesArm = 6,
        TwoHandedThaumaturgesArm = 7,
        OneHandedConjurersArm = 8,
        TwoHandedConjurersArm = 9,
        ArcanistsGrimoire = 10,
        Shield = 11,
        CarpentersPrimaryTool = 12,
        CarpentersSecondaryTool = 13,
        BlacksmithsPrimaryTool = 14,
        BlacksmithsSecondaryTool = 15,
        ArmorersPrimaryTool = 16,
        ArmorersSecondaryTool = 17,
        GoldsmithsPrimaryTool = 18,
        GoldsmithsSecondaryTool = 19,
        LeatherworkersPrimaryTool = 20,
        LeatherworkersSecondaryTool = 21,
        WeaversPrimaryTool = 22,
        WeaversSecondaryTool = 23,
        AlchemistsPrimaryTool = 24,
        AlchemistsSecondaryTool = 25,
        CulinariansPrimaryTool = 26,
        CulinariansSecondaryTool = 27,
        MinersPrimaryTool = 28,
        MinersSecondaryTool = 29,
        BotanistsPrimaryTool = 30,
        BotanistsSecondaryTool = 31,
        FishersPrimaryTool = 32,
        FishingTackle = 33,
        Head = 34,
        Body = 35,
        Legs = 36,
        Hands = 37,
        Feet = 38,
        Unobtainable = 39,
        Necklace = 40,
        Earrings = 41,
        Bracelets = 42,
        Ring = 43,
        Medicine = 44,
        Ingredient = 45,
        Meal = 46,
        Seafood = 47,
        Stone = 48,
        Metal = 49,
        Lumber = 50,
        Cloth = 51,
        Leather = 52,
        Bone = 53,
        Reagent = 54,
        Dye = 55,
        Part = 56,
        Furnishing = 57,
        Materia = 58,
        Crystal = 59,
        Catalyst = 60,
        Miscellany = 61,
        SoulCrystal = 62,
        Other = 63,
        ConstructionPermit = 64,
        Roof = 65,
        ExteriorWall = 66,
        Window = 67,
        Door = 68,
        RoofDecoration = 69,
        ExteriorWallDecoration = 70,
        Placard = 71,
        Fence = 72,
        InteriorWall = 73,
        Flooring = 74,
        CeilingLight = 75,
        OutdoorFurnishing = 76,
        Table = 77,
        Tabletop = 78,
        WallMounted = 79,
        Rug = 80,
        Minion = 81,
        Gardening = 82,
        Demimateria = 83,
        RoguesArm = 84,
        SeasonalMiscellany = 85,
        TripleTriadCard = 86,
        DarkKnightsArm = 87,
        MachinistsArm = 88,
        AstrologiansArm = 89,
        AirshipHull = 90,
        AirshipRigging = 91,
        AirshipAftcastle = 92,
        AirshipForecastle = 93,
        OrchestrionRoll = 94,
        Painting = 95,
        SamuraisArm = 96,
        RedMagesArm = 97,
        ScholarsArm = 98,
        FishersSecondaryTool = 99,
        Currency = 100,
        SubmersibleHull = 101,
        SubmersibleStern = 102,
        SubmersibleBow = 103,
        SubmersibleBridge = 104,
        BlueMagesArm = 105,
        GunbreakersArm = 106,
        DancersArm = 107,
        ReapersArm = 108,
        SagesArm = 109,
        VipersArm = 110,
        PictomancersArm = 111,
        Outfits = 112
    }
}
