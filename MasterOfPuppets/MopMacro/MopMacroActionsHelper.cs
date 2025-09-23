using System.Linq;
using System.Collections.Generic;

namespace MasterOfPuppets;

public static class MopMacroActionsHelper
{
    public static List<MopAction> Actions = new()
    {
        new MopAction
        {
            Category = MopActionCategory.PluginCommand,
            TextCommand = "/mop run <macro number> \"Macro Name\"",
            SuggestionCommand = "/mop run ",
            Example = """
            /mop run "My macro"
            /mop run 10
            """,
            Notes = """
            * This is a plugin command (works only on local clients)
            """
        },
        new MopAction
        {
            Category = MopActionCategory.PluginCommand,
            TextCommand = "/mop stop",
            SuggestionCommand = "/mop stop",
            Example = """
            /mop stop
            """,
            Notes = """
            * This is a plugin command (works only on local clients)
            Stops all macro execution
            """
        },
        new MopAction
        {
            Category = MopActionCategory.PluginCommand,
            TextCommand = "/mop queue",
            SuggestionCommand = "/mop queue",
            Example = """
            /mop queue
            """,
            Notes = """
            * This is a plugin command (works only on local clients)
            Open macro queue
            """
        },
        new MopAction
        {
            Category = MopActionCategory.PluginCommand,
            TextCommand = "/mop targetmytarget",
            SuggestionCommand = "/mop targetmytarget",
            Example = """
            /mop targetmytarget
            """,
            Notes = """
            * This is a plugin command (works only on local clients)
            Useful to complete event quests
            """
        },
        new MopAction
        {
            Category = MopActionCategory.PluginCommand,
            TextCommand = "/mop targetclear",
            SuggestionCommand = "/mop targetclear",
            Example = """
            /mop targetclear
            """,
            Notes = """
            * This is a plugin command (works only on local clients)
            Useble only in local clients to remove the target
            """
        },

        // ---------------------------

        new MopAction
        {
            Category = MopActionCategory.ChatSyncCommand,
            TextCommand = "moprun <macro number> \"Macro Name\"",
            SuggestionCommand = "moprun ",
            Example = """
            moprun "my macro"
            moprun 10
            """,
            Notes = """
            * This is a chat sync command

            You may also use inline chat execution for single action instead of a macro
                moprun /clap
                moprun /cheer
                moprun /mopaction "Action Name"
                moprun /moptargetof "Warrior of Light@World"

            Some special game characters need to be replaced to work correctly,
            for example, <me> will be translated to the current character's name instead of being printed in the chat,
            for the correct functioning of inline chat actions replace <> with []
                /ac heal <me> => /ac heal [me]
                /ac heal <t> => /ac heal [t]
            """
        },
        new MopAction
        {
            Category = MopActionCategory.ChatSyncCommand,
            TextCommand = "mopstop",
            SuggestionCommand = "mopstop",
            Example = """
            mopstop
            """,
            Notes = """
            * This is a chat sync command

            Stops all macro execution
            """
        },

        // ---------------------------

        new MopAction
        {
            Category = MopActionCategory.MacroAction,
            TextCommand = "/mopwait <time>",
            SuggestionCommand = "/mopwait ",
            Example = """
            /mopwait 3
            /mopwait 3.5
            /mopwait 0.5
            """,
            Notes = "Use to wait a certain amount of time, it accepts decimals"
        },
         new MopAction
        {
            Category = MopActionCategory.MacroAction,
            TextCommand = "/moploop <loop amount>",
            SuggestionCommand = "/moploop",
            Example = """
            Run indefinitely:
                /clap
                /moploop

            Run 3 times:
                /clap
                /moploop 3
            """,
            Notes = """
            Use this action to loop current macro for a certain number of times or empty to run indefinitely,
            usually this should be the last action in the actions and you may have only one per command
            """
        },
        new MopAction
        {
            Category = MopActionCategory.MacroAction,
            TextCommand = "/mopmacro <macro number> \"Macro Name\"",
            SuggestionCommand = "/mopmacro ",
            Example = """
            /clap
            /wow
            /mopmacro "My macro 2"
            """,
            Notes = """
            Use this to call another macro inside a macro
            """
        },
        new MopAction
        {
            Category = MopActionCategory.MacroAction,
            TextCommand = "/moppetbarslot <slot number>",
            SuggestionCommand = "/moppetbarslot ",
            Example = """
            Rain Check:
                /moppetbarslot 1

            Umbrella Dance:
                /moppetbarslot 2

            Complete umbrella dance macro example
                /fashion "Fat Cat Parasol"
                /mopwait 3
                /moppetbarslot 2
                /mopwait 13
                /fashion "Fat Cat Parasol"
                /cheer
            Or
                /fashion "Fat Cat Parasol"
                /mopwait 3
                /mopaction "Umbrella Dance"
                /mopwait 13
                /fashion "Fat Cat Parasol"
                /cheer
            """,
            Notes = """
            Use especial pet hotbat slots like mount specials and parasol actions
            As alternativa it can also be used with skill name
                /mopaction "Umbrella Dance"
            """
        },
        new MopAction
        {
            Category = MopActionCategory.MacroAction,
            TextCommand = "/mopaction <action id> | \"Action Name\"",
            SuggestionCommand = "/mopaction ",
            Example = """
            Peloton:
                /mopaction "Peloton"
                /mopaction 7557

            Umbrella Dance:
                /mopaction "Umbrella Dance"
            """,
            Notes = "Similar to native game macro commands /action /ac but allows some actions that cant be used with /ac"
        },
        new MopAction
        {
            Category = MopActionCategory.MacroAction,
            TextCommand = "/moptarget \"Target Name\"",
            SuggestionCommand = "/moptarget ",
            Example = """
                /moptarget "Warrior of Light"
                /moptarget "Cloud@Siren"
                /moptarget "Annoying Moogle@Exodus"

            Useful for targetable emotes
                /moptarget "Warrior of Light"
                /mopwait 0.5
                /dote
            """,
            Notes = """
            For characters with same names in different world combine the world at end:
            /moptarget "Warrior of Light@Jenova"
            """
        },
        new MopAction
        {
            Category = MopActionCategory.MacroAction,
            TextCommand = "/moptargetof \"Target Name\"",
            SuggestionCommand = "/moptargetof ",
            Example = """
                /moptargetof "Sephiroth"
            """,
            Notes = """
            Useful for targetable emotes, target someone with main char then for the others use
                /moptargetof "Main Char Name"
                /mopwait 0.5
                /dote
            """
        },
        new MopAction
        {
            Category = MopActionCategory.MacroAction,
            TextCommand = "/moptargetclear",
            SuggestionCommand = "/moptargetclear",
            Example = """
                /moptargetclear

            Clear target after emote
                /moptarget "Warrior of Light"
                /dote
                /moptargetclear
            """,
            Notes = """
            Remove the target so that your head is not turned towards the target
            """
        },
        new MopAction
        {
            Category = MopActionCategory.MacroAction,
            TextCommand = "/mopitem <item id> | \"Item Name\"",
            SuggestionCommand = "/mopitem ",
            Example = """
            /mopitem "Heavenscracker"

            /mopitem 12042

            /mopitem "Realm Reborn Red"

            /mopitem 8214
            """,
            Notes = """
            Use items like fireworks and prisms, you can set each character to use a different item
            """
        },
        new MopAction
        {
            Category = MopActionCategory.MacroAction,
            TextCommand = "/mopobjectquantity <0-5>",
            SuggestionCommand = "/mopobjectquantity ",
            Example = """
            /mopobjectquantity 3
            /mopwait 1
            /moptarget "Warrior of Light"
            /mopwait 1
            /dote
            /mopobjectquantity 5
            /moptargetclear
            """,
            Notes = """
            Use to increase object limit to target someone in high populated areas and then reduce it
                0 - Automatic
                1 - Maximum
                2 - High
                3 - Normal
                4 - Low
                5 - Minimum
            """
        },

        // ---------------------------

        new MopAction
        {
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
        new MopAction
        {
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
        new MopAction
        {
            Category = MopActionCategory.GameAction,
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
            Category = MopActionCategory.GameAction,
            TextCommand = "/ac \"Skill Name\"",
            SuggestionCommand = "/ac ",
            Example = """
            /ac "Peloton"
            /action "Peloton"
            """,
            Notes = """
            Use native macro actions
            """
        },
        new MopAction
        {
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
    public MopActionCategory Category;
    public string TextCommand { get; set; } = string.Empty;
    public string SuggestionCommand { get; set; } = string.Empty;
    public string Example { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
}

public enum MopActionCategory
{
    PluginCommand,
    MacroAction,
    ChatSyncCommand,
    GameAction,
}
