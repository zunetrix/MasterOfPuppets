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

public class MinionsWidget : Widget {
    public override string Title => "Minions";
    public override FontAwesomeIcon Icon => FontAwesomeIcon.Cat;

    private readonly List<ExecutableAction> UnlockedActions = new();
    private string _searchString = string.Empty;
    private readonly List<int> ListSearchedIndexes = new();
    private bool _filterCommonItems = false;

    public MinionsWidget(WidgetContext ctx) : base(ctx) {
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

        ImGui.BeginChild("##MinionListScrollableContent", new Vector2(-1, 0), false, ImGuiWindowFlags.NoScrollbar);
        DrawMinionGird();
        ImGui.EndChild();
    }

    private void ReloadData() {
        UnlockedActions.Clear();
        UnlockedActions.AddRange(MinionHelper.GetAllowedItems());
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
                    var common = Context.Plugin.IpcProvider.CommonMinions;

                    if (common.Count > 0 && !common.Contains(x.item.ActionId))
                        return false;
                }

                return true;
            })
            .Select(x => x.index)
        );
    }

    private void DrawHeader() {
        ImGui.Text($"{Language.MinionTitle} (unlocked)");
        ImGui.SameLine();
        ImGuiUtil.HelpMarker("""
        Click on icon to execute (broadcast)
        Right click to copy command
        """);

        ImGui.Spacing();
        if (ImGuiUtil.IconButton(FontAwesomeIcon.Sync, $"##MinionReloadDataBtn", "Reload")) {
            ReloadData();
        }

        ImGui.SameLine();
        if (ImGuiUtil.IconButtonStyled(
            FontAwesomeIcon.SatelliteDish,
            Context.Plugin.IpcProvider.CommonMinions.Count > 0 ? ImGuiUtil.IconButtonStyle.Success : ImGuiUtil.IconButtonStyle.Danger,
            "##MinionSyncPeersBtn",
            "Request peers unlocked data (emotes, facewear, fashion, items, minions, mounts)")) {
            Context.Plugin.IpcProvider.RequestUnlockedState();
        }

        ImGui.SameLine();
        DrawFilterButton();

        ImGui.SameLine();
        ImGui.SetNextItemWidth(400 * ImGuiHelpers.GlobalScale);
        if (ImGui.InputTextWithHint("##MinionSearchInput", Language.SearchInputLabel, ref _searchString, 255, ImGuiInputTextFlags.AutoSelectAll)) {
            Search();
        }

        ImGui.SameLine();
        ImGui.Text("Size:");
        ImGui.SameLine();
        int minionIconSize = (int)Context.Plugin.Config.ActionIconSize;
        ImGui.SetNextItemWidth(70 * ImGuiHelpers.GlobalScale);
        if (ImGui.DragInt("##MinionIconSize", ref minionIconSize, 1, 20, 150)) {
            Context.Plugin.Config.ActionIconSize = Math.Clamp(minionIconSize, 20, 150);
            Context.Plugin.IpcProvider.SyncConfiguration();
        }
        if (ImGui.IsItemClicked(ImGuiMouseButton.Right)) {
            Context.Plugin.Config.ActionIconSize = 48;
            Context.Plugin.IpcProvider.SyncConfiguration();
        }
        ImGuiUtil.ToolTip("Icon size (drag or double-click to type)");

        ImGui.SameLine();
        if (ImGui.Button(Language.DismissBtn)) {
            Context.Plugin.IpcProvider.ExecuteTextCommand("/minion");
        }

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();
    }

    private void DrawFilterButton() {
        ImGui.SameLine();
        if (ImGuiUtil.IconButton(FontAwesomeIcon.Filter, "##btnShowMinionsFilter", "Filter")) {
            ImGui.OpenPopup("MinionsFilterPopup");
        }

        using var borderColor = ImRaii.PushColor(ImGuiCol.Border, Style.Components.TooltipBorderColor);
        using var popupBorder = ImRaii.PushStyle(ImGuiStyleVar.PopupBorderSize, 1);
        using var popUp = ImRaii.Popup("MinionsFilterPopup");
        if (!popUp) return;

        ImGui.Text("Filter Minions");
        ImGui.Separator();
        if (ImGui.Checkbox("Show only minions all peers have in common", ref _filterCommonItems)) {
            Search();
        }
    }

    public void DrawMinionGird() {
        float iconSize = Context.Plugin.Config.ActionIconSize * ImGuiHelpers.GlobalScale;

        if (ImGui.BeginTable("##MinionTable", 1, ImGuiTableFlags.Resizable)) {
            ImGui.TableSetupColumn("Icons", ImGuiTableColumnFlags.WidthStretch);

            ImGui.TableNextRow();
            ImGui.TableNextColumn();

            ImGui.TableNextColumn();
            using (var child = ImRaii.Child("Search##MinionIconList")) {
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

        ImGuiClip.ClippedDraw(itemsToDraw, (ExecutableAction minion) => {
            DalamudApi.TextureProvider.DrawIcon(minion.IconId, new Vector2(iconSize));
            ImGuiUtil.ToolTip($"""
            {minion.ActionName} ({minion.ActionId})
            Icon: {minion.IconId}

            Command:
            {minion.TextCommand}
            """);
            if (ImGui.IsItemClicked(ImGuiMouseButton.Right)) {
                ImGui.SetClipboardText(minion.TextCommand);
                DalamudApi.ShowNotification(Language.ClipboardCopyMessage, NotificationType.Info, 5000);
            } else if (ImGui.IsItemClicked(ImGuiMouseButton.Left)) {
                Context.Plugin.IpcProvider.ExecuteTextCommand(minion.TextCommand);
            }
        }, columns, lineHeight);
    }
}
