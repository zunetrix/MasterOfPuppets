using System;
using System.Numerics;

using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;

public class ImGuiModalDialog {
    private readonly string _id;
    private string _title = "Dialog";
    private string _message = string.Empty;
    private bool _isOpen = false;
    private (string Label, Action Callback)[] _buttons = Array.Empty<(string, Action)>();

    private Vector2 _minSize;

    public ImGuiModalDialog(string id = "##ModalDialog", Vector2? minSize = null) {
        _id = id;
        _minSize = minSize ?? ImGuiHelpers.ScaledVector2(300, 100);
    }

    public void Show(string title, string message, params (string Label, Action Callback)[] buttons) {
        _title = title;
        _message = message;
        _isOpen = true;
        _buttons = buttons.Length > 0 ? buttons : new (string, Action)[] { ("OK", () => { }) };
    }

    public void Show(string title, string message, Vector2 minSize, params (string Label, Action Callback)[] buttons) {
        _title = title;
        _message = message;
        _isOpen = true;
        _minSize = minSize;
        _buttons = buttons.Length > 0 ? buttons : new (string, Action)[] { ("OK", () => { }) };
    }

    public void Draw() {
        if (_isOpen) {
            ImGui.OpenPopup($"{_title}##{_id}");
            _isOpen = false;
        }

        var viewport = ImGui.GetMainViewport();
        Vector2 center = viewport.GetCenter();
        ImGui.SetNextWindowPos(center, ImGuiCond.Appearing, new Vector2(0.5f, 0.5f));
        ImGui.SetNextWindowSizeConstraints(_minSize, new Vector2(float.MaxValue, float.MaxValue));

        if (ImGui.BeginPopupModal($"{_title}##{_id}", ImGuiWindowFlags.AlwaysAutoResize)) {
            ImGui.TextWrapped(_message);
            ImGui.NewLine();
            ImGui.Separator();
            ImGui.Spacing();
            ImGui.Spacing();

            // center buttons
            float buttonWidth = 120f;
            float spacing = ImGui.GetStyle().ItemSpacing.X;
            float totalWidth = (_buttons.Length * buttonWidth) + ((_buttons.Length - 1) * spacing);
            float availableWidth = ImGui.GetWindowContentRegionMax().X - ImGui.GetWindowContentRegionMin().X;
            float offsetX = (availableWidth - totalWidth) / 2.0f;
            ImGui.SetCursorPosX(ImGui.GetCursorPosX() + offsetX);

            for (int i = 0; i < _buttons.Length; i++) {
                var (label, callback) = _buttons[i];

                if (ImGui.Button(label, new Vector2(buttonWidth, 0))) {
                    callback?.Invoke();
                    ImGui.CloseCurrentPopup();
                }

                if (i < _buttons.Length - 1) {
                    ImGui.SameLine();
                }
            }

            ImGui.EndPopup();
        }
    }
}
