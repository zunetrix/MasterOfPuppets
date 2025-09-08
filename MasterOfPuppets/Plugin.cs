using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

using Dalamud.Game.Command;
using Dalamud.Interface.ImGuiNotification;
using Dalamud.Plugin;

using MasterOfPuppets.Ipc;
using MasterOfPuppets.Resources;

namespace MasterOfPuppets;

public class Plugin : IDalamudPlugin
{
    internal static string Name => "Master Of Puppets";

    internal Configuration Config { get; }
    internal PluginUi Ui { get; }
    internal IpcProvider IpcProvider { get; }
    internal ChatWatcher ChatWatcher { get; }

    public Plugin(IDalamudPluginInterface pluginInterface)
    {
        pluginInterface.Create<DalamudApi>();
        Config = pluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        Config.Initialize(DalamudApi.PluginInterface);

        Ui = new PluginUi(this);
        IpcProvider = new IpcProvider(this);
        ChatWatcher = new ChatWatcher(this);

        OnLanguageChange(DalamudApi.PluginInterface.UiLanguage);
        DalamudApi.PluginInterface.LanguageChanged += OnLanguageChange;

        DalamudApi.CommandManager.AddHandler("/masterofpuppets", new CommandInfo(OnCommand)
        {
            HelpMessage = "Use with no arguments to show plugin window.",
        });

        DalamudApi.CommandManager.AddHandler("/mop", new CommandInfo(OnCommand)
        {
            HelpMessage = """
            Alias command
                /mop run macro_number
                /mop run "Macro name"
                /mop stop

                /mop targetmytarget
                /mop targetclear
            """,
        });

        DalamudApi.ClientState.Login += OnLogin;
        DalamudApi.ClientState.Logout += OnLogout;
        DalamudApi.PluginInterface.UiBuilder.Draw += Ui.Draw;
        DalamudApi.PluginInterface.UiBuilder.OpenConfigUi += Ui.SettingsWindow.Toggle;
        DalamudApi.PluginInterface.UiBuilder.OpenMainUi += Ui.MainWindow.Toggle;

        if (Config.OpenOnStartup)
        {
            Ui.MainWindow.IsOpen = true;
        }
    }

    public void Dispose()
    {
        DalamudApi.PluginInterface.UiBuilder.OpenConfigUi -= Ui.SettingsWindow.Toggle;
        DalamudApi.PluginInterface.UiBuilder.OpenMainUi -= Ui.MainWindow.Toggle;
        DalamudApi.PluginInterface.UiBuilder.Draw -= Ui.Draw;
        DalamudApi.ClientState.Logout -= OnLogout;
        DalamudApi.ClientState.Login -= OnLogin;
        DalamudApi.PluginInterface.LanguageChanged -= OnLanguageChange;

        DalamudApi.CommandManager.RemoveHandler("/masterofpuppets");
        DalamudApi.CommandManager.RemoveHandler("/mop");
        IpcProvider.Dispose();
        ChatWatcher.Dispose();
        Ui.Dispose();
    }

    private static List<string> ParseArgs(string args)
    {
        var matches = Regex.Matches(args.ToLowerInvariant(), @"[\""].+?[\""]|[^ ]+");
        var list = new List<string>();

        foreach (Match match in matches)
        {
            var value = match.Value;

            if (value.StartsWith("\"") && value.EndsWith("\""))
            {
                value = value.Substring(1, value.Length - 2);
            }

            list.Add(value);
        }

        return list;
    }

    private void OnCommand(string command, string argsRaw)
    {
        var args = ParseArgs(argsRaw);
        // DalamudApi.PluginLog.Debug($"command: {command}: {string.Join('|', args)}");

        if (args.Any())
        {
            switch (args[0])
            {
                case "run":
                    {
                        int macroIndexByName = Config.Macros.FindIndex(m => string.Equals(m.Name, args[1], StringComparison.OrdinalIgnoreCase));

                        if (args.Count <= 1 || (!int.TryParse(args[1], out var macroIndexArg) && macroIndexByName == -1))
                        {
                            DalamudApi.ShowNotification($"Invalid arguments to run macro", NotificationType.Error, 5000);
                            return;
                        }

                        // user input 1 index based
                        int macroIndex = macroIndexByName != -1 ? macroIndexByName : macroIndexArg - 1;
                        var isValidMacroIndex = Config.Macros.IndexExists(macroIndex);
                        if (!isValidMacroIndex) return;

                        IpcProvider.RunMacro(macroIndex);
                    }
                    break;
                case "stop":
                    IpcProvider.StopMacroExecution();
                    break;
                case "targetmytarget":
                    IpcProvider.ExecuteTargetMyTarget();
                    break;

                case "targetclear":
                    IpcProvider.ExecuteTargetClear();
                    break;
            }
        }
        else
        {
            // no args toggle plugin window
            Ui.MainWindow.IsOpen = !Ui.MainWindow.IsOpen;
        }
    }

    private static void OnLanguageChange(string langCode)
    {
        Language.Culture = new CultureInfo(langCode);
    }

    private void OnLogin()
    {
        if (Config.OpenOnLogin)
        {
            Ui.MainWindow.IsOpen = true;
        }
    }

    private void OnLogout(int type, int code)
    {
        Ui.MainWindow.IsOpen = false;
    }
}

