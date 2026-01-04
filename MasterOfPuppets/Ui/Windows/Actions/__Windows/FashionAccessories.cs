using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

using Dalamud.Bindings.ImGui;
using Dalamud.Interface.ImGuiNotification;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;

using MasterOfPuppets.Extensions.Dalamud;
using MasterOfPuppets.Resources;
using MasterOfPuppets.Util.ImGuiExt;

namespace MasterOfPuppets;

public class FashionAccessoriesWindow : Window {
    private Plugin Plugin { get; }

    private readonly List<ExecutableAction> UnlockedActions = new();
    private string _searchString = string.Empty;
    private readonly List<int> ListSearchedIndexes = new();

    public FashionAccessoriesWindow(Plugin plugin) : base($"{Language.FashionAccessoriesTitle}###FashionAccessoriesWindow") {
        Plugin = plugin;

        Size = ImGuiHelpers.ScaledVector2(500, 450);
        SizeCondition = ImGuiCond.FirstUseEver;
        // SizeCondition = ImGuiCond.Always;
        // Flags = ImGuiWindowFlags.NoResize;
    }

    public override void OnOpen() {
        UnlockedActions.Clear();
        UnlockedActions.AddRange(FashionAccessoriesHelper.GetAllowedItems());
        base.OnOpen();
    }

    public override void OnClose() {
        ListSearchedIndexes.Clear();
        _searchString = string.Empty;
        base.OnClose();
    }

    public override void Draw() {
        ImGui.BeginGroup();
        DrawHeader();
        ImGui.EndGroup();

        ImGui.BeginChild("##FashionListScrollableContent", new Vector2(-1, 0), false, ImGuiWindowFlags.NoScrollbar);
        DrawFashionGird();
        // DrawFashionTable();
        ImGui.EndChild();
    }

    private void Search() {
        ListSearchedIndexes.Clear();
        ListSearchedIndexes.AddRange(
            UnlockedActions
            .Select((item, index) => new { item, index })
            .Where(x => x.item.ActionName.Contains(_searchString, StringComparison.OrdinalIgnoreCase))
            .Select(x => x.index)
            .ToList()
        );
    }

    //table layout
    // private void DrawFashionEntry(int actionIndex, ExecutableAction fashionAccessorie) {
    //     ImGui.PushID(actionIndex);
    //     ImGui.TableNextRow();
    //     ImGui.TableNextColumn();
    //     ImGui.Text($"{actionIndex + 1:000}");

    //     ImGui.TableNextColumn();
    //     DalamudApi.TextureProvider.DrawIcon(fashionAccessorie.IconId, ImGuiHelpers.ScaledVector2(48, 48));
    //     if (ImGui.IsItemClicked()) {
    //         Plugin.IpcProvider.ExecuteTextCommand(fashionAccessorie.TextCommand);
    //     }
    //     ImGuiUtil.ToolTip(Language.ClickToExecute);

    //     ImGui.TableNextColumn();
    //     ImGui.Text($"{fashionAccessorie.ActionName}");
    //     if (ImGui.IsItemClicked()) {
    //         ImGui.SetClipboardText($"{fashionAccessorie.ActionName}");
    //         DalamudApi.ShowNotification(Language.ClipboardCopyMessage, NotificationType.Info, 5000);
    //     }
    //     ImGuiUtil.ToolTip(Language.ClickToCopy);

    //     ImGui.TableNextColumn();
    //     ImGui.Text(fashionAccessorie.TextCommand);
    //     if (ImGui.IsItemClicked()) {
    //         ImGui.SetClipboardText(fashionAccessorie.TextCommand);
    //         DalamudApi.ShowNotification(Language.ClipboardCopyMessage, NotificationType.Info, 5000);
    //     }
    //     ImGuiUtil.ToolTip(Language.ClickToCopy);

    //     ImGui.PopID();
    // }

    // private void DrawFashionTable() {
    //     var tableFlags = ImGuiTableFlags.RowBg | ImGuiTableFlags.PadOuterX |
    //            ImGuiTableFlags.NoSavedSettings | ImGuiTableFlags.BordersInnerV;
    //     var tableColumnCount = 4;

    //     var isFiltered = !string.IsNullOrEmpty(_searchString);
    //     var itemCount = isFiltered ? ListSearchedIndexes.Count : UnlockedActions.Count;

    //     if (ImGui.BeginTable("##FashionAccessoriesTable", tableColumnCount, tableFlags)) {
    //         ImGui.TableSetupColumn("  ", ImGuiTableColumnFlags.WidthFixed);
    //         ImGui.TableSetupColumn("Icon", ImGuiTableColumnFlags.WidthFixed);
    //         ImGui.TableSetupColumn("Name", ImGuiTableColumnFlags.WidthStretch, 1.0f);
    //         ImGui.TableSetupColumn("Text Commands", ImGuiTableColumnFlags.WidthStretch);

    //         ImGuiListClipperPtr clipper;
    //         unsafe {
    //             clipper = new ImGuiListClipperPtr(ImGuiNative.ImGuiListClipper());
    //         }

    //         clipper.Begin(itemCount);

