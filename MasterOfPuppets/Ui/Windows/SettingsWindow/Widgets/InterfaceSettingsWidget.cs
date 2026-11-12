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

        if (ImGui.CollapsingHeader("Main Window Menu Options##MainWindowMenuOptions", ImGuiTreeNodeFlags.DefaultOpen)) {
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

        ImGui.Spacing();
        ImGui.Spacing();

        using (ImGuiGroupPanel.BeginGroupPanel("Aetheryte Overlay")) {
            var showOverlay = Plugin.Config.ShowAetheryteOverlay;
            if (ImGui.Checkbox("Show overlay when standing near an aetheryte", ref showOverlay)) {
                Plugin.Config.ShowAetheryteOverlay = showOverlay;
                Plugin.IpcProvider.SyncConfiguration();
            }

            ImGui.SameLine();
            if (ImGuiUtil.IconButton(FontAwesomeIcon.Users, $"##ShowCharactersBtn", Language.ShowCharactersBtn)) {
                Plugin.Ui.CharactersWindow.Toggle();
            }
            ImGuiUtil.ToolTip("Global toggle. Each character can also be enabled/disabled in the Characters settings");


            using (ImRaii.Disabled(!showOverlay)) {
                ImGui.Indent();

                var showAethernet = Plugin.Config.AetheryteOverlayShowAethernet;
                if (ImGui.Checkbox("Aethernet##OverlayAethernet", ref showAethernet)) {
                    Plugin.Config.AetheryteOverlayShowAethernet = showAethernet;
                    Plugin.IpcProvider.SyncConfiguration();
                }

                using (ImRaii.Disabled(!showAethernet)) {
                    ImGui.Indent();
                    var nativeMap = Plugin.Config.AetheryteOverlayNativeMapPreview;
                    if (ImGui.Checkbox("Show destination on native aethernet map##OverlayNativeMap", ref nativeMap)) {
                        Plugin.Config.AetheryteOverlayNativeMapPreview = nativeMap;
                        Plugin.IpcProvider.SyncConfiguration();
                    }
                    ImGuiUtil.ToolTip("Opening the Aethernet submenu interacts with the aetheryte to open the game's aethernet window.\nHovering a destination in the overlay moves the native map to it.");
                    ImGui.Unindent();
                }

                var showResidential = Plugin.Config.AetheryteOverlayShowResidential;
                if (ImGui.Checkbox("Residential Travel##OverlayResidential", ref showResidential)) {
                    Plugin.Config.AetheryteOverlayShowResidential = showResidential;
                    Plugin.IpcProvider.SyncConfiguration();
                }

                var showWorldVisit = Plugin.Config.AetheryteOverlayShowWorldVisit;
                if (ImGui.Checkbox("World Visit##OverlayWorldVisit", ref showWorldVisit)) {
                    Plugin.Config.AetheryteOverlayShowWorldVisit = showWorldVisit;
                    Plugin.IpcProvider.SyncConfiguration();
                }

                ImGui.Spacing();

                var iconsOnly = Plugin.Config.AetheryteOverlayIconsOnly;
                if (ImGui.Checkbox("Show category icons only (no text)##OverlayIconsOnly", ref iconsOnly)) {
                    Plugin.Config.AetheryteOverlayIconsOnly = iconsOnly;
                    Plugin.IpcProvider.SyncConfiguration();
                }

                var dock = (int)Plugin.Config.AetheryteOverlayDock;
                ImGui.SetNextItemWidth(150f * ImGuiHelpers.GlobalScale);
                if (ImGui.Combo("Position##OverlayDock", ref dock, ["Left side", "Right side", "Free (drag)"])) {
                    Plugin.Config.AetheryteOverlayDock = (AetheryteOverlayDock)dock;
                    Plugin.IpcProvider.SyncConfiguration();
                }

                using (ImRaii.Disabled(Plugin.Config.AetheryteOverlayDock == AetheryteOverlayDock.Free)) {
                    var offsetY = Plugin.Config.AetheryteOverlayOffsetY * 100f;
                    ImGui.SetNextItemWidth(150f * ImGuiHelpers.GlobalScale);
                    if (ImGui.SliderFloat("Vertical offset##OverlayOffsetY", ref offsetY, -50f, 50f, "%.0f%%")) {
                        Plugin.Config.AetheryteOverlayOffsetY = offsetY / 100f;
                    }
                    if (ImGui.IsItemDeactivatedAfterEdit())
                        Plugin.IpcProvider.SyncConfiguration();
                }

                ImGui.Unindent();
            }
        }

        ImGui.Spacing();
        ImGui.Spacing();

        using (ImGuiGroupPanel.BeginGroupPanel("Housing Overlay")) {
            var showOverlay = Plugin.Config.ShowHousingOverlay;
            if (ImGui.Checkbox("Show overlay when standing near a house entrance or exit", ref showOverlay)) {
                Plugin.Config.ShowHousingOverlay = showOverlay;
                Plugin.IpcProvider.SyncConfiguration();
            }
            ImGuiUtil.ToolTip("Only in residential districts and house interiors. Uses the same per-character overlay toggle as the aetheryte overlay");

            using (ImRaii.Disabled(!showOverlay)) {
                ImGui.Indent();

                var range = Plugin.Config.HousingOverlayRange;
                ImGui.SetNextItemWidth(150f * ImGuiHelpers.GlobalScale);
                if (ImGui.SliderFloat("Detection range##HousingOverlayRange", ref range, 1f, 6f, "%.0f"))
                    Plugin.Config.HousingOverlayRange = range;
                if (ImGui.IsItemDeactivatedAfterEdit())
                    Plugin.IpcProvider.SyncConfiguration();
                ImGuiUtil.ToolTip("Distance from the door at which the overlay appears. The action walks into interact range by itself");

                var anywhereInside = Plugin.Config.HousingOverlayShowAnywhereInside;
                if (ImGui.Checkbox("Show anywhere inside a house (Exit / Front Door)##HousingOverlayInside", ref anywhereInside)) {
                    Plugin.Config.HousingOverlayShowAnywhereInside = anywhereInside;
                    Plugin.IpcProvider.SyncConfiguration();
                }

                ImGui.Spacing();

                var iconsOnly = Plugin.Config.HousingOverlayIconsOnly;
                if (ImGui.Checkbox("Show icons only (no text)##HousingOverlayIconsOnly", ref iconsOnly)) {
                    Plugin.Config.HousingOverlayIconsOnly = iconsOnly;
                    Plugin.IpcProvider.SyncConfiguration();
                }

                var dock = (int)Plugin.Config.HousingOverlayDock;
                ImGui.SetNextItemWidth(150f * ImGuiHelpers.GlobalScale);
                if (ImGui.Combo("Position##HousingOverlayDock", ref dock, ["Left side", "Right side", "Free (drag)"])) {
                    Plugin.Config.HousingOverlayDock = (AetheryteOverlayDock)dock;
                    Plugin.IpcProvider.SyncConfiguration();
                }

                using (ImRaii.Disabled(Plugin.Config.HousingOverlayDock == AetheryteOverlayDock.Free)) {
                    var offsetY = Plugin.Config.HousingOverlayOffsetY * 100f;
                    ImGui.SetNextItemWidth(150f * ImGuiHelpers.GlobalScale);
                    if (ImGui.SliderFloat("Vertical offset##HousingOverlayOffsetY", ref offsetY, -50f, 50f, "%.0f%%"))
                        Plugin.Config.HousingOverlayOffsetY = offsetY / 100f;
                    if (ImGui.IsItemDeactivatedAfterEdit())
                        Plugin.IpcProvider.SyncConfiguration();
                }

                ImGui.Unindent();
            }
        }
    }
}
