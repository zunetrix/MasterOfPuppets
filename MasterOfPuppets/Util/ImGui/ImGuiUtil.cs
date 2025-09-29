using System;
using System.Linq;
using System.Numerics;

using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Components;
using Dalamud.Interface.Utility;

namespace MasterOfPuppets.Util.ImGuiExt;

public static class ImGuiUtil
{

    // ------------------------
    // COMPONENTS
    // ------------------------
    public static bool IconButton(FontAwesomeIcon icon, string? id = null, string tooltip = null, Vector4? color = null, Vector2? size = null)
    {
        ImGui.PushFont(UiBuilder.IconFont);
        try
        {
            var iconButtonSize = ImGui.CalcTextSize(icon.ToIconString()) + ImGui.GetStyle().FramePadding * 2;

            if (color != null) ImGui.PushStyleColor(ImGuiCol.Text, (Vector4)color);
            var buttonSize = size != null ? size.Value : iconButtonSize;
            return ImGui.Button($"{icon.ToIconString()}##{id}{tooltip}", buttonSize);
        }
        finally
        {
            ImGui.PopFont();
            if (color != null) ImGui.PopStyleColor();
            if (tooltip != null) ToolTip(tooltip);
        }
    }

    public static void IconButtonWithText(FontAwesomeIcon icon, string text, Vector2 size)
    {
        ImGuiComponents.IconButtonWithText(icon, text, size);
    }

    public static bool EnumCombo<TEnum>(
    string label,
    ref TEnum @enum,
    ImGuiComboFlags flags = ImGuiComboFlags.None,
    bool showValue = false,
    string[]? toolTips = null,
    string[]? labelsOverride = null
    // Func<TEnum, object>? orderBy = null,
) where TEnum : struct, Enum
    {
        var ret = false;
        var enumValues = Enum.GetValues<TEnum>();
        var enumIndex = Array.IndexOf(enumValues, @enum);

        // preview text
        string selectedValue = labelsOverride != null && enumIndex >= 0 && enumIndex < labelsOverride.Length
            ? labelsOverride[enumIndex]
            : (showValue
                ? $"{@enum} ({Convert.ChangeType(@enum, @enum.GetTypeCode())})"
                : @enum.ToString());

        if (ImGui.BeginCombo(label, selectedValue, flags))
        {
            var values = enumValues;

            // if (orderBy != null)
            //     values = values.OrderBy(orderBy).ToArray();

            for (var i = 0; i < values.Length; i++)
            {
                try
                {
                    ImGui.PushID(i);

                    // Label
                    string itemLabel = labelsOverride != null && i < labelsOverride.Length
                        ? labelsOverride[i]
                        : (showValue
                            ? $"{values[i]} ({Convert.ChangeType(values[i], values[i].GetTypeCode())})"
                            : values[i].ToString());

                    if (ImGui.Selectable(itemLabel, values[i].Equals(@enum)))
                    {
                        ret = true;
                        @enum = values[i];
                    }

                    // Tooltip
                    if (toolTips != null && i < toolTips.Length && toolTips[i] != null && ImGui.IsItemHovered())
                    {
                        ToolTip(toolTips[i]);
                    }

                    ImGui.PopID();
                }
                catch (Exception e)
                {
                    DalamudApi.PluginLog.Error(e.ToString());
                }
            }

            ImGui.EndCombo();
        }

        return ret;
    }

    public static void DrawFontawesomeIconOutlined(FontAwesomeIcon icon, Vector4 outline, Vector4 iconColor)
    {
        var positionOffset = ImGuiHelpers.ScaledVector2(0.0f, 1.0f);
        var cursorStart = ImGui.GetCursorPos() + positionOffset;
        ImGui.PushFont(UiBuilder.IconFont);

        ImGui.PushStyleColor(ImGuiCol.Text, outline);
        foreach (var x in Enumerable.Range(-1, 3))
        {
            foreach (var y in Enumerable.Range(-1, 3))
            {
                if (x is 0 && y is 0) continue;

                ImGui.SetCursorPos(cursorStart + new Vector2(x, y));
                ImGui.Text(icon.ToIconString());
            }
        }

        ImGui.PopStyleColor();

        ImGui.PushStyleColor(ImGuiCol.Text, iconColor);
        ImGui.SetCursorPos(cursorStart);
        ImGui.Text(icon.ToIconString());
        ImGui.PopStyleColor();

        ImGui.PopFont();

        ImGui.SetCursorPos(ImGui.GetCursorPos() - positionOffset);
    }

    public static void ToolTip(string desc, int wrap = 400, bool showBorder = true)
    {
        if (ImGui.IsItemHovered())
        {
            if (showBorder)
            {
                ImGui.PushStyleColor(ImGuiCol.Border, Style.Components.TooltipBorderColor);
                ImGui.PushStyleVar(ImGuiStyleVar.PopupBorderSize, 1);
            }
            ImGui.PushFont(UiBuilder.DefaultFont);
            ImGui.BeginTooltip();
            ImGui.PushTextWrapPos(ImGuiHelpers.GlobalScale * wrap);
            ImGui.TextUnformatted(desc);
            ImGui.PopTextWrapPos();
            ImGui.EndTooltip();
            ImGui.PopFont();
            if (showBorder)
            {
                ImGui.PopStyleVar();
                ImGui.PopStyleColor();
            }
        }
    }

