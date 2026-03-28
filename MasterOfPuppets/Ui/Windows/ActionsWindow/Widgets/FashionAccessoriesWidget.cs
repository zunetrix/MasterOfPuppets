using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.ImGuiNotification;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;

using MasterOfPuppets.Extensions.Dalamud;
using MasterOfPuppets.Resources;
using MasterOfPuppets.Util.ImGuiExt;

namespace MasterOfPuppets.Actions;

public class FashionAccessoriesWidget : Widget {
    public override string Title => "Fashion Accessories";
    public override FontAwesomeIcon Icon => FontAwesomeIcon.Umbrella;

    private readonly List<ExecutableAction> UnlockedActions = new();
    private string _searchString = string.Empty;
    private readonly List<int> ListSearchedIndexes = new();
    private bool _filterCommonItems = false;

    public FashionAccessoriesWidget(WidgetContext ctx) : base(ctx) {
    }

    public override void OnShow() {
        ReloadData();
        base.OnShow();
    }

    public override void OnHide() {
        ListSearchedIndexes.Clear();
        _searchString = string.Empty;
        base.OnHide();
    }

    public override void Draw() {
        using (ImRaii.Group()) {
            DrawHeader();
        }

        ImGui.BeginChild("##FashionListScrollableContent", new Vector2(-1, 0), false, ImGuiWindowFlags.NoScrollbar);
        DrawFashionGird();
        ImGui.EndChild();
    }

    private void ReloadData() {
        UnlockedActions.Clear();
        UnlockedActions.AddRange(FashionAccessoriesHelper.GetAllowedItems());
    }

    private void Search() {
        ListSearchedIndexes.Clear();
        ListSearchedIndexes.AddRange(
            UnlockedActions
            .Select((item, index) => new { item, index })
            .Where(x => {
                if (!string.IsNullOrEmpty(_searchString) &&
                    !x.item.ActionName.Contains(_searchString, StringComparison.OrdinalIgnoreCase))
                    return false;

                if (_filterCommonItems) {
                    var common = Context.Plugin.IpcProvider.CommonFashionAccessories;

                    if (common.Count > 0 && !common.Contains(x.item.ActionId))
                        return false;
                }

                return true;
            })
            .Select(x => x.index)
        );
    }

    private void DrawHeader() {
        ImGui.Text($"{Language.FashionAccessoriesTitle} (unlocked)");
        ImGui.SameLine();
        ImGuiUtil.HelpMarker("""
        Click on icon to execute (broadcast)
        Right click to copy command
        """);

        ImGui.Spacing();
        if (ImGuiUtil.IconButton(FontAwesomeIcon.Sync, $"##FashionAccessoriesReloadDataBtn", "Reload")) {
            ReloadData();
        }
        ImGui.SameLine();
        if (ImGuiUtil.IconButton(FontAwesomeIcon.UserFriends, "##FashionAccessoriesSyncPeersBtn", "Request unlocked fashion accessories from all peers")) {
            Context.Plugin.IpcProvider.RequestUnlockedState();
        }
        ImGui.SameLine();
        var filterCommon = _filterCommonItems;
        using (ImRaii.PushColor(ImGuiCol.Button, Style.Components.ButtonBlueNormal, filterCommon)
            .Push(ImGuiCol.ButtonHovered, Style.Components.ButtonBlueHovered, filterCommon)
            .Push(ImGuiCol.ButtonActive, Style.Components.ButtonBlueActive, filterCommon)) {
            if (ImGuiUtil.IconButton(FontAwesomeIcon.Filter, "##FilterCommonFashionBtn", "Show only fashion accessories all peers have in common")) {
                _filterCommonItems = !filterCommon;
                Search();
            }
        }
        ImGui.SameLine();
        if (ImGui.InputTextWithHint("##FashionAccessoriesSearchInput", Language.SearchInputLabel, ref _searchString, 255, ImGuiInputTextFlags.AutoSelectAll)) {
            Search();
        }

        ImGui.SameLine();
        var rainCheck = ActionHelper.GetExecutableAction(30869);
        var umbrellaDance = ActionHelper.GetExecutableAction(30868);
        var changePose = EmoteHelper.GetExecutableAction(90);
        var putUmbrellaAway = GeneralActionHelper.GetExecutableAction(28);
        var iconSize = ImGuiHelpers.ScaledVector2(30, 30);

        DalamudApi.TextureProvider.DrawIcon(rainCheck.IconId, iconSize);
        if (ImGui.IsItemClicked()) {
            Context.Plugin.IpcProvider.ExecuteActionCommand(rainCheck.ActionId);
        }
        ImGuiUtil.ToolTip(Language.ClickToExecute);

        ImGui.SameLine();
        DalamudApi.TextureProvider.DrawIcon(umbrellaDance.IconId, iconSize);
        if (ImGui.IsItemClicked()) {
            Context.Plugin.IpcProvider.ExecuteActionCommand(umbrellaDance.ActionId);
        }
        ImGuiUtil.ToolTip(Language.ClickToExecute);

        ImGui.SameLine();
        DalamudApi.TextureProvider.DrawIcon(changePose.IconId, iconSize);
        if (ImGui.IsItemClicked()) {
            Context.Plugin.IpcProvider.ExecuteTextCommand(changePose.TextCommand);
        }
        ImGuiUtil.ToolTip(Language.ClickToExecute);

        ImGui.SameLine();
        DalamudApi.TextureProvider.DrawIcon(putUmbrellaAway.IconId, iconSize);
        if (ImGui.IsItemClicked()) {
            Context.Plugin.IpcProvider.ExecuteTextCommand(putUmbrellaAway.TextCommand);
        }
        ImGuiUtil.ToolTip(Language.ClickToExecute);

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();
    }

    public void DrawFashionGird() {
        float iconSize = Context.Plugin.Config.ActionIconSize * ImGuiHelpers.GlobalScale;

        if (ImGui.BeginTable("##FashionTable", 1, ImGuiTableFlags.Resizable)) {
            ImGui.TableSetupColumn("Icons", ImGuiTableColumnFlags.WidthStretch);

            ImGui.TableNextRow();
            ImGui.TableNextColumn();

            ImGui.TableNextColumn();
            using (var child = ImRaii.Child("Search##FashionIconList")) {
                if (child) {
                    var columns = (int)((ImGui.GetContentRegionAvail().X - ImGui.GetStyle().WindowPadding.X) / (iconSize + ImGui.GetStyle().ItemSpacing.X));
                    DrawIconGrid(iconSize, columns);
                }
            }

            ImGui.EndTable();
        }
    }

    private void DrawIconGrid(float iconSize, int columns) {
        var lineHeight = iconSize + ImGui.GetStyle().ItemSpacing.Y;

        if (ListSearchedIndexes.Count == 0 && UnlockedActions.Count > 0)
            Search();

        var itemsToDraw = ListSearchedIndexes
            .Where(i => i >= 0 && i < UnlockedActions.Count)
            .Select(i => UnlockedActions[i])
            .ToList();

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
                Context.Plugin.IpcProvider.ExecuteTextCommand(fashionAccessorie.TextCommand);
            }
        }, columns, lineHeight);
    }
}
