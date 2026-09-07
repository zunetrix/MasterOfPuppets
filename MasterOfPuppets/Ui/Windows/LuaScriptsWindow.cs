using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.ImGuiNotification;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;

using MasterOfPuppets.LuaScripting;
using MasterOfPuppets.LuaScripting.Runs;
using MasterOfPuppets.Extensions;
using MasterOfPuppets.Extensions.Dalamud;
using MasterOfPuppets.Resources;
using MasterOfPuppets.Util.ImGuiExt;

namespace MasterOfPuppets;

public sealed class LuaScriptsWindow : Window {
    private readonly Plugin _plugin;
    private readonly PluginUi _ui;
    private readonly HashSet<string> _selectedTags = new(StringComparer.OrdinalIgnoreCase);
    private string _search = string.Empty;
    private bool _filterNoTags;
    private float _leftPanelWidth = 200f;
    private int _pendingDeleteIndex = -1;
    private bool _openDeletePopup;

    public LuaScriptsWindow(Plugin plugin, PluginUi ui) : base("Scripts###LuaScriptsWindow") {
        _plugin = plugin;
        _ui = ui;
        Size = ImGuiHelpers.ScaledVector2(760, 520);
        SizeCondition = ImGuiCond.FirstUseEver;
        SizeConstraints = new WindowSizeConstraints {
            MinimumSize = ImGuiHelpers.ScaledVector2(560, 360),
        };
    }

    public override void PreDraw() {
        Flags = ImGuiWindowFlags.None;
        if (!_plugin.Config.AllowMovement) Flags |= ImGuiWindowFlags.NoMove;
        if (!_plugin.Config.AllowResize) Flags |= ImGuiWindowFlags.NoResize;
        base.PreDraw();
    }

    public override void OnOpen() {
        base.OnOpen();
    }

    public override void Draw() {
        ImGui.BeginDisabled(_ui.LuaScriptEditorWindow.IsOpen);
        DrawToolbar();
        DrawRunStatus();
        DrawHeader();

        using (ImRaii.Child("##LuaScriptsContent", new Vector2(-1, 0), false)) {
            DrawPanels();
        }
        ImGui.EndDisabled();

        DrawDeleteConfirmation();
    }

    private void DrawToolbar() {
        if (ImGuiUtil.IconButton(FontAwesomeIcon.Plus, "##AddLuaScript", "Create a script"))
            _ui.LuaScriptEditorWindow.AddNewScript();

        ImGui.SameLine();
        if (ImGuiUtil.IconButton(FontAwesomeIcon.FileImport, "##ImportLuaScript", "Import a script from the clipboard"))
            ImportFromClipboard();

        ImGui.SameLine();
        if (ImGuiUtil.IconButton(FontAwesomeIcon.Pause, "##PauseLuaScripts", "Pause all locally running scripts"))
            _plugin.IpcProvider.PauseLuaScript();

        ImGui.SameLine();
        if (ImGuiUtil.IconButton(FontAwesomeIcon.Play, "##ResumeLuaScripts", "Resume all paused scripts"))
            _plugin.IpcProvider.ResumeLuaScript();

        ImGui.SameLine();
        using (ImRaii.PushColor(ImGuiCol.Button, Style.Components.ButtonDangerNormal)
            .Push(ImGuiCol.ButtonHovered, Style.Components.ButtonDangerHovered)
            .Push(ImGuiCol.ButtonActive, Style.Components.ButtonDangerActive)) {
            if (ImGuiUtil.IconButton(FontAwesomeIcon.Stop, "##StopLuaScripts", "Stop locally running scripts")) {
                _plugin.IpcProvider.StopLuaScript();
                DalamudApi.ShowNotification("Local Lua scripts stopped", NotificationType.Info, 3000);
            }
        }

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();
    }