    public static void HelpMarker(string description, bool sameline = true)
    {
        if (sameline) ImGui.SameLine();
        ImGuiUtil.DrawFontawesomeIconOutlined(FontAwesomeIcon.InfoCircle, Style.Colors.Black, Style.Components.TooltipBorderColor);
        ImGuiUtil.ToolTip(description);
    }

    public static void DrawColoredBanner(string content, Vector4 color)
    {
        ImGui.PushStyleColor(ImGuiCol.Button, color);
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, color);
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, color);
        ImGui.Button(content, new Vector2(-1, ImGui.GetFrameHeight()));
        ImGui.PopStyleColor(3);
    }

    /// Applies a border over the previous item
    public static void ItemBorder(
        uint col,
        float rounding = 1.0f,
        float thickness = 1.0f
    )
    {
        var drawList = ImGui.GetWindowDrawList();
        drawList.AddRect(
            ImGui.GetItemRectMin(),
            ImGui.GetItemRectMax(),
            col,
            rounding,
            ImDrawFlags.None,
            thickness
        );
    }

    public static void Spinner(string label, float radius, float thickness, Vector4 color)
    {
        var style = ImGui.GetStyle();
        ImGui.PushID(label);

        ImGui.SetCursorPosY(ImGui.GetCursorPosY() + style.FramePadding.Y);
        var size = new Vector2(radius * 2, radius * 2);

        ImGui.Dummy(size);
        var dummyPos = ImGui.GetItemRectMin();
        var dummySize = ImGui.GetItemRectSize();
        var center = new Vector2(
            dummyPos.X + (dummySize.X / 2),
            dummyPos.Y + (dummySize.Y / 2)
        );

        // Render
        ImGui.GetWindowDrawList().PathClear();

        var numSegments = 30;
        var start = Math.Abs(Math.Sin(ImGui.GetTime() * 1.8f) * (numSegments - 5));

        var aMin = Math.PI * 2.0f * ((float)start / (float)numSegments);
        var aMax = Math.PI * 2.0f * (((float)numSegments - 3) / (float)numSegments);

        for (var i = 0; i < numSegments; ++i)
        {
            var a = aMin + ((float)i / (float)numSegments) * (aMax - aMin);
            ImGui.GetWindowDrawList().PathLineTo(
                new Vector2(
                    center.X + (float)Math.Cos(a + (float)ImGui.GetTime() * 8) * (radius - thickness / 2),
                    center.Y + (float)Math.Sin(a + (float)ImGui.GetTime() * 8) * (radius - thickness / 2)
                )
            );
        }

        ImGui.GetWindowDrawList().PathStroke(ColorUtil.Vector4ToUint(color), ImDrawFlags.None, thickness);

        ImGui.PopID();
    }

    // ------------------------
    // HELPERS
    // ------------------------

    public static float GetWindowContentRegionWidth() => ImGui.GetWindowContentRegionMax().X - ImGui.GetWindowContentRegionMin().X;

    public static float GetWindowContentRegionHeight() => ImGui.GetWindowContentRegionMax().Y - ImGui.GetWindowContentRegionMin().Y;

    public static Vector2 GetWindowContentRegion() => ImGui.GetWindowContentRegionMax() - ImGui.GetWindowContentRegionMin();

    public static ImGui.ImGuiInputTextCallbackDelegate CallbackCharFilterFn(Func<char, bool> predicate)
    {
        unsafe
        {
            return (ref ImGuiInputTextCallbackData ev) =>
            {
                return predicate((char)ev.EventChar) ? 0 : 1;
            };
        }
    }

    public static bool IsNotNull(this ImGuiPayloadPtr payload)
    {
        return !payload.IsNull;
    }

    /// <summary>
    /// Returns the screen position of the text at `textPos` in `text` assuming it was drawn in
    /// an InputTextMultiline
    /// </summary>
    public static Vector2 InputTextCalcText2dPos(string text, int textPos)
    {
        var font = ImGui.GetFont();
        var fontScale = ImGui.GetIO().FontGlobalScale;
        float lineHeight = ImGui.GetFontSize();

        Vector2 textSize = new Vector2(0, 0);
        float lineWidth = 0.0f;

        int sIndex = 0;
        while (sIndex < textPos && sIndex < text.Length)
        {
            char c = text[sIndex];
            sIndex += 1;
            if (c == '\n')
            {
                textSize.X = 0;
                textSize.Y += lineHeight;
                lineWidth = 0.0f;
                continue;
            }
            if (c == '\r')
                continue;

            // ImGui.NET doesn't allow us to pass 32-bit wchar like the native implementation does, so instead
            // we need to account for surrogate pairs ourselves or the width gets misaligned
            // 0xE0F0 0x00BB   0xE0F00BB
            float charWidth = font.GetCharAdvance((ushort)c) * fontScale;
            lineWidth += charWidth;
        }


        if (textSize.X < lineWidth)
            textSize.X = lineWidth;

        return textSize;
    }
}
