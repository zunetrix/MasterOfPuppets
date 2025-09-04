using System;
using System.Numerics;
using System.Linq;
using System.Collections.Generic;

using Dalamud.Interface;
using Dalamud.Interface.Windowing;
using Dalamud.Interface.Utility;
using Dalamud.Interface.ImGuiNotification;
using Dalamud.Bindings.ImGui;

using MasterOfPuppets.Resources;

namespace MasterOfPuppets;

public class MacroHelpWindow : Window
{
    private Plugin Plugin { get; }

    private string _searchString = "";
    private readonly List<int> ListSearchedIndexs = new();

    public MacroHelpWindow(Plugin plugin) : base($"{Plugin.Name} Help###MacroHelpWindow")
    {
        Plugin = plugin;

        Size = ImGuiHelpers.ScaledVector2(500, 300);
        SizeCondition = ImGuiCond.FirstUseEver;
        // SizeCondition = ImGuiCond.Always;
        // Flags = ImGuiWindowFlags.NoResize;
    }

    public override void PreDraw()
    {
        base.PreDraw();
    }

    private void DrawMopActionRow(int itemIndex, MopAction mopAction)
    {
        ImGui.PushID(itemIndex);
        ImGui.TableNextRow();
        ImGui.TableNextColumn();
        ImGui.TextUnformatted($"{itemIndex + 1:000}");
        ImGui.TextUnformatted("");

        // ImGui.TableNextColumn();
        // var icon = DalamudApi.TextureProvider.GetFromGameIcon(mount.IconId).GetWrapOrEmpty().Handle;
        // var iconSize = ImGuiHelpers.ScaledVector2(50, 50);

        // ImGui.Image(icon, iconSize);
        // if (ImGui.IsItemClicked())
        // {
        //     Plugin.IpcProvider.ExecuteTextCommand(mount.TextCommand);
        // }
        // ImGuiUtil.ToolTip("Click to execute");

        ImGui.TableNextColumn();
        ImGui.TextWrapped(mopAction.TextCommand);
        if (ImGui.IsItemClicked())
        {
            ImGui.SetClipboardText($"{mopAction.TextCommand}");
            DalamudApi.ShowNotification($"Copied to clipboard", NotificationType.Info, 5000);
        }
        ImGuiUtil.ToolTip("Click to copy");
        ImGui.TextUnformatted("");

        ImGui.TableNextColumn();
        ImGui.TextWrapped(mopAction.Example);
        if (ImGui.IsItemClicked())
        {
            ImGui.SetClipboardText(mopAction.Example);
            DalamudApi.ShowNotification($"Copied to clipboard", NotificationType.Info, 5000);
        }
        ImGuiUtil.ToolTip("Click to copy");
        ImGui.TextUnformatted("");

        ImGui.TableNextColumn();
        ImGui.TextWrapped(mopAction.Notes);
        ImGui.TextUnformatted("");

        ImGui.PopID();
    }

    private unsafe void DrawMacroActionsTable()
    {
        var tableFlags = ImGuiTableFlags.RowBg | ImGuiTableFlags.PadOuterX |
               ImGuiTableFlags.NoSavedSettings | ImGuiTableFlags.BordersInnerV;
        var tableColumnCount = 4;

        var isFiltered = !string.IsNullOrEmpty(_searchString);
        var itemCount = isFiltered ? ListSearchedIndexs.Count : MopMacroActionsHelper.Actions.Count;

        if (ImGui.BeginTable("##MopMacroActionTable", tableColumnCount, tableFlags))
        {
            ImGui.TableSetupColumn("  ", ImGuiTableColumnFlags.WidthFixed);
            ImGui.TableSetupColumn("Text Command", ImGuiTableColumnFlags.WidthStretch, 1.0f);
            ImGui.TableSetupColumn("Example", ImGuiTableColumnFlags.WidthStretch, 1.0f);
            ImGui.TableSetupColumn("Notes", ImGuiTableColumnFlags.WidthStretch);

            ImGui.TableHeadersRow();

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
                    if (realIndex >= MopMacroActionsHelper.Actions.Count) continue;

                    DrawMopActionRow(realIndex, MopMacroActionsHelper.Actions[realIndex]);
                }
            }

            clipper.End();
            ImGui.EndTable();
        }
    }

    private void DrawMopActionEntry(int itemIndex, MopAction mopAction)
    {
        if (ImGuiUtil.IconButton(FontAwesomeIcon.Copy, $"##CopyMopActionTextCommand_{itemIndex}", "Copy Text Command"))
        {
            ImGui.SetClipboardText($"{mopAction.TextCommand}");
            DalamudApi.ShowNotification($"Copied to clipboard", NotificationType.Info, 5000);
        }

        ImGui.SameLine();

        if (ImGui.CollapsingHeader($"{mopAction.TextCommand}##MopAction_{itemIndex}"))
        {
            ImGui.Indent();

            ImGui.TextUnformatted(mopAction.Example);
            if (ImGui.IsItemClicked())
            {
                ImGui.SetClipboardText(mopAction.Example);
                DalamudApi.ShowNotification($"Copied to clipboard", NotificationType.Info, 5000);
            }
            ImGuiUtil.ToolTip("Click to copy");

            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();

            ImGui.TextUnformatted(mopAction.Notes);

            ImGui.Unindent();
        }

        ImGui.Spacing();
        ImGui.Spacing();
    }

    private unsafe void DrawMacroActionsGroups()
    {
        var isFiltered = !string.IsNullOrEmpty(_searchString);
        var itemCount = isFiltered ? ListSearchedIndexs.Count : MopMacroActionsHelper.Actions.Count;

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
                if (realIndex >= MopMacroActionsHelper.Actions.Count) continue;

                DrawMopActionEntry(realIndex, MopMacroActionsHelper.Actions[realIndex]);
            }
        }

        clipper.End();
    }

    private void Search()
    {
        ListSearchedIndexs.Clear();

        ListSearchedIndexs.AddRange(
            MopMacroActionsHelper.Actions
            .Select((item, index) => new { item, index })
            .Where(x => x.item.TextCommand.Contains(_searchString, StringComparison.OrdinalIgnoreCase))
            .Select(x => x.index)
            .ToList()
        );
    }

    private void DrawHeader()
    {
        ImGui.TextUnformatted($"{Language.ActionsTitle}");
        ImGui.SameLine();
        ImGuiUtil.HelpMarker("""
        Click to copy
        """);

        ImGui.Spacing();

        if (ImGui.InputTextWithHint("##MacroHelpSearchInput", Language.SearchInputLabel, ref _searchString, 255, ImGuiInputTextFlags.AutoSelectAll))
        {
            Search();
        }

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();
    }

    public override void Draw()
    {
        ImGui.BeginChild("##MacroHelpHeaderFixedHeight", new Vector2(-1, 60 * ImGuiHelpers.GlobalScale), false, ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse);
        DrawHeader();
        ImGui.EndChild();

        // ImGui.BeginChild("##MacroHelpListScrollableContent", new Vector2(-1, 0), false, ImGuiWindowFlags.HorizontalScrollbar);
        // DrawMacroActionsTable();
        // ImGui.EndChild();
        DrawMacroActionsGroups();
    }
}
