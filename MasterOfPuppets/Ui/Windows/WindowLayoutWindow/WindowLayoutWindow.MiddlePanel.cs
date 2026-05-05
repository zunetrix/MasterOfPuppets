using System;
using System.Numerics;

using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;

using MasterOfPuppets.Util;
using MasterOfPuppets.Util.ImGuiExt;
using MasterOfPuppets.WindowLayouts;

namespace MasterOfPuppets;

public partial class WindowLayoutWindow {
    private void DrawMiddlePanel() {
        var layout = SelectedLayout;

        //  Toolbar
        ImGui.BeginDisabled(layout == null);

        if (ImGuiUtil.PrimaryIconButton(FontAwesomeIcon.Plus, "##wladdslot", "Add slot")) {
            var slot = new WindowLayoutSlot { X = 0, Y = 0, Width = 960, Height = 540 };
            layout.Slots.Add(slot);
            _selSlot = layout.Slots.Count - 1;
            Plugin.Config.Save();
            Plugin.IpcProvider.SyncConfiguration();
        }

        ImGui.SameLine();

        using (ImRaii.PushColor(ImGuiCol.Button, _captureInProgress
            ? new Vector4(0.6f, 0.4f, 0f, 1f)
            : ImGui.GetStyle().Colors[(int)ImGuiCol.Button])) {
            if (ImGui.Button(_captureInProgress
                ? $"Capturing... ({_captureCooldown:F1}s)##wlcapture"
                : "Capture From Screen##wlcapture")) {
                if (!_captureInProgress && layout != null)
                    BeginCapture();
            }
        }
        ImGuiUtil.ToolTip("Ask all connected clients to report their current window position and size, then populate slots automatically.");

        ImGui.SameLine();

        if (ImGui.Button("Generate...##wlgenerate")) {
            ImGui.OpenPopup("Generate Layout##wlgenpop");
        }
        ImGuiUtil.ToolTip("Generate a layout pattern like Tile, Stack, or Titlebar.");

        ImGui.EndDisabled();
        // DrawGeneratePopup must stay outside tabs - popups are global
        DrawGeneratePopup();

        ImGui.Separator();

        if (layout == null) {
            ImGui.TextDisabled("Select or create a layout");
            return;
        }

        // Enumerate monitors; fall back to primary work area if none detected
        var monitors = WindowsApi.EnumerateMonitors();
        if (monitors.Count == 0) {
            GetSystemMetrics_TryGetScreenWorkArea(out var fb);
            monitors.Add(new WindowsApi.MONITORINFO {
                cbSize = 0,
                rcMonitor = fb,
                rcWork = fb,
                dwFlags = WindowsApi.MONITORINFOF_PRIMARY,
            });
        }

        _selectedMonitorTab = Math.Clamp(_selectedMonitorTab, 0, monitors.Count - 1);

        var slotColors = new uint[] {
            0xFF4CAF83, 0xFF4C83AF, 0xFFAF834C, 0xFFAF4C83,
            0xFF83AF4C, 0xFF4C4CAF, 0xFFAF4C4C, 0xFF4CAFAF,
        };

        if (monitors.Count > 1) {
            if (ImGui.BeginTabBar("##wlmonitortabs")) {
                for (int m = 0; m < monitors.Count; m++) {
                    bool isPrimary = (monitors[m].dwFlags & WindowsApi.MONITORINFOF_PRIMARY) != 0;
                    var mr = monitors[m].rcMonitor;
                    string label = isPrimary
                        ? $"Monitor {m + 1} (Primary)  {mr.Width}x{mr.Height}##wlmontab{m}"
                        : $"Monitor {m + 1}  {mr.Width}x{mr.Height}##wlmontab{m}";
                    if (ImGui.BeginTabItem(label)) {
                        _selectedMonitorTab = m;
                        DrawMonitorCanvas(layout, monitors[m], slotColors);
                        ImGui.EndTabItem();
                    }
                }
                ImGui.EndTabBar();
            }
        } else {
            DrawMonitorCanvas(layout, monitors[0], slotColors);
        }
    }

