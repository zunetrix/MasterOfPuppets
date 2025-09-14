using System;
using System.Numerics;
using System.Linq;
using System.Collections.Generic;

using Dalamud.Interface.Windowing;
using Dalamud.Interface.Utility;
using Dalamud.Interface.ImGuiNotification;
using Dalamud.Bindings.ImGui;

using MasterOfPuppets.Resources;
using Lumina.Excel.Sheets;

namespace MasterOfPuppets;

public class FashionAccessoriesWindow : Window
{
    private Plugin Plugin { get; }

    private readonly List<ExecutableAction> UnlockedActions = new();
    private string _searchString = string.Empty;
    private readonly List<int> ListSearchedIndexes = new();

    public FashionAccessoriesWindow(Plugin plugin) : base($"{Language.FashionAccessoriesTitle}###FashionAccessoriesWindow")
    {
        Plugin = plugin;

        Size = ImGuiHelpers.ScaledVector2(500, 300);
        SizeCondition = ImGuiCond.FirstUseEver;
        // SizeCondition = ImGuiCond.Always;
        // Flags = ImGuiWindowFlags.NoResize;
    }

    public override void OnOpen()
    {
        UnlockedActions.Clear();
        UnlockedActions.AddRange(FashionAccessoriesHelper.GetAllowedItems());
        base.OnOpen();
    }

    public override void OnClose()
    {
        ListSearchedIndexes.Clear();
        _searchString = string.Empty;
        base.OnClose();
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

    private void DrawFashionEntry(int actionIndex, ExecutableAction fashionAccessorie)
    {
        ImGui.PushID(actionIndex);
        ImGui.TableNextRow();
        ImGui.TableNextColumn();
        ImGui.TextUnformatted($"{actionIndex + 1:000}");

        ImGui.TableNextColumn();
        var icon = DalamudApi.TextureProvider.GetFromGameIcon(fashionAccessorie.IconId).GetWrapOrEmpty().Handle;
        var iconSize = ImGuiHelpers.ScaledVector2(50, 50);

        ImGui.Image(icon, iconSize);
        if (ImGui.IsItemClicked())
        {
            Plugin.IpcProvider.ExecuteTextCommand(fashionAccessorie.TextCommand);
        }
        ImGuiUtil.ToolTip(Language.ClickToExecute);

        ImGui.TableNextColumn();
        ImGui.TextUnformatted($"{fashionAccessorie.ActionName}");
        if (ImGui.IsItemClicked())
        {
            ImGui.SetClipboardText($"{fashionAccessorie.ActionName}");
            DalamudApi.ShowNotification(Language.ClipboardCopyMessage, NotificationType.Info, 5000);
        }
        ImGuiUtil.ToolTip(Language.ClickToCopy);

        ImGui.TableNextColumn();
        ImGui.TextUnformatted(fashionAccessorie.TextCommand);
        if (ImGui.IsItemClicked())
        {
            ImGui.SetClipboardText(fashionAccessorie.TextCommand);
            DalamudApi.ShowNotification(Language.ClipboardCopyMessage, NotificationType.Info, 5000);
        }
        ImGuiUtil.ToolTip(Language.ClickToCopy);

        ImGui.PopID();
    }

    private unsafe void DrawFashionTable()
    {
        var tableFlags = ImGuiTableFlags.RowBg | ImGuiTableFlags.PadOuterX |
               ImGuiTableFlags.NoSavedSettings | ImGuiTableFlags.BordersInnerV;
        var tableColumnCount = 4;

        var isFiltered = !string.IsNullOrEmpty(_searchString);
        var itemCount = isFiltered ? ListSearchedIndexes.Count : UnlockedActions.Count;

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
                    int realIndex = isFiltered ? ListSearchedIndexes[i] : i;
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
        ListSearchedIndexes.Clear();
        ListSearchedIndexes.AddRange(
            UnlockedActions
            .Select((item, index) => new { item, index })
            .Where(x => x.item.ActionName.Contains(_searchString, StringComparison.OrdinalIgnoreCase))
            .Select(x => x.index)
            .ToList()
        );
    }

    private void DrawHeader()
    {
        ImGui.TextUnformatted($"{Language.FashionAccessoriesTitle} (unlocked)");
        ImGui.SameLine();
        ImGuiUtil.HelpMarker("""
        Click on icon to execute
        Click on command to copy
        """);

        ImGui.Spacing();

        if (ImGui.InputTextWithHint("##FashionAccessoriesSearchInput", Language.SearchInputLabel, ref _searchString, 255, ImGuiInputTextFlags.AutoSelectAll))
        {
            Search();
        }

        ImGui.SameLine();
        var rainCheck = ActionHelper.GetExecutableActionById(30869); // Rain Check
        var rainCheckIcon = DalamudApi.TextureProvider.GetFromGameIcon(rainCheck.IconId).GetWrapOrEmpty().Handle;

        var umbrellaDance = ActionHelper.GetExecutableActionById(30868); // Umbrella Dance
        var umbrellaDanceIcon = DalamudApi.TextureProvider.GetFromGameIcon(umbrellaDance.IconId).GetWrapOrEmpty().Handle;

        var changePose = EmoteHelper.GetExecutableActionById(90); // Change Pose
        var changePoseIcon = DalamudApi.TextureProvider.GetFromGameIcon(changePose.IconId).GetWrapOrEmpty().Handle;

        var putAway = GeneralActionHelper.GetExecutableActionById(28); // Put Away
        var putAwayIcon = DalamudApi.TextureProvider.GetFromGameIcon(putAway.IconId).GetWrapOrEmpty().Handle;

        var iconSize = ImGuiHelpers.ScaledVector2(30, 30);

        ImGui.Image(rainCheckIcon, iconSize);
        if (ImGui.IsItemClicked())
        {
            Plugin.IpcProvider.ExecuteActionCommand(rainCheck.ActionId);
        }
        ImGuiUtil.ToolTip(Language.ClickToExecute);

        ImGui.SameLine();
        ImGui.Image(umbrellaDanceIcon, iconSize);
        if (ImGui.IsItemClicked())
        {
            Plugin.IpcProvider.ExecuteActionCommand(umbrellaDance.ActionId);
        }
        ImGuiUtil.ToolTip(Language.ClickToExecute);

        ImGui.SameLine();
        ImGui.Image(changePoseIcon, iconSize);
        if (ImGui.IsItemClicked())
        {
            Plugin.IpcProvider.ExecuteTextCommand(changePose.TextCommand);
        }
        ImGuiUtil.ToolTip(Language.ClickToExecute);

        ImGui.SameLine();
        ImGui.Image(putAwayIcon, iconSize);
        if (ImGui.IsItemClicked())
        {
            Plugin.IpcProvider.ExecuteTextCommand(putAway.TextCommand);
        }
        ImGuiUtil.ToolTip(Language.ClickToExecute);

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();
    }
}
