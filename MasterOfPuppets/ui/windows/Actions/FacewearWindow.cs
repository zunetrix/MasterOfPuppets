using System;
using System.Numerics;
using System.Linq;
using System.Collections.Generic;

using Dalamud.Interface.Windowing;
using Dalamud.Interface.Utility;
using Dalamud.Interface.ImGuiNotification;
using Dalamud.Bindings.ImGui;

using MasterOfPuppets.Resources;

namespace MasterOfPuppets;

public class FacewearWindow : Window
{
    private Plugin Plugin { get; }

    private readonly List<ExecutableAction> UnlockedActions = new();
    private string _searchString = "";
    private readonly List<int> ListSearchedIndexs = new();

    public FacewearWindow(Plugin plugin) : base($"{Language.FacewearTitle}###FacewearWindow")
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

    private void DrawFacewearEntry(int actionIndex, ExecutableAction facewear)
    {
        ImGui.PushID(actionIndex);
        ImGui.TableNextRow();
        ImGui.TableNextColumn();
        ImGui.TextUnformatted($"{actionIndex + 1:000}");

        ImGui.TableNextColumn();
        var icon = DalamudApi.TextureProvider.GetFromGameIcon(facewear.IconId).GetWrapOrEmpty().Handle;
        var iconSize = ImGuiHelpers.ScaledVector2(50, 50);

        ImGui.Image(icon, iconSize);
        if (ImGui.IsItemClicked())
        {
            Plugin.IpcProvider.ExecuteTextCommand(facewear.TextCommand);
        }
        ImGuiUtil.ToolTip("Click to execute");

        ImGui.TableNextColumn();
        ImGui.TextUnformatted($"{facewear.ActionName}");
        if (ImGui.IsItemClicked())
        {
            ImGui.SetClipboardText($"{facewear.ActionName}");
            DalamudApi.ShowNotification(Language.ClipboardCopyMessage, NotificationType.Info, 5000);
        }
        ImGuiUtil.ToolTip("Click to copy");

        ImGui.TableNextColumn();
        ImGui.TextUnformatted(facewear.TextCommand);
        if (ImGui.IsItemClicked())
        {
            ImGui.SetClipboardText(facewear.TextCommand);
            DalamudApi.ShowNotification(Language.ClipboardCopyMessage, NotificationType.Info, 5000);
        }
        ImGuiUtil.ToolTip("Click to copy");

        ImGui.PopID();
    }

    private unsafe void DrawFacewearTable()
    {
        UnlockedActions.Clear();
        UnlockedActions.AddRange(FacewearHelper.GetAllowedItems());

        var tableFlags = ImGuiTableFlags.RowBg | ImGuiTableFlags.PadOuterX |
               ImGuiTableFlags.NoSavedSettings | ImGuiTableFlags.BordersInnerV;
        var tableColumnCount = 4;

        var isFiltered = !string.IsNullOrEmpty(_searchString);
        var itemCount = isFiltered ? ListSearchedIndexs.Count : UnlockedActions.Count;

        if (ImGui.BeginTable("##FacewearTable", tableColumnCount, tableFlags))
        {
            ImGui.TableSetupColumn("  ", ImGuiTableColumnFlags.WidthFixed);
            ImGui.TableSetupColumn("Icon", ImGuiTableColumnFlags.WidthFixed);
            ImGui.TableSetupColumn("Name", ImGuiTableColumnFlags.WidthStretch, 1.0f);
            ImGui.TableSetupColumn("Text Commands", ImGuiTableColumnFlags.WidthStretch);

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

                    DrawFacewearEntry(realIndex, UnlockedActions[realIndex]);
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
            .Where(x => x.item.ActionName.Contains(_searchString, StringComparison.OrdinalIgnoreCase))
            .Select(x => x.index)
            .ToList()
        );
    }

    private void DrawHeader()
    {
        ImGui.TextUnformatted($"{Language.FacewearTitle} (unlocked)");
        ImGui.SameLine();
        ImGuiUtil.HelpMarker("""
        Click on icon to execute
        Click on command to copy
        """);

        ImGui.Spacing();

        if (ImGui.InputTextWithHint("##FacewearSearchInput", Language.SearchInputLabel, ref _searchString, 255, ImGuiInputTextFlags.AutoSelectAll))
        {
            Search();
        }

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();
    }

    public override void Draw()
    {
        ImGui.BeginChild("##FacewearFixedHeight", new Vector2(-1, 55 * ImGuiHelpers.GlobalScale), false, ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse);
        DrawHeader();
        ImGui.EndChild();

        ImGui.BeginChild("##FacewerarListScrollableContent", new Vector2(-1, 0), false, ImGuiWindowFlags.HorizontalScrollbar);
        DrawFacewearTable();
        ImGui.EndChild();
    }
}
