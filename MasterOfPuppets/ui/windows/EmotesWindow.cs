using System;
using System.Numerics;
using System.Linq;
using System.Collections.Generic;

using Dalamud.Interface.Windowing;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.ImGuiNotification;
using Lumina.Excel.Sheets;

using MasterOfPuppets.Resources;

namespace MasterOfPuppets;

public class EmotesWindow : Window
{
    private Plugin Plugin { get; }

    private List<Emote> UnlockedEmotes = new();
    private string EmoteSearchString = "";
    private readonly List<int> EmoteListSearchedIndexs = new();

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

    private void DrawEmoteEntry(int emoteIndex, Emote emote)
    {
        ImGui.PushID(emoteIndex);
        ImGui.TableNextRow();
        // ImGui.TableSetColumnIndex(0);
        ImGui.TableNextColumn();
        ImGui.TextUnformatted($"{emoteIndex + 1:000}");

        ImGui.TableNextColumn();
        var emoteIcon = (int)emote.Icon;
        // var icon = DalamudApi.TextureProvider.GetFromGameIcon(60042).GetWrapOrEmpty().Handle;
        var icon = DalamudApi.TextureProvider.GetFromGameIcon(emoteIcon).GetWrapOrEmpty().Handle;
        // var iconSize = new Vector2(ImGui.GetFrameHeight(), ImGui.GetFrameHeight());
        var iconSize = new Vector2(50, 50);

        ImGui.Image(icon, iconSize);
        if (ImGui.IsItemClicked())
        {
            Plugin.IpcProvider.BroadcastTextCommand($"{emote.TextCommand.Value.Command}");
        }
        ImGuiUtil.ToolTip("Click to execute");

        ImGui.TableNextColumn();
        ImGui.TextUnformatted($"{emote.Name.ToString()}");
        // ImGui.TextUnformatted($"{emote.Name.ToString()} {emote.TextCommand.Value.Command.ToString()} {}");

        ImGui.TableNextColumn();
        ImGui.TextUnformatted($"{emote.TextCommand.Value.Command.ToString()}\n{string.Join("\n", emote.TextCommand.Value.Alias.ToList())}");
        if (ImGui.IsItemClicked())
        {
            ImGui.SetClipboardText($"{emote.TextCommand.Value.Command.ToString()}");
            DalamudApi.ShowNotification($"Emote copied to clipboard", NotificationType.Info, 5000);
        }
        ImGuiUtil.ToolTip("Click to copy");

        ImGui.TableNextColumn();
        ImGui.TextUnformatted($"{emote.EmoteCategory.Value.Name.ToString()}");

        ImGui.TableNextColumn();
        ImGui.TextUnformatted($"{emoteIcon}");

        // ImGui.TableNextColumn();
        // ImGui.SameLine();
        // if (ImGuiUtil.IconButton(FontAwesomeIcon.Copy, $"##CopyEmote_{emoteIndex}", "Copy"))
        // {
        //     ImGui.SetClipboardText($"{emote.TextCommand.Value.Command.ToString()}");
        //     DalamudApi.ShowNotification($"Emote copied to clipboard", NotificationType.Info, 5000);
        // }

        // ImGui.SameLine();
        // if (ImGuiUtil.IconButton(FontAwesomeIcon.Play, $"##ExecuteEmote_{emoteIndex}", "Execute Emote"))
        // {
        //     Chat.SendMessage($"{emote.TextCommand.Value.Command}");
        // }

        ImGui.PopID();
    }

    private unsafe void DrawEmoteTable()
    {

        UnlockedEmotes.Clear();

        foreach (Emote emote in DalamudApi.DataManager.GetExcelSheet<Emote>()!)
        {
            if (UnlockState.IsUnlocked(emote) && emote.EmoteCategory.Value.Name.ToString() != "Expressions")
            {
                UnlockedEmotes.Add(emote);
            }
        }


        var tableFlags = ImGuiTableFlags.RowBg | ImGuiTableFlags.PadOuterX |
               ImGuiTableFlags.NoSavedSettings | ImGuiTableFlags.BordersInnerV;
        var tableColumnCount = 6;

        var isFiltered = !string.IsNullOrEmpty(EmoteSearchString);
        var itemCount = isFiltered ? EmoteListSearchedIndexs.Count : UnlockedEmotes.Count;

        if (ImGui.BeginTable("##EmotesTable", tableColumnCount, tableFlags))
        {
            ImGui.TableSetupColumn("  ", ImGuiTableColumnFlags.WidthFixed);
            ImGui.TableSetupColumn("Icon", ImGuiTableColumnFlags.WidthFixed);
            ImGui.TableSetupColumn("Name", ImGuiTableColumnFlags.WidthFixed);
            ImGui.TableSetupColumn("Text Commands", ImGuiTableColumnFlags.WidthFixed);
            ImGui.TableSetupColumn("Category", ImGuiTableColumnFlags.WidthFixed);
            ImGui.TableSetupColumn("ID", ImGuiTableColumnFlags.WidthFixed);


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
                    int realIndex = isFiltered ? EmoteListSearchedIndexs[i] : i;
                    if (realIndex >= UnlockedEmotes.Count) continue;

                    DrawEmoteEntry(realIndex, UnlockedEmotes[realIndex]);
                }
            }

            clipper.End();

            // for (int emoteIndex = 0; emoteIndex < unlockedEmotes.Count; emoteIndex++)
            // {
            //     DrawEmoteEntry(emoteIndex, unlockedEmotes[emoteIndex]);
            // }

            ImGui.EndTable();
        }
    }

    private void SearchEmote()
    {
        EmoteListSearchedIndexs.Clear();

        EmoteListSearchedIndexs.AddRange(
            UnlockedEmotes
            .Select((item, index) => new { item, index })
            .Where(x => x.item.Name.ToString().Contains(EmoteSearchString, StringComparison.OrdinalIgnoreCase))
            .Select(x => x.index)
            .ToList()
        );
    }

    public override void Draw()
    {
        ImGui.TextUnformatted("Emotes (unlocked)");
        ImGui.SameLine();
        ImGuiUtil.HelpMarker("""
        Click on emote to execute
        Click on command to copy
        """);

        ImGui.Separator();

        if (ImGui.InputTextWithHint("##EmoteSearchInput", Language.EmoteSearchInputLabel, ref EmoteSearchString, 255, ImGuiInputTextFlags.AutoSelectAll))
        {
            SearchEmote();
        }

        ImGui.Spacing();
        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();
        ImGui.Spacing();

        // DrawEmoteList();
        DrawEmoteTable();
    }
}