    private static bool SlotIntersectsMonitor(WindowLayoutSlot slot, WindowsApi.MONITORINFO mon) {
        var r = mon.rcMonitor;
        return slot.X < r.Right && slot.X + slot.Width > r.Left
            && slot.Y < r.Bottom && slot.Y + slot.Height > r.Top;
    }

    private void DrawMonitorCanvas(WindowLayout layout, WindowsApi.MONITORINFO monitor, uint[] slotColors) {
        int screenW = monitor.rcWork.Width;
        int screenH = monitor.rcWork.Height;
        int workLeft = monitor.rcWork.Left;
        int workTop = monitor.rcWork.Top;
        if (screenW <= 0) screenW = 1920;
        if (screenH <= 0) screenH = 1080;

        var avail = ImGui.GetContentRegionAvail();
        float aspect = (float)screenW / screenH;
        float canvasW = avail.X;
        float canvasH = canvasW / aspect;
        if (canvasH > avail.Y - 2) {
            canvasH = avail.Y - 2;
            canvasW = canvasH * aspect;
        }

        float scaleX = canvasW / screenW;
        float scaleY = canvasH / screenH;

        var canvasPos = ImGui.GetCursorScreenPos();
        var drawList = ImGui.GetWindowDrawList();

        // Canvas background
        drawList.AddRectFilled(canvasPos, canvasPos + new Vector2(canvasW, canvasH),
            ImGui.ColorConvertFloat4ToU32(new Vector4(0.08f, 0.08f, 0.08f, 1f)));
        drawList.AddRect(canvasPos, canvasPos + new Vector2(canvasW, canvasH),
            ImGui.ColorConvertFloat4ToU32(new Vector4(0.4f, 0.4f, 0.4f, 1f)));

        // Resolution label
        drawList.AddText(canvasPos + new Vector2(4, 2),
            ImGui.ColorConvertFloat4ToU32(new Vector4(0.5f, 0.5f, 0.5f, 1f)),
            $"{screenW}x{screenH}");

        // Clip rendering to canvas bounds
        drawList.PushClipRect(canvasPos, canvasPos + new Vector2(canvasW, canvasH), true);

        // Dummy reserves the canvas space without stealing mouse focus
        ImGui.Dummy(new Vector2(canvasW, canvasH));
        if (ImGui.IsWindowHovered() && ImGui.IsMouseClicked(ImGuiMouseButton.Left) && !ImGui.IsAnyItemHovered())
            _selSlot = -1;

        for (int i = 0; i < layout.Slots.Count; i++) {
            var slot = layout.Slots[i];

            // Skip slots that live entirely on another monitor
            if (!SlotIntersectsMonitor(slot, monitor)) continue;

            var color = slotColors[i % slotColors.Length];
            bool selected = i == _selSlot;

            float sx = canvasPos.X + (slot.X - workLeft) * scaleX;
            float sy = canvasPos.Y + (slot.Y - workTop) * scaleY;
            float sw = slot.Width * scaleX;
            float sh = slot.Height * scaleY;

            var slotMin = new Vector2(sx, sy);
            var slotMax = new Vector2(sx + sw, sy + sh);

            uint fillColor = selected ? color : (color & 0x00FFFFFF) | 0x88000000;
            uint borderColor = selected ? 0xFFFFFFFF : color;

            drawList.AddRectFilled(slotMin, slotMax, fillColor);
            drawList.AddRect(slotMin, slotMax, borderColor, 0, ImDrawFlags.None, selected ? 2f : 1f);

            string label = $"#{i + 1}";
            if (sw > 24 && sh > 14)
                drawList.AddText(slotMin + new Vector2(4, 3), 0xFFFFFFFF, label);

            // Resize handle (bottom-right corner)
            var handleSize = new Vector2(8f * ImGuiHelpers.GlobalScale, 8f * ImGuiHelpers.GlobalScale);
            var handleMin = slotMax - handleSize;
            drawList.AddRectFilled(handleMin, slotMax, borderColor);

            // Move button
            ImGui.SetCursorScreenPos(slotMin);
            ImGui.PushID(i * 2);
            ImGui.InvisibleButton("##wlslot", new Vector2(MathF.Max(sw - handleSize.X, 1), MathF.Max(sh, 1)));

            if (ImGui.IsItemActivated()) {
                _selSlot = i;
                _dragStartX = slot.X;
                _dragStartY = slot.Y;
            } else if (ImGui.IsItemClicked()) {
                _selSlot = i;
            }

            if (selected && ImGui.IsItemActive() && ImGui.IsMouseDragging(ImGuiMouseButton.Left)) {
                var delta = ImGui.GetMouseDragDelta(ImGuiMouseButton.Left, 0f);
                int newX = _dragStartX + (int)(delta.X / scaleX);
                int newY = _dragStartY + (int)(delta.Y / scaleY);
                slot.X = (int)Math.Clamp(newX, workLeft, workLeft + screenW - slot.Width);
                slot.Y = (int)Math.Clamp(newY, workTop, workTop + screenH - slot.Height);
            }

            if (ImGui.IsItemDeactivatedAfterEdit() || (ImGui.IsItemDeactivated() && ImGui.GetMouseDragDelta(ImGuiMouseButton.Left, 0f).LengthSquared() > 0)) {
                Plugin.Config.Save();
                Plugin.IpcProvider.SyncConfiguration();
            }
            ImGui.PopID();

            // Resize button
            ImGui.SetCursorScreenPos(handleMin);
            ImGui.PushID(i * 2 + 1);
            ImGui.InvisibleButton("##wlresize", handleSize);
            if (ImGui.IsItemHovered()) ImGui.SetMouseCursor(ImGuiMouseCursor.ResizeAll);

            if (ImGui.IsItemActivated()) {
                _selSlot = i;
                _dragStartW = slot.Width;
                _dragStartH = slot.Height;
            } else if (ImGui.IsItemClicked()) {
                _selSlot = i;
            }

            if (selected && ImGui.IsItemActive() && ImGui.IsMouseDragging(ImGuiMouseButton.Left)) {
                var delta = ImGui.GetMouseDragDelta(ImGuiMouseButton.Left, 0f);
                int newW = _dragStartW + (int)(delta.X / scaleX);
                int newH = _dragStartH + (int)(delta.Y / scaleY);
                slot.Width = (int)Math.Clamp(newW, 100, workLeft + screenW - slot.X);
                slot.Height = (int)Math.Clamp(newH, 60, workTop + screenH - slot.Y);
            }

            if (ImGui.IsItemDeactivatedAfterEdit() || (ImGui.IsItemDeactivated() && ImGui.GetMouseDragDelta(ImGuiMouseButton.Left, 0f).LengthSquared() > 0)) {
                Plugin.Config.Save();
                Plugin.IpcProvider.SyncConfiguration();
            }
            ImGui.PopID();
        }

        drawList.PopClipRect();
    }



