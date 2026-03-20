using System.Collections.Generic;
using System.Linq;

namespace MasterOfPuppets;

public static class MopMacroActionsHelper {
    public static List<MopAction> Actions = new()
    {
        new MopAction {
            Category = MopActionCategory.PluginCommand,
            TextCommand = "/mop run <\"Macro Name\" | macro number> [-var=$name=value;...]",
            SuggestionCommand = "/mop run ",
            Example = """
            /mop run "My macro"
            /mop run 10

            With inline variable overrides:
            /mop run "My macro" -var=$emote=/clap
            /mop run "My macro" -var=$emote=/clap;$delay=0.5;$target="Warrior of Light"
            """,
            Notes = """
            * This is a plugin command (works only on local clients)
            Broadcasts macro execution to all local clients via IPC.

            Inline variables (-var=) override macro-level and command-local variables at runtime.
            Format: -var=$name=value;$name2="value with spaces";$name3=/command
            Variable names must start with a letter or underscore.
            Priority: inline vars > command-local vars > macro-level vars
            """
        },
        new MopAction {
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
        new MopAction {
            Category = MopActionCategory.PluginCommand,
            TextCommand = "/mop queue",
            SuggestionCommand = "/mop queue",
            Example = """
            /mop queue
            """,
            Notes = """
            * This is a plugin command (works only on local clients)
            Open macro queue window
            """
        },
        new MopAction {
            Category = MopActionCategory.PluginCommand,
            TextCommand = "/mop actions",
            SuggestionCommand = "/mop actions",
            Example = """
            /mop actions
            """,
            Notes = """
            * This is a plugin command (works only on local clients)
            Opens the Actions Broadcast window to manually trigger broadcast commands
            """
        },

        new MopAction {
            Category = MopActionCategory.PluginCommand,
            TextCommand = "/mop targetmytarget",
            SuggestionCommand = "/mop targetmytarget",
            Example = """
            /mop targetmytarget
            """,
            Notes = """
            * This is a plugin command (works only on local clients)
            All local clients will target whatever your main client is currently targeting.
            Useful for syncing targets during event quests
            """
        },

        new MopAction {
            Category = MopActionCategory.PluginCommand,
            TextCommand = "/mop interactwithmytarget",
            SuggestionCommand = "/mop interactwithmytarget",
            Example = """
            /mop interactwithmytarget
            """,
            Notes = """
            * This is a plugin command (works only on local clients)
            All local clients interact with the main client's current target.
            Useful to complete event quests where all characters must interact with the same object
            """
        },

        new MopAction {
            Category = MopActionCategory.PluginCommand,
            TextCommand = "/mop interactwithtarget",
            SuggestionCommand = "/mop interactwithtarget",
            Example = """
            /mop interactwithtarget
            """,
            Notes = """
            * This is a plugin command (works only on local clients)
            Each local client interacts with their own current target.
            Useful for event quests where each character must interact with their own nearby object
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
            Remove the target
            """
        },

        new MopAction
        {
            Category = MopActionCategory.PluginCommand,
            TextCommand = "/mop invite",
            SuggestionCommand = "/mop invite \"Character Name@World\"",
            Example = """
            /mop invite "Character Name@World"

            Invites all local charactrers:
            /mop invite
            """,
            Notes = """
            * This is a plugin command (works only on local clients)
            Invites a character by Name@World to your party
            """
        },

        // MOVEMENT
        new MopAction
        {
            Category = MopActionCategory.PluginCommand,
            TextCommand = "/mop move \"X Y Z\"",
            SuggestionCommand = "/mop move \"0 0 0\"",
            Example = """
            /mop move "0 0 4"
            /mop move "4 0 0"
            """,
            Notes = """
            * This is a plugin command (works only on local clients)
            Moves to the desired position using your current position as reference (origin)
            X (+Left | -Right)
            Y (+Fly Up | -Fly Down) *Inactive
            Z (+Forward | -Back)
            """
        },

        new MopAction
        {
            Category = MopActionCategory.PluginCommand,
            TextCommand = "/mop stopmove",
            SuggestionCommand = "/mop stopmove",
            Example = """
            /mop stopmove
            """,
            Notes = """
            * This is a plugin command (works only on local clients)
            Stops all movements
            """
        },

        new MopAction
        {
            Category = MopActionCategory.PluginCommand,
            TextCommand = "/mop movetotarget",
            SuggestionCommand = "/mop movetotarget",
            Example = """
            /mop movetotarget
            """,
            Notes = """
            * This is a plugin command (works only on local clients)
            Moves to target position
            """
        },

        new MopAction
        {
            Category = MopActionCategory.PluginCommand,
            TextCommand = "/mop stackonme",
            SuggestionCommand = "/mop stackonme",
            Example = """
            /mop stackonme
            """,
            Notes = """
            * This is a plugin command (works only on local clients)
            All local clients move to your current position
            """
        },

        new MopAction
        {
            Category = MopActionCategory.PluginCommand,
            TextCommand = "/mop movetocharacter \"Character Name\"",
            SuggestionCommand = "/mop movetocharacter \"\"",
            Example = """
            /mop movetocharacter "Character Name"
            """,
            Notes = """
            * This is a plugin command (works only on local clients)
            Moves to the position of the given character
            This also works with NPCs, Minions, Etc
            """
        },

        new MopAction
        {
            Category = MopActionCategory.PluginCommand,
            TextCommand = "/mop movetomytarget",
            SuggestionCommand = "/mop movetomytarget",
            Example = """
            /mop movetomytarget
            """,
            Notes = """
            * This is a plugin command (works only on local clients)
            Broadcasts to all local clients (except yourself) to move to your current target's position
            """
        },

        new MopAction
        {
            Category = MopActionCategory.PluginCommand,
            TextCommand = "/mop enablewalk",
            SuggestionCommand = "/mop enablewalk",
            Example = """
            /mop enablewalk

            Broadcast:
            /mopbr /mop enablewalk
            """,
            Notes = """
            * This is a plugin command (works only on local clients)
            Enable walking
            """
        },

        new MopAction
        {
            Category = MopActionCategory.PluginCommand,
            TextCommand = "/mop disablewalk",
            SuggestionCommand = "/mop disablewalk",
            Example = """
            /mop disablewalk

            Broadcast:
            /mopbr /mop disablewalk
            """,
            Notes = """
            * This is a plugin command (works only on local clients)
            Disable walking
            """
        },

        new MopAction
        {
            Category = MopActionCategory.PluginCommand,
            TextCommand = "/mop togglewalk",
            SuggestionCommand = "/mop togglewalk",
            Example = """
            /mop togglewalk

            Broadcast:
            /mopbr /mop togglewalk
            """,
            Notes = """
            * This is a plugin command (works only on local clients)
            Toggle walking
            """
        },

        new MopAction
        {
            Category = MopActionCategory.PluginCommand,
            TextCommand = "/mop gs <gearset number>",
            SuggestionCommand = "/mop gs",
            Example = """
            /mop gs 1
            /mop gs 5

            Broadcast:
            /mopbr /mop gs 5
            """,
            Notes = """
            * This is a plugin command (works only on local clients)
            Change the gearset using inventory items. First moves the gearset items from the inventory to the armoury, then equips the gearset. Prioritizes empty armoury slots; if none are available, uses the first slot and swaps that item back into inventory.
            """
        },

        new MopAction
        {
            Category = MopActionCategory.PluginCommand,
            TextCommand = "/mop movegearsets <gearset1,gearset2,...>",
            SuggestionCommand = "/mop movegearsets ",
            Example = """
            /mop movegearsets 12,13,14

            Broadcast:
            /mopbr /mop movegearsets 12,13,14
            """,
            Notes = """
            * This is a plugin command (works only on local clients)
            Moves any items from the specified gearsets (comma-separated, 1-based) that are in inventory bags to the armoury chest. Does not equip any gearset.
            """
        },

        new MopAction
        {
            Category = MopActionCategory.PluginCommand,
            TextCommand = "/mop keybroadcast on|off",
            SuggestionCommand = "/mop keybroadcast ",
            Example = """
            /mop keybroadcast on
            /mop keybroadcast off

            Broadcast:
            /mopbr /mop keybroadcast on
            """,
            Notes = """
            * This is a plugin command (works only on local clients)
            Enable or disable keyboard broadcast on this client. Use /mopbr to toggle all clients at once.
            """
        },

        // PARTY
        new MopAction {
            Category = MopActionCategory.PluginCommand,
            TextCommand = "/mop follow on|off",
            SuggestionCommand = "/mop follow ",
            Example = """
            /mop follow on
            /mop follow off
            """,
            Notes = """
            * This is a plugin command (works only on local clients)
            Enables or disables native follow mode, targeting the invoking client's entity.
            """
        },
        new MopAction {
            Category = MopActionCategory.PluginCommand,
            TextCommand = "/mop getleader",
            SuggestionCommand = "/mop getleader",
            Example = """
            /mop getleader
            """,
            Notes = """
            * This is a plugin command (works only on local clients)
            Promotes the main client to party leader.
            """
        },
        new MopAction {
            Category = MopActionCategory.PluginCommand,
            TextCommand = "/mop disband",
            SuggestionCommand = "/mop disband",
            Example = """
            /mop disband
            """,
            Notes = """
            * This is a plugin command (works only on local clients)
            Disbands the current party.
            """
        },

        // HOUSING & TRAVEL
        new MopAction {
            Category = MopActionCategory.PluginCommand,
            TextCommand = "/mop enterhouse",
            SuggestionCommand = "/mop enterhouse",
            Example = """
            /mop enterhouse

            Broadcast:
            /mopbr /mop enterhouse
            """,
            Notes = """
            * This is a plugin command (works only on local clients)
            Interacts with the nearest house entrance and confirms entry.
            """
        },
        new MopAction {
            Category = MopActionCategory.PluginCommand,
            TextCommand = "/mop exithouse",
            SuggestionCommand = "/mop exithouse",
            Example = """
            /mop exithouse

            Broadcast:
            /mopbr /mop exithouse
            """,
            Notes = """
            * This is a plugin command (works only on local clients)
            Interacts with the nearest house exit and confirms.
            """
        },
        new MopAction {
            Category = MopActionCategory.PluginCommand,
            TextCommand = "/mop movefrontdoor",
            SuggestionCommand = "/mop movefrontdoor",
            Example = """
            /mop movefrontdoor

            Broadcast:
            /mopbr /mop movefrontdoor
            """,
            Notes = """
            * This is a plugin command (works only on local clients)
            Teleports to the estate front door via the housing menu (/housing).
            Must be inside a house.
            """
        },
        new MopAction {
            Category = MopActionCategory.PluginCommand,
            TextCommand = "/mop ward <1-30>",
            SuggestionCommand = "/mop ward ",
            Example = """
            /mop ward 1
            /mop ward 15

            Broadcast:
            /mopbr /mop ward 5
            """,
            Notes = """
            * This is a plugin command (works only on local clients)
            Teleports all instances to the specified residential ward (1-30) via the aetheryte menu.
            Requires the player to be near a main-city residential district aetheryte.
            """
        },
        new MopAction {
            Category = MopActionCategory.PluginCommand,
            TextCommand = "/mop world <WorldName>",
            SuggestionCommand = "/mop world ",
            Example = """
            /mop world Excalibur
            /mop world Goblin

            Broadcast:
            /mopbr /mop world Excalibur
            """,
            Notes = """
            * This is a plugin command (works only on local clients)
            Travels all instances to the specified world via the aetheryte world travel menu.
            Requires the player to be near a main-city aetheryte. World must be in the same data center.
            """
        },

        // SETTINGS & UTILITY
        new MopAction {
            Category = MopActionCategory.PluginCommand,
            TextCommand = "/mop objectquantity <0-5>",
            SuggestionCommand = "/mop objectquantity ",
            Example = """
            /mop objectquantity 3

            Broadcast:
            /mopbr /mop objectquantity 3
            """,
            Notes = """
            * This is a plugin command (works only on local clients)
            Sets the display object limit on all local clients.
                0 - Automatic
                1 - Maximum
                2 - High
                3 - Normal
                4 - Low
                5 - Minimum
            """
        },
        new MopAction {
            Category = MopActionCategory.PluginCommand,
            TextCommand = "/mop camhack on|off",
            SuggestionCommand = "/mop camhack ",
            Example = """
            /mop camhack on
            /mop camhack off
            """,
            Notes = """
            * This is a plugin command (works only on local clients)
            Enables or disables high-altitude camera override.
            """
        },
        new MopAction {
            Category = MopActionCategory.PluginCommand,
            TextCommand = "/mop logout",
            SuggestionCommand = "/mop logout",
            Example = """
            /mop logout

            Broadcast:
            /mopbr /mop logout
            """,
            Notes = """
            * This is a plugin command (works only on local clients)
            Logs out the character to the character selection screen.
            """
        },
        new MopAction {
            Category = MopActionCategory.PluginCommand,
            TextCommand = "/mop shutdown",
            SuggestionCommand = "/mop shutdown",
            Example = """
            /mop shutdown

            Broadcast:
            /mopbr /mop shutdown
            """,
            Notes = """
            * This is a plugin command (works only on local clients)
            Closes the game client.
            """
        },

        new MopAction
        {
            Category = MopActionCategory.PluginCommand,
            TextCommand = "/mopbr <command>",
            SuggestionCommand = "/mopbr ",
            Example = """
            /mopbr /clap
            """,
            Notes = """
            * This is a plugin command (works only on local clients)
            Broadcast a command to all local clients
            """
        },
        new MopAction
        {
            Category = MopActionCategory.PluginCommand,
            TextCommand = "/mopbrn <command>",
            SuggestionCommand = "/mopbrn ",
            Example = """
            /mopbrn /clap
            """,
            Notes = """
            * This is a plugin command (works only on local clients)
            Broadcast a command to local clients except yourself
            """
        },
        new MopAction
        {
            Category = MopActionCategory.PluginCommand,
            TextCommand = "/mopbrc \"Character Name\" <command>",
            SuggestionCommand = "/mopbrc ",
            Example = """
            /mopbrc "Character Name" /clap
            """,
            Notes = """
            * This is a plugin command (works only on local clients)
            Broadcast a command to a specific local client by character name
            """
        },

        // ---------------------------

        new MopAction
        {
            Category = MopActionCategory.ChatSyncCommand,
            TextCommand = "moprun <\"Macro Name\" | macro number> [-var=$name=value;...]",
            SuggestionCommand = "moprun ",
            Example = """
            moprun "my macro"
            moprun 10

            With inline variable overrides:
            moprun "my macro" -var=$emote=/clap
            moprun "my macro" -var=$emote=/clap;$delay=0.5;$target="Warrior of Light"
            """,
            Notes = """
            * This is a chat sync command - all local clients reading the chat will execute the macro.

            Inline variables (-var=) override macro-level and command-local variables at runtime.
            Format: -var=$name=value;$name2="value with spaces";$name3=/command
            Variable names must start with a letter or underscore.
            Priority: inline vars > command-local vars > macro-level vars
            """
        },
        new MopAction
        {
            Category = MopActionCategory.ChatSyncCommand,
            TextCommand = "mopbr <command>",
            SuggestionCommand = "mopbr ",
            Example = """
            Broadcast a command to all local clients via chat:
                mopbr /clap
                mopbr /cheer
                mopbr /mopaction "Action Name"
                mopbr /moptargetof "Warrior of Light@World"
            """,
            Notes = """
            * This is a chat sync command - broadcast a command to all local clients via chat

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
            TextCommand = "mopbrn <command>",
            SuggestionCommand = "mopbrn ",
            Example = """
            Broadcast a command to all local clients except yourself via chat:
                mopbrn /clap
                mopbrn /cheer
                mopbrn /mopaction "Action Name"
                mopbrn /moptargetof "Warrior of Light@World"
            """,
            Notes = """
            * This is a chat sync command - broadcast a command to all local clients except yourself via chat

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
            TextCommand = "mopbrc \"Character Name\" <command>",
            SuggestionCommand = "mopbrc ",
            Example = """
            Broadcast a command to a specific local client via chat:
                mopbrc "Character Name" /clap
                mopbrc "Character Name" /cheer
                mopbrc "Character Name" /mopaction "Action Name"
                mopbrc "Character Name" /moptargetof "Warrior of Light@World"
            """,
            Notes = """
            * This is a chat sync command - broadcast a command to a specific local client via chat

            Some special game characters need to be replaced to work correctly,
            for example, <me> will be translated to the current character's name instead of being printed in the chat,
            for the correct functioning of inline chat actions replace <> with []
                /ac heal <me> => /ac heal [me]
                /ac heal <t> => /ac heal [t]
            """
        },
        new MopAction {
            Category = MopActionCategory.ChatSyncCommand,
            TextCommand = "mopstop",
            SuggestionCommand = "mopstop",
            Example = """
            mopstop
            """,
            Notes = """
            * This is a chat sync command - all local clients reading the chat will stop macro execution
            """
        },

        // ---------------------------

        new MopAction {
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
         new MopAction {
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
            Loops the current macro a set number of times, or indefinitely if no number is given.
            Should be the last action in the macro. Only one /moploop per macro.
            """
         },
        new MopAction {
            Category = MopActionCategory.MacroAction,
            TextCommand = "/moploopstart [loop amount]",
            SuggestionCommand = "/moploopstart",
            Example = """
            Run a setup block once, then loop a section 5 times:
                /mopmovegearsets 12,13,14
                /mopwait 3

                /moploopstart 5
                /gs change 12
                /mopwait 1
                /gs change 13
                /mopwait 1
                /moploopend

            Loop a section indefinitely (no argument):
                /mopwait 1

                /moploopstart
                /clap
                /mopwait 2
                /moploopend
            """,
            Notes = """
            Marks the start of a loop block. Actions before /moploopstart run once.
            Actions between /moploopstart and /moploopend repeat N times, or indefinitely if no number is given.
            Use /moploopend to close the block.
            """
        },
        new MopAction {
            Category = MopActionCategory.MacroAction,
            TextCommand = "/moploopend",
            SuggestionCommand = "/moploopend",
            Example = """
            /moploopstart 3
            /clap
            /mopwait 1
            /moploopend
            """,
            Notes = """
            Marks the end of a loop block started by /moploopstart.
            """
        },
        new MopAction {
            Category = MopActionCategory.MacroAction,
            TextCommand = "{random(min,max)}",
            SuggestionCommand = "{random(",
            Example = """
            Random integer wait between 1 and 3 seconds:
                /mopwait {random(1,3)}

            Random float wait between 1.5 and 3.5 seconds:
                /mopwait {random(1.5,3.5)}

            Use inside any command or chat line:
                /say Rolling {random(1,100)}!

            Works inside loop blocks (new value each iteration):
                /moploopstart 5
                /mopwait {random(0.5,2.0)}
                /clap
                /moploopend
            """,
            Notes = """
            Replaced at execution time by a random value between min and max (inclusive).
            Integer inputs produce an integer result: {random(1,5)} → 1, 2, 3, 4, or 5.
            Decimal inputs produce a float result with 2 decimal places: {random(1.5,3.5)} → e.g. 2.17.
            The value is re-evaluated on every execution, including each loop iteration.
            Use {random(a,b,c,...)} with three or more values to pick from a specific list.
            """
        },
        new MopAction {
            Category = MopActionCategory.MacroAction,
            TextCommand = "{random(v1,v2,v3,...)}",
            SuggestionCommand = "{random(",
            Example = """
            Pick one gearset slot at random from a specific list:
                /gs change {random(1,3,7,12)}

            Pick a random float wait from specific values:
                /mopwait {random(0.5,1.0,1.5,2.0)}

            Works inside loop blocks (new pick each iteration):
                /moploopstart
                /gs change {random(1,3,7,12)}
                /mopwait {random(0.5,1.0,2.0)}
                /moploopend
            """,
            Notes = """
            When three or more values are given, picks one at random from the list instead of generating a range.
            Two values always produce a range: {random(1,5)} = any integer 1–5.
            Three+ values pick from the exact set: {random(1,3,5)} = 1, 3, or 5 only.
            Supports decimals in lists: {random(0.5,1.0,1.5)} = 0.5, 1.0, or 1.5.
            """
        },
        new MopAction {
            Category = MopActionCategory.MacroAction,
            TextCommand = "/mopmacro <\"Macro Name\" | macro number>",
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
        new MopAction {
            Category = MopActionCategory.MacroAction,
            TextCommand = "/mophotbar <bar number 1-10> <slot number 1-12>",
            SuggestionCommand = "/mophotbar ",
            Example = """
                /mophotbar 1 1
                /mophotbar 5 4

            """,
            Notes = """
            Use actions assigned to a hotbar slot
            """
        },
        new MopAction {
            Category = MopActionCategory.MacroAction,
            TextCommand = "/moppetbarslot <slot number 1-12>",
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
            Use special pet hotbar slots like mount specials and parasol actions.
            As an alternative it can also be used with skill name:
                /mopaction "Umbrella Dance"
            """
        },
        new MopAction {
            Category = MopActionCategory.MacroAction,
            TextCommand = "/mophotbaremote <emote ID>",
            SuggestionCommand = "/mophotbaremote ",
            Example = """
            Sleep / wake
                /mophotbaremote 88

            -----------------------------

            Sit on ground <pose 1>
                /mophotbaremote 97

            Sit on ground <pose 2>
                /mophotbaremote 98

            Sit on ground <pose 3>
                /mophotbaremote 117

            Stand up from groundsit
                /mophotbaremote 53

            -----------------------------

            Sleep / wake (Gpose)
                /mophotbaremote 99

            -----------------------------

            Change <Pose 1>
                /mophotbaremote 91

            Change <Pose 2>
                /mophotbaremote 92

            Change <Pose 3>
                /mophotbaremote 107

            Change <Pose 4>
                /mophotbaremote 108

            Change <Pose 5>
                /mophotbaremote 218

            Change <Pose 6>
                /mophotbaremote 219

            -----------------------------

            Chair Sit <pose 1>
                /mophotbaremote 95

            Chair Sit <pose 2>
                /mophotbaremote 96

            Chair Sit <pose 3>
                /mophotbaremote 254

            Chair Sit <pose 4>
                /mophotbaremote 255

            Stand up from chairsit
                /mophotbaremote 51

            -----------------------------

            Umbrella <Pose 1>
                /mophotbaremote 243

            Umbrella <Pose 2>
                /mophotbaremote 244

            Umbrella <Pose 3>
                /mophotbaremote 253
            """,
            Notes = """
            Use emotes as if they were assigned to a hotbar slot. Works with hidden emotes like sleep/wake or sit anywhere.
            Warning: using an unknown emote ID may crash the game.
            """
        },
        new MopAction {
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
            Notes = "Similar to native game macro commands /action /ac but allows some actions that can't be used with /ac"
        },
        new MopAction {
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
        new MopAction {
            Category = MopActionCategory.MacroAction,
            TextCommand = "/moptargetof \"Target Name\"",
            SuggestionCommand = "/moptargetof ",
            Example = """
                /moptargetof "Sephiroth"
            """,
            Notes = """
            Targets whoever the specified character is currently targeting.
            Useful to sync a targetable emote: have your main character target someone,
            then have other clients target that same person using:
                /moptargetof "Main Char Name"
                /mopwait 0.5
                /dote
            """
        },
        new MopAction {
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
        new MopAction {
            Category = MopActionCategory.MacroAction,
            TextCommand = "/moptargetmyminion",
            SuggestionCommand = "/moptargetmyminion",
            Example = """
                /moptargetmyminion
            """,
            Notes = """
            Target your current minion
            """
        },
        new MopAction {
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
            Use items like fireworks and prisms,
            you can set each character to use a different item
            """
        },
        new MopAction {
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

        // MOVEMENT
        new MopAction
        {
            Category = MopActionCategory.MacroAction,
            TextCommand = "/mopmove X Y Z [angle]",
            SuggestionCommand = "/mopmove 0 0 0",
            Example = """
            /mopmove 0 0 4
            /mopmove 4 0 0

            Move and face east (90°) after arriving:
                /mopmove 0 0 4 90
            """,
            Notes = """
            Moves to the desired position using your current position as reference (origin).
            X (+Left | -Right)
            Y (+Fly Up | -Fly Down) *Inactive
            Z (+Forward | -Back)
            Optional 4th argument: face this direction in degrees after arriving (0=north, 90=east, 180=south, 270=west).
            """
        },

        new MopAction
        {
            Category = MopActionCategory.MacroAction,
            TextCommand = "/mopmoverelativeto X Y Z \"Character Name\" [angle]",
            SuggestionCommand = "/mopmoverelativeto 0 0 0 \"Character Name\"",
            Example = """
            /mopmoverelativeto 0 0 4 "Character Name"
            /mopmoverelativeto 4 0 0 "Character Name"

            Move relative and face south (180°) after arriving:
                /mopmoverelativeto 0 0 2 "Character Name" 180
            """,
            Notes = """
            Moves to the desired position using the specified character as the reference point (origin).
            X (+Left | -Right)
            Y (+Fly Up | -Fly Down) *Inactive
            Z (+Forward | -Back)
            Optional 5th argument: face this direction in degrees after arriving (0=north, 90=east, 180=south, 270=west).
            """
        },

        new MopAction
        {
            Category = MopActionCategory.MacroAction,
            TextCommand = "/mopstopmove",
            SuggestionCommand = "/mopstopmove",
            Example = """
            /mopstopmove
            """,
            Notes = """
            Stops all movements
            """
        },

        new MopAction
        {
            Category = MopActionCategory.MacroAction,
            TextCommand = "/mopface <angle>",
            SuggestionCommand = "/mopface ",
            Example = """
            Turn right 90°:
                /mopface 90

            Turn left 90°:
                /mopface -90

            Turn around:
                /mopface 180

            Move then turn right 90°:
                /mopmove 0 0 4
                /mopwait 2
                /mopface 90
            """,
            Notes = """
            Rotates the character by the given offset relative to their current facing.
            Positive values turn clockwise (right), negative values counter-clockwise (left).
            Use /mopfaceabs for absolute compass directions.
            """
        },

        new MopAction
        {
            Category = MopActionCategory.MacroAction,
            TextCommand = "/mopfaceabs <angle>",
            SuggestionCommand = "/mopfaceabs ",
            Example = """
            Face north:
                /mopfaceabs 0

            Face east:
                /mopfaceabs 90

            Face south:
                /mopfaceabs 180

            Face west:
                /mopfaceabs 270

            Move then face a specific direction:
                /mopmove 0 0 4
                /mopwait 2
                /mopfaceabs 180
            """,
            Notes = """
            Rotates the character to face an absolute compass direction.
            0 = north, 90 = east, 180 = south, 270 = west (increases clockwise).
            Use /mopface for relative turns from the current facing.
            """
        },

        new MopAction
        {
            Category = MopActionCategory.MacroAction,
            TextCommand = "/mopmovetotarget",
            SuggestionCommand = "/mopmovetotarget",
            Example = """
            /mopmovetotarget
            """,
            Notes = """
            Moves to target position
            """
        },

        new MopAction
        {
            Category = MopActionCategory.MacroAction,
            TextCommand = "/mopmovetocharacter \"Character Name\"",
            SuggestionCommand = "/mopmovetocharacter \"\"",
            Example = """
            /mopmovetocharacter "Character Name"
            """,
            Notes = """
            Moves to the position of the given character
            This also works with NPCs, Minions, Etc
            """
        },

        new MopAction
        {
            Category = MopActionCategory.MacroAction,
            TextCommand = "/mopenablewalk",
            SuggestionCommand = "/mopenablewalk",
            Example = """
            /mopenablewalk
            """,
            Notes = """
            Enable walking
            """
        },

        new MopAction
        {
            Category = MopActionCategory.MacroAction,
            TextCommand = "/mopdisablewalk",
            SuggestionCommand = "/mopdisablewalk",
            Example = """
            /mopdisablewalk
            """,
            Notes = """
            Disable walking
            """
        },

        new MopAction
        {
            Category = MopActionCategory.MacroAction,
            TextCommand = "/moptogglewalk",
            SuggestionCommand = "/moptogglewalk",
            Example = """
            /moptogglewalk
            """,
            Notes = """
            Toggle walking
            """
        },

        new MopAction
        {
            Category = MopActionCategory.MacroAction,
            TextCommand = "/mopmovegearsets <gearset1,gearset2,...>",
            SuggestionCommand = "/mopmovegearsets ",
            Example = """
            /mopmovegearsets 12,13,14

            Prepare multiple gearsets before equipping:
                /mopmovegearsets 1,2,3
                /mopwait 3
                /gs change 1
            """,
            Notes = """
            Moves any items from the specified gearsets (comma-separated, 1-based) that are in inventory bags to the armoury chest.
            Awaits completion before continuing to the next macro action.
            Use /mopwait after if you need extra time for the game to process the moves.
            """
        },

        // ---------------------------

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
        new MopAction {
            Category = MopActionCategory.PluginCommand,
            TextCommand = "/mop formations",
            SuggestionCommand = "/mop formations",
            Example = """
            /mop formations
            """,
            Notes = """
            * This is a plugin command (works only on local clients)
            Opens the Formations window to create and manage formations.
            """
        },
        new MopAction {
            Category = MopActionCategory.PluginCommand,
            TextCommand = "/mop formation \"<Formation Name>\"",
            SuggestionCommand = "/mop formation ",
            Example = """
            /mop formation "My Formation"
            """,
            Notes = """
            * This is a plugin command (works only on local clients)
            Broadcasts formation execution to all local clients.
            The issuing character's world position is used as the leader origin.
            Each client moves to the formation point whose assigned CIDs/groups include it.
            """
        }
    };

    public static List<string> GetSuggestionCommands() {
        return Actions
            .Select(a => a.SuggestionCommand)
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Distinct()
            .ToList();
    }
}

public class MopAction {
    public MopActionCategory Category;
    public string TextCommand { get; set; } = string.Empty;
    public string SuggestionCommand { get; set; } = string.Empty;
    public string Example { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
}

public enum MopActionCategory {
    PluginCommand,
    MacroAction,
    ChatSyncCommand,
    GameAction,
}
