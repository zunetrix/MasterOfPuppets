using System;
using System.Linq;
using System.Numerics;

using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Windowing;

using MasterOfPuppets.Ipc;
using MasterOfPuppets.Util.ImGuiExt;
using MasterOfPuppets.WindowLayouts;

namespace MasterOfPuppets;

public partial class WindowLayoutWindow : Window {
    private Plugin Plugin { get; }

    // Left panel state
    private string _searchFilter = string.Empty;
    private int _selLayout = -1;
    private string _newLayoutName = string.Empty;
    private int _renamingIdx = -1;
    private string _renameBuffer = string.Empty;
    private bool _renamingFocusPending;

    // Middle panel state
    private int _selSlot = -1;
    private int _dragStartX;
    private int _dragStartY;
    private int _dragStartW;
    private int _dragStartH;
    private int _selectedMonitorTab = 0;

    // Right panel state
    private readonly ImGuiComboSearch _charCombo = new();
    private readonly ImGuiComboSearch _groupCombo = new();
    private string _charSelected = string.Empty;
    private string _groupSelected = string.Empty;

    // Capture state
    private bool _captureInProgress;
    private float _captureCooldown;
    private const float CaptureWaitSeconds = 3f;

    // Splitter widths
    private float _leftPanelWidth;
    private float _rightPanelWidth = 300f;

    private WindowLayout? SelectedLayout =>
        _selLayout >= 0 && _selLayout < Plugin.Config.WindowLayouts.Count
            ? Plugin.Config.WindowLayouts[_selLayout] : null;

    private WindowLayoutSlot? SelectedSlot {
        get {
            var layout = SelectedLayout;
            if (layout == null) return null;
            return _selSlot >= 0 && _selSlot < layout.Slots.Count
                ? layout.Slots[_selSlot] : null;
        }
    }

    public WindowLayoutWindow(Plugin plugin) : base("Window Layouts###WindowLayoutWindow") {
        Plugin = plugin;
        Size = ImGuiHelpers.ScaledVector2(960, 620);
        SizeCondition = ImGuiCond.FirstUseEver;
        SizeConstraints = new WindowSizeConstraints {
            MinimumSize = ImGuiHelpers.ScaledVector2(500, 200),
            // MaximumSize = ImGuiHelpers.ScaledVector2(float.MaxValue, float.MaxValue)
        };
    }

    public override void OnOpen() {
        Plugin.IpcProvider.RequestCharacterData();
        base.OnOpen();
    }

    public override void Draw() {
        float splitterW = 6f * ImGuiHelpers.GlobalScale;
        float minLeftW = 180f * ImGuiHelpers.GlobalScale;
        float minRightW = 280f * ImGuiHelpers.GlobalScale;
        float minMidW = 200f * ImGuiHelpers.GlobalScale;

        if (_leftPanelWidth <= 0f) _leftPanelWidth = 240f * ImGuiHelpers.GlobalScale;
        if (_rightPanelWidth <= 0f) _rightPanelWidth = minRightW;

        var avail = ImGui.GetContentRegionAvail();
        float spacing = ImGui.GetStyle().ItemSpacing.X;

        float totalFixed = splitterW * 2 + minMidW + spacing * 4;

        float maxLeftW = MathF.Max(avail.X - totalFixed - minRightW, minLeftW);
        float maxRightW = MathF.Max(avail.X - totalFixed - _leftPanelWidth, minRightW);

        _leftPanelWidth = Math.Clamp(_leftPanelWidth, minLeftW, maxLeftW);
        _rightPanelWidth = Math.Clamp(_rightPanelWidth, minRightW, maxRightW);

        float midW = MathF.Max(avail.X - _leftPanelWidth - splitterW * 2 - _rightPanelWidth - spacing * 4, minMidW);
        float h = avail.Y;

        // Left panel
        ImGui.BeginChild("##wlleft", new Vector2(_leftPanelWidth, h), true);
        DrawLeftPanel();
        ImGui.EndChild();
        ImGui.SameLine();

        // Left splitter
        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, Vector2.Zero);
        ImGui.InvisibleButton("##wlsplitl", new Vector2(splitterW, h));
        if (ImGui.IsItemHovered()) ImGui.SetMouseCursor(ImGuiMouseCursor.ResizeEw);
        if (ImGui.IsItemActive() && ImGui.IsMouseDragging(ImGuiMouseButton.Left)) {
            _leftPanelWidth += ImGui.GetIO().MouseDelta.X;
            _leftPanelWidth = Math.Clamp(_leftPanelWidth, minLeftW, maxLeftW);
        }
        ImGui.PopStyleVar();
        ImGui.SameLine();

