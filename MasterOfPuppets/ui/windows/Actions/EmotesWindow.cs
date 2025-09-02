using System;
using System.Numerics;
using System.Linq;
using System.Collections.Generic;

using Dalamud.Interface.Windowing;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.ImGuiNotification;

using MasterOfPuppets.Resources;
using Dalamud.Interface.Utility;

namespace MasterOfPuppets;

public class EmotesWindow : Window
{
    private Plugin Plugin { get; }

    private readonly List<ExecutableAction> UnlockedActions = new();
    private string SearchString = "";
    private readonly List<int> ListSearchedIndexs = new();

    public EmotesWindow(Plugin plugin) : base($"{Language.EmotesTitle}###EmotesWindow")
    {
        Plugin = plugin;

        Size = new Vector2(500, 300);
        SizeCondition = ImGuiCond.FirstUseEver;
        // SizeCondition = ImGuiCond.Always;
        // Flags = ImGuiWindowFlags.NoResize;
    }

    public override void PreDraw()
    {
        base.PreDraw();
    }

    private void DrawEmoteEntry(int actionIndex, ExecutableAction emote)
    {
        ImGui.PushID(actionIndex);
        ImGui.TableNextRow();
        // ImGui.TableSetColumnIndex(0);
        ImGui.TableNextColumn();
        ImGui.TextUnformatted($"{actionIndex + 1:000}");

        ImGui.TableNextColumn();
        var icon = DalamudApi.TextureProvider.GetFromGameIcon(emote.IconId).GetWrapOrEmpty().Handle;
        var iconSize = new Vector2(50 * ImGuiHelpers.GlobalScale, 50 * ImGuiHelpers.GlobalScale);

        ImGui.Image(icon, iconSize);
        if (ImGui.IsItemClicked())
        {
            Plugin.IpcProvider.ExecuteTextCommand(emote.TextCommand);
        }
        ImGuiUtil.ToolTip("Click to execute");

        ImGui.TableNextColumn();
        ImGui.TextUnformatted($"{emote.ActionName}");
        if (ImGui.IsItemClicked())
        {
            ImGui.SetClipboardText($"{emote.ActionName}");
            DalamudApi.ShowNotification($"Name copied to clipboard", NotificationType.Info, 5000);
        }
        ImGuiUtil.ToolTip("Click to copy");

        ImGui.TableNextColumn();
        ImGui.TextUnformatted(emote.TextCommand);
        if (ImGui.IsItemClicked())
        {
            ImGui.SetClipboardText(emote.TextCommand);
            DalamudApi.ShowNotification($"Command copied to clipboard", NotificationType.Info, 5000);
        }
        ImGuiUtil.ToolTip("Click to copy");

        // ImGui.TableNextColumn();
        // ImGui.TextUnformatted(emote.Category);

        ImGui.PopID();
    }

    private unsafe void DrawEmoteTable()
    {
        UnlockedActions.Clear();
        UnlockedActions.AddRange(EmoteHelper.GetAllowedItems());

        var tableFlags = ImGuiTableFlags.RowBg | ImGuiTableFlags.PadOuterX |
               ImGuiTableFlags.NoSavedSettings | ImGuiTableFlags.BordersInnerV;
        var tableColumnCount = 4;

        var isFiltered = !string.IsNullOrEmpty(SearchString);
        var itemCount = isFiltered ? ListSearchedIndexs.Count : UnlockedActions.Count;

        if (ImGui.BeginTable("##EmotesTable", tableColumnCount, tableFlags))
        {
            ImGui.TableSetupColumn("  ", ImGuiTableColumnFlags.WidthFixed);
            ImGui.TableSetupColumn("Icon", ImGuiTableColumnFlags.WidthFixed);
            ImGui.TableSetupColumn("Name", ImGuiTableColumnFlags.WidthStretch, 1.0f);
            ImGui.TableSetupColumn("Text Commands", ImGuiTableColumnFlags.WidthStretch, 1.0f);
            // ImGui.TableSetupColumn("Category", ImGuiTableColumnFlags.WidthStretch);

            ImGuiListClipperPtr clipper;
            unsafe
            {
                clipper = new ImGuiListClipperPtr(ImGuiNative.ImGuiListClipper());
            }

            clipper.Begin(itemCount);

            while (clipper.Step())
            {
                for (int i = clipper.DisplayStart; i < clipper.DisplayEnd; i++)
                {
                    if (i >= itemCount) break;
                    int realIndex = isFiltered ? ListSearchedIndexs[i] : i;
                    if (realIndex >= UnlockedActions.Count) continue;

                    DrawEmoteEntry(realIndex, UnlockedActions[realIndex]);
                }
            }

            clipper.End();
            ImGui.EndTable();
        }
    }

    private void Search()
    {
        ListSearchedIndexs.Clear();

        ListSearchedIndexs.AddRange(
            UnlockedActions
            .Select((item, index) => new { item, index })
            .Where(x => x.item.ActionName.ToString().Contains(SearchString, StringComparison.OrdinalIgnoreCase))
            .Select(x => x.index)
            .ToList()
        );
    }

    public void DrawHeader()
    {
        ImGui.TextUnformatted($"{Language.EmotesTitle} (unlocked)");
        ImGui.SameLine();
        ImGuiUtil.HelpMarker("""
        Click on icon to execute
        Click on command to copy
        """);

        ImGui.Spacing();

        if (ImGui.InputTextWithHint("##EmoteSearchInput", Language.SearchInputLabel, ref SearchString, 255, ImGuiInputTextFlags.AutoSelectAll))
        {
            Search();
        }

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();
    }

    public override void Draw()
    {
        ImGui.BeginChild("##EmotesHeaderFixedHeight", new Vector2(-1, 55 * ImGuiHelpers.GlobalScale), false, ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse);
        DrawHeader();
        ImGui.EndChild();

        ImGui.BeginChild("##EmotesListScrollableContent", new Vector2(-1, 0), false, ImGuiWindowFlags.HorizontalScrollbar);
        DrawEmoteTable();
        ImGui.EndChild();
    }
}
