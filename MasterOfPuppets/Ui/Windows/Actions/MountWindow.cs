using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

using Dalamud.Bindings.ImGui;
using Dalamud.Interface.ImGuiNotification;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Windowing;

using MasterOfPuppets.Resources;
using MasterOfPuppets.Util.ImGuiExt;

namespace MasterOfPuppets;

public class MountWindow : Window {
    private Plugin Plugin { get; }

    private readonly List<ExecutableAction> UnlockedActions = new();
    private string _searchString = string.Empty;
    private readonly List<int> ListSearchedIndexes = new();

    public MountWindow(Plugin plugin) : base($"{Language.MountTitle}###MountWindow") {
        Plugin = plugin;

        Size = ImGuiHelpers.ScaledVector2(500, 450);
        SizeCondition = ImGuiCond.FirstUseEver;
        // SizeCondition = ImGuiCond.Always;
        // Flags = ImGuiWindowFlags.NoResize;
    }

    public override void OnOpen() {
        UnlockedActions.Clear();
        UnlockedActions.AddRange(MountHelper.GetAllowedItems());
        base.OnOpen();
    }

    public override void OnClose() {
        ListSearchedIndexes.Clear();
        _searchString = string.Empty;
        base.OnClose();
    }

    public override void Draw() {
        ImGui.BeginChild("##MountHeaderFixedHeight", new Vector2(-1, 55 * ImGuiHelpers.GlobalScale), false, ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse);
        DrawHeader();
        ImGui.EndChild();

        ImGui.BeginChild("##MountListScrollableContent", new Vector2(-1, 0), false, ImGuiWindowFlags.HorizontalScrollbar);
        DrawMountTable();
        ImGui.EndChild();
    }

    private void DrawMountEntry(int actionIndex, ExecutableAction mount) {
        ImGui.PushID(actionIndex);
        ImGui.TableNextRow();
        ImGui.TableNextColumn();
        ImGui.TextUnformatted($"{actionIndex + 1:000}");

        ImGui.TableNextColumn();
        var icon = DalamudApi.TextureProvider.GetFromGameIcon(mount.IconId).GetWrapOrEmpty().Handle;
        var iconSize = ImGuiHelpers.ScaledVector2(50, 50);

        ImGui.Image(icon, iconSize);
        if (ImGui.IsItemClicked()) {
            Plugin.IpcProvider.ExecuteTextCommand(mount.TextCommand);
        }
        ImGuiUtil.ToolTip(Language.ClickToExecute);

        ImGui.TableNextColumn();
        ImGui.TextUnformatted($"{mount.ActionName}");
        if (ImGui.IsItemClicked()) {
            ImGui.SetClipboardText($"{mount.ActionName}");
            DalamudApi.ShowNotification(Language.ClipboardCopyMessage, NotificationType.Info, 5000);
        }
        ImGuiUtil.ToolTip(Language.ClickToCopy);

        ImGui.TableNextColumn();
        ImGui.TextUnformatted(mount.TextCommand);
        if (ImGui.IsItemClicked()) {
            ImGui.SetClipboardText(mount.TextCommand);
            DalamudApi.ShowNotification(Language.ClipboardCopyMessage, NotificationType.Info, 5000);
        }
        ImGuiUtil.ToolTip(Language.ClickToCopy);

        ImGui.PopID();
    }

    private unsafe void DrawMountTable() {
        var tableFlags = ImGuiTableFlags.RowBg | ImGuiTableFlags.PadOuterX |
               ImGuiTableFlags.NoSavedSettings | ImGuiTableFlags.BordersInnerV;
        var tableColumnCount = 4;

        var isFiltered = !string.IsNullOrEmpty(_searchString);
        var itemCount = isFiltered ? ListSearchedIndexes.Count : UnlockedActions.Count;

        if (ImGui.BeginTable("##MountTable", tableColumnCount, tableFlags)) {
            ImGui.TableSetupColumn("  ", ImGuiTableColumnFlags.WidthFixed);
            ImGui.TableSetupColumn("Icon", ImGuiTableColumnFlags.WidthFixed);
            ImGui.TableSetupColumn("Name", ImGuiTableColumnFlags.WidthStretch, 1.0f);
            ImGui.TableSetupColumn("Text Commands", ImGuiTableColumnFlags.WidthStretch);

            ImGuiListClipperPtr clipper;
            unsafe {
                clipper = new ImGuiListClipperPtr(ImGuiNative.ImGuiListClipper());
            }

            clipper.Begin(itemCount);

            while (clipper.Step()) {
                for (int i = clipper.DisplayStart; i < clipper.DisplayEnd; i++) {
                    if (i >= itemCount) break;
                    int realIndex = isFiltered ? ListSearchedIndexes[i] : i;
                    if (realIndex >= UnlockedActions.Count) continue;

                    DrawMountEntry(realIndex, UnlockedActions[realIndex]);
                }
            }

            clipper.End();
            ImGui.EndTable();
        }
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

    private void DrawHeader() {
        ImGui.TextUnformatted($"{Language.MountTitle} (unlocked)");
        ImGui.SameLine();
        ImGuiUtil.HelpMarker("""
        Click on icon to execute
        Click on command to copy
        """);

        ImGui.Spacing();

        if (ImGui.InputTextWithHint("##MountSearchInput", Language.SearchInputLabel, ref _searchString, 255, ImGuiInputTextFlags.AutoSelectAll)) {
            Search();
        }

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();
    }
}