    private void DrawRunStatus() {
        var active = _plugin.LuaScriptManager.ActiveRuns;
        if (active.Count == 0) {
            var recent = _plugin.LuaScriptManager.History.FirstOrDefault();
            if (recent != null) {
                ImGui.TextDisabled($"Last run: {recent.ToStatusText()}");
                DrawRunDiagnostics(recent.RunId);
            }
            DrawDistributedProtocolStatus();
            return;
        }

        ImGui.TextUnformatted(active.Count == 1 ? "Active Lua run" : $"Active Lua runs ({active.Count})");
        foreach (var run in active) {
            ImGui.PushID(run.RunId);
            var stateColor = run.State switch {
                LuaRunState.Paused => new Vector4(1f, 0.75f, 0.25f, 1f),
                LuaRunState.Running => new Vector4(0.45f, 0.9f, 0.55f, 1f),
                _ => new Vector4(0.65f, 0.75f, 1f, 1f),
            };
            using (ImRaii.PushColor(ImGuiCol.Text, stateColor))
                ImGui.TextUnformatted(run.State.ToString());
            ImGui.SameLine();
            ImGui.TextUnformatted($"{run.ScriptName}  slot {run.Slot + 1}/{run.ParticipantCount}");
            ImGui.SameLine();
            ImGui.TextDisabled(run.RunId);
            ImGui.SameLine();
            if (run.State == LuaRunState.Paused) {
                if (ImGuiUtil.IconButton(FontAwesomeIcon.Play, "##Resume", "Resume this run"))
                    _plugin.IpcProvider.ResumeLuaScript(run.RunId);
            } else if (ImGuiUtil.IconButton(FontAwesomeIcon.Pause, "##Pause", "Pause this run")) {
                _plugin.IpcProvider.PauseLuaScript(run.RunId);
            }
            ImGui.SameLine();
            if (ImGuiUtil.IconButton(FontAwesomeIcon.Stop, "##Stop", "Stop this run"))
                _plugin.IpcProvider.StopLuaScript(run.RunId);
            if (!string.IsNullOrWhiteSpace(run.Detail)) {
                ImGui.SameLine();
                ImGui.TextDisabled(run.Detail);
            }
            DrawRunDiagnostics(run.RunId);
            ImGui.PopID();
        }
        DrawDistributedProtocolStatus();
        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();
    }

    private void DrawDistributedProtocolStatus() {
        var sessions = _plugin.ChatWatcher.LuaDistributedSessions.Snapshot();
        var clocks = _plugin.ChatWatcher.LuaDistributedLaunches.ClockDiagnostics()
            .ToDictionary(clock => clock.RunToken, StringComparer.OrdinalIgnoreCase);
        if (sessions.Count == 0 || !ImGui.TreeNode($"Distributed protocol ({sessions.Count})##LuaDistributedProtocol"))
            return;
        foreach (var session in sessions) {
            ImGui.PushID(session.RunToken);
            ImGui.TextUnformatted(
                $"{session.Protocol.Phase}: {session.RunToken}  "
                + $"ready {session.Protocol.ReadyCount}/{session.Protocol.Participants.Count}");
            ImGui.SameLine();
            ImGui.TextDisabled($"conductor {session.Conductor}; last {session.LastMessageKind}");
            if (clocks.TryGetValue(session.RunToken, out var clock)) {
                ImGui.TextDisabled(clock.Estimate.IsReady
                    ? $"clock offset {clock.Estimate.OffsetSeconds:+0.0000;-0.0000;0.0000}s, "
                        + $"RTT {clock.Estimate.RoundTripSeconds:0.0000}s, "
                        + $"jitter {clock.Estimate.JitterSeconds:0.0000}s, "
                        + $"samples {clock.Estimate.SampleCount}"
                    : "clock awaiting conductor sample (wall-clock GO fallback active)");
            }
            if (ImGui.TreeNode("Participants")) {
                foreach (var participant in session.Protocol.Participants) {
                    ImGui.TextUnformatted(
                        $"{participant.ContentId}: {participant.State}  "
                        + $"position {participant.PositionError:0.000}, facing {participant.FacingErrorRadians:0.000}, speed {participant.Speed:0.000}");
                    if (!string.IsNullOrWhiteSpace(participant.Error)) {
                        ImGui.SameLine();
                        ImGui.TextDisabled(participant.Error);
                    }
                }
                ImGui.TreePop();
            }
            ImGui.PopID();
        }
        ImGui.TreePop();
    }