    //         while (clipper.Step()) {
    //             for (int i = clipper.DisplayStart; i < clipper.DisplayEnd; i++) {
    //                 if (i >= itemCount) break;
    //                 int realIndex = isFiltered ? ListSearchedIndexes[i] : i;
    //                 if (realIndex >= UnlockedActions.Count) continue;

    //                 DrawFashionEntry(realIndex, UnlockedActions[realIndex]);
    //             }
    //         }

    //         clipper.End();
    //         ImGui.EndTable();
    //     }
    // }

    private void DrawHeader() {
        ImGui.Text($"{Language.FashionAccessoriesTitle} (unlocked)");
        ImGui.SameLine();
        ImGuiUtil.HelpMarker("""
        Click on icon to execute (broadcast)
        Right click to copy command
        """);

        ImGui.Spacing();

        if (ImGui.InputTextWithHint("##FashionAccessoriesSearchInput", Language.SearchInputLabel, ref _searchString, 255, ImGuiInputTextFlags.AutoSelectAll)) {
            Search();
        }

        ImGui.SameLine();
        var rainCheck = ActionHelper.GetExecutableAction(30869); // Rain Check
        var umbrellaDance = ActionHelper.GetExecutableAction(30868); // Umbrella Dance
        var changePose = EmoteHelper.GetExecutableAction(90); // Change Pose
        var putAway = GeneralActionHelper.GetExecutableAction(28); // Put Away
        var iconSize = ImGuiHelpers.ScaledVector2(30, 30);

        DalamudApi.TextureProvider.DrawIcon(rainCheck.IconId, iconSize);
        if (ImGui.IsItemClicked()) {
            Plugin.IpcProvider.ExecuteActionCommand(rainCheck.ActionId);
        }
        ImGuiUtil.ToolTip(Language.ClickToExecute);

        ImGui.SameLine();
        DalamudApi.TextureProvider.DrawIcon(umbrellaDance.IconId, iconSize);
        if (ImGui.IsItemClicked()) {
            Plugin.IpcProvider.ExecuteActionCommand(umbrellaDance.ActionId);
        }
        ImGuiUtil.ToolTip(Language.ClickToExecute);

        ImGui.SameLine();
        DalamudApi.TextureProvider.DrawIcon(changePose.IconId, iconSize);
        if (ImGui.IsItemClicked()) {
            Plugin.IpcProvider.ExecuteTextCommand(changePose.TextCommand);
        }
        ImGuiUtil.ToolTip(Language.ClickToExecute);

        ImGui.SameLine();
        DalamudApi.TextureProvider.DrawIcon(putAway.IconId, iconSize);
        if (ImGui.IsItemClicked()) {
            Plugin.IpcProvider.ExecuteTextCommand(putAway.TextCommand);
        }
        ImGuiUtil.ToolTip(Language.ClickToExecute);

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();
    }

    public void DrawFashionGird() {
        float iconSize = 48 * ImGuiHelpers.GlobalScale;

        if (ImGui.BeginTable("##FashionTable", 1, ImGuiTableFlags.Resizable)) {
            ImGui.TableSetupColumn("Icons", ImGuiTableColumnFlags.WidthStretch);

            ImGui.TableNextRow();
            ImGui.TableNextColumn();

            ImGui.TableNextColumn();
            using (ImRaii.Child("Search##FashionIconList")) {
                var columns = (int)((ImGui.GetContentRegionAvail().X - ImGui.GetStyle().WindowPadding.X) / (iconSize + ImGui.GetStyle().ItemSpacing.X));
                DrawIconGrid(iconSize, columns);
            }

            ImGui.EndTable();
        }
    }

    private void DrawIconGrid(float iconSize, int columns) {
        var lineHeight = iconSize + ImGui.GetStyle().ItemSpacing.Y;

        List<ExecutableAction> itemsToDraw;
        if (string.IsNullOrEmpty(_searchString)) {
            itemsToDraw = UnlockedActions;
        } else {
            itemsToDraw = ListSearchedIndexes
                .Where(i => i >= 0 && i < UnlockedActions.Count)
                .Select(i => UnlockedActions[i])
                .ToList();
        }

        ImGuiClip.ClippedDraw(itemsToDraw, (ExecutableAction fashionAccessorie) => {
            DalamudApi.TextureProvider.DrawIcon(fashionAccessorie.IconId, new Vector2(iconSize));
            ImGuiUtil.ToolTip($"""
            {fashionAccessorie.ActionName} ({fashionAccessorie.ActionId})
            Icon: {fashionAccessorie.IconId}

            Command:
            {fashionAccessorie.TextCommand}
            """);
            if (ImGui.IsItemClicked(ImGuiMouseButton.Right)) {
                ImGui.SetClipboardText(fashionAccessorie.TextCommand);
                DalamudApi.ShowNotification(Language.ClipboardCopyMessage, NotificationType.Info, 5000);
            } else if (ImGui.IsItemClicked(ImGuiMouseButton.Left)) {
                Plugin.IpcProvider.ExecuteTextCommand(fashionAccessorie.TextCommand);
            }
        }, columns, lineHeight);
    }
}
