using System;
using System.Numerics;

using Dalamud.Bindings.ImGui;
using Dalamud.Interface.ImGuiNotification;
using Dalamud.Interface.ImGuiSeStringRenderer;
using Dalamud.Interface.Utility;

using MasterOfPuppets.Extensions.Dalamud;
using MasterOfPuppets.Util;
using MasterOfPuppets.Util.ImGuiExt;

namespace MasterOfPuppets.Debug;

public sealed class UiElementDebugWidget : Widget {
    public override string Title => "Ui Element";

    private uint _macroIconId = 60042;

    private readonly TextAnimator _textAnimator = new TextAnimator();

    private readonly ImGuiModalDialog ImGuiModalDialog = new("##ConfirmModal", new Vector2(400, 200));

    private readonly ImGuiInputTextMultiline? _inputTextMultiline;
    private static string _inputMacroContent = string.Empty;

    public UiElementDebugWidget(WidgetContext ctx) : base(ctx) {
        _inputTextMultiline = new ImGuiInputTextMultiline(ctx.Plugin);
    }

    public override void Draw() {
        DrawSpinner();

        DrawIconPicker();

        DrawConfirmModalDialog();

        DrawMultilineInput();

        ImGuiModalDialog.Draw();

        ImGuiHelpers.SeStringWrapped(
            _textAnimator.GetAnimatedText("TEST RAINBOW TEXT MASTER OF PUPPETS"),
            new SeStringDrawParams() {
                WrapWidth = 600 * ImGuiHelpers.GlobalScale,
                FontSize = 30,
            }
        );

        // SearchableCombo();
        if (ImGui.Button("Modal")) {
            ImGuiModalDialog.Show(
                "Confirm",
                "Confirm Delete?",
                ("YES", () => {
                    // DO DELETE
                    DalamudApi.ShowNotification("Item deleted", NotificationType.Success, 3000);
                }
            ),
                ("NO", () => {
                    // DO NOTHING
                }
            )
            );
        }
    }

    private void DrawSpinner() {
        var spinnerLabel = $"##Spinner_{1}";
        // var spinnerRadius = ImGui.GetTextLineHeight() / 4;
        var spinnerRadius = ImGui.GetTextLineHeight();
        var spinnerThickness = 5 * ImGuiHelpers.GlobalScale;
        ImGui.SetCursorPosY(ImGui.GetCursorPosY() + spinnerRadius);
        ImGuiUtil.Spinner(spinnerLabel, spinnerRadius, spinnerThickness, Style.Colors.Blue);
    }

    private void DrawIconPicker() {
        DalamudApi.TextureProvider.DrawIcon(_macroIconId, ImGuiHelpers.ScaledVector2(50, 50));
        if (ImGui.IsItemClicked()) {
            Context.Plugin.Ui.IconPickerDialogWindow.Open(_macroIconId, selectedIconId => {
                _macroIconId = selectedIconId;
                DalamudApi.PluginLog.Warning($"selectedIconId: {selectedIconId}");
            });
        }
    }

    private void DrawConfirmModalDialog() {
        if (ImGui.Button("Delete"))
            ImGui.OpenPopup("##DeleteConfirmPopup");

        var viewport = ImGui.GetMainViewport();
        Vector2 center = viewport.GetCenter();
        ImGui.SetNextWindowPos(center, ImGuiCond.Appearing, new Vector2(0.5f, 0.5f));

        if (ImGui.BeginPopupModal("##DeleteConfirmPopup", ImGuiWindowFlags.AlwaysAutoResize)) {
            ImGui.Text("All those beautiful files will be deleted.\nThis operation cannot be undone!");
            ImGui.Separator();

            bool dontAskNextTime = false;
            ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(0, 0));
            ImGui.Checkbox("Don't ask me next time", ref dontAskNextTime);
            ImGui.PopStyleVar();

            if (ImGui.Button("OK", new Vector2(120, 0)))
                ImGui.CloseCurrentPopup();

            ImGui.SetItemDefaultFocus();
            ImGui.SameLine();
            if (ImGui.Button("Cancel", new Vector2(120, 0)))
                ImGui.CloseCurrentPopup();

            ImGui.EndPopup();
        }
    }

    private void DrawMultilineInput() {
        if (_inputTextMultiline.Draw(
            "###MacroContent",
            ref _inputMacroContent,
            ushort.MaxValue,
            new Vector2(
                MathF.Min(ImGui.GetContentRegionAvail().X, 500f * ImGuiHelpers.GlobalScale),
                ImGui.GetTextLineHeight() * 20
            ),
        ImGuiInputTextFlags.None
        )) {
            // DalamudApi.PluginLog.Warning($"{_inputMacroContent}");
        }
    }
}
