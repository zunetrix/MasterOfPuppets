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

public class FashionAccessoriesFragment : Fragment {
    public override string Title => "Fashion Accessories";
    public override FontAwesomeIcon Icon => FontAwesomeIcon.Umbrella;

    private readonly List<ExecutableAction> UnlockedActions = new();
    private string _searchString = string.Empty;
    private readonly List<int> ListSearchedIndexes = new();

    public FashionAccessoriesFragment(FragmentContext ctx) : base(ctx) {
    }

    public override void OnShow() {
        UnlockedActions.Clear();
        UnlockedActions.AddRange(FashionAccessoriesHelper.GetAllowedItems());
        base.OnShow();
    }

    public override void OnHide() {
        ListSearchedIndexes.Clear();
        _searchString = string.Empty;
        base.OnHide();
    }

    public override void Draw() {
        ImGui.BeginGroup();
        DrawHeader();
        ImGui.EndGroup();

        ImGui.BeginChild("##FashionListScrollableContent", new Vector2(-1, 0), false, ImGuiWindowFlags.NoScrollbar);
        DrawFashionGird();
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
        DalamudApi.TextureProvider.DrawIcon(putAway.IconId, iconSize);
        if (ImGui.IsItemClicked()) {
            Context.Plugin.IpcProvider.ExecuteTextCommand(putAway.TextCommand);
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
                Context.Plugin.IpcProvider.ExecuteTextCommand(fashionAccessorie.TextCommand);
            }
        }, columns, lineHeight);
    }
}
