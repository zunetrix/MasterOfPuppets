using System.Collections.Generic;

namespace MasterOfPuppets;

public static partial class MopCommandsHelper {
    private static List<MopAction> GetLuaScriptCommands() =>
    [
        new MopAction {
            Category = MopActionCategory.LuaScript,
            TextCommand = "/mop lua run \"Script Name\" [-var=$name=value;...]",
            SuggestionCommand = "/mop lua run ",
            Example = """
            /mop lua run "Swirling Vortex" -var=$anchor="[t]"
            """,
            Notes = """
            * Runs a script on all active MoP clients on the current PC.
            * Uses the same inline -var syntax as /mop run. Lua reads values with mop.get_var("name").
            * The saved Participant Formation determines which clients run and their global slot order.
            * The current selected target is supplied to Lua as mop.get_run_target().
            * A script can choose to turn that target into a movement anchor with mop.set_anchor(anchor).
            * mop.follow_actor({...}) follows the first visible actor in an ordered fallback list using a Lua-defined rotated offset, facing, braking, prediction, and steering policy.
            * Lua actor following is independent from macro execution and saved formation definitions.
            * Scripts that do not use a target simply ignore this value.
            """
        },
        new MopAction {
            Category = MopActionCategory.LuaScript,
            TextCommand = "/mop lua sync \"Script Name\" [-var=$name=value;...]",
            SuggestionCommand = "/mop lua sync ",
            Example = """
            /mop lua sync "Swirling Vortex" -var=$anchor="[t]"
            """,
            Notes = """
            * Local alias for the cross-PC mopluarun command.
            * MoP sends the synchronized run through the Chat Sync prefix configured in Settings.
            * The preferred matching chat command is: /cwl2 mopluarun "Script Name" -var=$anchor="<t>".
            * Performer membership and slot order come from the script's saved Participant Formation.
            * The script name, hash, variables, target, shared seed, and start time are synchronized.
            """
        },
        new MopAction {
            Category = MopActionCategory.LuaScript,
            TextCommand = "/mop lua pause|resume|stop [run-id|\"Script Name\"]",
            SuggestionCommand = "/mop lua stop",
            Example = """
            /mop lua pause "Swirling Vortex"
            /mop lua resume 08dc000000000000-12345678-abcd1234
            /mop lua stop "Swirling Vortex"

            Cross-PC:
            /cwl2 mopluastop
            """,
            Notes = """
            * Without a selector, pause, resume, and stop affect all active Lua runs on every MoP client on the current PC.
            * A selector can be a synchronized run ID or an exact script name.
            * Pausing immediately releases movement input while preserving the script VM and controller state.
            * Use mopluastop through the configured Chat Sync channel to stop every listening PC.
            """
        },
        new MopAction {
            Category = MopActionCategory.LuaScript,
            TextCommand = "/mop lua restart [run-id|\"Script Name\"]",
            SuggestionCommand = "/mop lua restart ",
            Example = """
            /mop lua restart "Swirling Vortex"
            """,
            Notes = """
            * Stops the selected run on local MoP clients and starts its saved script again with a new synchronized identity.
            * Without a selector, restarts the primary active run or the most recent run in local history.
            """
        },
        new MopAction {
            Category = MopActionCategory.LuaScript,
            TextCommand = "/mop lua status [run-id|\"Script Name\"]",
            SuggestionCommand = "/mop lua status",
            Example = """
            /mop lua status
            /mop lua status "Swirling Vortex"
            """,
            Notes = """
            * Prints active runs or matching history with run IDs, lifecycle state, and diagnostic detail.
            * Status is local to the game client where the command is entered.
            """
        },
    ];
}
