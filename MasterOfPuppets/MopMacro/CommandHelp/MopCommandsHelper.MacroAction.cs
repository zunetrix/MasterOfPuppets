using System.Collections.Generic;

namespace MasterOfPuppets;

public static partial class MopCommandsHelper {
    private static List<MopAction> GetMacroActions() =>
    [
        //  Flow Control
        new MopAction {
            Category = MopActionCategory.MacroAction,
            SubCategory = MopActionSubCategory.FlowControl,
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
            SubCategory = MopActionSubCategory.FlowControl,
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
            SubCategory = MopActionSubCategory.FlowControl,
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
            SubCategory = MopActionSubCategory.FlowControl,
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
            SubCategory = MopActionSubCategory.FlowControl,
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

        //  Variables
        new MopAction {
            Category = MopActionCategory.MacroAction,
            SubCategory = MopActionSubCategory.Variables,
            TextCommand = "{random(min,max)}",
            SuggestionCommand = "{random(min,max)}",
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
            SubCategory = MopActionSubCategory.Variables,
            TextCommand = "{random(v1,v2,v3,...)}",
            SuggestionCommand = "{random(v1,v2,v3,...)}",
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
            Two values always produce a range: {random(1,5)} = any integer 1-5.
            Three+ values pick from the exact set: {random(1,3,5)} = 1, 3, or 5 only.
            Supports decimals in lists: {random(0.5,1.0,1.5)} = 0.5, 1.0, or 1.5.
            """
        },

        //  Target
        new MopAction {
            Category = MopActionCategory.MacroAction,
            SubCategory = MopActionSubCategory.Target,
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
            SubCategory = MopActionSubCategory.Target,
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
            SubCategory = MopActionSubCategory.Target,
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
            SubCategory = MopActionSubCategory.Target,
            TextCommand = "/moptargetmyminion",
            SuggestionCommand = "/moptargetmyminion",
            Example = """
                /moptargetmyminion
            """,
            Notes = """
            Target your current minion
            """
        },

        //  Movement
        new MopAction {
            Category = MopActionCategory.MacroAction,
            SubCategory = MopActionSubCategory.Movement,
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
        new MopAction {
            Category = MopActionCategory.MacroAction,
            SubCategory = MopActionSubCategory.Movement,
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
        new MopAction {
            Category = MopActionCategory.MacroAction,
            SubCategory = MopActionSubCategory.Movement,
            TextCommand = "/mopformationmove \"Formation Name\" [forward|backward] [stride] [sequenceIndex] [continuous|precise] [self|target|ftarget]",
            SuggestionCommand = "/mopformationmove \"Formation Name\" forward 1 0",
            Example = """
            /mopformationmove "Circle" forward 1 0
            /mopformationmove "Circle" backward 2 3
            /mopformationmove "Circle" forward 1 0 precise
            /mopformationmove "Circle" forward 1 0 precise target
            """,
            Notes = """
            Broadcasts one saved-formation movement step from the current character.
            Stride is the skip amount through formation point order; sequenceIndex is the zero-based step to execute from each recipient's computed path.
            Default: precise. Use continuous for smoother loops.
            Add target to anchor the movement at the controller's current target.
            Generated formation macros use continuous by default.
            """
        },
        new MopAction {
            Category = MopActionCategory.MacroAction,
            SubCategory = MopActionSubCategory.Movement,
            TextCommand = "/mopformationgoto \"Formation Name\" <pointNumber> [anchor=<self|target|\"Character Name@World\">] [continuous|precise]",
            SuggestionCommand = "/mopformationgoto \"Formation Name\" 2 anchor=\"Character Name@World\"",
            Example = """
            /mopformationgoto "Circle" 2 anchor="Anchor Character@World"
            /mopformationgoto "Circle" 3 anchor=target precise
            /mopformationgoto "Circle" 4 anchor=self
            """,
            Notes = """
            Moves only this client to a specific saved formation point.
            Point numbers are 1-based; point 1 is always the live anchor/origin.
            Use anchor="Name@World" when each PC should place itself relative to the same visible anchor character.
            anchor=target uses this client's current target.
            Default: precise. Use continuous for smoother loops.
            This is the precise local alternative to IPC-broadcast /mopformationmove.
            """
        },
        new MopAction {
            Category = MopActionCategory.MacroAction,
            SubCategory = MopActionSubCategory.Movement,
            TextCommand = "/mopstopmove",
            SuggestionCommand = "/mopstopmove",
            Example = """
            /mopstopmove
            """,
            Notes = """
            Stops all movements
            """
        },
        new MopAction {
            Category = MopActionCategory.MacroAction,
            SubCategory = MopActionSubCategory.Movement,
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
        new MopAction {
            Category = MopActionCategory.MacroAction,
            SubCategory = MopActionSubCategory.Movement,
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
        new MopAction {
            Category = MopActionCategory.MacroAction,
            SubCategory = MopActionSubCategory.Movement,
            TextCommand = "/mopmovetotarget",
            SuggestionCommand = "/mopmovetotarget",
            Example = """
            /mopmovetotarget
            """,
            Notes = """
            Moves to target position
            """
        },
        new MopAction {
            Category = MopActionCategory.MacroAction,
            SubCategory = MopActionSubCategory.Movement,
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
        new MopAction {
            Category = MopActionCategory.MacroAction,
            SubCategory = MopActionSubCategory.Movement,
            TextCommand = "/mopwalkon",
            SuggestionCommand = "/mopwalkon",
            Example = """
            /mopwalkon
            """,
            Notes = """
            Enable walking
            """
        },
        new MopAction {
            Category = MopActionCategory.MacroAction,
            SubCategory = MopActionSubCategory.Movement,
            TextCommand = "/mopwalkoff",
            SuggestionCommand = "/mopwalkoff",
            Example = """
            /mopwalkoff
            """,
            Notes = """
            Disable walking
            """
        },
        new MopAction {
            Category = MopActionCategory.MacroAction,
            SubCategory = MopActionSubCategory.Movement,
            TextCommand = "/mopwalktoggle",
            SuggestionCommand = "/mopwalktoggle",
            Example = """
            /mopwalktoggle
            """,
            Notes = """
            Toggle walking
            """
        },
        new MopAction {
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

        //  General
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
    ];
}
