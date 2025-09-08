using System.Linq;
using System.Collections.Generic;

namespace MasterOfPuppets;

public static class MopMacroActionsHelper
{
    public static List<MopAction> Actions = new()
    {
        new MopAction
        {
            TextCommand = "/wait <time>",
            SuggestionCommand = "/wait ",
            Example = "/wait 3",
            Notes = "Use to wait a certain amount of time"
        },
        new MopAction
        {
            TextCommand = "/petbarslot <slot number>",
            SuggestionCommand = "/petbarslot ",
            Example = """
            Rain Check:
                /petbarslot 1

            Umbrella Dance:
                /petbarslot 2

            Complete umbrella dance macro example
                /fashion "Fat Cat Parasol"
                /wait 3
                /petbarslot 2
                /wait 13
                /fashion "Fat Cat Parasol"
                /cheer
            """,
            Notes = "Use especial pet hotbat slots like mount specials and parasol actions"
        },
        new MopAction
        {
            TextCommand = "/mopaction <action id> | \"Action Name\"",
            SuggestionCommand = "/mopaction ",
            Example = """
            Peloton:
                /mopaction "Peloton"
                /mopaction 7557

            Umbrella Dance:
                /petbarslot 2
            """,
            Notes = "Similar to native game /action /ac but allows some actions that cant be used with /ac"
        },
        new MopAction
        {
            TextCommand = "/moptarget \"Target Name\"",
            SuggestionCommand = "/moptarget ",
            Example = """
                /moptarget "John Doe"
            """,
            Notes = """
            Useful for targetable emotes
                /moptarget "John Doe"
                /dote
            """
        },
        new MopAction
        {
            TextCommand = "/moptargetof \"Target Name\"",
            SuggestionCommand = "/moptargetof ",
            Example = """
                /moptargetof "John Doe"
            """,
            Notes = """
            Useful for targetable emotes, target someone with main char then for the others use
                /moptargetof "Main Char Name"
                /dote
            """
        },
        new MopAction
        {
            TextCommand = "/moptargetclear",
            SuggestionCommand = "/moptargetclear",
            Example = """
            /moptargetclear
            """,
            Notes = """
            Clear target after emote
                /moptarget "John Doe"
                /dote
                /moptargetclear
            """
        },
        new MopAction
        {
            TextCommand = "/mopitem <item id> | \"Item Name\"",
            SuggestionCommand = "/mopitem ",
            Example = """
            /item "Heavenscracker"
            /item 12042
            """,
            Notes = """
            Use items like fireworks and prisms
            """
        },
        new MopAction
        {
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
        new MopAction
        {
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
        new MopAction
        {
            TextCommand = "/mount \"Mount Name\"",
            SuggestionCommand = "/mount ",
            Example = """
            /mount "company chocobo"
            /mount "behemoth"
            """,
            Notes = """
            Use mounts
            """
        },
        new MopAction
        {
            TextCommand = "/mop run <macro number> \"Macro Name\"",
            SuggestionCommand = "/mop run ",
            Example = """
            /mount "My macro"
            /mount 3
            """,
            Notes = """
            Call it recursively
                /mount "behemoth"
                /mop run "My macro"
            """
        }
    };

    public static List<string> GetSuggestionCommands()
    {
        return Actions
            .Select(a => a.SuggestionCommand)
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Distinct()
            .ToList();
    }
}

public class MopAction
{
    public string TextCommand { get; set; } = string.Empty;
    public string SuggestionCommand { get; set; } = string.Empty;
    public string Example { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
}