    private void DrawRunDiagnostics(string runId) {
        var diagnostics = _plugin.LuaScriptManager.Diagnostics
            .FirstOrDefault(item => item.Run.RunId.Equals(runId, StringComparison.OrdinalIgnoreCase));
        if (diagnostics == null || !ImGui.TreeNode($"Logs and diagnostics##{runId}"))
            return;

        ImGui.TextDisabled(
            $"Events: {diagnostics.Events.Published} published, "
            + $"{diagnostics.Events.Consumed} consumed, {diagnostics.Events.Dropped} dropped");
        if (diagnostics.Trajectory is { } trajectory) {
            ImGui.TextDisabled(
                $"Trajectory: anchor {trajectory.AnchorName}; speed {trajectory.RequestedSpeed:0.000}; "
                + $"{trajectory.Locomotion}; radial error {trajectory.RadialError:0.000}; "
                + $"phase error {trajectory.PhaseError:0.000}; "
                + (trajectory.Recovering ? "recovering" : "tracking"));
        }
        using (ImRaii.Child($"##LuaRunLog{runId}", ImGuiHelpers.ScaledVector2(-1, 110), true)) {
            if (diagnostics.Logs.Count == 0) {
                ImGui.TextDisabled("No script log output yet.");
            } else {
                foreach (var entry in diagnostics.Logs) {
                    ImGui.TextDisabled(entry.Timestamp.ToLocalTime().ToString("HH:mm:ss.fff"));
                    ImGui.SameLine();
                    ImGui.TextWrapped($"[{entry.Level}] {entry.Message}");
                }
            }
        }
        ImGui.TreePop();
    }

    private void DrawHeader() {
        if (ImGuiUtil.IconButton(FontAwesomeIcon.Tags, "##ToggleLuaScriptTags", Language.TogglePanelBtn)) {
            _plugin.Config.ShowPanelLuaScriptTags = !_plugin.Config.ShowPanelLuaScriptTags;
            _plugin.IpcProvider.SyncConfiguration();
        }

        ImGui.SameLine();
        ImGui.SetNextItemWidth(-1);
        ImGui.InputTextWithHint("##LuaScriptSearch", "Search scripts...", ref _search, 255, ImGuiInputTextFlags.AutoSelectAll);

        ImGui.Spacing();
        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();
    }

    private void DrawPanels() {
        if (_plugin.Config.ShowPanelLuaScriptTags) {
            DrawTagsPanel();
            ImGui.SameLine();
            DrawTagsSplitter();
            ImGui.SameLine();
        }

        using (ImRaii.Child("##LuaScriptList", new Vector2(0, -1), false, ImGuiWindowFlags.HorizontalScrollbar)) {
            DrawScriptsTable();
        }
    }

    private void DrawTagsPanel() {
        var allTags = GetAllTags();
        var totalAvailable = ImGui.GetContentRegionAvail().X;
        var minPanelWidth = 120f * ImGuiHelpers.GlobalScale;
        var maxPanelWidth = MathF.Max(minPanelWidth, totalAvailable - minPanelWidth);
        _leftPanelWidth = Math.Clamp(_leftPanelWidth, minPanelWidth, maxPanelWidth);

        using (ImRaii.Child("##LuaScriptTags", ImGuiHelpers.ScaledVector2(_leftPanelWidth, -1), true)) {
            ImGui.TextUnformatted("Script Tags");

            int buttonFilterCount = 2;
            float buttonWidth = ImGui.GetFrameHeight();
            float spacing = ImGui.GetStyle().ItemSpacing.X;
            float marginRight = 10f * ImGuiHelpers.GlobalScale;
            float totalButtonsWidth = (buttonWidth * buttonFilterCount) + (spacing * (buttonFilterCount - 1)) + marginRight;
            ImGui.SameLine(ImGui.GetWindowContentRegionMax().X - totalButtonsWidth);

            ImGui.BeginGroup();
            if (ImGuiUtil.IconButton(FontAwesomeIcon.Filter, "##SelectAllLuaTags", "Filter all tags")) {
                _filterNoTags = false;
                _selectedTags.Clear();
                foreach (var tag in allTags)
                    _selectedTags.Add(tag);
            }
            ImGui.SameLine();
            if (ImGuiUtil.IconButton(FontAwesomeIcon.Eraser, "##ClearLuaTags", "Clear filter")) {
                _selectedTags.Clear();
                _filterNoTags = false;
            }
            ImGui.EndGroup();

            ImGui.Separator();
            using var colors = ImRaii.PushColor(ImGuiCol.Header, Style.Components.ButtonBlueHovered)
                .Push(ImGuiCol.HeaderHovered, Style.Components.ButtonBlueHovered)
                .Push(ImGuiCol.HeaderActive, Style.Components.ButtonBlueHovered);

            var noTagCount = _plugin.Config.LuaScripts.Count(script => script.Tags == null || script.Tags.Count == 0);
            if (ImGui.Selectable($"No Tags ({noTagCount})##LuaNoTags", _filterNoTags)) {
                _selectedTags.Clear();
                _filterNoTags = !_filterNoTags;
            }

            ImGui.Separator();
            for (var index = 0; index < allTags.Count; index++) {
                var tag = allTags[index];
                var count = _plugin.Config.LuaScripts.Count(script =>
                    (script.Tags ?? new List<string>()).Any(item => item.Equals(tag, StringComparison.OrdinalIgnoreCase)));
                var selected = _selectedTags.Contains(tag);
                if (ImGui.Selectable($"{tag} ({count})##LuaTag{index}", selected, ImGuiSelectableFlags.SpanAllColumns)) {
                    _filterNoTags = false;
                    if (selected) _selectedTags.Remove(tag);
                    else _selectedTags.Add(tag);
                }
            }
        }
    }

