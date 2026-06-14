using System.Collections.Generic;

namespace MasterOfPuppets;

public static partial class MopCommandsHelper {
    private static List<MopAction> GetPluginCommands() =>
    [
        //  General
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
            TextCommand = "/mop gamemacro <index|\"Game Macro Name\"> [individual|i|shared|share|s]",
            SuggestionCommand = "/mop gamemacro ",
            Example = """
            /mop gamemacro 01 i
            /mop gamemacro 12 shared
            /mop gamemacro "Buff Opener" individual
            /mop gamemacro "Travel Setup"

            Broadcast:
            /mopbr /mop gamemacro "Buff Opener" i
            """,
            Notes = """
            * This is a plugin command (works only on local clients)
            Executes an in-game FFXIV macro from the individual or shared macro set.

            Numeric indexes use the game macro index directly (0-99), matching BardToolbox runmacro behavior.
            If scope is omitted for a name, individual macros are searched first, then shared macros.
            If the same name exists more than once, specify individual or shared, or rename one macro.
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
            TextCommand = "/mop globaldelay",
            SuggestionCommand = "/mop globaldelay",
            Example = """
            /mop globaldelay 0.5
            /mop globaldelay 0
            /mop globaldelay 2.5
            /mop globaldelay 3
            """,
            Notes = """
            * This is a plugin command (works only on local clients)
            Change global delay between actions
            """
        },
        new MopAction {
            Category = MopActionCategory.PluginCommand,
            TextCommand = "/mop item <item id> | \"Item Name\"",
            SuggestionCommand = "/mop item ",
            Example = """
            /mop item "Heavenscracker"

            /mop item 12042

            /mop item "Realm Reborn Red"

            /mop item 8214
            /mopbr /mop item "Heavenscracker"
            """,
            Notes = """
            * This is a plugin command (works only on local clients)
            Use items like fireworks, prisms etc
            """
        },
        new MopAction {
            Category = MopActionCategory.PluginCommand,
            TextCommand = "/mop formation \"Formation Name\" [self|target|ftarget] [continuous|precise]",
            SuggestionCommand = "/mop formation ",
            Example = """
            /mop formation "My Formation"
            /mop formation "My Formation" target
            /mop formation "My Formation" ftarget
            /mop formation "My Formation" precise
            /mop formation "My Formation" target precise
            """,
            Notes = """
            * This is a plugin command (works only on local clients)
            Broadcasts formation execution to all local clients.
            Each client moves to the formation point whose assigned CIDs/groups include it.
            Add target to place your assigned formation point at your current target.
            Default: precise. Use continuous for smoother loops.
            """
        },
        new MopAction {
            Category = MopActionCategory.PluginCommand,
            TextCommand = "/mop layout \"Layout Name\"",
            SuggestionCommand = "/mop layout ",
            Example = """
            /mop layout "my layout"
            /mop layout minimal
            """,
            Notes = """
            * This is a plugin command (works only on local clients)
            Manage window size and position layouts for all clients.
            """
        },

        //  Target
        new MopAction {
            Category = MopActionCategory.PluginCommand,
            SubCategory = MopActionSubCategory.Target,
            TextCommand = "/mop targetmytarget",
            SuggestionCommand = "/mop targetmytarget",
            Example = """
            /mop targetmytarget

            Alias:
            /mop tmt
            """,
            Notes = """
            * This is a plugin command (works only on local clients)
            All local clients will target whatever your main client is currently targeting.
            Useful for syncing targets during event quests
            """
        },
        new MopAction {
            Category = MopActionCategory.PluginCommand,
            SubCategory = MopActionSubCategory.Target,
            TextCommand = "/mop interactwithmytarget",
            SuggestionCommand = "/mop interactwithmytarget",
            Example = """
            /mop interactwithmytarget

            Alias:
            /mop iwmt
            """,
            Notes = """
            * This is a plugin command (works only on local clients)
            All local clients interact with the main client's current target.
            Useful to complete event quests where all characters must interact with the same object
            """
        },
        new MopAction {
            Category = MopActionCategory.PluginCommand,
            SubCategory = MopActionSubCategory.Target,
            TextCommand = "/mop interactwithtarget",
            SuggestionCommand = "/mop interactwithtarget",
            Example = """
            /mop interactwithtarget

            Alias:
            /mop iwt
            """,
            Notes = """
            * This is a plugin command (works only on local clients)
            Each local client interacts with their own current target.
            Useful for event quests where each character must interact with their own nearby object
            """
        },
        new MopAction {
            Category = MopActionCategory.PluginCommand,
            SubCategory = MopActionSubCategory.Target,
            TextCommand = "/mop targetclear",
            SuggestionCommand = "/mop targetclear",
            Example = """
            /mop targetclear

            Alias:
            /mop tc
            """,
            Notes = """
            * This is a plugin command (works only on local clients)
            Remove the target
            """
        },

        //  Party
        new MopAction {
            Category = MopActionCategory.PluginCommand,
            SubCategory = MopActionSubCategory.Party,
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
        new MopAction {
            Category = MopActionCategory.PluginCommand,
            SubCategory = MopActionSubCategory.Party,
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
        new MopAction {
            Category = MopActionCategory.PluginCommand,
            SubCategory = MopActionSubCategory.Party,
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
            SubCategory = MopActionSubCategory.Party,
            TextCommand = "/mop follow",
            SuggestionCommand = "/mop follow",
            Example = """
            /mop follow
            """,
            Notes = """
            * This is a plugin command (works only on local clients)
            Enables follow mode.
            """
        },
        new MopAction {
            Category = MopActionCategory.PluginCommand,
            SubCategory = MopActionSubCategory.Party,
            TextCommand = "/mop stopfollow",
            SuggestionCommand = "/mop stopfollow",
            Example = """
            /mop stopfollow
            """,
            Notes = """
            * This is a plugin command (works only on local clients)
            Disables follow mode.
            """
        },

        //  Movement
        new MopAction {
            Category = MopActionCategory.PluginCommand,
            SubCategory = MopActionSubCategory.Movement,
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
        new MopAction {
            Category = MopActionCategory.PluginCommand,
            SubCategory = MopActionSubCategory.Movement,
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
        new MopAction {
            Category = MopActionCategory.PluginCommand,
            SubCategory = MopActionSubCategory.Movement,
            TextCommand = "/mop movetotarget",
            SuggestionCommand = "/mop movetotarget",
            Example = """
            /mop movetotarget

            Alias:
            /mop mtt
            """,
            Notes = """
            * This is a plugin command (works only on local clients)
            Moves to target position
            """
        },
        new MopAction {
            Category = MopActionCategory.PluginCommand,
            SubCategory = MopActionSubCategory.Movement,
            TextCommand = "/mop stackonme",
            SuggestionCommand = "/mop stackonme",
            Example = """
            /mop stackonme

            Alias:
            /mop som
            """,
            Notes = """
            * This is a plugin command (works only on local clients)
            All local clients move to your current position
            """
        },
        new MopAction {
            Category = MopActionCategory.PluginCommand,
            SubCategory = MopActionSubCategory.Movement,
            TextCommand = "/mop movetocharacter \"Character Name\"",
            SuggestionCommand = "/mop movetocharacter \"\"",
            Example = """
            /mop movetocharacter "Character Name"

            Alias:
            /mop mtc "Character Name"
            """,
            Notes = """
            * This is a plugin command (works only on local clients)
            Moves to the position of the given character
            This also works with NPCs, Minions, Etc
            """
        },
        new MopAction {
            Category = MopActionCategory.PluginCommand,
            SubCategory = MopActionSubCategory.Movement,
            TextCommand = "/mop movetomytarget",
            SuggestionCommand = "/mop movetomytarget",
            Example = """
            /mop movetomytarget

            Alias:
            /mop mtmt
            """,
            Notes = """
            * This is a plugin command (works only on local clients)
            Broadcasts to all local clients (except yourself) to move to your current target's position
            """
        },
        new MopAction {
            Category = MopActionCategory.PluginCommand,
            SubCategory = MopActionSubCategory.Movement,
            TextCommand = "/mop walk <on|off|toggle>",
            SuggestionCommand = "/mop walk",
            Example = """
            /mop walk on
            /mop walk off
            /mop walk toggle

            Broadcast:
            /mopbr /mop walk on
            """,
            Notes = """
            * This is a plugin command (works only on local clients)
            Switch walking mode
            """
        },
        new MopAction {
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
        new MopAction {
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

        //  Housing & Travel
        new MopAction {
            Category = MopActionCategory.PluginCommand,
            SubCategory = MopActionSubCategory.HousingAndTravel,
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
            SubCategory = MopActionSubCategory.HousingAndTravel,
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
            SubCategory = MopActionSubCategory.HousingAndTravel,
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
            SubCategory = MopActionSubCategory.HousingAndTravel,
            TextCommand = "/mop estate \"<Friend Name>\" <fc|pe|ap>",
            SuggestionCommand = "/mop estate ",
            Example = """
            Teleport to Apartments:
            /mop estate "Character Name" ap

            Teleport to Free Company:
            /mop estate "Character Name" fc

            Teleport to Private Estate:
            /mop estate "Character Name" pe

            Broadcast:
            /mopbr /mop estate "Character Name" ap
            """,
            Notes = """
            * This is a plugin command (works only on local clients)
            Teleports to a friend's estate.
            Friend name supports partial match (case-insensitive).
            Options:
                fc - Free Company Estate
                pe - Private Estate
                ap - Apartments
            """
        },
        new MopAction {
            Category = MopActionCategory.PluginCommand,
            SubCategory = MopActionSubCategory.HousingAndTravel,
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
            SubCategory = MopActionSubCategory.HousingAndTravel,
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

        //  Broadcast
        new MopAction {
            Category = MopActionCategory.PluginCommand,
            SubCategory = MopActionSubCategory.Broadcast,
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
        new MopAction {
            Category = MopActionCategory.PluginCommand,
            SubCategory = MopActionSubCategory.Broadcast,
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
        new MopAction {
            Category = MopActionCategory.PluginCommand,
            SubCategory = MopActionSubCategory.Broadcast,
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
        new MopAction {
            Category = MopActionCategory.PluginCommand,
            SubCategory = MopActionSubCategory.Broadcast,
            TextCommand = "/mopbrg \"Group Name\" <command>",
            SuggestionCommand = "/mopbrg ",
            Example = """
            /mopbrg "Group Name" /clap
            """,
            Notes = """
            * This is a plugin command (works only on local clients)
            Broadcast a command to a specific group
            """
        },

        //  Settings
        new MopAction {
            Category = MopActionCategory.PluginCommand,
            TextCommand = "/mop keybroadcast on|off|toggle",
            SuggestionCommand = "/mop keybroadcast ",
            Example = """
            /mop keybroadcast on
            /mop keybroadcast off
            /mop keybroadcast toggle

            Broadcast:
            /mopbr /mop keybroadcast on
            """,
            Notes = """
            * This is a plugin command (works only on local clients)
            Enable or disable keyboard broadcast on this client. Use /mopbr to toggle all clients at once.
            """
        },
        new MopAction {
            Category = MopActionCategory.PluginCommand,
            SubCategory = MopActionSubCategory.Settings,
            TextCommand = "/mop sound on|off|toggle",
            SuggestionCommand = "/mop sound ",
            Example = """
            /mop sound on
            /mop sound off
            /mop sound toggle

            Broadcast:
            /mopbr /mop sound on
            """,
            Notes = """
            * This is a plugin command (works only on local clients)
            Enable or disable sound. Use /mopbr to toggle all clients at once.
            """
        },
        new MopAction {
            Category = MopActionCategory.PluginCommand,
            SubCategory = MopActionSubCategory.Settings,
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
            SubCategory = MopActionSubCategory.RenderAndCamera,
            TextCommand = "/mop camhack on|off|toggle",
            SuggestionCommand = "/mop camhack ",
            Example = """
            /mop camhack on
            /mop camhack off
            /mop camhack toggle
            """,
            Notes = """
            * This is a plugin command (works only on local clients)
            Enables or disables high-altitude camera override.
            """
        },
        new MopAction {
            Category = MopActionCategory.PluginCommand,
            SubCategory = MopActionSubCategory.RenderAndCamera,
            TextCommand = "/mop renderhack on|off|toggle",
            SuggestionCommand = "/mop renderhack ",
            Example = """
            /mop renderhack on
            /mop renderhack off
            /mop renderhack toggle
            """,
            Notes = """
            * This is a plugin command (works only on local clients)
            Enables or disables game render to save computer resources.
            """
        },
        new MopAction {
            Category = MopActionCategory.PluginCommand,
            SubCategory = MopActionSubCategory.Settings,
            TextCommand = "/mop settingsprofile \"profile name\"",
            SuggestionCommand = "/mop settingsprofile \"\"",
            Example = """
            /mop settingsprofile low
            /mop settingsprofile high
            """,
            Notes = """
            * This is a plugin command (works only on local clients)
            Changes the game settings according to those saved in the profile.
            """
        },

        //  Exit Actions
        new MopAction {
            Category = MopActionCategory.PluginCommand,
            SubCategory = MopActionSubCategory.Party,
            TextCommand = "/mop abandonduty",
            SuggestionCommand = "/mop abandonduty",
            Example = """
            /mop abandonduty
            /mop ad
            """,
            Notes = """
            * This is a plugin command (works only on local clients)
            Leave the current duty instance.
            """
        },
        new MopAction {
            Category = MopActionCategory.PluginCommand,
            SubCategory = MopActionSubCategory.ExitActions,
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
            SubCategory = MopActionSubCategory.ExitActions,
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
    ];
}