        // Middle panel
        ImGui.BeginChild("##wlmid", new Vector2(midW, h), true);
        DrawMiddlePanel();
        ImGui.EndChild();
        ImGui.SameLine();

        // Right splitter
        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, Vector2.Zero);
        ImGui.InvisibleButton("##wlsplitr", new Vector2(splitterW, h));
        if (ImGui.IsItemHovered()) ImGui.SetMouseCursor(ImGuiMouseCursor.ResizeEw);
        if (ImGui.IsItemActive() && ImGui.IsMouseDragging(ImGuiMouseButton.Left)) {
            _rightPanelWidth -= ImGui.GetIO().MouseDelta.X;
            _rightPanelWidth = Math.Clamp(_rightPanelWidth, minRightW, maxRightW);
        }
        ImGui.PopStyleVar();
        ImGui.SameLine();

        // Right panel
        ImGui.BeginChild("##wlright", new Vector2(_rightPanelWidth, h), true);
        DrawRightPanel();
        ImGui.EndChild();

        // Capture cooldown tick
        if (_captureInProgress) {
            _captureCooldown -= ImGui.GetIO().DeltaTime;
            if (_captureCooldown <= 0f) {
                var infos = Plugin.IpcProvider.EndCaptureWindowInfos();
                ApplyCaptureResults(infos);
                _captureInProgress = false;
            }
        }
    }

    private void AddLayout() {
        if (string.IsNullOrWhiteSpace(_newLayoutName)) return;
        var trimmed = _newLayoutName.Trim();
        if (Plugin.Config.WindowLayouts.Any(l => l.Name.Equals(trimmed, StringComparison.OrdinalIgnoreCase))) return;
        Plugin.Config.WindowLayouts.Add(new WindowLayout { Name = trimmed });
        _selLayout = Plugin.Config.WindowLayouts.Count - 1;
        _selSlot = -1;
        _newLayoutName = string.Empty;
        Plugin.Config.Save();
        Plugin.IpcProvider.SyncConfiguration();
    }

    private void BeginCapture() {
        if (_captureInProgress) return;
        _captureInProgress = true;
        _captureCooldown = CaptureWaitSeconds;
        Plugin.IpcProvider.BeginCaptureWindowInfos();
    }

    private void ApplyCaptureResults(System.Collections.Generic.IReadOnlyList<WindowInfoPayload> infos) {
        DalamudApi.PluginLog.Debug($"[WindowLayout] EndCaptureWindowInfos returned {infos.Count} payloads.");
        var layout = SelectedLayout;
        if (layout == null || infos.Count == 0) return;

        foreach (var info in infos) {
            // Find existing slot for this CID, or create new one
            var slot = layout.Slots.FirstOrDefault(s => s.Cids.Contains(info.Cid));
            if (slot == null) {
                slot = new WindowLayoutSlot();
                if (!slot.Cids.Contains(info.Cid))
                    slot.Cids.Add(info.Cid);
                layout.Slots.Add(slot);
            }
            slot.X = info.X;
            slot.Y = info.Y;
            slot.Width = info.Width;
            slot.Height = info.Height;
        }

        Plugin.Config.Save();
        Plugin.IpcProvider.SyncConfiguration();
    }
}