    private void DrawTagsSplitter() {
        var splitterWidth = 6f * ImGuiHelpers.GlobalScale;
        using var style = ImRaii.PushStyle(ImGuiStyleVar.FramePadding, Vector2.Zero);
        ImGui.InvisibleButton("##LuaScriptTagsSplitter", new Vector2(splitterWidth, -1));
        if (ImGui.IsItemHovered())
            ImGui.SetMouseCursor(ImGuiMouseCursor.ResizeEw);
        if (ImGui.IsItemActive() && ImGui.IsMouseDragging(ImGuiMouseButton.Left))
            _leftPanelWidth += ImGui.GetIO().MouseDelta.X;
    }

    private void DrawScriptsTable() {
        var visibleIndexes = Enumerable.Range(0, _plugin.Config.LuaScripts.Count)
            .Where(IsVisible)
            .ToList();

        if (visibleIndexes.Count == 0) {
            ImGui.TextDisabled("No scripts match the current filters.");
            return;
        }

        var flags = ImGuiTableFlags.RowBg | ImGuiTableFlags.PadOuterX |
                    ImGuiTableFlags.NoSavedSettings | ImGuiTableFlags.BordersInnerV;
        if (!ImGui.BeginTable("##LuaScriptsTable", 4, flags))
            return;

        ImGui.TableSetupColumn("#", ImGuiTableColumnFlags.WidthFixed);
        ImGui.TableSetupColumn("Icon", ImGuiTableColumnFlags.WidthFixed);
        ImGui.TableSetupColumn("Script", ImGuiTableColumnFlags.WidthStretch);
        ImGui.TableSetupColumn("Options", ImGuiTableColumnFlags.WidthFixed);
        ImGui.TableHeadersRow();

        foreach (var scriptIndex in visibleIndexes)
            DrawScriptEntry(scriptIndex);

        ImGui.EndTable();
    }

    private bool IsVisible(int index) {
        var script = _plugin.Config.LuaScripts[index];
        var tags = script.Tags ?? new List<string>();
        if (_filterNoTags && tags.Count != 0)
            return false;
        if (!_filterNoTags && _selectedTags.Count > 0 &&
            !_selectedTags.All(selected => tags.Any(tag => tag.Equals(selected, StringComparison.OrdinalIgnoreCase))))
            return false;
        if (string.IsNullOrWhiteSpace(_search))
            return true;

        return script.Name.Contains(_search, StringComparison.OrdinalIgnoreCase)
               || (script.Description ?? string.Empty).Contains(_search, StringComparison.OrdinalIgnoreCase)
               || tags.Any(tag => tag.Contains(_search, StringComparison.OrdinalIgnoreCase));
    }

