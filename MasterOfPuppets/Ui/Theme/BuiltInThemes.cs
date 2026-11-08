using System;
using System.Collections.Generic;
using System.Numerics;

using Dalamud.Bindings.ImGui;

namespace MasterOfPuppets;

/// <summary>
/// Built-in themes. Read-only: use "Copy to edit" to customise one.
/// The default theme is not listed here, it means "no override" (Dalamud style).
/// Each theme is a four color palette; every ImGui color is derived from it in <see cref="Create"/>,
/// which keeps text readable on all fills. To add a theme, add one line.
/// </summary>
internal static class BuiltInThemes {
    private const float MinTextContrast = 4.5f;
    private const float MinDisabledTextContrast = 4f;
    private const float MinControlContrast = 3f;

    // Vivid themes only: hover and press are brief, so they can trade some text contrast for color
    private const float MinVividHoverContrast = 3f;

    // Strongest share of the accent a fill can have; the softer states are fractions of it
    private const float MaxAccentMix = 0.75f;

    public static readonly IReadOnlyList<UiTheme> All = [
        //     id                 name                 background  text       accent     highlight
        Create("dark",            "Dark",              "#1A1A1A", "#F2F2F2", "#8C8C8C", "#D0D0D0"),
        Create("modern-dark",     "Modern Dark",       "#222831", "#EEEEEE", "#00ADB5", "#4FD8DF", vivid: true),
        Create("light",           "Light",             "#F2F2F2", "#1A1A1A", "#4A90D9", "#2F6DB0"),
        Create("ocean-fishing",   "Ocean Fishing",     "#0D1A26", "#D9F2FF", "#3A8FD9", "#66CCFF"),
        Create("deep-blue",       "Deep Blue",         "#12121A", "#E6EDF2", "#5999D9", "#80CCFF"),
        Create("catnip",          "Catnip",            "#171F17", "#E6F2E6", "#5FB35F", "#B2FFB2", vivid: true),
        Create("chocobo",         "Chocobo",           "#2E2512", "#FFF2CC", "#CCA633", "#FFD966"),
        Create("dracula",         "Dracula",           "#282A36", "#F8F8F2", "#BD93F9", "#50FA7B", vivid: true, hover: "#FF79C6"),
        Create("neon",            "Neon",              "#1A051F", "#F7F0FF", "#FF2EF2", "#F5DB42", vivid: true),
        Create("purple",          "Purple",            "#14101C", "#F2EDFA", "#9942FA", "#C8A2FF"),
        Create("wine",            "Wine",              "#1F0D14", "#F2E6E6", "#A63D5A", "#E08AA0"),
        Create("barbie-pink",     "Barbie Pink",       "#260D26", "#FFE3F3", "#E0218A", "#FF8AC9"),
        Create("cotton-candy",    "Cotton Candy",      "#FFF9F0", "#3A2E4D", "#AA96DA", "#E76FA3"),
        Create("tropical",        "Tropical",          "#17212E", "#F4FBFF", "#2E9EEB", "#7ED321", vivid: true),
        Create("sunset",          "Sunset",            "#FFFBDA", "#33261A", "#ED9455", "#C8553D"),
        Create("orange",          "Orange",            "#261A0D", "#FFF2D9", "#E08A33", "#FFCC66"),
        Create("nord",            "Nord",              "#2E3440", "#ECEFF4", "#88C0D0", "#A3BE8C"),
        Create("solarized-dark",  "Solarized Dark",    "#002B36", "#EEE8D5", "#268BD2", "#2AA198"),
        Create("solarized-light", "Solarized Light",   "#FDF6E3", "#073642", "#268BD2", "#D33682"),
        Create("tokyo-night",     "Tokyo Night",       "#1A1B26", "#C0CAF5", "#7AA2F7", "#BB9AF7"),
        Create("rose-pine",       "Rose Pine",         "#191724", "#E0DEF4", "#C4A7E7", "#EBBCBA"),
        Create("one-dark",        "One Dark",          "#282C34", "#D7DAE0", "#61AFEF", "#98C379"),
        Create("monokai",         "Monokai",           "#272822", "#F8F8F2", "#F92672", "#A6E22E"),
        Create("midnight",        "Midnight",          "#000000", "#E8E8E8", "#3D7EFF", "#7FA8FF"),
        Create("sakura",          "Sakura",            "#FFF0F5", "#4A2035", "#E75480", "#B03060"),
        Create("mint",            "Mint",              "#F0FAF5", "#17352A", "#2FA37C", "#1B7A5A"),
    ];

