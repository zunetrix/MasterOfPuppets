using System;
using System.Linq;
using System.Numerics;

using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;

using MasterOfPuppets.Extensions.Dalamud;

namespace MasterOfPuppets.Util.ImGuiExt;

// Original: https://github.com/grittyfrog/MacroMate/blob/master/MacroMate/Extensions/Imgui/InputTextMultilineLineNumbers.cs
public class ImGuiInputTextMultilineLineNumbers {
    public required Vector2 GutterSize { get; init; }
    public required Vector2 RemainingTextSize { get; init; }
    public required Vector2 ItemRectMin { get; init; }
    public required Vector2 ItemRectMax { get; init; }

    public static ImGuiInputTextMultilineLineNumbers Reserve(string text, Vector2 size) {
        var style = ImGui.GetStyle();
        var lineCount = text.Count(c => c == '\n') + 1;
        var maxLineNumberWidth = CalculateLineNumberWidth(lineCount);
        var lineNumberGutterWidth = maxLineNumberWidth + style.ItemSpacing.X * 2;

        // Reserve space with a dummy
        var gutterSize = new Vector2(lineNumberGutterWidth, size.Y);
        ImGui.Dummy(gutterSize);

        // Capture the rect of the dummy for later drawing
        var itemRectMin = ImGui.GetItemRectMin();
        var itemRectMax = ImGui.GetItemRectMax();

        ImGui.SameLine(0, 0);

        // Return reservation object that can be drawn later
        return new ImGuiInputTextMultilineLineNumbers {
            GutterSize = gutterSize,
            RemainingTextSize = new Vector2(size.X - gutterSize.X, size.Y),
            ItemRectMin = itemRectMin,
            ItemRectMax = itemRectMax
        };
    }

