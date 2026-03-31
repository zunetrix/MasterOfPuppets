using System;
using System.Diagnostics;
using System.Linq;
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
            layout!.Slots.Add(slot);
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

        if (ImGui.Button("Tiled...##wltiled")) {
            ImGui.OpenPopup("Tiled Layout##wltiledpop");
        }
        ImGuiUtil.ToolTip("Divide the screen evenly among all connected clients.");

        ImGui.EndDisabled();
        DrawTiledPopup();

        ImGui.Separator();

        if (layout == null) {
            ImGui.TextDisabled("Select or create a layout");
            return;
        }

        //  Canvas
        // Read master's current screen resolution for proportional preview
        if (!WindowsApi.GetWindowRect(Process.GetCurrentProcess().MainWindowHandle, out var masterRect))
            masterRect = new WindowsApi.RECT { Left = 0, Top = 0, Right = 1920, Bottom = 1080 };

        // Try to get the primary screen working area via user32
        GetSystemMetrics_TryGetScreenSize(out int screenW, out int screenH);

        var avail = ImGui.GetContentRegionAvail();
        float aspect = screenW > 0 && screenH > 0 ? (float)screenW / screenH : 16f / 9f;
        float canvasW = avail.X;
        float canvasH = canvasW / aspect;
        if (canvasH > avail.Y - 2) {
            canvasH = avail.Y - 2;
            canvasW = canvasH * aspect;
        }

        float scaleX = screenW > 0 ? canvasW / screenW : canvasW / 1920f;
        float scaleY = screenH > 0 ? canvasH / screenH : canvasH / 1080f;

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

        // Dummy reserves space without swallowing mouse input.
        ImGui.Dummy(new Vector2(canvasW, canvasH));
        if (ImGui.IsWindowHovered() && ImGui.IsMouseClicked(ImGuiMouseButton.Left) && !ImGui.IsAnyItemHovered())
            _selSlot = -1;

        // Draw slots
        var slotColors = new uint[] {
            0xFF4CAF83, 0xFF4C83AF, 0xFFAF834C, 0xFFAF4C83,
            0xFF83AF4C, 0xFF4C4CAF, 0xFFAF4C4C, 0xFF4CAFAF,
        };

        for (int i = 0; i < layout.Slots.Count; i++) {
            var slot = layout.Slots[i];
            var color = slotColors[i % slotColors.Length];
            bool selected = i == _selSlot;

            float sx = canvasPos.X + slot.X * scaleX;
            float sy = canvasPos.Y + slot.Y * scaleY;
            float sw = slot.Width * scaleX;
            float sh = slot.Height * scaleY;

            var slotMin = new Vector2(sx, sy);
            var slotMax = new Vector2(sx + sw, sy + sh);

            uint fillColor = selected ? color : (color & 0x00FFFFFF) | 0x88000000;
            uint borderColor = selected ? 0xFFFFFFFF : color;

            drawList.AddRectFilled(slotMin, slotMax, fillColor);
            drawList.AddRect(slotMin, slotMax, borderColor, 0, ImDrawFlags.None, selected ? 2f : 1f);

            // Label: slot number + associated CIDs
            // var effectiveCids = slot.GetEffectiveCids(Plugin.Config.CidsGroups);
            // var cidNames = effectiveCids
            //     .Select(c => Plugin.Config.Characters.FirstOrDefault(ch => ch.Cid == c)?.Name ?? c.ToString("X8"))
            //     .ToList();
            string label = $"#{i + 1}";
            // if (cidNames.Count > 0) label += "\n" + string.Join(", ", cidNames.Take(2));
            // if (cidNames.Count > 2) label += $"\n+{cidNames.Count - 2} more";

            if (sw > 24 && sh > 14) {
                drawList.AddText(slotMin + new Vector2(4, 3),
                    0xFFFFFFFF, label);
            }

            // Resize handle (bottom-right corner)
            var handleSize = new Vector2(8f * ImGuiHelpers.GlobalScale, 8f * ImGuiHelpers.GlobalScale);
            var handleMin = slotMax - handleSize;
            drawList.AddRectFilled(handleMin, slotMax, borderColor);

            // Hit-test with ImGui IDs
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
                slot.X = (int)Math.Clamp(newX, 0, screenW - slot.Width);
                slot.Y = (int)Math.Clamp(newY, 0, screenH - slot.Height);
            }

            if (ImGui.IsItemDeactivatedAfterEdit() || (ImGui.IsItemDeactivated() && ImGui.GetMouseDragDelta(ImGuiMouseButton.Left, 0f).LengthSquared() > 0)) {
                Plugin.Config.Save();
                Plugin.IpcProvider.SyncConfiguration();
            }
            ImGui.PopID();

            // Resize handle button
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
                slot.Width = (int)Math.Clamp(newW, 100, screenW - slot.X);
                slot.Height = (int)Math.Clamp(newH, 60, screenH - slot.Y);
            }

            if (ImGui.IsItemDeactivatedAfterEdit() || (ImGui.IsItemDeactivated() && ImGui.GetMouseDragDelta(ImGuiMouseButton.Left, 0f).LengthSquared() > 0)) {
                Plugin.Config.Save();
                Plugin.IpcProvider.SyncConfiguration();
            }
            ImGui.PopID();
        }

        // Restore cursor below canvas
        // ImGui.SetCursorScreenPos(canvasPos + new Vector2(0, canvasH + ImGui.GetStyle().ItemSpacing.Y));

        // Slot info strip below canvas
        // var selSlot = SelectedSlot;
        // if (selSlot != null) {
        //     ImGui.TextDisabled($"Slot #{_selSlot + 1}:  X={selSlot.X}  Y={selSlot.Y}  W={selSlot.Width}  H={selSlot.Height}");
        // }
    }

    private void DrawTiledPopup() {
        using var color = ImRaii.PushColor(ImGuiCol.Border, Style.Components.TooltipBorderColor);
        using var style = ImRaii.PushStyle(ImGuiStyleVar.PopupBorderSize, 1);
        using var popUp = ImRaii.Popup("Tiled Layout##wltiledpop", ImGuiWindowFlags.NoResize);
        if (!popUp) return;

        ImGui.Text("Divide screen evenly among all connected clients.");
        ImGui.Spacing();

        ImGui.SetNextItemWidth(120);
        ImGui.InputInt("Columns##wltiledcols", ref _tiledColumns);
        _tiledColumns = Math.Clamp(_tiledColumns, 1, 8);
        ImGuiUtil.ToolTip("Number of windows per row.");

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        if (ImGui.Button("Apply Tiled##wltiledapply", new Vector2(-1, 0))) {
            ApplyTiled();
            ImGui.CloseCurrentPopup();
        }
    }

    private void ApplyTiled() {
        GetSystemMetrics_TryGetScreenSize(out int screenW, out int screenH);
        if (screenW <= 0) screenW = 1920;
        if (screenH <= 0) screenH = 1080;

        var peers = Plugin.IpcProvider.GetConnectedPeers();
        int count = peers.Count;
        if (count == 0) return;

        int cols = Math.Clamp(_tiledColumns, 1, count);
        int rows = (int)Math.Ceiling((double)count / cols);

        int cellW = screenW / cols;
        int cellH = screenH / rows;

        var layout = SelectedLayout;
        if (layout == null) {
            // create a new layout named "Tiled"
            var name = MakeUniqueName("Tiled");
            Plugin.Config.WindowLayouts.Add(new WindowLayout { Name = name });
            _selLayout = Plugin.Config.WindowLayouts.Count - 1;
            layout = SelectedLayout!;
        }

        layout.Slots.Clear();
        for (int i = 0; i < count; i++) {
            int col = i % cols;
            int row = i / cols;
            var slot = new WindowLayoutSlot {
                X = col * cellW,
                Y = row * cellH,
                Width = cellW,
                Height = cellH,
            };
            var peer = peers[i];
            slot.Cids.Add(peer.ContentId);
            layout.Slots.Add(slot);
        }

        _selSlot = -1;
        Plugin.Config.Save();
        Plugin.IpcProvider.SyncConfiguration();

        Plugin.IpcProvider.ApplyWindowLayout(layout.Name);
    }

    private static void GetSystemMetrics_TryGetScreenSize(out int w, out int h) {
        w = WindowsApi.GetSystemMetrics(WindowsApi.SM_CXSCREEN);
        h = WindowsApi.GetSystemMetrics(WindowsApi.SM_CYSCREEN);
    }
}