    /// <param name="id">Stable id, stored in the configuration.</param>
    /// <param name="name">Display name.</param>
    /// <param name="background">Window background.</param>
    /// <param name="text">Main text color.</param>
    /// <param name="accent">Buttons, selection, title bars and tabs.</param>
    /// <param name="highlight">Small marks that must stand out: check marks, active sliders, drop targets.</param>
    /// <param name="vivid">Stronger accents: pure accent borders, saturated title and active tab, bolder hover and press.</param>
    /// <param name="hover">Vivid only: second accent for hover, press and the active tab (default: accent).</param>
    private static UiTheme Create(string id, string name, string background, string text, string accent, string highlight,
        bool vivid = false, string? hover = null) {
        var bg = ColorUtil.HexToVector4(background);
        var fg = ColorUtil.HexToVector4(text);
        var acc = ColorUtil.HexToVector4(accent);
        var mark = ColorUtil.HexToVector4(highlight);
        var hoverAccent = hover != null ? ColorUtil.HexToVector4(hover) : acc;
        var transparent = Vector4.Zero;

        // neutral surfaces between background and text, with a touch of the accent
        Vector4 Neutral(float amount) => Mix(Mix(bg, fg, amount), acc, amount * 0.3f);

        // accent fills, capped so the text stays readable on the strongest one
        var strongest = MathF.Min(MaxAccentMix, MaxMixKeepingContrast(bg, acc, fg, MinTextContrast));
        Vector4 Fill(float strength) => Mix(bg, acc, strongest * strength);

        // vivid hover / press; the states that stay on screen never go below the regular text contrast
        var strongestHover = MaxMixKeepingContrast(bg, hoverAccent, fg, MinVividHoverContrast);
        Vector4 Hovered(float regularStrength) => vivid ? Mix(bg, hoverAccent, strongestHover * 0.85f) : Fill(regularStrength);
        var pressed = vivid ? Mix(bg, hoverAccent, strongestHover) : Fill(1f);
        var titleActive = vivid ? Mix(bg, acc, MaxMixKeepingContrast(bg, acc, fg, MinTextContrast)) : Fill(0.6f);
        var tabActive = vivid ? Mix(bg, hoverAccent, MaxMixKeepingContrast(bg, hoverAccent, fg, MinTextContrast)) : Fill(0.9f);

        var frame = Neutral(0.10f);
        var accentMark = Readable(acc, bg, MinControlContrast, fg);
        var neutralBorder = Readable(Neutral(0.22f), bg, 1.4f, fg);
        var border = vivid ? accentMark : neutralBorder;
        var highlightMark = Readable(mark, frame, MinControlContrast, fg);
        var titleBg = Neutral(0.06f);
        var tab = Mix(Neutral(0.08f), acc, 0.08f);
        var dim = new Vector4(0f, 0f, 0f, Luminance(bg) > 0.4f ? 0.25f : 0.5f);

        return new UiTheme {
            Id = UiTheme.BuiltInPrefix + id,
            Name = name,
            IsBuiltIn = true,
            Colors = new Dictionary<ImGuiCol, Vector4> {
                [ImGuiCol.Text] = fg,
                [ImGuiCol.TextDisabled] = Readable(Mix(bg, fg, 0.45f), bg, MinDisabledTextContrast, fg),
                [ImGuiCol.TextSelectedBg] = WithAlpha(acc, 0.35f),

                [ImGuiCol.WindowBg] = bg,
                [ImGuiCol.ChildBg] = transparent,
                [ImGuiCol.PopupBg] = Neutral(0.04f),
                [ImGuiCol.MenuBarBg] = Neutral(0.05f),
                [ImGuiCol.ModalWindowDimBg] = dim,

                [ImGuiCol.TitleBg] = titleBg,
                [ImGuiCol.TitleBgActive] = titleActive,
                [ImGuiCol.TitleBgCollapsed] = WithAlpha(titleBg, 0.75f),

                [ImGuiCol.Border] = border,
                [ImGuiCol.BorderShadow] = transparent,
                [ImGuiCol.Separator] = border,
                [ImGuiCol.SeparatorHovered] = accentMark,
                [ImGuiCol.SeparatorActive] = highlightMark,
                [ImGuiCol.ResizeGrip] = WithAlpha(accentMark, 0.25f),
                [ImGuiCol.ResizeGripHovered] = WithAlpha(accentMark, 0.65f),
                [ImGuiCol.ResizeGripActive] = accentMark,

                [ImGuiCol.FrameBg] = frame,
                [ImGuiCol.FrameBgHovered] = Neutral(0.16f),
                [ImGuiCol.FrameBgActive] = Neutral(0.22f),
                [ImGuiCol.CheckMark] = highlightMark,
                [ImGuiCol.SliderGrab] = Readable(acc, frame, 2.5f, fg),
                [ImGuiCol.SliderGrabActive] = highlightMark,
                [ImGuiCol.DragDropTarget] = highlightMark,

                [ImGuiCol.ScrollbarBg] = transparent,
                [ImGuiCol.ScrollbarGrab] = Neutral(0.25f),
                [ImGuiCol.ScrollbarGrabHovered] = vivid ? accentMark : Neutral(0.35f),
                [ImGuiCol.ScrollbarGrabActive] = vivid ? Readable(hoverAccent, bg, MinControlContrast, fg) : Neutral(0.45f),

                [ImGuiCol.Button] = Fill(0.5f),
                [ImGuiCol.ButtonHovered] = Hovered(0.75f),
                [ImGuiCol.ButtonActive] = pressed,
                [ImGuiCol.Header] = Fill(0.7f),
                [ImGuiCol.HeaderHovered] = Hovered(0.85f),
                [ImGuiCol.HeaderActive] = pressed,

                [ImGuiCol.Tab] = tab,
                [ImGuiCol.TabHovered] = Hovered(0.75f),
                [ImGuiCol.TabActive] = tabActive,
                [ImGuiCol.TabUnfocused] = titleBg,
                [ImGuiCol.TabUnfocusedActive] = Fill(0.5f),

                [ImGuiCol.TableHeaderBg] = Neutral(0.10f),
                [ImGuiCol.TableBorderStrong] = neutralBorder,
                [ImGuiCol.TableBorderLight] = Neutral(0.12f),
                [ImGuiCol.TableRowBg] = transparent,
                [ImGuiCol.TableRowBgAlt] = WithAlpha(fg, 0.04f),

                [ImGuiCol.DockingPreview] = WithAlpha(acc, 0.5f),
                [ImGuiCol.DockingEmptyBg] = bg,
                [ImGuiCol.PlotLines] = accentMark,
                [ImGuiCol.PlotLinesHovered] = highlightMark,
                [ImGuiCol.PlotHistogram] = accentMark,
                [ImGuiCol.PlotHistogramHovered] = highlightMark,

                [ImGuiCol.NavHighlight] = accentMark,
                [ImGuiCol.NavWindowingHighlight] = WithAlpha(fg, 0.7f),
                [ImGuiCol.NavWindowingDimBg] = dim,
            },
        };
    }

