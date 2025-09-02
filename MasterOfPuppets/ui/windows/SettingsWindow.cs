using System;
using System.Numerics;

using Dalamud.Interface.ImGuiFileDialog;
using Dalamud.Interface.Windowing;
using Dalamud.Bindings.ImGui;

using MasterOfPuppets.Resources;

namespace MasterOfPuppets;

public class SettingsWindow : Window
{
    private Plugin Plugin { get; }
    private FileDialogManager FileDialogManager { get; }

    public SettingsWindow(Plugin plugin) : base($"{Plugin.Name} {Language.SettingsTitle}###SettingsWindow")
    {
        Plugin = plugin;

        Size = new Vector2(500, 300);
        SizeCondition = ImGuiCond.Always;
        Flags = ImGuiWindowFlags.NoResize;

        FileDialogManager = new FileDialogManager();
    }

    public override void PreDraw()
    {
        FileDialogManager.Draw();
        base.PreDraw();
    }

    public override void Draw()
    {
        if (!ImGui.BeginTabBar("##SettingsTabs")) return;

        if (ImGui.BeginTabItem($"{Language.SettingsGeneralTab}###GeneralTab"))
        {
            var syncClients = Plugin.Config.SyncClients;
            if (ImGui.Checkbox(Language.SettingsWindowSyncClients, ref syncClients))
            {
                Plugin.Config.SyncClients = syncClients;
                Plugin.Config.Save();
                Plugin.IpcProvider.SyncConfiguration();
            }

            ImGui.Spacing();
            ImGui.Spacing();

            var saveConfigAfterSync = Plugin.Config.SaveConfigAfterSync;
            if (ImGui.Checkbox(Language.SettingsWindowSaveConfigAfterSync, ref saveConfigAfterSync))
            {
                Plugin.Config.SaveConfigAfterSync = saveConfigAfterSync;
                Plugin.Config.Save();
                Plugin.IpcProvider.SyncConfiguration();
            }
            ImGuiUtil.HelpMarker("Enable for accounts with individual config file");

            ImGui.Spacing();
            ImGui.Spacing();

            ImGui.TextUnformatted("Delay between actions");
            ImGui.SetNextItemWidth(150);
            var delayBetweenActions = Plugin.Config.DelayBetweenActions;
            if (ImGui.InputInt("##DelayBetrweenActions", ref delayBetweenActions, 1, 10, default, ImGuiInputTextFlags.AutoSelectAll))
            {
                delayBetweenActions = Math.Clamp(delayBetweenActions, 1, 10);
                Plugin.Config.DelayBetweenActions = delayBetweenActions;
                Plugin.Config.Save();
                Plugin.IpcProvider.SyncConfiguration();
            }


            // ImGui.Spacing();
            // var targetedColour = Plugin.Config.TargetedColour;
            // if (ImGui.ColorEdit4(Language.SettingsMarkersMarkTargetColour, ref targetedColour))
            // {
            //     Plugin.Config.TargetedColour = targetedColour;
            //     Plugin.Config.Save();
            // }

            // var targetedSize = Plugin.Config.TargetedSize;
            // if (ImGui.DragFloat(Language.SettingsMarkersMarkTargetSize, ref targetedSize, 0.01f, 0f, 15f))
            // {
            //     targetedSize = Math.Max(0f, targetedSize);
            //     Plugin.Config.TargetedSize = targetedSize;
            //     Plugin.Config.Save();
            // }

            ImGui.EndTabItem();
        }


        // if (ImGui.BeginTabItem($"{Language.SettingsSoundTab}###sound-tab"))
        // {
        //     var playSound = Plugin.Config.PlaySoundOnTarget;
        //     if (ImGui.Checkbox(Language.SettingsSoundEnabled, ref playSound))
        //     {
        //         Plugin.Config.PlaySoundOnTarget = playSound;
        //         Plugin.Config.Save();
        //     }

        //     ImGui.TextUnformatted(Language.SettingsSoundPath);
        //     Vector2 buttonSize;
        //     ImGui.PushFont(UiBuilder.IconFont);
        //     try
        //     {
        //         buttonSize = ImGuiHelpers.GetButtonSize(FontAwesomeIcon.Folder.ToIconString());
        //     }
        //     finally
        //     {
        //         ImGui.PopFont();
        //     }

        //     var path = Plugin.Config.SoundPath ?? "";
        //     ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X - buttonSize.X);
        //     if (ImGui.InputText("###sound-path", ref path, 1_000))
        //     {
        //         path = path.Trim();
        //         Plugin.Config.SoundPath = path.Length == 0 ? null : path;
        //         Plugin.Config.Save();
        //     }

        //     ImGui.SameLine();

        //     ImGui.PushFont(UiBuilder.IconFont);
        //     try
        //     {
        //         if (ImGui.Button(FontAwesomeIcon.Folder.ToIconString()))
        //         {
        //             FileDialogManager.OpenFileDialog(Language.SettingsSoundPath, ".wav,.mp3,.aif,.aiff,.wma,.aac", (selected, selectedPath) =>
        //             {
        //                 if (!selected)
        //                 {
        //                     return;
        //                 }

        //                 path = selectedPath.Trim();
        //                 Plugin.Config.SoundPath = path.Length == 0 ? null : path;
        //                 Plugin.Config.Save();
        //             });
        //         }
        //     }
        //     finally
        //     {
        //         ImGui.PopFont();
        //     }

        //     ImGui.Text(Language.SettingsSoundPathHelp);

        //     var volume = Plugin.Config.SoundVolume * 100f;
        //     if (ImGui.DragFloat(Language.SettingsSoundVolume, ref volume, .1f, 0f, 100f, "%.1f%%"))
        //     {
        //         Plugin.Config.SoundVolume = Math.Max(0f, Math.Min(1f, volume / 100f));
        //         Plugin.Config.Save();
        //     }

        //     var soundCooldown = Plugin.Config.SoundCooldown;
        //     if (ImGui.DragFloat(Language.SettingsSoundCooldown, ref soundCooldown, .01f, 0f, 30f))
        //     {
        //         soundCooldown = Math.Max(0f, soundCooldown);
        //         Plugin.Config.SoundCooldown = soundCooldown;
        //         Plugin.Config.Save();
        //     }

        //     var playWhenClosed = Plugin.Config.PlaySoundWhenClosed;
        //     if (ImGui.Checkbox(Language.SettingsSoundPlayWhenClosed, ref playWhenClosed))
        //     {
        //         Plugin.Config.PlaySoundWhenClosed = playWhenClosed;
        //         Plugin.Config.Save();
        //     }

        //     ImGui.EndTabItem();
        // }

        if (ImGui.BeginTabItem($"{Language.SettingsWindowTab}###WindowTab"))
        {
            var openOnStartup = Plugin.Config.OpenOnStartup;
            if (ImGui.Checkbox(Language.SettingsWindowOpenOnStartup, ref openOnStartup))
            {
                Plugin.Config.OpenOnStartup = openOnStartup;
                Plugin.Config.Save();
                Plugin.IpcProvider.SyncConfiguration();
            }

            var openOnLogin = Plugin.Config.OpenOnLogin;
            if (ImGui.Checkbox(Language.SettingsWindowOpenLogin, ref openOnLogin))
            {
                Plugin.Config.OpenOnLogin = openOnLogin;
                Plugin.Config.Save();
                Plugin.IpcProvider.SyncConfiguration();
            }

            // var showSettingsButton = Plugin.Config.ShowSettingsButton;
            // if (ImGui.Checkbox(Language.SettingsWindowShowConfigButton, ref showSettingsButton))
            // {
            //     Plugin.Config.ShowSettingsButton = showSettingsButton;
            //     Plugin.Config.Save();
            //     Plugin.Ui.MainWindow.UpdateConfig();
            // }

            // var allowMovement = Plugin.Config.AllowMovement;
            // if (ImGui.Checkbox(Language.SettingsWindowAllowMovement, ref allowMovement))
            // {
            //     Plugin.Config.AllowMovement = allowMovement;
            //     Plugin.Config.Save();
            // }

            // var allowResizing = Plugin.Config.AllowResize;
            // if (ImGui.Checkbox(Language.SettingsWindowAllowResize, ref allowResizing))
            // {
            //     Plugin.Config.AllowResize = allowResizing;
            //     Plugin.Config.Save();
            // }

            // var allowCloseWithEscape = Plugin.Config.AllowCloseWithEscape;
            // if (ImGui.Checkbox(Language.SettingsWindowAllowCloseWithEscape, ref allowCloseWithEscape))
            // {
            //     Plugin.Config.AllowCloseWithEscape = allowCloseWithEscape;
            //     Plugin.Config.Save();
            //     Plugin.Ui.MainWindow.UpdateConfig();
            // }
            ImGui.EndTabItem();
        }

        ImGui.EndTabBar();
    }
}
