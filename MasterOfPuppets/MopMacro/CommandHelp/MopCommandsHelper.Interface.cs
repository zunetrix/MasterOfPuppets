using System.Collections.Generic;

namespace MasterOfPuppets;

public static partial class MopCommandsHelper {
    private static List<MopAction> GetInterfaceCommands() =>
    [
        new MopAction {
            Category = MopActionCategory.Interface,
            TextCommand = "/mop",
            SuggestionCommand = "/mop",
            Example = """
            /mop
            """,
            Notes = """
            * This is a plugin command (works only on local clients)
            Toggle main window
            """
        },
        new MopAction {
            Category = MopActionCategory.Interface,
            TextCommand = "/mop formation",
            SuggestionCommand = "/mop formation",
            Example = """
            /mop formation
            """,
            Notes = """
            * This is a plugin command (works only on local clients)
            Toggle formation window
            """
        },
        new MopAction {
            Category = MopActionCategory.Interface,
            TextCommand = "/mop settings",
            SuggestionCommand = "/mop settings",
            Example = """
            /mop settings
            """,
            Notes = """
            * This is a plugin command (works only on local clients)
            Toggle settings window
            """
        },
        new MopAction {
            Category = MopActionCategory.Interface,
            TextCommand = "/mop layout",
            SuggestionCommand = "/mop layout",
            Example = """
            /mop layout
            """,
            Notes = """
            * This is a plugin command (works only on local clients)
            Toggle layout window
            """
        },
        new MopAction {
            Category = MopActionCategory.Interface,
            TextCommand = "/mop queue",
            SuggestionCommand = "/mop queue",
            Example = """
            /mop queue
            """,
            Notes = """
            * This is a plugin command (works only on local clients)
            Toggle macro queue window
            """
        },
        new MopAction {
            Category = MopActionCategory.Interface,
            TextCommand = "/mop actions",
            SuggestionCommand = "/mop actions",
            Example = """
            /mop actions
            """,
            Notes = """
            * This is a plugin command (works only on local clients)
            Toggle actions broadcast window
            """
        },
        new MopAction {
            Category = MopActionCategory.Interface,
            TextCommand = "/mop monitor",
            SuggestionCommand = "/mop monitor",
            Example = """
            /mop monitor
            """,
            Notes = """
            * This is a plugin command (works only on local clients)
            Toggle peer monitor window
            """
        },
    ];
}
