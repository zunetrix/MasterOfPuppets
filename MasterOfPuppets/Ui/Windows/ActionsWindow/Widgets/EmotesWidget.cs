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

public sealed class EmotesWidget : Widget {
    public override string Title => "Emotes";
    public override FontAwesomeIcon Icon => FontAwesomeIcon.SmileWink;

    private readonly List<ExecutableAction> UnlockedActions = new();
    private string _searchString = string.Empty;
    private readonly List<int> ListSearchedIndexes = new();
    private bool _showGeneralEmotes = true;
    private bool _showExpressionsEmotes = true;
    private bool _showInternalEmotes = true;
    private bool _filterCommonItems = false;

    public EmotesWidget(WidgetContext ctx) : base(ctx) {
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

        ImGui.BeginChild("##EmotesListScrollableContent", new Vector2(-1, 0), false, ImGuiWindowFlags.NoScrollbar);
        DrawEmoteGird();
        ImGui.EndChild();
    }

    private void ReloadData() {
        UnlockedActions.Clear();
        UnlockedActions.AddRange(EmoteHelper.GetAllowedItems());
    }

    private void Search() {
        ListSearchedIndexes.Clear();

        ListSearchedIndexes.AddRange(
            UnlockedActions
                .Select((item, index) => new { item, index })
                .Where(x => {
                    var cat = x.item.Category;

                    bool isExpressions = string.Equals(cat, "Expressions", StringComparison.OrdinalIgnoreCase);
                    bool isInternalEmote = string.Equals(cat, "InternalEmote", StringComparison.OrdinalIgnoreCase);
                    bool isGeneral = !isExpressions && !isInternalEmote;

                    bool categoryAllowed =
                        (isGeneral && _showGeneralEmotes) ||
                        (isExpressions && _showExpressionsEmotes) ||
                        (isInternalEmote && _showInternalEmotes);

                    // search only on enabled categories
                    if (!categoryAllowed)
                        return false;

                    // Filter to emotes all peers have in common (using new global unlocked state).
                    // Internal emotes are hardcoded and always available on every client - skip them.
                    if (_filterCommonItems && !isInternalEmote) {
                        var common = Context.Plugin.IpcProvider.CommonEmotes;
                        if (common.Count > 0 && !common.Contains(x.item.ActionId))
                            return false;
                    }

                    if (!string.IsNullOrEmpty(_searchString)) {
                        return x.item.ActionName.Contains(_searchString, StringComparison.OrdinalIgnoreCase);
                    }
                    return true;
                })
                .Select(x => x.index)
        );
    }

    private void DrawHeader() {
        ImGui.Text($"{Language.EmotesTitle} (unlocked)");
        ImGui.SameLine();
        ImGuiUtil.HelpMarker("""
        Click on icon to execute (broadcast)
        Right click to copy command
        """);

        ImGui.Spacing();
        if (ImGuiUtil.IconButton(FontAwesomeIcon.Sync, $"##EmotesReloadDataBtn", "Reload")) {
            ReloadData();
        }

        ImGui.SameLine();
        if (ImGuiUtil.IconButtonStyled(
            FontAwesomeIcon.SatelliteDish,
            Context.Plugin.IpcProvider.CommonEmotes.Count > 0 ? ImGuiUtil.IconButtonStyle.Success : ImGuiUtil.IconButtonStyle.Danger,
            "##EmotesSyncPeersBtn",
            "Request peers unlocked data (emotes, facewear, fashion, items, minions, mounts)")) {
            Context.Plugin.IpcProvider.RequestUnlockedState();
        }

        ImGui.SameLine();
        DrawFilterButton();

        ImGui.SameLine();
        ImGui.SetNextItemWidth(400 * ImGuiHelpers.GlobalScale);
        if (ImGui.InputTextWithHint("##EmoteSearchInput", Language.SearchInputLabel, ref _searchString, 255, ImGuiInputTextFlags.AutoSelectAll)) {
            Search();
        }

        ImGui.SameLine();
        ImGui.Text("Size:");
        ImGui.SameLine();
        int emoteIconSize = (int)Context.Plugin.Config.ActionIconSize;
        ImGui.SetNextItemWidth(70 * ImGuiHelpers.GlobalScale);
        if (ImGui.DragInt("##EmoteIconSize", ref emoteIconSize, 1, 20, 150)) {
            Context.Plugin.Config.ActionIconSize = Math.Clamp(emoteIconSize, 20, 150);
            Context.Plugin.IpcProvider.SyncConfiguration();
        }
        if (ImGui.IsItemClicked(ImGuiMouseButton.Right)) {
            Context.Plugin.Config.ActionIconSize = 48;
            Context.Plugin.IpcProvider.SyncConfiguration();
        }
        ImGuiUtil.ToolTip("Icon size (drag or double-click to type)");

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();
    }

