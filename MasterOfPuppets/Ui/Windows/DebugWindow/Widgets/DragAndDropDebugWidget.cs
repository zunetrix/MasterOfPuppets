using System;
using System.Collections.Generic;
using System.Linq;

using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;

namespace MasterOfPuppets.Debug;

public sealed class DragAndDropDebugWidget : Widget {
    public override string Title => "Drag And Drop";

    private int? DragDropSelection;
    private readonly List<string> _items = Enumerable
        .Range(1, 10)
        .Select(i => $"Item {i}")
        .ToList();

    public DragAndDropDebugWidget(WidgetContext ctx) : base(ctx) {
    }

    public override void Draw() {
        ImGui.Text($"DND CS Payload Context");
        DrawDragAndDropCsContext();

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        ImGui.Text($"DND ImGui Payload Context");
        DrawDragAndDropImGuiPayloadPtr();

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        // ImGui.Text($"DND ImRaii Payload Context");
        // DrawDragAndDropImRaiiPayload();

    }

    public unsafe void DrawDragAndDropCsContext() {
        for (int i = 0; i < _items.Count; i++) {
            ImGui.Selectable($"{_items[i]}##item_cs_{i}");

            // ==============================
            // START DRAG (SOURCE)
            // ==============================
            if (ImGui.IsItemActive() && ImGui.IsMouseDragging(ImGuiMouseButton.Left)) {
                DragDropSelection = i;
            }

            if (DragDropSelection != null && ImGui.IsMouseDragging(ImGuiMouseButton.Left)) {
                using var source = ImRaii.DragDropSource(ImGuiDragDropFlags.None);
                if (source) {
                    ImGui.SetDragDropPayload("DND_CS_CONTEXT_PAYLOAD", default, 0);
                    ImGui.Text($"Moving {_items[DragDropSelection.Value]}");
                }
            }

            // ==============================
            // DROP TARGET
            // ==============================
            using var target = ImRaii.DragDropTarget();
            if (target.Success) {
                var payload = ImGui.AcceptDragDropPayload("DND_CS_CONTEXT_PAYLOAD", ImGuiDragDropFlags.SourceExtern);

                if (payload.Handle != null && DragDropSelection != null) {
                    int sourceIndex = DragDropSelection.Value;
                    int targetIndex = i;

                    if (sourceIndex != targetIndex) {
                        var item = _items[sourceIndex];
                        _items.RemoveAt(sourceIndex);
                        _items.Insert(targetIndex, item);
                    }

                    DragDropSelection = null;
                }
            }
        }
    }

    // public void DrawDragAndDropImRaiiPayload() {
    //     for (int i = 0; i < _items.Count; i++) {

    //         ImGui.Selectable($"{_items[i]}##item_imraii_{i}");
    //         DragDropSource(i);
    //         DragDropTarget(i);
    //     }
    // }

    // private void DragDropSource(int payloadSend) {
    //     using (var DragDropSource = ImRaii.DragDropSource()) {
    //         if (!DragDropSource) return;

    //         byte[] payload = BitConverter.GetBytes(payloadSend);
    //         ImGui.SetDragDropPayload("DND_IMRAII_PAYLOAD", payload, ImGuiCond.None);
    //     }
    // }

    // private void DragDropTarget(int i) {
    //     using (var DragDropTarget = ImRaii.DragDropTarget()) {
    //         if (!DragDropTarget) return;

    //         var dragDropPayload = ImGui.AcceptDragDropPayload("DND_IMRAII_PAYLOAD");
    //         if (dragDropPayload.IsDelivery() && !dragDropPayload.IsNull) {
    //             unsafe {
    //                 var spanPayload = new ReadOnlySpan<byte>(dragDropPayload.Data, sizeof(int));
    //                 int sourceIndex = BitConverter.ToInt32(spanPayload);

    //                 int offset = i - sourceIndex;
    //                 if (offset != 0 && sourceIndex + offset >= 0) {
    //                     int targetIndex = sourceIndex + offset;
    //                     DalamudApi.PluginLog.Warning($"Drag end [{i}]: original: {sourceIndex} target: {targetIndex}] offset: {offset}");

    //                     var item = _items[sourceIndex];
    //                     _items.RemoveAt(sourceIndex);
    //                     _items.Insert(targetIndex, item);
    //                 }
    //             }
    //         }
    //     }
    // }

    public void DrawDragAndDropImGuiPayloadPtr() {
        for (int i = 0; i < _items.Count; i++) {
            ImGui.Selectable($"{_items[i]}##item_payload_{i}");

            // ======================
            // SOURCE
            // ======================
            if (ImGui.BeginDragDropSource()) {
                unsafe {
                    ImGui.SetDragDropPayload("DND_IMGUI_CONTEXT_PAYLOAD", new ReadOnlySpan<byte>(&i, sizeof(int)), ImGuiCond.None);
                    ImGui.Button($"({i + 1}) {_items[i]}");
                }

                ImGui.Text($"Moving {_items[i]}");
                ImGui.EndDragDropSource();
            }

            // ======================
            // TARGET
            // ======================
            using (ImRaii.PushColor(ImGuiCol.DragDropTarget, Style.Components.DragDropTarget)) {
                if (ImGui.BeginDragDropTarget()) {
                    ImGuiPayloadPtr dragDropPayload = ImGui.AcceptDragDropPayload("DND_IMGUI_CONTEXT_PAYLOAD");

                    bool isDropping = false;
                    unsafe {
                        isDropping = !dragDropPayload.IsNull;
                    }

                    if (isDropping && dragDropPayload.IsDelivery()) {
                        unsafe {
                            int sourceIndex = *(int*)dragDropPayload.Data;

                            int offset = i - sourceIndex;
                            if (offset != 0 && sourceIndex + offset >= 0) {
                                int targetIndex = sourceIndex + offset;
                                DalamudApi.PluginLog.Warning($"Drag end [{i}]: original: {sourceIndex} target: {targetIndex}] offset: {offset}");

                                var item = _items[sourceIndex];
                                _items.RemoveAt(sourceIndex);
                                _items.Insert(targetIndex, item);
                            }
                        }
                    }
                    ImGui.EndDragDropTarget();
                }
            }
        }
    }
}
