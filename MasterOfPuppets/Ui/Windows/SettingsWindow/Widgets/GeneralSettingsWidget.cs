using System;

using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;

using MasterOfPuppets.Resources;
using MasterOfPuppets.Util;
using MasterOfPuppets.Util.ImGuiExt;

namespace MasterOfPuppets;

public class GeneralSettingsWidget : Widget {
    public override string Title => Language.SettingsGeneralTab;
    public override FontAwesomeIcon Icon => FontAwesomeIcon.Cog;

    public GeneralSettingsWidget(WidgetContext ctx) : base(ctx) {
    }

    public override void Draw() {
        var Plugin = Context.Plugin;

        using (ImGuiGroupPanel.BeginGroupPanel(Language.SettingsGeneralTab)) {
            var syncClients = Plugin.Config.SyncClients;
            if (ImGui.Checkbox(Language.SettingsWindowSyncClients, ref syncClients)) {
                Plugin.Config.SyncClients = syncClients;
                Plugin.Config.Save();
                Plugin.IpcProvider.SyncConfiguration();
            }
            ImGuiUtil.HelpMarker("Allow actions to be executed in broadcast to all clients");

            var saveConfigAfterSync = Plugin.Config.SaveConfigAfterSync;
            if (ImGui.Checkbox(Language.SettingsWindowSaveConfigAfterSync, ref saveConfigAfterSync)) {
                Plugin.Config.SaveConfigAfterSync = saveConfigAfterSync;
                Plugin.Config.Save();
                Plugin.IpcProvider.SyncConfiguration();
            }
            ImGuiUtil.HelpMarker("Enable for accounts with individual config file");
        }

        ImGui.Spacing();
        ImGui.Spacing();

        using (ImGuiGroupPanel.BeginGroupPanel("Plugin Window")) {
            var openOnStartup = Plugin.Config.OpenOnStartup;
            if (ImGui.Checkbox(Language.SettingsWindowOpenOnStartup, ref openOnStartup)) {
                Plugin.Config.OpenOnStartup = openOnStartup;
                Plugin.IpcProvider.SyncConfiguration();
            }

            var openOnLogin = Plugin.Config.OpenOnLogin;
            if (ImGui.Checkbox(Language.SettingsWindowOpenLogin, ref openOnLogin)) {
                Plugin.Config.OpenOnLogin = openOnLogin;
                Plugin.IpcProvider.SyncConfiguration();
            }

            var allowCloseWithEscape = Plugin.Config.AllowCloseWithEscape;
            if (ImGui.Checkbox(Language.SettingsWindowAllowCloseWithEscape, ref allowCloseWithEscape)) {
                Plugin.Config.AllowCloseWithEscape = allowCloseWithEscape;
                Plugin.IpcProvider.SyncConfiguration();
                Plugin.Ui.MainWindow.UpdateWindowConfig();
            }
        }

        ImGui.Spacing();
        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();
        ImGui.Spacing();

        using (ImRaii.PushColor(ImGuiCol.Button, Style.Components.ButtonPurpleNormal)
            .Push(ImGuiCol.ButtonHovered, Style.Components.ButtonPurpleHovered)
            .Push(ImGuiCol.ButtonActive, Style.Components.ButtonPurpleActive)) {

            if (ImGui.Button(Language.OpenPluginFolder)) {
                WindowsApi.OpenFolder(DalamudApi.PluginInterface.ConfigDirectory.FullName);
            }

            ImGui.SameLine();
            ImGuiHelpers.ScaledDummy(0, 20);
            ImGui.SameLine();

            if (ImGui.Button(Language.OpenPluginConfigFile)) {
                WindowsApi.OpenFile(DalamudApi.PluginInterface.ConfigFile.FullName);
            }
        }
    }
}
