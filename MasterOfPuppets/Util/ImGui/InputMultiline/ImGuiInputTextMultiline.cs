using System.Numerics;

using Dalamud.Bindings.ImGui;

namespace MasterOfPuppets.Util.ImGuiExt;

public class ImGuiInputTextMultiline {
    private Plugin Plugin { get; }
    private (int, int)? previousEditWord = null; // The word range we want to edit if we get an auto-complete event

    private readonly AutoCompletePopup AutoCompletePopup;

    private unsafe ImGuiInputTextStatePtr TextState => new(&ImGui.GetCurrentContext().Handle->InputTextState);

    public ImGuiInputTextMultiline(Plugin plugin) {
        Plugin = plugin;
        AutoCompletePopup = new AutoCompletePopup(plugin);
    }

    public bool Draw(
        string label,
        ref string input,
        int maxLength,
        Vector2 size,
        ImGuiInputTextFlags flags
    // ImGui.ImGuiInputTextCallbackDelegate callback
    ) {
        AutoCompletePopup.Draw();

        bool edited = false;

        flags |= ImGuiInputTextFlags.CallbackAlways;
        flags |= ImGuiInputTextFlags.CallbackEdit;
        flags |= ImGuiInputTextFlags.CallbackCompletion;

        var text = input;

        ImGui.ImGuiInputTextCallbackDelegate decoratedCallback = (scoped ref ImGuiInputTextCallbackData data) =>
        {
            // var result = callback(ref data);
            // var result = ref data;

            if (data.EventFlag == ImGuiInputTextFlags.CallbackCompletion) {
                var currentWord = TextState.CurrentEditWord();
                if (currentWord != null) {
                    previousEditWord = TextState.CurrentEditWordBounds();
                    AutoCompletePopup.AutoCompleteFilter = currentWord;
                    AutoCompletePopup.PopupPos =
                        ImGui.GetItemRectMin() +
                        ImGuiUtil.InputTextCalcText2dPos(text, TextState.CurrentEditWordStart()) +
                        new Vector2(0, ImGui.GetFontSize() + ImGui.GetStyle().ItemSpacing.Y * 2);

                    AutoCompletePopup.Open();
                }
            }

            return 1;
        };

        // Reserve space for line numbers first
        var lineNumbers = ImGuiInputTextMultilineLineNumbers.Reserve(text, size);

        // Draw the text input with reduced width
        var result = ImGui.InputTextMultiline(
            label,
            ref text,
            maxLength,
            lineNumbers.RemainingTextSize,
            flags,
            decoratedCallback
        );

        // draw the line numbers after the InputText is rendered
        lineNumbers.Draw(label);

        if (result) {
            input = text;
            edited = true;
        }

        // var focused = ImGui.IsItemFocused();

        while (AutoCompletePopup.CompletionEvents.TryDequeue(out var completion)) {
            if (!previousEditWord.HasValue) { break; }
            var (editStart, editEnd) = previousEditWord.Value;
            text = text.Remove(editStart, editEnd - editStart); // Remove current word

            var completionString = completion.SeString.ExtractText();
            var completionText = completionString.StartsWith("/") ? completionString : $"\"{completionString}\"";
            text = text.Insert(editStart, completionText);
            input = text;
            edited = true;
        }
        return edited;
    }
}
