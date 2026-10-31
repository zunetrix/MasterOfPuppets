using System;
using System.Numerics;

using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;

using MasterOfPuppets.Resources;
using MasterOfPuppets.Util.ImGuiExt;

namespace MasterOfPuppets;

public class InterfaceSettingsWidget : Widget {
    public override string Title => "Interface";
    public override FontAwesomeIcon Icon => FontAwesomeIcon.Desktop;

    public InterfaceSettingsWidget(WidgetContext ctx) : base(ctx) { }

    public override void Draw() {
        var Plugin = Context.Plugin;

        using (ImGuiGroupPanel.BeginGroupPanel("Main Window Menu Options")) {
            ImGui.TextDisabled("Drag items to reorder the sidebar menu. Check or uncheck to display or hide options.");
            ImGui.Spacing();

            if (ImGuiUtil.ButtonStyled("Reset to Default Order##ResetMainMenu", ImGuiUtil.ButtonStyle.Default)) {
                Plugin.Config.ResetMainMenuItems();
                Plugin.IpcProvider.SyncConfiguration();
            }

            ImGui.Spacing();

            var items = Plugin.Config.MainMenuItems;
            var tableFlags = ImGuiTableFlags.RowBg | ImGuiTableFlags.BordersInnerV | ImGuiTableFlags.BordersOuterH | ImGuiTableFlags.PadOuterX;

            using var table = ImRaii.Table("##MainMenuCustomizationTable", 4, tableFlags);
            if (table) {
                ImGui.TableSetupColumn("#", ImGuiTableColumnFlags.WidthFixed, 30f * ImGuiHelpers.GlobalScale);
                ImGui.TableSetupColumn("Type", ImGuiTableColumnFlags.WidthFixed, 65f * ImGuiHelpers.GlobalScale);
                ImGui.TableSetupColumn("Category", ImGuiTableColumnFlags.WidthStretch);
                ImGui.TableSetupColumn("Show", ImGuiTableColumnFlags.WidthFixed, 45f * ImGuiHelpers.GlobalScale);
                ImGui.TableHeadersRow();

                for (int i = 0; i < items.Count; i++) {
                    var item = items[i];
                    if (!MainMenuRegistry.TryGetDef(item.Id, out var def)) continue;

                    ImGui.TableNextRow();

                    // Col 0: Order index (#)
                    ImGui.TableNextColumn();
                    ImGui.AlignTextToFramePadding();
                    ImGui.TextDisabled($"{i + 1}");

                    // Col 1: Type (View vs Action)
                    ImGui.TableNextColumn();
                    ImGui.AlignTextToFramePadding();
                    if (def.Kind == MenuItemKind.View) {
                        ImGui.TextColored(new Vector4(0.4f, 0.8f, 1f, 1f), "Panel");
                        ImGuiUtil.ToolTip("Embedded Main Window Panel");
                    } else {
                        ImGui.TextColored(new Vector4(1f, 0.75f, 0.3f, 1f), "Action");
                        ImGuiUtil.ToolTip("Executes an action on click (e.g. opens a window)");
                    }

                    // Col 2: Category (Icon + Label) with Selectable & DND
                    ImGui.TableNextColumn();
                    ImGui.AlignTextToFramePadding();

                    ImGui.Selectable($"{def.Label}##MenuSelectable_{item.Id}", false, ImGuiSelectableFlags.None);
                    ImGuiUtil.ToolTip("Drag to reorder");

                    if (ImGui.BeginDragDropSource()) {
                        unsafe {
                            int draggedIdx = i;
                            ImGui.SetDragDropPayload("DND_MAIN_MENU_ITEM", new ReadOnlySpan<byte>(&draggedIdx, sizeof(int)), ImGuiCond.None);
                            ImGui.Button($"({i + 1}) {def.Label}");
                        }
                        ImGui.EndDragDropSource();
                    }

                    using (ImRaii.PushColor(ImGuiCol.DragDropTarget, Style.Components.DragDropTarget)) {
                        if (ImGui.BeginDragDropTarget()) {
                            var dragDropPayload = ImGui.AcceptDragDropPayload("DND_MAIN_MENU_ITEM");
                            bool isDropping = false;
                            unsafe { isDropping = !dragDropPayload.IsNull; }
                            if (isDropping && dragDropPayload.IsDelivery()) {
                                unsafe {
                                    int originalIndex = *(int*)dragDropPayload.Data;
                                    if (originalIndex != i && originalIndex >= 0 && originalIndex < items.Count) {
                                        Plugin.Config.MoveMainMenuItemToIndex(originalIndex, i);
                                        Plugin.IpcProvider.SyncConfiguration();
                                    }
                                }
                            }
                            ImGui.EndDragDropTarget();
                        }
                    }

                    // Col 3: Show checkbox
                    ImGui.TableNextColumn();
                    bool enabled = item.Enabled;
                    if (ImGui.Checkbox($"##Show_{item.Id}", ref enabled)) {
                        item.Enabled = enabled;
                        Plugin.Config.Save();
                        Plugin.IpcProvider.SyncConfiguration();
                    }
                }
            }
        }

        ImGui.Spacing();
        ImGui.Spacing();

        using (ImGuiGroupPanel.BeginGroupPanel("Window Behavior")) {
            var allowCloseWithEscape = Plugin.Config.AllowCloseWithEscape;
            if (ImGui.Checkbox(Language.SettingsWindowAllowCloseWithEscape, ref allowCloseWithEscape)) {
                Plugin.Config.AllowCloseWithEscape = allowCloseWithEscape;
                Plugin.IpcProvider.SyncConfiguration();
                Plugin.Ui.MainWindow.UpdateWindowConfig();
            }

            // var allowMovement = Plugin.Config.AllowMovement;
            // if (ImGui.Checkbox("Allow Window Movement", ref allowMovement)) {
            //     Plugin.Config.AllowMovement = allowMovement;
            //     Plugin.IpcProvider.SyncConfiguration();
            // }

            // var allowResize = Plugin.Config.AllowResize;
            // if (ImGui.Checkbox("Allow Window Resize", ref allowResize)) {
            //     Plugin.Config.AllowResize = allowResize;
            //     Plugin.IpcProvider.SyncConfiguration();
            // }

            var showSettingsButton = Plugin.Config.ShowSettingsButton;
            if (ImGui.Checkbox("Show Settings Title Bar Button", ref showSettingsButton)) {
                Plugin.Config.ShowSettingsButton = showSettingsButton;
                Plugin.IpcProvider.SyncConfiguration();
                Plugin.Ui.MainWindow.UpdateWindowConfig();
            }
        }
    }
}