    private int _layoutGenMode = 0; // 0 = Tile, 1 = Stack, 2 = Titlebar

    // Tile settings
    private int _genTileMode = 0; // 0 = Auto, 1 = Fixed
    private int _genTileColumns = 2;
    private int _genTileAspect = 0; // 0 = Stretch, 1 = Keep Proportion (16:9)

    // Stack settings
    private int _genStackWidth = 1280;
    private int _genStackHeight = 720;
    private int _genStackOffsetX = 30;
    private int _genStackOffsetY = 30;

    // Titlebar settings
    private int _genTitlebarMainWidth = 1280;
    private int _genTitlebarMainHeight = 720;
    private int _genTitlebarWidth = 640;
    private int _genTitlebarHeight = 8;

    private void DrawGeneratePopup() {
        using var color = ImRaii.PushColor(ImGuiCol.Border, Style.Components.TooltipBorderColor);
        using var style = ImRaii.PushStyle(ImGuiStyleVar.PopupBorderSize, 1);
        using var popUp = ImRaii.Popup("Generate Layout##wlgenpop", ImGuiWindowFlags.NoResize);
        if (!popUp) return;

        ImGui.Text("Generate slots automatically based on a pattern.");
        ImGui.Spacing();

        ImGui.SetNextItemWidth(150 * ImGuiHelpers.GlobalScale);
        ImGui.Combo("Pattern##wlgenmode", ref _layoutGenMode, "Grid (Tile)\0Cascade (Stack)\0Titlebar (Minimal)\0");

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        if (_layoutGenMode == 0) {
            // Tile
            ImGui.SetNextItemWidth(150 * ImGuiHelpers.GlobalScale);
            ImGui.Combo("Division##wlgentilediv", ref _genTileMode, "Auto-Factorize\0Fixed Columns\0");

            if (_genTileMode == 1) {
                ImGui.SetNextItemWidth(150 * ImGuiHelpers.GlobalScale);
                ImGui.InputInt("Columns##wltiledcols", ref _genTileColumns);
                _genTileColumns = Math.Clamp(_genTileColumns, 1, 16);
            }

            ImGui.SetNextItemWidth(150 * ImGuiHelpers.GlobalScale);
            ImGui.Combo("Proportions##wlgentileasp", ref _genTileAspect, "Stretch to fit\0Keep Aspect (16:9)\0");
        } else if (_layoutGenMode == 1) {
            // Stack
            ImGui.TextDisabled("Base Window Size");
            ImGui.SetNextItemWidth(80 * ImGuiHelpers.GlobalScale);
            ImGui.InputInt("Width##wlgenstackw", ref _genStackWidth);
            ImGui.SameLine();
            ImGui.SetNextItemWidth(80 * ImGuiHelpers.GlobalScale);
            ImGui.InputInt("Height##wlgenstackh", ref _genStackHeight);

            ImGui.TextDisabled("Cascade Offset");
            ImGui.SetNextItemWidth(80 * ImGuiHelpers.GlobalScale);
            ImGui.InputInt("Offset X##wlgenstackx", ref _genStackOffsetX);
            ImGui.SameLine();
            ImGui.SetNextItemWidth(80 * ImGuiHelpers.GlobalScale);
            ImGui.InputInt("Offset Y##wlgenstacky", ref _genStackOffsetY);
        } else if (_layoutGenMode == 2) {
            // Titlebar
            ImGui.TextDisabled("Main Slot Size");
            ImGui.SetNextItemWidth(80 * ImGuiHelpers.GlobalScale);
            ImGui.InputInt("Width##wlgentmainw", ref _genTitlebarMainWidth);
            ImGui.SameLine();
            ImGui.SetNextItemWidth(80 * ImGuiHelpers.GlobalScale);
            ImGui.InputInt("Height##wlgentmainh", ref _genTitlebarMainHeight);

            ImGui.TextDisabled("Alt Titlebar Size");
            ImGui.SetNextItemWidth(80 * ImGuiHelpers.GlobalScale);
            ImGui.InputInt("Width##wlgentaltw", ref _genTitlebarWidth);
            ImGui.SameLine();
            ImGui.SetNextItemWidth(80 * ImGuiHelpers.GlobalScale);
            ImGui.InputInt("Height##wlgentalth", ref _genTitlebarHeight);
        }

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        if (ImGui.Button("Generate Slots##wlgenapply", new Vector2(-1, 0))) {
            ApplyGenerate();
            ImGui.CloseCurrentPopup();
        }
    }