    private void DrawFilterButton() {
        ImGui.SameLine();
        if (ImGuiUtil.IconButton(FontAwesomeIcon.Filter, "##btnShowEmotesFilter", "Filter")) {
            ImGui.OpenPopup("EmotesFilterPopup");
        }

        using var borderColor = ImRaii.PushColor(ImGuiCol.Border, Style.Components.TooltipBorderColor);
        using var popupBorder = ImRaii.PushStyle(ImGuiStyleVar.PopupBorderSize, 1);
        using var popUp = ImRaii.Popup("EmotesFilterPopup");
        if (!popUp) return;

        ImGui.Text("Filter Emotes");
        ImGui.Separator();

        if (ImGui.Checkbox("Show General Emotes", ref _showGeneralEmotes)) {
            Search();
        }
        if (ImGui.Checkbox("Show Expressions Emotes", ref _showExpressionsEmotes)) {
            Search();
        }
        if (ImGui.Checkbox("Show Internal Emotes (sleep/sit anywhere etc)", ref _showInternalEmotes)) {
            Search();
        }

        ImGui.Separator();
        if (ImGui.Checkbox("Show only emotes all peers have in common", ref _filterCommonItems)) {
            Search();
        }
    }

    public void DrawEmoteGird() {
        float iconSize = Context.Plugin.Config.ActionIconSize * ImGuiHelpers.GlobalScale;

        if (ImGui.BeginTable("##EmoteTable", 1, ImGuiTableFlags.Resizable)) {
            ImGui.TableSetupColumn("Icons", ImGuiTableColumnFlags.WidthStretch);

            ImGui.TableNextRow();
            ImGui.TableNextColumn();

            ImGui.TableNextColumn();
            using (var child = ImRaii.Child("Search##EmoteIconList")) {
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

        if (ListSearchedIndexes.Count == 0)
            Search();

        var itemsToDraw = ListSearchedIndexes
        .Select(i => UnlockedActions[i])
        .ToList();

        ImGuiClip.ClippedDraw(itemsToDraw, (ExecutableAction emote) => {
            var tintColor = emote.Category == "InternalEmote" ? Style.Colors.Cyan : Vector4.One;
            DalamudApi.TextureProvider.DrawIcon(emote.IconId, new Vector2(iconSize), tintCol: tintColor);
            ImGuiUtil.ToolTip($"""
            {emote.ActionName} ({emote.ActionId})
            Icon: {emote.IconId}

            Command:
            {emote.TextCommand}
            """);
            if (ImGui.IsItemClicked(ImGuiMouseButton.Right)) {
                ImGui.SetClipboardText(emote.TextCommand);
                DalamudApi.ShowNotification(Language.ClipboardCopyMessage, NotificationType.Info, 5000);
            } else if (ImGui.IsItemClicked(ImGuiMouseButton.Left)) {
                Context.Plugin.IpcProvider.ExecuteTextCommand(emote.TextCommand);
            }
        }, columns, lineHeight);
    }
}
