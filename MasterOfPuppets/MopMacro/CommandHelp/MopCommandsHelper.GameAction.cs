using System.Collections.Generic;

namespace MasterOfPuppets;

public static partial class MopCommandsHelper {
    private static List<MopAction> GetGameActions() =>
    [
        new MopAction {
            Category = MopActionCategory.GameAction,
            TextCommand = "/fashion \"Item Name\"",
            SuggestionCommand = "/fashion ",
            Example = """
            /fashion "Fat Cat Parasol"
            /fashion "Archangel Wings"
            """,
            Notes = """
            Equip fashion accessories
            """
        },
        new MopAction {
            Category = MopActionCategory.GameAction,
            TextCommand = "/facewear \"Item Name\"",
            SuggestionCommand = "/facewear ",
            Example = """
            /facewear "Groovy Glasses"
            /facewear "Pince-nez"
            """,
            Notes = """
            Equip glasses
            """
        },
        new MopAction {
            Category = MopActionCategory.GameAction,
            TextCommand = "/mount \"Mount Name\"",
            SuggestionCommand = "/mount ",
            Example = """
            /mount "company chocobo"
            /mount "behemoth"
            """,
            Notes = """
            Summon a mount by name
            """
        },
        new MopAction {
            Category = MopActionCategory.GameAction,
            TextCommand = "/ac \"Skill Name\"",
            SuggestionCommand = "/ac ",
            Example = """
            /ac "Peloton"
            /action "Peloton"
            """,
            Notes = """
            Native game command to use skills and actions in macros (/ac and /action are aliases)
            """
        },
        new MopAction {
            Category = MopActionCategory.GameAction,
            TextCommand = "/gs change <gear set number>",
            SuggestionCommand = "/gs change ",
            Example = """
            /gs change 1
            /gs change 3
            """,
            Notes = """
            Switch gear sets
            """
        },
    ];
}
