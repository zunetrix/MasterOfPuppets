using Dalamud.Bindings.ImGui;
using Dalamud.Interface.ImGuiNotification;
using Dalamud.Interface.Utility;

using FFXIVClientStructs.FFXIV.Client.UI.Misc;

using MasterOfPuppets.Extensions.Dalamud;
using MasterOfPuppets.Resources;
using MasterOfPuppets.Util.ImGuiExt;

namespace MasterOfPuppets.Debug;

public sealed class HotbarDebugWidget : Widget {
    public override string Title => "Hotbar";

    public HotbarDebugWidget(WidgetContext ctx) : base(ctx) {
    }

    public override unsafe void Draw() {
        var hotbars = RaptureHotbarModule.Instance()->Hotbars;
        if (hotbars.IsEmpty || hotbars.Length <= 0) {
            DalamudApi.PluginLog.Warning($"Invalid Hotbars");
            return;
        }

        // for (var hotbarIndex = 0; hotbarIndex < hotbars.Length; hotbarIndex++)
        int hotbarIndex = 0;
        foreach (var hotbar in hotbars) {
            if (ImGui.CollapsingHeader($"Hotbar [{hotbarIndex}]")) {
                if (hotbar.Slots.IsEmpty) {
                    DalamudApi.PluginLog.Warning($"hotbar.Slots.IsEmpty");
                    return;
                }

                var tableFlags = ImGuiTableFlags.RowBg | ImGuiTableFlags.PadOuterX |
                    ImGuiTableFlags.NoSavedSettings | ImGuiTableFlags.BordersInnerV;
                var tableColumnCount = 5;

                if (ImGui.BeginTable($"##HotbarTable_{hotbarIndex}", tableColumnCount, tableFlags)) {
                    ImGui.TableSetupColumn("  ", ImGuiTableColumnFlags.WidthFixed);
                    ImGui.TableSetupColumn("Type", ImGuiTableColumnFlags.WidthFixed);
                    ImGui.TableSetupColumn("ID", ImGuiTableColumnFlags.WidthFixed);
                    ImGui.TableSetupColumn("Icon", ImGuiTableColumnFlags.WidthFixed);
                    ImGui.TableSetupColumn("Name", ImGuiTableColumnFlags.WidthStretch);

                    // hotbar.GetHotbarSlot(slotIndex);

                    for (var slotIndex = 0; slotIndex < hotbar.Slots.Length; slotIndex++) {
                        // if (hotbar.Slots[slotIndex].IsEmpty) return;
                        var slot = hotbar.Slots[slotIndex];

                        ImGui.PushID(slotIndex);
                        ImGui.TableNextRow();
                        ImGui.TableNextColumn();
                        ImGui.Text($"{slotIndex + 1:000}");

                        ImGui.TableNextColumn();
                        ImGui.Text($"{slot.CommandType} - ({slot.ApparentSlotType})");

                        ImGui.TableNextColumn();
                        ImGui.Text($"{slot.CommandId} - ({slot.ApparentActionId})");
                        if (ImGui.IsItemClicked()) {
                            ImGui.SetClipboardText($"{slot.ApparentActionId}");
                            DalamudApi.ShowNotification(Language.ClipboardCopyMessage, NotificationType.Info, 5000);
                        }
                        ImGuiUtil.ToolTip(Language.ClickToCopy);

                        ImGui.TableNextColumn();
                        ImGui.Text($"{slot.IconId}");
                        DalamudApi.TextureProvider.DrawIcon(slot.IconId, ImGuiHelpers.ScaledVector2(30, 30));
                        if (ImGui.IsItemClicked()) {
                            HotbarManager.ExecuteHotbarActionByIndex((uint)hotbarIndex, (uint)slotIndex);
                        }
                        ImGuiUtil.ToolTip(Language.ClickToExecute);

                        ImGui.TableNextColumn();
                        ImGui.Text($"{slot.GetDisplayNameForSlot(slot.ApparentSlotType, slot.ApparentActionId)}");

                        ImGui.PopID();
                    }
                    ImGui.EndTable();
                }
            }

            hotbarIndex++;
        }
    }
}