    private void DrawScriptEntry(int scriptIndex) {
        var script = _plugin.Config.LuaScripts[scriptIndex];
        ImGui.PushID(scriptIndex);
        ImGui.TableNextRow();

        ImGui.TableNextColumn();
        ImGui.TextUnformatted($"{scriptIndex + 1:000}");

        ImGui.TableNextColumn();
        DalamudApi.TextureProvider.DrawIcon(script.IconId, ImGuiHelpers.ScaledVector2(30, 30));
        if (script.Tags is { Count: > 0 })
            ImGuiUtil.ToolTip(string.Join("\n", script.Tags));

        ImGui.TableNextColumn();
        using (ImRaii.PushColor(ImGuiCol.Text, script.Color))
            ImGui.Selectable(script.Name);
        if (!string.IsNullOrWhiteSpace(script.Description))
            ImGuiUtil.ToolTip(script.Description);
        ImGui.OpenPopupOnItemClick("##LuaScriptContext", ImGuiPopupFlags.MouseButtonRight);
        DrawContextMenu(scriptIndex);

        ImGuiUtil.ToolTip("""
        Right click for more options
        Drag to reorder
        """);

        if (ImGui.BeginDragDropSource()) {
            unsafe {
                ImGui.SetDragDropPayload("DND_LUA_SCRIPTS_TABLE", new ReadOnlySpan<byte>(&scriptIndex, sizeof(int)), ImGuiCond.None);
                ImGui.PushStyleColor(ImGuiCol.Text, script.Color);
                ImGui.Button($"({scriptIndex + 1}) {script.Name}");
                ImGui.PopStyleColor();
            }
            ImGui.EndDragDropSource();
        }

        using (ImRaii.PushColor(ImGuiCol.DragDropTarget, Style.Components.DragDropTarget)) {
            if (ImGui.BeginDragDropTarget()) {
                ImGuiPayloadPtr dragDropPayload = ImGui.AcceptDragDropPayload("DND_LUA_SCRIPTS_TABLE");

                bool isDropping = false;
                unsafe {
                    isDropping = !dragDropPayload.IsNull;
                }

                if (isDropping && dragDropPayload.IsDelivery()) {
                    unsafe {
                        int originalIndex = *(int*)dragDropPayload.Data;
                        int offset = scriptIndex - originalIndex;
                        if (offset != 0 && originalIndex + offset >= 0) {
                            int targetIndex = originalIndex + offset;
                            _plugin.Config.MoveLuaScriptToIndex(originalIndex, targetIndex);
                            _plugin.IpcProvider.SyncConfiguration();
                        }
                    }
                }

                ImGui.EndDragDropTarget();
            }
        }

        ImGui.TableNextColumn();
        if (ImGuiUtil.IconButton(FontAwesomeIcon.Trash, "##DeleteLuaScript", "Delete script")) {
            _pendingDeleteIndex = scriptIndex;
            _openDeletePopup = true;
        }
        ImGui.SameLine();
        if (ImGuiUtil.IconButton(FontAwesomeIcon.Edit, "##EditLuaScript", "Edit script"))
            _ui.LuaScriptEditorWindow.EditScript(scriptIndex);
        ImGui.SameLine();
        if (ImGuiUtil.IconButton(FontAwesomeIcon.Play, "##RunLuaScript", "Run on all active MoP clients on this PC\nRight-Click for command options"))
            _plugin.IpcProvider.StartLuaScript(script.Name);
        ImGui.OpenPopupOnItemClick("ContextMenuRunLuaScript", ImGuiPopupFlags.MouseButtonRight);

        using (ImRaii.PushColor(ImGuiCol.Border, Style.Components.TooltipBorderColor))
        using (ImRaii.PushStyle(ImGuiStyleVar.PopupBorderSize, 1)) {
            if (ImGui.BeginPopup("ContextMenuRunLuaScript")) {
                if (ImGui.MenuItem("Copy Run Command")) {
                    ImGui.SetClipboardText($"/mop lua run \"{script.Name}\"");
                    DalamudApi.ShowNotification(Language.ClipboardCopyMessage, NotificationType.Info, 5000);
                }

                if (ImGui.MenuItem("Copy Chat Sync Command")) {
                    var scriptRunMessage = $"{GetChatSyncPrefix()} mopluarun \"{script.Name}\"";
                    ImGui.SetClipboardText(scriptRunMessage);
                    DalamudApi.ShowNotification(Language.ClipboardCopyMessage, NotificationType.Info, 5000);
                }

                if (ImGui.MenuItem("Run Chat Sync Command")) {
                    var scriptRunMessage = $"{GetChatSyncPrefix()} mopluarun \"{script.Name}\"";
                    Chat.SendMessage(scriptRunMessage);
                }

                ImGui.EndPopup();
            }
        }

        ImGui.PopID();
    }

