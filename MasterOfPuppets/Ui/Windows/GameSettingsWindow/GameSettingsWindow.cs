using System;

using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Windowing;

namespace MasterOfPuppets;

public partial class GameSettingsWindow : Window {
    private readonly Plugin Plugin;

    // Split panel state
    private float _leftPanelWidth;
    private float _rightPanelWidth;

    // Profile list state
    private int _selectedProfileIdx = -1;
    private string _newProfileName = string.Empty;
    private string _profileSearchFilter = string.Empty;
    private int _renamingProfileIdx = -1;
    private string _renameProfileBuffer = string.Empty;
    private bool _renamingProfileFocusPending;

    // Right panel (keys view) state
    private string _keysViewSearchFilter = string.Empty;

    // Profile Config Keys tab state
    private string _profileKeySearchFilter = string.Empty;
    private bool _showOnlyEnabledProfileKeys;

    // Broadcast tab state
    private SettingsDisplayObjectLimitType _objectQuantityType;

    internal GameSettingsWindow(Plugin plugin)
        : base($"{Plugin.Name} Game Settings Profile###GameSettingsWindow") {
        Plugin = plugin;

        Size = ImGuiHelpers.ScaledVector2(820, 580);
        SizeCondition = ImGuiCond.FirstUseEver;
        SizeConstraints = new WindowSizeConstraints {
            MinimumSize = ImGuiHelpers.ScaledVector2(600, 380),
        };
    }

    public override void OnOpen() {
        _objectQuantityType = GameSettingsManager.GetDisplayObjectLimit();
        base.OnOpen();
    }

    public override void Draw() {
        if (ImGui.BeginTabBar("##GscTabs")) {
            if (ImGui.BeginTabItem("Game Settings Profiles##GscProfiles")) {
                DrawProfilesTab();
                ImGui.EndTabItem();
            }
            if (ImGui.BeginTabItem("Profile Settings Keys##GscProfileKeys")) {
                DrawProfileConfigKeysTab();
                ImGui.EndTabItem();
            }
            if (ImGui.BeginTabItem("Settings Broadcast##GscBroadcast")) {
                DrawBroadcastTab();
                ImGui.EndTabItem();
            }
            if (ImGui.BeginTabItem("Settings Debug##GscDebug")) {
                DrawDebugTab();
                ImGui.EndTabItem();
            }
            ImGui.EndTabBar();
        }
    }

    private void CommitProfileRename(int i) {
        if (_renamingProfileIdx != i) return;
        var trimmed = _renameProfileBuffer.Trim();
        if (!string.IsNullOrEmpty(trimmed) &&
            !Plugin.Config.GameSettingsProfiles.Exists(p =>
                p.Name.Equals(trimmed, StringComparison.OrdinalIgnoreCase))) {
            Plugin.Config.GameSettingsProfiles[i].Name = trimmed;
        }
        _renamingProfileIdx = -1;
        Plugin.Config.Save();
        Plugin.IpcProvider.SyncConfiguration();
    }
}