    public void Draw(string inputTextLabel) {
        float lastTextScrollY = 0;

        if (ItemRectMax.X <= ItemRectMin.X || ItemRectMax.Y <= ItemRectMin.Y)
            return;

        // compute intersection between reserved rect and current window rect to get the actually visible portion
        var winPos = ImGui.GetWindowPos();
        var winSize = ImGui.GetWindowSize();
        var winRectMin = winPos;
        var winRectMax = winPos + winSize;

        var clipMin = new Vector2(Math.Max(ItemRectMin.X, winRectMin.X), Math.Max(ItemRectMin.Y, winRectMin.Y));
        var clipMax = new Vector2(Math.Min(ItemRectMax.X, winRectMax.X), Math.Min(ItemRectMax.Y, winRectMax.Y));

        if (clipMax.X <= clipMin.X || clipMax.Y <= clipMin.Y)
            return;

        // if the intersection is not visible according to ImGui, bail
        if (!ImGui.IsRectVisible(clipMin, clipMax))
            return;

        using (var child = ImRaii.Child(inputTextLabel)) {
            if (child) {
                lastTextScrollY = ImGui.GetScrollY();
            }
        }

        // Calculate current line from cursor position
        var currentLine = 1;
        var textState = ImGuiP.GetInputTextState(ImGui.GetID(inputTextLabel));
        if (!textState.IsNull) {
            var textBeforeCursor = textState.TextW.AsEnumerable().Take(textState.Stb.Cursor).Select(us => (char)us);
            currentLine = textBeforeCursor.Count(c => c == '\n') + 1;
        }

        var drawList = ImGui.GetWindowDrawList();
        var style = ImGui.GetStyle();

        // Use the visible intersection (clipMin/clipMax) for clipping so gutter draws only the visible part
        drawList.PushClipRect(clipMin, clipMax);

        // Draw background for the gutter with rounded corners on the left side
        var rounding = style.FrameRounding;
        drawList.AddRectFilled(
            ItemRectMin,
            ItemRectMax,
            ImGui.GetColorU32(ImGuiCol.FrameBg),
            rounding,
            ImDrawFlags.RoundCornersLeft
        );

        // Draw triangular corner patches on foreground to fill gaps with InputText rounded corners
        var foregroundDrawList = ImGui.GetForegroundDrawList();
        var backgroundColor = ImGui.GetColorU32(ImGuiCol.FrameBg);

        // Fill top-right corner gap with triangle (clockwise winding)
        foregroundDrawList.AddTriangleFilled(
            new Vector2(ItemRectMax.X, ItemRectMin.Y), // Top-right corner
            new Vector2(ItemRectMax.X + rounding, ItemRectMin.Y), // Right from corner
            new Vector2(ItemRectMax.X, ItemRectMin.Y + rounding), // Down from corner
            backgroundColor
        );

        // Fill bottom-right corner gap with triangle
        foregroundDrawList.AddTriangleFilled(
            new Vector2(ItemRectMax.X, ItemRectMax.Y), // Bottom-right corner
            new Vector2(ItemRectMax.X, ItemRectMax.Y - rounding), // Up from corner
            new Vector2(ItemRectMax.X + rounding, ItemRectMax.Y), // Right from corner
            backgroundColor
        );

        var lineHeight = ImGui.GetFontSize();
        var font = ImGui.GetFont();
        var fontSize = ImGui.GetFontSize();
        var normalTextColor = ImGui.GetColorU32(ImGuiCol.TextDisabled);
        var highlightTextColor = ImGui.GetColorU32(ImGuiCol.Text);
        var highlightBackgroundColor = ImGui.GetColorU32(ImGuiCol.FrameBgHovered);

        // Use the scroll state we captured earlier
        var scrollY = lastTextScrollY;

        // Calculate visible line range accounting for clipped visible area (clipMin/clipMax)
        var availableHeight = clipMax.Y - clipMin.Y - (style.FramePadding.Y * 2);
        var visibleLines = (int)Math.Ceiling(Math.Max(0, availableHeight) / lineHeight);

        // determine first visible line by solving contentY(line) >= clipMin.Y - lineHeight
        var contentOffsetY = ItemRectMin.Y + style.FramePadding.Y;
        var firstVisibleLine = Math.Max(1, (int)Math.Floor((clipMin.Y + scrollY - contentOffsetY) / lineHeight) + 1);
        var lastVisibleLine = firstVisibleLine + visibleLines;

        // Draw line numbers for the visible range
        for (int line = firstVisibleLine; line <= lastVisibleLine; line++) {
            var lineY = ItemRectMin.Y + style.FramePadding.Y + ((line - 1) * lineHeight) - scrollY;

            // Only draw if line is within the clipped intersection (clipMin..clipMax)
            if (lineY + lineHeight < clipMin.Y || lineY > clipMax.Y) continue;

            var lineText = line.ToString();
            var textSize = ImGui.CalcTextSize(lineText);

            var textPos = new Vector2(
                ItemRectMax.X - style.FramePadding.X - textSize.X,
                lineY
            );

            // Draw background highlight for current cursor line
            if (line == currentLine) {
                drawList.AddRectFilled(
                    new Vector2(ItemRectMin.X + 2, lineY + 1),
                    new Vector2(ItemRectMax.X - 2, lineY + lineHeight - 1),
                    highlightBackgroundColor
                );
            }

            // Use regular text color for current line, disabled for others
            var textColor = line == currentLine ? highlightTextColor : normalTextColor;
            drawList.AddText(font, fontSize, textPos, textColor, lineText);
        }

        drawList.PopClipRect();
    }

    // public void Draw2(string inputTextLabel) {
    //     float lastTextScrollY = 0;

    //     if (ItemRectMax.X <= ItemRectMin.X || ItemRectMax.Y <= ItemRectMin.Y)
    //         return;

    //     if (!ImGui.IsRectVisible(ItemRectMin, ItemRectMax))
    //         return;

    //     using (var child = ImRaii.Child(inputTextLabel)) {
    //         if (child) {
    //             lastTextScrollY = ImGui.GetScrollY();
    //         }
    //     }

    //     // Calculate current line from cursor position
    //     var currentLine = 1;
    //     var textState = ImGuiP.GetInputTextState(ImGui.GetID(inputTextLabel));
    //     if (!textState.IsNull) {
    //         var textBeforeCursor = textState.TextW.AsEnumerable().Take(textState.Stb.Cursor).Select(us => (char)us);
    //         currentLine = textBeforeCursor.Count(c => c == '\n') + 1;
    //     }

    //     var drawList = ImGui.GetWindowDrawList();
    //     var style = ImGui.GetStyle();

    //     // Set up clipping for the gutter area
    //     drawList.PushClipRect(ItemRectMin, ItemRectMax);

