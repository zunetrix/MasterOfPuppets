using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

using Dalamud.Game.Command;
using Dalamud.Interface.ImGuiNotification;
using Dalamud.Plugin;

using MasterOfPuppets.Ipc;
using MasterOfPuppets.Resources;
using MasterOfPuppets.Util.ImGuiExt.AutoComplete;

namespace MasterOfPuppets;

public class Plugin : IDalamudPlugin {
    internal static string Name => "Master Of Puppets";

    internal Configuration Config { get; }
    internal PluginUi Ui { get; }
    internal IpcProvider IpcProvider { get; }
    internal ChatWatcher ChatWatcher { get; }
    internal MacroHandler MacroHandler { get; }
    internal MacroManager MacroManager { get; }
    internal CompletionIndex CompletionIndex { get; }

    public Plugin(IDalamudPluginInterface pluginInterface) {
        pluginInterface.Create<DalamudApi>();
        Config = pluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        Config.Initialize(DalamudApi.PluginInterface);

        Ui = new PluginUi(this);
        IpcProvider = new IpcProvider(this);
        ChatWatcher = new ChatWatcher(this);
        MacroManager = new MacroManager(this);
        MacroHandler = new MacroHandler(this);
        CompletionIndex = new CompletionIndex();

        OnLanguageChange(DalamudApi.PluginInterface.UiLanguage);
        DalamudApi.PluginInterface.LanguageChanged += OnLanguageChange;

        DalamudApi.CommandManager.AddHandler("/mop", new CommandInfo(OnCommand) {
            HelpMessage = """
            Commands:
                / mop->show / hide UI
                / mop run "Macro name"->execute macro
                / mop stop->stop macro execution
                / mop queue->show queue window
                / mop targetmytarget
                / mop targetclear
            """,
        });

        DalamudApi.ClientState.Login += OnLogin;
        DalamudApi.ClientState.Logout += OnLogout;
        DalamudApi.PluginInterface.UiBuilder.Draw += Ui.Draw;
        DalamudApi.PluginInterface.UiBuilder.OpenConfigUi += Ui.SettingsWindow.Toggle;
        DalamudApi.PluginInterface.UiBuilder.OpenMainUi += Ui.MainWindow.Toggle;

        if (Config.OpenOnStartup) {
            Ui.MainWindow.IsOpen = true;
        }
    }

    public void Dispose() {
        DalamudApi.PluginInterface.UiBuilder.OpenConfigUi -= Ui.SettingsWindow.Toggle;
        DalamudApi.PluginInterface.UiBuilder.OpenMainUi -= Ui.MainWindow.Toggle;
        DalamudApi.PluginInterface.UiBuilder.Draw -= Ui.Draw;
        DalamudApi.ClientState.Logout -= OnLogout;
        DalamudApi.ClientState.Login -= OnLogin;
        DalamudApi.PluginInterface.LanguageChanged -= OnLanguageChange;

        DalamudApi.CommandManager.RemoveHandler("/mop");
        IpcProvider.Dispose();
        ChatWatcher.Dispose();
        Ui.Dispose();
    }

    private static List<string> ParseArgs(string args) {
        var matches = Regex.Matches(args.ToLowerInvariant(), @"[\""].+?[\""]|[^ ]+");
        var list = new List<string>();

        foreach (Match match in matches) {
            var value = match.Value;

            if (value.StartsWith("\"") && value.EndsWith("\"")) {
                value = value.Substring(1, value.Length - 2);
            }

            list.Add(value);
        }

        return list;
    }

    private void OnCommand(string command, string argsRaw) {
        var args = ParseArgs(argsRaw);
        // DalamudApi.PluginLog.Debug($"command: {command}: {string.Join('|', args)}");

        if (args.Any()) {
            switch (args[0]) {
                case "run": {
                        if (args.Count <= 1) {
                            DalamudApi.ShowNotification($"Invalid arguments to run macro", NotificationType.Error, 5000);
                            return;
                        }

                        var macroNameOrNumber = args[1];
                        int macroIndex = MacroManager.FindMacroIndex(macroNameOrNumber);
                        IpcProvider.RunMacro(macroIndex);
                    }
                    break;

                case "stop":
                    IpcProvider.StopMacroExecution();
                    break;

                case "queue":
                    Ui.MacroQueueWindow.Toggle();
                    break;

                case "targetmytarget":
                    IpcProvider.ExecuteTargetMyTarget();
                    break;

                case "targetclear":
                    IpcProvider.ExecuteTargetClear();
                    break;
                    // case "objectquantity":
                    //     {
                    //         if (args.Count <= 1)
                    //         {
                    //             DalamudApi.ShowNotification($"Invalid arguments to setobjectquantity", NotificationType.Error, 5000);
                    //             return;
                    //         }

                    //         if (!Enum.TryParse<SettingsDisplayObjectLimitType>(args[1], ignoreCase: true, out var displayObjectLimitType)
                    //             || !Enum.IsDefined(typeof(SettingsDisplayObjectLimitType), displayObjectLimitType))
                    //         {
                    //             DalamudApi.PluginLog.Warning($"Invalid object quantity value (0-5): {displayObjectLimitType}");
                    //             return;
                    //         }

                    //         IpcProvider.SetGameSettingsObjectQuantity(displayObjectLimitType);
                    //     }
                    //     break;
            }
        } else {
            // no args toggle plugin window
            Ui.MainWindow.Toggle();
        }
    }

    private static void OnLanguageChange(string langCode) {
        Language.Culture = new CultureInfo(langCode);
    }

    private void OnLogin() {
        if (Config.OpenOnLogin) {
            Ui.MainWindow.IsOpen = true;
        }
    }

    private void OnLogout(int type, int code) {
        Ui.MainWindow.IsOpen = false;
    }
}

