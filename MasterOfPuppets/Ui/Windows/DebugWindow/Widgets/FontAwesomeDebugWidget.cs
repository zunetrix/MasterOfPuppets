

using System.Linq;
using System.Numerics;

using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

using MasterOfPuppets.Extensions;

namespace MasterOfPuppets.Debug;

public sealed class FontAwesomeDebugWidget : Widget {
    public override string Title => "Font Awesome";

    static readonly (FontAwesomeIcon icon, string name)[] glyphs =
        Enumerable.Range(0xE000, 0xF000)
        .Select(i => ((FontAwesomeIcon)i, ((FontAwesomeIcon)i).ToString()))
        .Where(i => i.Item2.Any(char.IsLetter)) // remove unknown icons
        .ToArray();

    private static (FontAwesomeIcon icon, string name)[] searchedGlyphs = glyphs;
    private static string searchedString = string.Empty;

    public FontAwesomeDebugWidget(WidgetContext ctx) : base(ctx) {
    }

    public override void Draw() {
        DrawFontAwesomeIconBrowser();
    }

    private static void DrawFontAwesomeIconBrowser() {
        ImGui.Spacing();

        if (ImGui.InputTextWithHint("##FontAwesomeSearchInput", "Search", ref searchedString, 100)) {
            if (!string.IsNullOrWhiteSpace(searchedString)) {
                searchedGlyphs = glyphs.Where(i => i.name.ContainsIgnoreCase(searchedString)).ToArray();
            } else {
                searchedGlyphs = glyphs;
            }
        }

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();
        ImGui.Spacing();

        ImGui.PushFont(UiBuilder.IconFont);
        var windowWidth = ImGui.GetWindowWidth() - 60 * ImGui.GetIO().FontGlobalScale;
        var lineLength = 0f;

        foreach (var icon in searchedGlyphs) {
            ImGui.Text(icon.icon.ToIconString());

            if (ImGui.IsItemHovered()) {
                ImGui.BeginTooltip();
                ImGui.SetWindowFontScale(3);
                ImGui.Text(icon.icon.ToIconString());
                ImGui.SetWindowFontScale(1);
                ImGui.PushFont(UiBuilder.DefaultFont);
                ImGui.Text($"{icon.name}\n{(int)icon.icon}\n0x{(int)icon.icon:X}");
                ImGui.EndTooltip();
                ImGui.PopFont();
            }

            if (ImGui.IsItemClicked()) {
                ImGui.SetClipboardText($"(FontAwesomeIcon){(int)icon.Item1}");
            }

            if (lineLength < windowWidth) {
                lineLength += 30 * ImGui.GetIO().FontGlobalScale;
                ImGui.SameLine(lineLength);
            } else {
                lineLength = 0;
                ImGui.Dummy(new Vector2(0, 10 * ImGui.GetIO().FontGlobalScale));
            }
        }

        ImGui.PopFont();
    }
}

