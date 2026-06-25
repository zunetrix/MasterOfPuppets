using System;
using System.IO;
using System.Numerics;

using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.ImGuiFileDialog;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;

using MasterOfPuppets.Extensions;
using MasterOfPuppets.Util;
using MasterOfPuppets.Util.ImGuiExt;

namespace MasterOfPuppets;

public partial class XivLauncherWindow : Window {
    private readonly Plugin Plugin;
    private readonly FileDialogManager _fileDialogManager = new();

    private float _leftPanelWidth;
    private float _rightPanelWidth;

    private string _searchFilter = string.Empty;
    private int _selEntry = -1;
    private int _renamingIdx = -1;
    private string _renameBuffer = string.Empty;
    private bool _renamingFocusPending;
    private readonly System.Collections.Generic.HashSet<XivLaunchEntry> _selectedForLaunch = new();

    private XivLaunchEntry? SelectedEntry {
        get {
            if (_selEntry >= 0 && _selEntry < Plugin.Config.XivLaunchEntries.Count)
                return Plugin.Config.XivLaunchEntries[_selEntry];
            return null;
        }
    }

    internal XivLauncherWindow(Plugin plugin)
        : base($"{Plugin.Name} XIV Launcher###XIVLauncherWindow") {
        Plugin = plugin;
        Size = ImGuiHelpers.ScaledVector2(780, 560);
        SizeCondition = ImGuiCond.FirstUseEver;
        SizeConstraints = new WindowSizeConstraints {
            MinimumSize = ImGuiHelpers.ScaledVector2(500, 300),
        };
    }

    public override void PreDraw() {
        _fileDialogManager.Draw();
        base.PreDraw();
    }

    public override void Draw() {
        if (ImGui.BeginTabBar("##xltabs")) {
            if (ImGui.BeginTabItem("Accounts")) {
                DrawAccountsTab();
                ImGui.EndTabItem();
            }
            if (ImGui.BeginTabItem("Settings")) {
                DrawGlobalLaunchSettings();
                ImGui.EndTabItem();
            }
            ImGui.EndTabBar();
        }
    }

    private void DrawAccountsTab() {
        float splitterW = 10f * ImGuiHelpers.GlobalScale;
        float minLeftW = 250f * ImGuiHelpers.GlobalScale;
        float minRightW = 280f * ImGuiHelpers.GlobalScale;

        var avail = ImGui.GetContentRegionAvail();

        if (_leftPanelWidth <= 0f) _leftPanelWidth = 260f * ImGuiHelpers.GlobalScale;
        if (_rightPanelWidth <= 0f) _rightPanelWidth = avail.X - _leftPanelWidth - splitterW;

        float maxLeftW = MathF.Max(avail.X - splitterW - minRightW, minLeftW);
        _leftPanelWidth = Math.Clamp(_leftPanelWidth, minLeftW, maxLeftW);
        _rightPanelWidth = MathF.Max(avail.X - _leftPanelWidth - splitterW, minRightW);

        float h = avail.Y;

        // Left panel
        using (ImRaii.Child("##xlleft", new Vector2(_leftPanelWidth, h), true)) {
            DrawLeftPanel();
        }

        ImGui.SameLine(0, 0);

        // Splitter
        using (ImRaii.PushStyle(ImGuiStyleVar.FramePadding, Vector2.Zero)) {
            ImGui.InvisibleButton("##xlsplit", new Vector2(splitterW, h));
            if (ImGui.IsItemHovered()) ImGui.SetMouseCursor(ImGuiMouseCursor.ResizeEw);
            if (ImGui.IsItemActive() && ImGui.IsMouseDragging(ImGuiMouseButton.Left)) {
                _leftPanelWidth += ImGui.GetIO().MouseDelta.X;
                _leftPanelWidth = Math.Clamp(_leftPanelWidth, minLeftW, maxLeftW);
            }
        }

        ImGui.SameLine(0, 0);

        // Right panel
        using (ImRaii.Child("##xlright", new Vector2(_rightPanelWidth, h), true)) {
            DrawRightPanel();
        }
    }

    private void DrawGlobalLaunchSettings() {
        ImGui.Text("Launch saved XIVLauncher accounts from the currently running client");

        if (!Plugin.Config.MultiboxEnabled) {
            ImGui.TextColored(Style.Colors.Yellow,
                "Tip: enable Multibox in MOP Settings when launching enough clients to hit FFXIV's instance limit.");
        }

        ImGui.Spacing();

        using (ImGuiGroupPanel.BeginGroupPanel("Settings")) {
            ImGui.Text("XIVLauncher Path:");
            ImGui.Text(Plugin.Config.XivLauncherPath.EllipsisPath(50));

            ImGui.SameLine();
            ImGuiHelpers.ScaledDummy(5);

            ImGui.SameLine();
            if (ImGuiUtil.IconButton(FontAwesomeIcon.Folder, "##SetXivLaunchFolderBtn", "Select Launcher Path")) {
                _fileDialogManager.OpenFileDialog(
                    title: "Open",
                     filters: ".exe",
                    startPath: XivLauncherManager.GetDefaultLauncherDirecotry(),
                    selectionCountMax: 1,
                    callback: (result, selectedPaths) => {
                        if (!result || selectedPaths.Count == 0) return;
                        if (!File.Exists(selectedPaths[0])) return;

                        Plugin.Config.XivLauncherPath = selectedPaths[0];
                        Plugin.Config.Save();
                        Plugin.IpcProvider.SyncConfiguration();
                    }
                );
            }

            ImGui.SameLine();
            if (ImGuiUtil.IconButton(FontAwesomeIcon.Undo, "##ReseXivLaunchFolderBtn", "Reset to Default")) {
                Plugin.Config.XivLauncherPath = XivLauncherManager.GetDefaultLauncherPath();
                Plugin.Config.Save();
                Plugin.IpcProvider.SyncConfiguration();
            }

            ImGui.SameLine();
            if (ImGuiUtil.IconButton(FontAwesomeIcon.FolderOpen, "##OpenXivLaunchFolderBtn", "Open Launcher Folder")) {
                WindowsApi.OpenFolder(Path.GetDirectoryName(Plugin.Config.XivLauncherPath));
            }

            ImGui.Text("Delay between launches (seconds):");
            ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X * 0.4f);
            var delaySeconds = Plugin.Config.XivLaunchDelaySeconds;
            if (ImGui.DragInt("##XivLaunchDelay", ref delaySeconds, 1, 0, 120)) {
                Plugin.Config.XivLaunchDelaySeconds = Math.Clamp(delaySeconds, 0, 120);
            }
            if (ImGui.IsItemDeactivatedAfterEdit()) {
                Plugin.Config.Save();
                Plugin.IpcProvider.SyncConfiguration();
            }
            if (ImGui.IsItemClicked(ImGuiMouseButton.Right)) {
                Plugin.Config.XivLaunchDelaySeconds = 20;
                Plugin.Config.Save();
                Plugin.IpcProvider.SyncConfiguration();
            }
            ImGuiUtil.ToolTip("Right-click to reset");
        }
    }
}
