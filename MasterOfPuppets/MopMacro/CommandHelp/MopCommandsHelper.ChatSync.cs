using System.Collections.Generic;

namespace MasterOfPuppets;

public static partial class MopCommandsHelper {
    private static List<MopAction> GetChatSyncCommands() =>
    [
        new MopAction {
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

            Inline variables (-var=) override the macro's variables at runtime.
            Format: -var=$name=value;$name2="value with spaces";$name3=/command
            Variable names must start with a letter or underscore.
            When a name is set in more than one place, inline vars win over command-level vars,
            which win over the macro's own Variables field.
            """
        },
        new MopAction {
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

            Chat translates a few special names before the command runs. For inline chat
            actions, use [ ] instead of < > so the intended character name is used:
                /ac heal <me> => /ac heal [me]
                /ac heal <t> => /ac heal [t]
            """
        },
        new MopAction {
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

            Chat translates a few special names before the command runs. For inline chat
            actions, use [ ] instead of < > so the intended character name is used:
                /ac heal <me> => /ac heal [me]
                /ac heal <t> => /ac heal [t]
            """
        },
        new MopAction {
            Category = MopActionCategory.ChatSyncCommand,
            TextCommand = "mopbrc \"Character Name\" <command>",
            SuggestionCommand = "mopbrc ",
            Example = """
            Broadcast a command to a specific character via chat:
                mopbrc "Character Name" /clap
                mopbrc "Character Name" /cheer
                mopbrc "Character Name" /mopaction "Action Name"
                mopbrc "Character Name" /moptargetof "Warrior of Light@World"
            """,
            Notes = """
            * This is a chat sync command - broadcast a command to a specific character via chat

            Chat translates a few special names before the command runs. For inline chat
            actions, use [ ] instead of < > so the intended character name is used:
                /ac heal <me> => /ac heal [me]
                /ac heal <t> => /ac heal [t]
            """
        },
        new MopAction {
            Category = MopActionCategory.ChatSyncCommand,
            TextCommand = "mopbrg \"Group Name\" <command>",
            SuggestionCommand = "mopbrg ",
            Example = """
            Broadcast a command to a specific group via chat:
                mopbrg "Group Name" /clap
                mopbrg "Group Name" /cheer
                mopbrg "Group Name" /mopaction "Action Name"
                mopbrg "Group Name" /moptargetof "Warrior of Light@World"
            """,
            Notes = """
            * This is a chat sync command - broadcast a command to a specific group via chat

            Chat translates a few special names before the command runs. For inline chat
            actions, use [ ] instead of < > so the intended character name is used:
                /ac heal <me> => /ac heal [me]
                /ac heal <t> => /ac heal [t]
            """
        },
        new MopAction {
            Category = MopActionCategory.ChatSyncCommand,
            TextCommand = "mopformation \"Formation Name\" [sender|default|self|target|\"Character Name\"|\"Character Name@World\"] [continuous|precise|natural]",
            SuggestionCommand = "mopformation ",
            Example = """
            Move each chat-sync client to its assigned point in a shared formation:
                mopformation "Tight Circle"
                mopformation "Tight Circle" target precise
                mopformation "Tight Circle" "Anchor Character@World"
            """,
            Notes = """
            * This is a chat sync command - all clients reading the chat move only their local character.

            Without an explicit anchor, the chat sender is used as the live anchor and must be visible.
            Use default to anchor on point 1's assigned character.
            Default: precise. continuous gives smoother loops. natural keeps the walk/run
            toggle as it is while the character tracks the formation live.
            All clients need the same formation imported or configured.
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
    ];
}
