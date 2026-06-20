using System.Collections.Generic;

using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;

using MasterOfPuppets.Extensions;
using MasterOfPuppets.Resources;
using MasterOfPuppets.Util.ImGuiExt;

namespace MasterOfPuppets;

public partial class CharactersWindow : Window {
    private Plugin Plugin { get; }

    // Characters tab state
    private string _charSearchFilter = string.Empty;
    private string _addCharSelected = string.Empty;
    private readonly ImGuiComboSearch _addCharCombo = new();
    public HashSet<ulong> _copiedCids = new();

    // Groups tab state
    private string _editGroupName = string.Empty;
    private int _editGroupNameLastIndex = -1;
    private int _selectedCidGroupIndex { get; set; } = 0;
    private string _groupSearchFilter = string.Empty;
    private string _newGroupName = string.Empty;
    private int _groupRenamingIdx = -1;
    private string _groupRenameBuffer = string.Empty;
    private bool _groupRenamingFocusPending;
    private float _groupLeftPanelWidth;

    public CharactersWindow(Plugin plugin) : base($"{Plugin.Name} Characters###CharactersWindow") {
        Plugin = plugin;

        Size = ImGuiHelpers.ScaledVector2(450, 400);
        SizeCondition = ImGuiCond.FirstUseEver;
    }

    public override void PreDraw() {
        base.PreDraw();
    }

    private bool IsValidGroup() {
        return Plugin.Config.CidsGroups.IndexExists(_selectedCidGroupIndex) && Plugin.Config.CidsGroups.Count > 0;
    }

    public override void Draw() {
        using var tabBar = ImRaii.TabBar($"{Language.SettingsGeneralTab}###CharactersManagerTabs");
        if (!tabBar) return;

        DrawCharactersTab();
        DrawCidsGroupsTab();
    }
}
