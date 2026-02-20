

using System.Linq;
using System.Numerics;

using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

using MasterOfPuppets.Extensions;

namespace MasterOfPuppets.Debug;

public sealed class FontAwesomeDebugWidget : Widget {
    public override string Title => "Font Awesome";
    private static int iconSize = 30;

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

        ImGui.SameLine();
        ImGui.Text("Icon Size:");
        ImGui.SetNextItemWidth(100);
        ImGui.SameLine();
        if (ImGui.DragInt("##FontAwesomeIconSizeInput", ref iconSize, 1, 20, 150)) {
            //
        }

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();
        ImGui.Spacing();

        ImGui.PushFont(UiBuilder.IconFont);
        var itemSpacing = iconSize * ImGui.GetIO().FontGlobalScale;
        var windowWidth = ImGui.GetContentRegionMax().X - itemSpacing;
        var lineLength = 0f;

        foreach (var icon in searchedGlyphs) {
            var iconScale = iconSize / 30f;
            ImGui.SetWindowFontScale(iconScale);
            ImGui.Text(icon.icon.ToIconString());
            ImGui.SetWindowFontScale(1);

            if (ImGui.IsItemHovered()) {
                ImGui.BeginTooltip();
                ImGui.SetWindowFontScale(iconSize / 10f + 0.5f);
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

            if (lineLength + itemSpacing < windowWidth) {
                lineLength += itemSpacing;
                ImGui.SameLine(lineLength);
            } else {
                lineLength = 0;
                ImGui.Dummy(new Vector2(0, iconSize * ImGui.GetIO().FontGlobalScale));
            }
        }

        ImGui.PopFont();
    }
}