    private void DrawContextMenu(int scriptIndex) {
        using var border = ImRaii.PushColor(ImGuiCol.Border, Style.Components.TooltipBorderColor);
        using var popupStyle = ImRaii.PushStyle(ImGuiStyleVar.PopupBorderSize, 1);
        if (!ImGui.BeginPopup("##LuaScriptContext"))
            return;

        var script = _plugin.Config.LuaScripts[scriptIndex];
        if (ImGui.MenuItem("Clone Script"))
            Clone(scriptIndex);
        if (ImGui.MenuItem("Export Script")) {
            ImGui.SetClipboardText(LuaScriptCatalog.Export(script));
            DalamudApi.ShowNotification("Script copied to clipboard", NotificationType.Info, 4000);
        }
        ImGui.EndPopup();
    }

    private void Clone(int scriptIndex) {
        var clone = _plugin.Config.LuaScripts[scriptIndex].Clone();
        clone.RegenerateIdentity();
        clone.Name = UniqueName(clone.Name + " (copy)");
        _plugin.Config.LuaScripts.Add(clone);
        _plugin.IpcProvider.SyncConfiguration();
        DalamudApi.ShowNotification("Script cloned", NotificationType.Success, 3000);
    }

    private void ImportFromClipboard() {
        try {
            var script = LuaScriptCatalog.Import(ImGui.GetClipboardText());
            script.RegenerateIdentity();
            script.Name = UniqueName(script.Name);
            _plugin.Config.LuaScripts.Add(script);
            _plugin.IpcProvider.SyncConfiguration();
            DalamudApi.ShowNotification("Script imported", NotificationType.Success, 4000);
        } catch (Exception ex) {
            DalamudApi.ShowNotification(ex.Message, NotificationType.Error, 6000);
        }
    }

    internal string UniqueName(string requested) {
        var baseName = string.IsNullOrWhiteSpace(requested) ? "Lua Script" : requested.Trim();
        var candidate = baseName;
        var suffix = 2;
        while (_plugin.Config.LuaScripts.Any(script => script.Name.Equals(candidate, StringComparison.OrdinalIgnoreCase)))
            candidate = $"{baseName} {suffix++}";
        return candidate;
    }

    internal List<string> GetAllTags() => _plugin.Config.LuaScripts
        .SelectMany(script => script.Tags ?? new List<string>())
        .Where(tag => !string.IsNullOrWhiteSpace(tag))
        .Select(tag => tag.Trim())
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .OrderBy(tag => tag, StringComparer.OrdinalIgnoreCase)
        .ToList();

    private string GetChatSyncPrefix() =>
        string.IsNullOrWhiteSpace(_plugin.Config.DefaultChatSyncPrefix)
            ? "/p"
            : _plugin.Config.DefaultChatSyncPrefix.Trim();

    private void DrawDeleteConfirmation() {
        if (_openDeletePopup) {
            ImGui.OpenPopup("Delete Script##LuaDelete");
            _openDeletePopup = false;
        }
        if (!ImGui.BeginPopupModal("Delete Script##LuaDelete", ImGuiWindowFlags.AlwaysAutoResize))
            return;

        var name = _pendingDeleteIndex >= 0 && _pendingDeleteIndex < _plugin.Config.LuaScripts.Count
            ? _plugin.Config.LuaScripts[_pendingDeleteIndex].Name
            : "this script";
        ImGui.TextUnformatted($"Delete '{name}'?");
        ImGui.TextDisabled("This cannot be undone.");

        if (ImGui.Button("Delete", ImGuiHelpers.ScaledVector2(100, 0))) {
            if (_pendingDeleteIndex >= 0 && _pendingDeleteIndex < _plugin.Config.LuaScripts.Count) {
                _plugin.Config.LuaScripts.RemoveAt(_pendingDeleteIndex);
                _plugin.IpcProvider.SyncConfiguration();
                DalamudApi.ShowNotification("Script deleted", NotificationType.Success, 3000);
            }
            _pendingDeleteIndex = -1;
            ImGui.CloseCurrentPopup();
        }
        ImGui.SameLine();
        if (ImGui.Button("Cancel", ImGuiHelpers.ScaledVector2(100, 0))) {
            _pendingDeleteIndex = -1;
            ImGui.CloseCurrentPopup();
        }
        ImGui.EndPopup();
    }
}
