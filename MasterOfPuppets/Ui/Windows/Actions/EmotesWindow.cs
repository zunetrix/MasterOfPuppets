using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.ImGuiNotification;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;

using MasterOfPuppets.Resources;
using MasterOfPuppets.Util.ImGuiExt;

namespace MasterOfPuppets;

public class EmotesWindow : Window {
    private Plugin Plugin { get; }

    private readonly List<ExecutableAction> UnlockedActions = new();
    private string _searchString = string.Empty;
    private readonly List<int> ListSearchedIndexes = new();
    private bool _showGeneralEmotes = true;
    private bool _showExpressionsEmotes = true;
    private bool _showInternalEmotes = true;

    public EmotesWindow(Plugin plugin) : base($"{Language.EmotesTitle}###EmotesWindow") {
        Plugin = plugin;

        Size = ImGuiHelpers.ScaledVector2(500, 450);
        SizeCondition = ImGuiCond.FirstUseEver;
        // SizeCondition = ImGuiCond.Always;
        // Flags = ImGuiWindowFlags.NoResize;
    }

    public override void OnOpen() {
        UnlockedActions.Clear();
        UnlockedActions.AddRange(EmoteHelper.GetAllowedItems());
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

        ImGui.BeginChild("##EmotesListScrollableContent", new Vector2(-1, 0), false, ImGuiWindowFlags.NoScrollbar);
        // DrawEmoteTable();
        DrawEmoteGird();
        ImGui.EndChild();
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

                    if (!string.IsNullOrEmpty(_searchString)) {
                        return x.item.ActionName.Contains(_searchString, StringComparison.OrdinalIgnoreCase);
                    }
                    return true;
                })
                .Select(x => x.index)
        );
    }

    // table layout
    // private void DrawEmoteEntry(int actionIndex, ExecutableAction emote) {
    //     ImGui.PushID(actionIndex);
    //     ImGui.TableNextRow();
    //     // ImGui.TableSetColumnIndex(0);
    //     ImGui.TableNextColumn();
    //     ImGui.TextUnformatted($"{actionIndex + 1:000}");

    //     ImGui.TableNextColumn();
    //     var icon = DalamudApi.TextureProvider.GetFromGameIcon(emote.IconId).GetWrapOrEmpty().Handle;
    //     var iconSize = ImGuiHelpers.ScaledVector2(48, 48);

    //     ImGui.Image(icon, iconSize);
    //     if (ImGui.IsItemClicked()) {
    //         Plugin.IpcProvider.ExecuteTextCommand(emote.TextCommand);
    //     }
    //     ImGuiUtil.ToolTip(Language.ClickToExecute);

    //     ImGui.TableNextColumn();
    //     ImGui.TextUnformatted($"{emote.ActionName} ({emote.ActionId})");
    //     if (ImGui.IsItemClicked()) {
    //         ImGui.SetClipboardText($"{emote.ActionName}");
    //         DalamudApi.ShowNotification(Language.ClipboardCopyMessage, NotificationType.Info, 5000);
    //     }
    //     ImGuiUtil.ToolTip(Language.ClickToCopy);

    //     ImGui.TableNextColumn();
    //     ImGui.TextUnformatted(emote.TextCommand);
    //     if (ImGui.IsItemClicked()) {
    //         ImGui.SetClipboardText(emote.TextCommand);
    //         DalamudApi.ShowNotification(Language.ClipboardCopyMessage, NotificationType.Info, 5000);
    //     }
    //     ImGuiUtil.ToolTip(Language.ClickToCopy);

    //     // ImGui.TableNextColumn();
    //     // ImGui.TextUnformatted(emote.Category);

    //     ImGui.PopID();
    // }

    // private void DrawEmoteTable() {
    //     var tableFlags = ImGuiTableFlags.RowBg | ImGuiTableFlags.PadOuterX |
    //            ImGuiTableFlags.NoSavedSettings | ImGuiTableFlags.BordersInnerV;
    //     var tableColumnCount = 4;

    //     var isFiltered = !string.IsNullOrEmpty(_searchString);
    //     var itemCount = isFiltered ? ListSearchedIndexes.Count : UnlockedActions.Count;

    //     if (ImGui.BeginTable("##EmotesTable", tableColumnCount, tableFlags)) {
    //         ImGui.TableSetupColumn("  ", ImGuiTableColumnFlags.WidthFixed);
    //         ImGui.TableSetupColumn("Icon", ImGuiTableColumnFlags.WidthFixed);
    //         ImGui.TableSetupColumn("Name", ImGuiTableColumnFlags.WidthStretch, 1.0f);
    //         ImGui.TableSetupColumn("Text Commands", ImGuiTableColumnFlags.WidthStretch, 1.0f);
    //         // ImGui.TableSetupColumn("Category", ImGuiTableColumnFlags.WidthStretch);

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

    //                 DrawEmoteEntry(realIndex, UnlockedActions[realIndex]);
    //             }
    //         }

    //         clipper.End();
    //         ImGui.EndTable();
    //     }
    // }

    private void DrawHeader() {
        ImGui.TextUnformatted($"{Language.EmotesTitle} (unlocked)");
        ImGui.SameLine();
        ImGuiUtil.HelpMarker("""
        Click on icon to execute (broadcast)
        Right click to copy command
        """);

        ImGui.Spacing();

        if (ImGui.InputTextWithHint("##EmoteSearchInput", Language.SearchInputLabel, ref _searchString, 255, ImGuiInputTextFlags.AutoSelectAll)) {
            Search();
        }

        ImGui.SameLine();
        var showingGeneralEmotes = false;
        if (_showGeneralEmotes) {
            ImGui.PushStyleColor(ImGuiCol.Button, Style.Components.ButtonBlueNormal);
            ImGui.PushStyleColor(ImGuiCol.ButtonHovered, Style.Components.ButtonBlueHovered);
            ImGui.PushStyleColor(ImGuiCol.ButtonActive, Style.Components.ButtonBlueActive);
            showingGeneralEmotes = true;
        }
        if (ImGuiUtil.IconButton(FontAwesomeIcon.Smile, $"##ShowGeneralEmotesBtn", "Show General Emotes")) {
            _showGeneralEmotes = !_showGeneralEmotes;
            Search();
        }
        if (showingGeneralEmotes)
            ImGui.PopStyleColor(3);

        ImGui.SameLine();
        var showingExpressionsEmotes = false;
        if (_showExpressionsEmotes) {
            ImGui.PushStyleColor(ImGuiCol.Button, Style.Components.ButtonBlueNormal);
            ImGui.PushStyleColor(ImGuiCol.ButtonHovered, Style.Components.ButtonBlueHovered);
            ImGui.PushStyleColor(ImGuiCol.ButtonActive, Style.Components.ButtonBlueActive);
            showingExpressionsEmotes = true;
        }
        if (ImGuiUtil.IconButton(FontAwesomeIcon.SmileWink, $"##ShowExpressionsEmotesBtn", "Show Expressions Emotes")) {
            _showExpressionsEmotes = !_showExpressionsEmotes;
            Search();
        }
        if (showingExpressionsEmotes)
            ImGui.PopStyleColor(3);

        ImGui.SameLine();
        var showingInternalEmotes = false;
        if (_showInternalEmotes) {
            ImGui.PushStyleColor(ImGuiCol.Button, Style.Components.ButtonBlueNormal);
            ImGui.PushStyleColor(ImGuiCol.ButtonHovered, Style.Components.ButtonBlueHovered);
            ImGui.PushStyleColor(ImGuiCol.ButtonActive, Style.Components.ButtonBlueActive);
            showingInternalEmotes = true;
        }
        if (ImGuiUtil.IconButton(FontAwesomeIcon.Bed, $"##ShowInternalEmotesBtn", "Show Internal Emotes (sleep/sit anywhere etc)")) {
            _showInternalEmotes = !_showInternalEmotes;
            Search();
        }
        if (showingInternalEmotes)
            ImGui.PopStyleColor(3);

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();
    }

    public void DrawEmoteGird() {
        float iconSize = 48 * ImGuiHelpers.GlobalScale;

        if (ImGui.BeginTable("##EmoteTable", 1, ImGuiTableFlags.Resizable)) {
            ImGui.TableSetupColumn("Icons", ImGuiTableColumnFlags.WidthStretch);

            ImGui.TableNextRow();
            ImGui.TableNextColumn();

            ImGui.TableNextColumn();
            using (ImRaii.Child("Search##EmoteIconList")) {
                var columns = (int)((ImGui.GetContentRegionAvail().X - ImGui.GetStyle().WindowPadding.X) / (iconSize + ImGui.GetStyle().ItemSpacing.X));
                DrawIconGrid(iconSize, columns);
            }

            ImGui.EndTable();
        }
    }

    private void DrawIconGrid(float iconSize, int columns) {
        var lineHeight = iconSize + ImGui.GetStyle().ItemSpacing.Y;

        List<ExecutableAction> itemsToDraw;
        bool allFiltersEnabled = _showGeneralEmotes && _showExpressionsEmotes && _showInternalEmotes;
        if (ListSearchedIndexes.Count == 0 && allFiltersEnabled) {
            itemsToDraw = UnlockedActions;
        } else {
            itemsToDraw = ListSearchedIndexes
                .Where(i => i >= 0 && i < UnlockedActions.Count)
                .Select(i => UnlockedActions[i])
                .ToList();
        }

        ImGuiClip.ClippedDraw(itemsToDraw, (ExecutableAction emote) => {
            // var icon = DalamudApi.TextureProvider.GetFromGameIcon(emote.IconId).GetWrapOrEmpty();
            var icon = DalamudApi.TextureProvider.GetFromGameIcon(emote.IconId).GetWrapOrEmpty().Handle;
            var tintColor = emote.Category == "InternalEmote" ? Style.Colors.Cyan : Vector4.One;

            ImGui.Image(
                icon.Handle,
                new Vector2(iconSize),
                new Vector2(0.0f, 0.0f),
                new Vector2(1.0f, 1.0f),
                tintColor
            );
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
                Plugin.IpcProvider.ExecuteTextCommand(emote.TextCommand);
            }
        }, columns, lineHeight);
    }
}
