using System;
using System.Globalization;
using System.Linq;

using Dalamud.Game.Command;
using Dalamud.Interface.ImGuiNotification;
using Dalamud.Plugin;

using MasterOfPuppets.Ipc;
using MasterOfPuppets.Resources;

namespace MasterOfPuppets;

public class Plugin : IDalamudPlugin
{
    internal static string Name => "Master Of Puppets";

    // added Configuration override
    internal Configuration Config { get; set; }
    internal PluginUi Ui { get; }
    internal IpcProvider IpcProvider { get; }

    public Plugin(IDalamudPluginInterface pluginInterface)
    {
        pluginInterface.Create<DalamudApi>();

        Config = pluginInterface.GetPluginConfig() as Configuration ?? new Configuration();

        Config.Initialize(DalamudApi.PluginInterface);

        Ui = new PluginUi(this);

        IpcProvider = new IpcProvider(this);

        OnLanguageChange(DalamudApi.PluginInterface.UiLanguage);

        DalamudApi.PluginInterface.LanguageChanged += OnLanguageChange;

        DalamudApi.CommandManager.AddHandler("/mastertofpuppets", new CommandInfo(OnCommand)
        {
            HelpMessage = "Use with no arguments to show plugin window. Use with \"run\" X to run macro number",
        });

        DalamudApi.CommandManager.AddHandler("/mop", new CommandInfo(OnCommand)
        {
            HelpMessage = "Alias command",
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
        DalamudApi.CommandManager.RemoveHandler("/mastertofpuppets");
        DalamudApi.CommandManager.RemoveHandler("/mop");
        DalamudApi.PluginInterface.LanguageChanged -= OnLanguageChange;
        IpcProvider.Dispose();
        Ui.Dispose();
    }

    private void OnCommand(string command, string args)
    {
        var argsList = args.ToLowerInvariant().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
        // DalamudApi.PluginLog.Debug($"command: {command}, {string.Join('|', argsList)}");

        if (argsList.Any())
        {
            switch (argsList[0])
            {
                case "run":
                    {
                        if (argsList.Count <= 1 || !int.TryParse(argsList[1], out var macroIndex))
                        {
                            DalamudApi.ShowNotification($"Invalid arguments for run command", NotificationType.Error, 5000);
                            return;
                        }

                        var isValidMacroIndex = macroIndex >= 0 || macroIndex < Config.Macros.Count;
                        if (!isValidMacroIndex) return;

                        // DalamudApi.PluginLog.Debug($"RunMacro: ({macroIndex - 1})");
                        IpcProvider.RunMacro(macroIndex - 1);
                    }
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

