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

public class FashionAccessoriesWindow : Window
{
    private Plugin Plugin { get; }

    private readonly List<ExecutableAction> UnlockedActions = new();
    private string SearchString = "";
    private readonly List<int> ListSearchedIndexs = new();

    public FashionAccessoriesWindow(Plugin plugin) : base($"{Language.FashionAccessoriesTitle}###FashionAccessoriesWindow")
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

    private void DrawFashionEntry(int actionIndex, ExecutableAction fashionAccessorie)
    {
        ImGui.PushID(actionIndex);
        ImGui.TableNextRow();
        ImGui.TableNextColumn();
        ImGui.TextUnformatted($"{actionIndex + 1:000}");

        ImGui.TableNextColumn();
        var icon = DalamudApi.TextureProvider.GetFromGameIcon(fashionAccessorie.IconId).GetWrapOrEmpty().Handle;
        var iconSize = new Vector2(50 * ImGuiHelpers.GlobalScale, 50 * ImGuiHelpers.GlobalScale);

        ImGui.Image(icon, iconSize);
        if (ImGui.IsItemClicked())
        {
            Plugin.IpcProvider.ExecuteTextCommand(fashionAccessorie.TextCommand);
        }
        ImGuiUtil.ToolTip("Click to execute");

        ImGui.TableNextColumn();
        ImGui.TextUnformatted($"{fashionAccessorie.ActionName}");
        if (ImGui.IsItemClicked())
        {
            ImGui.SetClipboardText($"{fashionAccessorie.ActionName}");
            DalamudApi.ShowNotification($"Name copied to clipboard", NotificationType.Info, 5000);
        }
        ImGuiUtil.ToolTip("Click to copy");

        ImGui.TableNextColumn();
        ImGui.TextUnformatted(fashionAccessorie.TextCommand);
        if (ImGui.IsItemClicked())
        {
            ImGui.SetClipboardText(fashionAccessorie.TextCommand);
            DalamudApi.ShowNotification($"Command copied to clipboard", NotificationType.Info, 5000);
        }
        ImGuiUtil.ToolTip("Click to copy");

        ImGui.PopID();
    }

    private unsafe void DrawFashionTable()
    {
        UnlockedActions.Clear();
        UnlockedActions.AddRange(FashionAccessoriesHelper.GetAllowedItems());

        var tableFlags = ImGuiTableFlags.RowBg | ImGuiTableFlags.PadOuterX |
               ImGuiTableFlags.NoSavedSettings | ImGuiTableFlags.BordersInnerV;
        var tableColumnCount = 4;

        var isFiltered = !string.IsNullOrEmpty(SearchString);
        var itemCount = isFiltered ? ListSearchedIndexs.Count : UnlockedActions.Count;

        if (ImGui.BeginTable("##FashionAccessoriesTable", tableColumnCount, tableFlags))
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

                    DrawFashionEntry(realIndex, UnlockedActions[realIndex]);
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
            .Where(x => x.item.ActionName.Contains(SearchString, StringComparison.OrdinalIgnoreCase))
            .Select(x => x.index)
            .ToList()
        );
    }

    public void DrawHeader()
    {
        ImGui.TextUnformatted($"{Language.FashionAccessoriesTitle} (unlocked)");
        ImGui.SameLine();
        ImGuiUtil.HelpMarker("""
        Click on icon to execute
        Click on command to copy
        """);

        ImGui.Spacing();

        if (ImGui.InputTextWithHint("##FashionAccessoriesSearchInput", Language.SearchInputLabel, ref SearchString, 255, ImGuiInputTextFlags.AutoSelectAll))
        {
            Search();
        }

        ImGui.SameLine();
        var rainCheckIcon = DalamudApi.TextureProvider.GetFromGameIcon(ActionHelper.FavoriteActions.RainCheck.IconId).GetWrapOrEmpty().Handle;
        var umbrellaDanceIcon = DalamudApi.TextureProvider.GetFromGameIcon(ActionHelper.FavoriteActions.UmbrellaDance.IconId).GetWrapOrEmpty().Handle;
        var iconSize = new Vector2(30 * ImGuiHelpers.GlobalScale, 30 * ImGuiHelpers.GlobalScale);

        ImGui.Image(rainCheckIcon, iconSize);
        if (ImGui.IsItemClicked())
        {
            Plugin.IpcProvider.ExecuteActionCommand(ActionHelper.FavoriteActions.RainCheck.ActionId);
        }
        ImGuiUtil.ToolTip("Click to execute");

        ImGui.SameLine();
        ImGui.Image(umbrellaDanceIcon, iconSize);
        if (ImGui.IsItemClicked())
        {
            Plugin.IpcProvider.ExecuteActionCommand(ActionHelper.FavoriteActions.UmbrellaDance.ActionId);
        }
        ImGuiUtil.ToolTip("Click to execute");

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();
    }

    public override void Draw()
    {
        ImGui.BeginChild("##FashionHeaderFixedHeight", new Vector2(-1, 70 * ImGuiHelpers.GlobalScale), false, ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse);
        DrawHeader();
        ImGui.EndChild();

        ImGui.BeginChild("##FashionListScrollableContent", new Vector2(-1, 0), false, ImGuiWindowFlags.HorizontalScrollbar);
        DrawFashionTable();
        ImGui.EndChild();
    }
}
