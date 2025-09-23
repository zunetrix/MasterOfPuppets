using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Components;
using Dalamud.Interface.Utility;

namespace MasterOfPuppets;

public static class ImGuiUtil
{
    public static Stack<Vector2> IconButtonSize = new Stack<Vector2>();

    public static bool IconButton(FontAwesomeIcon icon, string? id = null, string tooltip = null, Vector4? color = null, Vector2? size = null)
    {
        ImGui.PushFont(UiBuilder.IconFont);
        try
        {
            if (color != null) ImGui.PushStyleColor(ImGuiCol.Text, (Vector4)color);
            if (IconButtonSize.TryPeek(out var result))
            {
                return ImGui.Button($"{icon.ToIconString()}##{id}{tooltip}", result);
            }
            else
            {
                return size != null
                    ? ImGui.Button($"{icon.ToIconString()}##{id}{tooltip}", size.Value)
                    : ImGui.Button($"{icon.ToIconString()}##{id}{tooltip}");
            }
        }
        finally
        {
            ImGui.PopFont();
            if (color != null) ImGui.PopStyleColor();
            if (tooltip != null) ToolTip(tooltip);
        }
    }

    public static float GetWindowContentRegionWidth() => ImGui.GetWindowContentRegionMax().X - ImGui.GetWindowContentRegionMin().X;

    public static float GetWindowContentRegionHeight() => ImGui.GetWindowContentRegionMax().Y - ImGui.GetWindowContentRegionMin().Y;

    public static Vector2 GetWindowContentRegion() => ImGui.GetWindowContentRegionMax() - ImGui.GetWindowContentRegionMin();

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

    public static void HelpMarker(string description)
    {
        ImGui.SameLine();
        ImGuiUtil.DrawFontawesomeIconOutlined(FontAwesomeIcon.InfoCircle, Style.Colors.Black, Style.Components.TooltipBorderColor);
        ImGuiUtil.ToolTip(description);
    }

    public static void HelpMarker(string desc, bool sameline = true)
    {
        if (sameline) ImGui.SameLine();
        //ImGui.PushFont(UiBuilder.IconFont);
        ImGui.TextDisabled("(?)");
        //ImGui.PopFont();
        if (ImGui.IsItemHovered())
        {
            ImGui.PushFont(UiBuilder.DefaultFont);
            ImGui.BeginTooltip();
            ImGui.PushTextWrapPos(ImGui.GetFontSize() * 35.0f);
            ImGui.TextUnformatted(desc);
            ImGui.PopTextWrapPos();
            ImGui.EndTooltip();
            ImGui.PopFont();
        }
    }

    public static void IconButtonWithText(FontAwesomeIcon icon, string text, Vector2 size)
    {
        ImGuiComponents.IconButtonWithText(icon, text, size);
    }

    public static void DrawColoredBanner(string content, Vector4 color)
    {
        ImGui.PushStyleColor(ImGuiCol.Button, color);
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, color);
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, color);
        ImGui.Button(content, new Vector2(-1, ImGui.GetFrameHeight()));
        ImGui.PopStyleColor(3);
    }
}