    private static Vector4 Mix(Vector4 from, Vector4 to, float amount) => Vector4.Lerp(from, to, amount);

    private static Vector4 WithAlpha(Vector4 color, float alpha) => color with { W = alpha };

    private static float Luminance(Vector4 color) {
        static float Channel(float v) => v <= 0.03928f ? v / 12.92f : MathF.Pow((v + 0.055f) / 1.055f, 2.4f);
        return 0.2126f * Channel(color.X) + 0.7152f * Channel(color.Y) + 0.0722f * Channel(color.Z);
    }

    /// <summary>WCAG contrast ratio (1 = identical, 21 = black on white).</summary>
    private static float Contrast(Vector4 a, Vector4 b) {
        var (la, lb) = (Luminance(a), Luminance(b));
        return (MathF.Max(la, lb) + 0.05f) / (MathF.Min(la, lb) + 0.05f);
    }

    /// <summary>Largest mix of <paramref name="to"/> over <paramref name="from"/> that keeps <paramref name="text"/> readable.</summary>
    private static float MaxMixKeepingContrast(Vector4 from, Vector4 to, Vector4 text, float minContrast) {
        for (var amount = 1f; amount > 0f; amount -= 0.01f)
            if (Contrast(Mix(from, to, amount), text) >= minContrast)
                return amount;
        return 0f;
    }

    /// <summary>Moves <paramref name="color"/> towards <paramref name="towards"/> until it stands out from <paramref name="against"/>.</summary>
    internal static Vector4 Readable(Vector4 color, Vector4 against, float minContrast, Vector4 towards) {
        for (var amount = 0f; amount < 1f; amount += 0.02f) {
            var candidate = Mix(color, towards, amount);
            if (Contrast(candidate, against) >= minContrast)
                return candidate;
        }
        return towards;
    }
}