    //     // Draw background for the gutter with rounded corners on the left side
    //     var rounding = style.FrameRounding;
    //     drawList.AddRectFilled(
    //         ItemRectMin,
    //         ItemRectMax,
    //         ImGui.GetColorU32(ImGuiCol.FrameBg),
    //         rounding,
    //         ImDrawFlags.RoundCornersLeft
    //     );

    //     // Draw triangular corner patches on foreground to fill gaps with InputText rounded corners
    //     var foregroundDrawList = ImGui.GetForegroundDrawList();
    //     var backgroundColor = ImGui.GetColorU32(ImGuiCol.FrameBg);

    //     // Fill top-right corner gap with triangle (clockwise winding)
    //     foregroundDrawList.AddTriangleFilled(
    //         new Vector2(ItemRectMax.X, ItemRectMin.Y), // Top-right corner
    //         new Vector2(ItemRectMax.X + rounding, ItemRectMin.Y), // Right from corner
    //         new Vector2(ItemRectMax.X, ItemRectMin.Y + rounding), // Down from corner
    //         backgroundColor
    //     );

    //     // Fill bottom-right corner gap with triangle
    //     foregroundDrawList.AddTriangleFilled(
    //         new Vector2(ItemRectMax.X, ItemRectMax.Y), // Bottom-right corner
    //         new Vector2(ItemRectMax.X, ItemRectMax.Y - rounding), // Up from corner
    //         new Vector2(ItemRectMax.X + rounding, ItemRectMax.Y), // Right from corner
    //         backgroundColor
    //     );


    //     var lineHeight = ImGui.GetFontSize();
    //     var font = ImGui.GetFont();
    //     var fontSize = ImGui.GetFontSize();
    //     var normalTextColor = ImGui.GetColorU32(ImGuiCol.TextDisabled);
    //     var highlightTextColor = ImGui.GetColorU32(ImGuiCol.Text);
    //     var highlightBackgroundColor = ImGui.GetColorU32(ImGuiCol.FrameBgHovered);

    //     // Use the scroll state we captured earlier
    //     var scrollY = lastTextScrollY;

    //     // Calculate visible line range accounting for scroll position
    //     var availableHeight = ItemRectMax.Y - ItemRectMin.Y - (style.FramePadding.Y * 2);
    //     var visibleLines = (int)Math.Ceiling(availableHeight / lineHeight);
    //     var firstVisibleLine = Math.Max(1, (int)Math.Floor(scrollY / lineHeight) + 1);
    //     var lastVisibleLine = firstVisibleLine + visibleLines;

    //     // Draw line numbers for the visible range
    //     for (int line = firstVisibleLine; line <= lastVisibleLine; line++) {
    //         var lineY = ItemRectMin.Y + style.FramePadding.Y + ((line - 1) * lineHeight) - scrollY;

    //         // Only draw if line is visible in the clipped area
    //         if (lineY >= ItemRectMin.Y - lineHeight && lineY <= ItemRectMax.Y + lineHeight) {
    //             var lineText = line.ToString();
    //             var textSize = ImGui.CalcTextSize(lineText);

    //             var textPos = new Vector2(
    //                 ItemRectMax.X - style.FramePadding.X - textSize.X,
    //                 lineY
    //             );

    //             // Draw background highlight for current cursor line
    //             if (line == currentLine) {
    //                 drawList.AddRectFilled(
    //                     new Vector2(ItemRectMin.X + 2, lineY + 1),
    //                     new Vector2(ItemRectMax.X - 2, lineY + lineHeight - 1),
    //                     highlightBackgroundColor
    //                 );
    //             }

    //             // Use regular text color for current line, disabled for others
    //             var textColor = line == currentLine ? highlightTextColor : normalTextColor;
    //             drawList.AddText(font, fontSize, textPos, textColor, lineText);
    //         }
    //     }

    //     drawList.PopClipRect();
    // }

    private static float CalculateLineNumberWidth(int lineCount) {
        var digits = lineCount.ToString().Length;
        var maxDigits = Math.Max(digits, 2); // Minimum 2 digits for padding
        var sampleText = new string('9', maxDigits);
        return ImGui.CalcTextSize(sampleText).X;
    }
}
