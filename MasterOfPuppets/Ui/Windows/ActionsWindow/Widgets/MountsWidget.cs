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

public class MountsWidget : Widget {
    public override string Title => "Mounts";
    public override FontAwesomeIcon Icon => FontAwesomeIcon.Horse;

    private readonly List<ExecutableAction> UnlockedActions = new();
    private string _searchString = string.Empty;
    private readonly List<int> ListSearchedIndexes = new();
    private bool _filterCommonItems = false;

    public MountsWidget(WidgetContext ctx) : base(ctx) {
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

        ImGui.BeginChild("##MountListScrollableContent", new Vector2(-1, 0), false, ImGuiWindowFlags.NoScrollbar);
        DrawMountGird();
        ImGui.EndChild();
    }

    private void ReloadData() {
        UnlockedActions.Clear();
        UnlockedActions.AddRange(MountHelper.GetAllowedItems());
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
                    var common = Context.Plugin.IpcProvider.CommonMounts;

                    if (common.Count > 0 && !common.Contains(x.item.ActionId))
                        return false;
                }

                return true;
            })
            .Select(x => x.index)
        );
    }

    private void DrawHeader() {
        ImGui.Text($"{Language.MountTitle} (unlocked)");
        ImGui.SameLine();
        ImGuiUtil.HelpMarker("""
        Click on icon to execute (broadcast)
        Right click to copy command
        """);

        ImGui.Spacing();
        if (ImGuiUtil.IconButton(FontAwesomeIcon.Sync, $"##MountReloadDataBtn", "Reload")) {
            ReloadData();
        }
        ImGui.SameLine();
        if (ImGuiUtil.IconButton(FontAwesomeIcon.UserFriends, "##MountSyncPeersBtn", "Request unlocked mounts from all peers")) {
            Context.Plugin.IpcProvider.RequestUnlockedState();
        }
        ImGui.SameLine();
        var filterCommon = _filterCommonItems;
        using (ImRaii.PushColor(ImGuiCol.Button, Style.Components.ButtonBlueNormal, filterCommon)
            .Push(ImGuiCol.ButtonHovered, Style.Components.ButtonBlueHovered, filterCommon)
            .Push(ImGuiCol.ButtonActive, Style.Components.ButtonBlueActive, filterCommon)) {
            if (ImGuiUtil.IconButton(FontAwesomeIcon.Filter, "##FilterCommonMountsBtn", "Show only mounts all peers have in common")) {
                _filterCommonItems = !filterCommon;
                Search();
            }
        }
        ImGui.SameLine();
        if (ImGui.InputTextWithHint("##MountSearchInput", Language.SearchInputLabel, ref _searchString, 255, ImGuiInputTextFlags.AutoSelectAll)) {
            Search();
        }

        ImGui.SameLine();
        var unmount = GeneralActionHelper.GetExecutableAction(23);
        DalamudApi.TextureProvider.DrawIcon(unmount.IconId, ImGuiHelpers.ScaledVector2(30, 30));
        if (ImGui.IsItemClicked()) {
            Context.Plugin.IpcProvider.ExecuteGeneralActionCommand(unmount.ActionId);
        }
        ImGuiUtil.ToolTip($"{Language.ClickToExecute} (unmount)");

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();
    }

    public void DrawMountGird() {
        float iconSize = Context.Plugin.Config.ActionIconSize * ImGuiHelpers.GlobalScale;

        if (ImGui.BeginTable("##MountTable", 1, ImGuiTableFlags.Resizable)) {
            ImGui.TableSetupColumn("Icons", ImGuiTableColumnFlags.WidthStretch);

            ImGui.TableNextRow();
            ImGui.TableNextColumn();

            ImGui.TableNextColumn();
            using (var child = ImRaii.Child("Search##MountIconList")) {
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

        ImGuiClip.ClippedDraw(itemsToDraw, (ExecutableAction mount) => {
            DalamudApi.TextureProvider.DrawIcon(mount.IconId, new Vector2(iconSize));
            ImGuiUtil.ToolTip($"""
            {mount.ActionName} ({mount.ActionId})
            Icon: {mount.IconId}

            Command:
            {mount.TextCommand}
            """);
            if (ImGui.IsItemClicked(ImGuiMouseButton.Right)) {
                ImGui.SetClipboardText(mount.TextCommand);
                DalamudApi.ShowNotification(Language.ClipboardCopyMessage, NotificationType.Info, 5000);
            } else if (ImGui.IsItemClicked(ImGuiMouseButton.Left)) {
                Context.Plugin.IpcProvider.ExecuteTextCommand(mount.TextCommand);
            }
        }, columns, lineHeight);
    }
}
