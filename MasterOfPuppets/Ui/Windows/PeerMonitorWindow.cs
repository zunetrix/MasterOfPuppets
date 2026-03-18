using System;
using System.Linq;
using System.Numerics;

using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;

namespace MasterOfPuppets;

public class PeerMonitorWindow : Window {
    private Plugin Plugin { get; }

    private bool _autoRefresh;
    private int _refreshMinutes = 0;
    private int _refreshSeconds = 30;
    private int RefreshTotalSeconds => _refreshMinutes * 60 + _refreshSeconds;
    private DateTime _nextRefresh = DateTime.MinValue;

    public PeerMonitorWindow(Plugin plugin) : base("Peer Monitor###PeerMonitorWindow") {
        Plugin = plugin;
        Size = ImGuiHelpers.ScaledVector2(620, 300);
        SizeCondition = ImGuiCond.FirstUseEver;
    }

    public override void Draw() {
        // Auto-refresh tick
        if (_autoRefresh && DateTime.UtcNow >= _nextRefresh) {
            Plugin.IpcProvider.RequestCharacterData();
            _nextRefresh = DateTime.UtcNow.AddSeconds(RefreshTotalSeconds);
        }

        //  Toolbar
        if (ImGui.Button("Request Data"))
            Plugin.IpcProvider.RequestCharacterData();

        ImGui.SameLine();

        using (ImRaii.PushColor(ImGuiCol.Button, Style.Components.ButtonDangerNormal)
                     .Push(ImGuiCol.ButtonHovered, Style.Components.ButtonDangerHovered)
                     .Push(ImGuiCol.ButtonActive, Style.Components.ButtonDangerActive)) {
            if (ImGui.Button("Clear"))
                Plugin.IpcProvider.PeerCharacterData.Clear();
        }

        ImGui.SameLine();

        if (ImGui.Checkbox("Auto Refresh", ref _autoRefresh) && !_autoRefresh)
            _nextRefresh = DateTime.MinValue;

        if (_autoRefresh) {
            ImGui.SameLine();
            ImGui.SetNextItemWidth(ImGuiHelpers.ScaledVector2(40, 0).X);
            if (ImGui.InputInt("m##min", ref _refreshMinutes, 0))
                _refreshMinutes = Math.Max(0, _refreshMinutes);
            ImGui.SameLine();
            ImGui.SetNextItemWidth(ImGuiHelpers.ScaledVector2(40, 0).X);
            if (ImGui.InputInt("s##sec", ref _refreshSeconds, 0))
                _refreshSeconds = Math.Clamp(_refreshSeconds, _refreshMinutes == 0 ? 1 : 0, 59);
        }

        ImGui.Separator();

        //  Table
        var offlineThreshold = _autoRefresh
            ? TimeSpan.FromSeconds(RefreshTotalSeconds * 2.5)
            : TimeSpan.FromMinutes(10);

        using var table = ImRaii.Table(
            "##peers", 6,
            ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.ScrollY | ImGuiTableFlags.SizingFixedFit,
            new Vector2(0, 0));
        if (!table) return;

        ImGui.TableSetupScrollFreeze(0, 1);
        ImGui.TableSetupColumn("#", ImGuiTableColumnFlags.WidthFixed, 20);
        ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthFixed, 20);
        ImGui.TableSetupColumn("Name", ImGuiTableColumnFlags.WidthStretch);
        ImGui.TableSetupColumn("Home World", ImGuiTableColumnFlags.WidthFixed, 120);
        ImGui.TableSetupColumn("Current World", ImGuiTableColumnFlags.WidthFixed, 120);
        ImGui.TableSetupColumn("Last Seen", ImGuiTableColumnFlags.WidthFixed, 90);
        ImGui.TableHeadersRow();

        foreach (var (pair, index) in Plugin.IpcProvider.PeerCharacterData.Select((x, i) => (x, i))) {
            var (_, info) = pair;
            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            ImGui.Text($"{index + 1:00}");

            // Status icon
            ImGui.TableNextColumn();
            var isOnline = DateTime.UtcNow - info.LastSeen < offlineThreshold;
            var (icon, color) = isOnline
                ? (FontAwesomeIcon.Check, Style.Colors.Green)
                : (FontAwesomeIcon.Times, Style.Colors.Red);
            using (ImRaii.PushColor(ImGuiCol.Text, color)) {
                using (ImRaii.PushFont(UiBuilder.IconFont)) {
                    ImGui.Text(icon.ToIconString());
                }
            }

            ImGui.TableNextColumn();
            ImGui.Text(info.CharacterName);

            ImGui.TableNextColumn();
            ImGui.Text(info.HomeWorld);

            ImGui.TableNextColumn();
            ImGui.Text(info.CurrentWorld);

            ImGui.TableNextColumn();
            var elapsed = DateTime.UtcNow - info.LastSeen;
            var lastSeenText = elapsed.TotalSeconds < 60
                ? $"{(int)elapsed.TotalSeconds}s ago"
                : elapsed.TotalMinutes < 60
                    ? $"{(int)elapsed.TotalMinutes}m ago"
                    : info.LastSeen.ToLocalTime().ToString("HH:mm:ss");
            ImGui.Text(lastSeenText);
        }
    }
}
