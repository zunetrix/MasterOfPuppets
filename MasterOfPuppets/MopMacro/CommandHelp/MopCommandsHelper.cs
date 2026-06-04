using System.Collections.Generic;
using System.Linq;

namespace MasterOfPuppets;

public static partial class MopCommandsHelper {
    public static readonly List<MopAction> Actions =
    [
        .. GetInterfaceCommands(),
        .. GetPluginCommands(),
        .. GetChatSyncCommands(),
        .. GetMacroActions(),
        .. GetGameActions(),
    ];

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
    public MopActionSubCategory SubCategory;
    public string TextCommand { get; set; } = string.Empty;
    public string SuggestionCommand { get; set; } = string.Empty;
    public string Example { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
}

public enum MopActionCategory {
    PluginCommand,
    MacroAction,
    Interface,
    ChatSyncCommand,
    GameAction,
}

public enum MopActionSubCategory {
    None,

    // PluginCommand
    Movement,
    Target,
    Party,
    HousingAndTravel,
    Broadcast,
    ExitActions,
    Settings,

    // MacroAction
    FlowControl,
    Variables,
    RenderAndCamera,
}
