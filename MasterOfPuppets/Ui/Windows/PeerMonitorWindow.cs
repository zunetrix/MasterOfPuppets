using System;
using System.Linq;
using System.Numerics;

using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;

using MasterOfPuppets.Ipc;
using MasterOfPuppets.Util.ImGuiExt;

namespace MasterOfPuppets;

public class PeerMonitorWindow : ThemedWindow {
    private Plugin Plugin { get; }

    public PeerMonitorWindow(Plugin plugin) : base("Peer Monitor###PeerMonitorWindow") {
        Plugin = plugin;
        Size = ImGuiHelpers.ScaledVector2(620, 300);
        SizeCondition = ImGuiCond.FirstUseEver;
    }

    public override void Draw() {
        var now = DateTime.UtcNow;
        var selfCid = DalamudApi.PlayerState.ContentId;
        var characterOrder = Plugin.Config.Characters.Select(c => c.Cid).ToList();

        // stable order: Characters list order, then name (dictionary enumeration order isn't stable)
        var peers = Plugin.IpcProvider.PeerCharacterData.Values
            .OrderBy(p => characterOrder.IndexOf(p.ContentId) is var idx && idx >= 0 ? idx : int.MaxValue)
            .ThenBy(p => p.CharacterName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var onlineCount = peers.Count(p => IpcProvider.GetPeerStatus(p, now) == PeerStatus.Online);

        //  Toolbar
        if (ImGui.Button("Request Data"))
            Plugin.IpcProvider.RequestCharacterData();
        ImGuiUtil.ToolTip("Peers already send a heartbeat every few seconds, use this to force an update");

        ImGui.SameLine();

        using (ImRaii.PushColor(ImGuiCol.Button, Style.Components.ButtonDangerNormal)
                     .Push(ImGuiCol.ButtonHovered, Style.Components.ButtonDangerHovered)
                     .Push(ImGuiCol.ButtonActive, Style.Components.ButtonDangerActive)) {
            if (ImGui.Button("Clear"))
                Plugin.IpcProvider.PeerCharacterData.Clear();
        }

        ImGui.SameLine();
        ImGui.Text($"{onlineCount}/{peers.Count} online");
        ImGui.SameLine();
        ImGuiUtil.HelpMarker($"""
        Online: heartbeat received in the last {IpcProvider.PeerOnlineMaxAge.TotalSeconds:0}s
        Stale: no heartbeat, not used by broadcasts/auto-accept (removed after {IpcProvider.PeerRemoveMaxAge.TotalSeconds:0}s)
        """);

        ImGui.Separator();

        //  Table
        using var table = ImRaii.Table(
            "##peers", 5,
            ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.ScrollY | ImGuiTableFlags.SizingFixedFit,
            new Vector2(0, 0));
        if (!table) return;

        ImGui.TableSetupScrollFreeze(0, 1);
        ImGui.TableSetupColumn("#", ImGuiTableColumnFlags.WidthFixed, 20);
        ImGui.TableSetupColumn("Status", ImGuiTableColumnFlags.WidthFixed, ImGui.CalcTextSize("Status").X);
        ImGui.TableSetupColumn("Name", ImGuiTableColumnFlags.WidthStretch);
        ImGui.TableSetupColumn("Home World", ImGuiTableColumnFlags.WidthFixed, 120);
        ImGui.TableSetupColumn("Current World", ImGuiTableColumnFlags.WidthFixed, 120);
        ImGui.TableHeadersRow();

        for (int i = 0; i < peers.Count; i++) {
            var info = peers[i];
            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            ImGui.Text($"{i + 1:00}");

            // last seen only in the tooltip: online peers refresh every heartbeat, so a visible counter would flicker
            ImGui.TableNextColumn();
            var isOnline = IpcProvider.GetPeerStatus(info, now) == PeerStatus.Online;
            ImGuiUtil.TextIcon(FontAwesomeIcon.Circle, isOnline ? Style.Colors.Green : Style.Colors.Red);
            ImGuiUtil.ToolTip(isOnline ? "Online" : $"Stale\nLast seen {FormatElapsed(now - info.LastSeen)} ago");

            ImGui.TableNextColumn();
            ImGui.Text(info.ContentId == selfCid ? $"{info.CharacterName} (you)" : info.CharacterName);

            ImGui.TableNextColumn();
            ImGui.Text(info.HomeWorld);

            ImGui.TableNextColumn();
            ImGui.Text(info.CurrentWorld);
        }
    }

    private static string FormatElapsed(TimeSpan elapsed) =>
        elapsed.TotalSeconds < 60 ? $"{(int)elapsed.TotalSeconds}s" : $"{(int)elapsed.TotalMinutes}m";
}
