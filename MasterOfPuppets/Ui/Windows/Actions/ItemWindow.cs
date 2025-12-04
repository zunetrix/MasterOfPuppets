using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

using Dalamud.Bindings.ImGui;
using Dalamud.Interface.ImGuiNotification;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;

using MasterOfPuppets.Resources;
using MasterOfPuppets.Util.ImGuiExt;

namespace MasterOfPuppets;

public class ItemWindow : Window {
    private Plugin Plugin { get; }

    private readonly List<ExecutableAction> UnlockedActions = new();
    private string _searchString = string.Empty;
    private readonly List<int> ListSearchedIndexes = new();

    public ItemWindow(Plugin plugin) : base($"{Language.ItemTitle}###ItemWindow") {
        Plugin = plugin;

        Size = ImGuiHelpers.ScaledVector2(500, 450);
        SizeCondition = ImGuiCond.FirstUseEver;
        // SizeCondition = ImGuiCond.Always;
        // Flags = ImGuiWindowFlags.NoResize;
    }

    public override void OnOpen() {
        UnlockedActions.Clear();
        UnlockedActions.AddRange(ItemHelper.GetAllowedItems());
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

        ImGui.BeginChild("##ItemListScrollableContent", new Vector2(-1, 0), false, ImGuiWindowFlags.NoScrollbar);
        DrawItemGird();
        // DrawItemTable();
        ImGui.EndChild();
    }

    //table layout
    // private void DrawItemEntry(int actionIndex, ExecutableAction item) {
    //     ImGui.PushID(actionIndex);
    //     ImGui.TableNextRow();
    //     ImGui.TableNextColumn();
    //     ImGui.TextUnformatted($"{actionIndex + 1:000}");

    //     ImGui.TableNextColumn();
    //     var icon = DalamudApi.TextureProvider.GetFromGameIcon(item.IconId).GetWrapOrEmpty().Handle;
    //     var iconSize = ImGuiHelpers.ScaledVector2(48, 48);

    //     ImGui.Image(icon, iconSize);
    //     if (ImGui.IsItemClicked()) {
    //         Plugin.IpcProvider.ExecuteItemCommand(item.ActionId);
    //     }
    //     ImGuiUtil.ToolTip(Language.ClickToExecute);

    //     ImGui.TableNextColumn();
    //     ImGui.TextUnformatted($"{item.ActionName}");
    //     // ImGui.TextUnformatted($"{item.ActionName}\n({item.IconId}) {item.Category}");
    //     if (ImGui.IsItemClicked()) {
    //         ImGui.SetClipboardText($"{item.ActionName}");
    //         DalamudApi.ShowNotification(Language.ClipboardCopyMessage, NotificationType.Info, 5000);
    //     }
    //     ImGuiUtil.ToolTip(Language.ClickToCopy);

    //     ImGui.TableNextColumn();
    //     ImGui.TextUnformatted(item.TextCommand);
    //     if (ImGui.IsItemClicked()) {
    //         ImGui.SetClipboardText(item.TextCommand);
    //         DalamudApi.ShowNotification(Language.ClipboardCopyMessage, NotificationType.Info, 5000);
    //     }
    //     ImGuiUtil.ToolTip(Language.ClickToCopy);

    //     ImGui.PopID();
    // }

    // private void DrawItemTable() {
    //     var tableFlags = ImGuiTableFlags.RowBg | ImGuiTableFlags.PadOuterX |
    //            ImGuiTableFlags.NoSavedSettings | ImGuiTableFlags.BordersInnerV;
    //     var tableColumnCount = 4;

    //     var isFiltered = !string.IsNullOrEmpty(_searchString);
    //     var itemCount = isFiltered ? ListSearchedIndexes.Count : UnlockedActions.Count;

    //     if (ImGui.BeginTable("##ItemTable", tableColumnCount, tableFlags)) {
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

    //                 DrawItemEntry(realIndex, UnlockedActions[realIndex]);
    //             }
    //         }

    //         clipper.End();
    //         ImGui.EndTable();
    //     }
    // }

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

    private void DrawHeader() {
        ImGui.TextUnformatted($"{Language.ItemTitle} (unlocked)");
        ImGui.SameLine();
        ImGuiUtil.HelpMarker("""
        Click on icon to execute (broadcast)
        Right click to copy command
        """);

        ImGui.Spacing();

        if (ImGui.InputTextWithHint("##ItemSearchInput", Language.SearchInputLabel, ref _searchString, 255, ImGuiInputTextFlags.AutoSelectAll)) {
            Search();
        }

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();
    }

    public void DrawItemGird() {
        float iconSize = 48 * ImGuiHelpers.GlobalScale;

        if (ImGui.BeginTable("##ItemTable", 1, ImGuiTableFlags.Resizable)) {
            ImGui.TableSetupColumn("Icons", ImGuiTableColumnFlags.WidthStretch);

            ImGui.TableNextRow();
            ImGui.TableNextColumn();

            ImGui.TableNextColumn();
            using (ImRaii.Child("Search##ItemIconList")) {
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

        ImGuiClip.ClippedDraw(itemsToDraw, (ExecutableAction item) => {
            // var icon = DalamudApi.TextureProvider.GetFromGameIcon(item.IconId)!.GetWrapOrEmpty();
            var icon = DalamudApi.TextureProvider.GetFromGameIcon(item.IconId).GetWrapOrEmpty().Handle;
            ImGui.Image(
                icon.Handle,
                new Vector2(iconSize),
                new Vector2(0.0f, 0.0f),
                new Vector2(1.0f, 1.0f)
            );
            ImGuiUtil.ToolTip($"""
            {item.ActionName} ({item.ActionId})
            Icon: {item.IconId}

            Command:
            {item.TextCommand}
            """);
            if (ImGui.IsItemClicked(ImGuiMouseButton.Right)) {
                ImGui.SetClipboardText(item.TextCommand);
                DalamudApi.ShowNotification(Language.ClipboardCopyMessage, NotificationType.Info, 5000);
            } else if (ImGui.IsItemClicked(ImGuiMouseButton.Left)) {
                Plugin.IpcProvider.ExecuteTextCommand(item.TextCommand);
            }
        }, columns, lineHeight);
    }
}