    private void ApplyGenerate() {
        GetSystemMetrics_TryGetScreenWorkArea(out var workArea);
        int workLeft = workArea.Left;
        int workTop = workArea.Top;
        int screenW = workArea.Width;
        int screenH = workArea.Height;
        if (screenW <= 0) screenW = 1920;
        if (screenH <= 0) screenH = 1080;

        var peers = Plugin.IpcProvider.GetConnectedPeers();
        int count = peers.Count;
        if (count == 0) return;

        var layout = SelectedLayout;
        if (layout == null) {
            var modeName = _layoutGenMode switch {
                0 => "Tiled",
                1 => "Stacked",
                2 => "Titlebar",
                _ => "Generated"
            };
            var name = MakeUniqueName(modeName);
            Plugin.Config.WindowLayouts.Add(new WindowLayout { Name = name });
            _selLayout = Plugin.Config.WindowLayouts.Count - 1;
            layout = SelectedLayout!;
        }

        layout.Slots.Clear();

        if (_layoutGenMode == 0) {
            // Tile logic
            int cols = _genTileMode == 1 ? Math.Clamp(_genTileColumns, 1, count) : 1;
            int rows = 1;

            if (_genTileMode == 0) {
                // Auto factorize
                cols = (int)Math.Ceiling(Math.Sqrt(count));
                rows = (int)Math.Ceiling((double)count / cols);

                // If the factor is 2 and we only have 2, let's just make it 2 cols, 1 row
                if (count == 2) {
                    cols = 2;
                    rows = 1;
                }
            } else {
                rows = (int)Math.Ceiling((double)count / cols);
            }

            int cellW = screenW / cols;
            int cellH = screenH / rows;

            int globalSlotW = cellW;
            int globalSlotH = cellH;

            if (_genTileAspect == 1) { // Keep Aspect (16:9)
                int targetH = (int)(globalSlotW * (9f / 16f));
                if (targetH > cellH) {
                    targetH = cellH;
                    globalSlotW = (int)(targetH * (16f / 9f));
                } else {
                    globalSlotH = targetH;
                }
            }

            int offsetX = workLeft;
            int offsetY = workTop;

            for (int i = 0; i < count; i++) {
                int col = i % cols;
                int row = i / cols;

                int slotX = offsetX + col * globalSlotW;
                int slotY = offsetY + row * globalSlotH;

                var slot = new WindowLayoutSlot {
                    X = slotX,
                    Y = slotY,
                    Width = globalSlotW,
                    Height = globalSlotH,
                };
                slot.Cids.Add(peers[i].ContentId);
                layout.Slots.Add(slot);
            }
        } else if (_layoutGenMode == 1) {
            // Stack logic
            for (int i = 0; i < count; i++) {
                var slot = new WindowLayoutSlot {
                    X = workLeft + i * _genStackOffsetX,
                    Y = workTop + i * _genStackOffsetY,
                    Width = Math.Max(100, _genStackWidth),
                    Height = Math.Max(60, _genStackHeight),
                };
                slot.Cids.Add(peers[i].ContentId);
                layout.Slots.Add(slot);
            }
        } else if (_layoutGenMode == 2) {
            // Titlebar logic
            for (int i = 0; i < count; i++) {
                WindowLayoutSlot slot;
                if (i == 0) {
                    // Main
                    slot = new WindowLayoutSlot {
                        X = workLeft + 0,
                        Y = workTop + 0,
                        Width = Math.Max(100, _genTitlebarMainWidth),
                        Height = Math.Max(60, _genTitlebarMainHeight),
                    };
                } else {
                    // Alts (stacked below main)
                    slot = new WindowLayoutSlot {
                        X = workLeft + 0,
                        Y = workTop + _genTitlebarMainHeight + ((i - 1) * _genTitlebarHeight),
                        Width = Math.Max(100, _genTitlebarWidth),
                        Height = Math.Max(8, _genTitlebarHeight),
                    };
                }
                slot.Cids.Add(peers[i].ContentId);
                layout.Slots.Add(slot);
            }
        }

        _selSlot = -1;
        Plugin.Config.Save();
        Plugin.IpcProvider.SyncConfiguration();
    }

    private static void GetSystemMetrics_TryGetScreenWorkArea(out WindowsApi.RECT workArea) {
        if (!WindowsApi.SystemParametersInfo(WindowsApi.SPI_GETWORKAREA, 0, out workArea, 0)) {
            workArea.Left = 0;
            workArea.Top = 0;
            workArea.Right = WindowsApi.GetSystemMetrics(WindowsApi.SM_CXSCREEN);
            workArea.Bottom = WindowsApi.GetSystemMetrics(WindowsApi.SM_CYSCREEN);
        }
    }
}
