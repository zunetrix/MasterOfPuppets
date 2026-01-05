using Dalamud.Bindings.ImGui;
using Dalamud.Interface.ImGuiNotification;
using Dalamud.Interface.Utility;

using FFXIVClientStructs.FFXIV.Client.UI.Misc;

using MasterOfPuppets.Extensions.Dalamud;
using MasterOfPuppets.Resources;
using MasterOfPuppets.Util.ImGuiExt;

namespace MasterOfPuppets.Debug;

public sealed class HotbarPetDebugWidget : Widget {
    public override string Title => "Hotbar Pet";

    public HotbarPetDebugWidget(WidgetContext ctx) : base(ctx) {
    }

    public unsafe override void Draw() {

        if (RaptureHotbarModule.Instance()->PetHotbar.Slots.IsEmpty) {
            DalamudApi.PluginLog.Warning($"petHotbar.Slots");
            return;
        }

        if (ImGui.CollapsingHeader($"Pet Hotbar")) {
            var tableFlags = ImGuiTableFlags.RowBg | ImGuiTableFlags.PadOuterX |
                ImGuiTableFlags.NoSavedSettings | ImGuiTableFlags.BordersInnerV;
            var tableColumnCount = 5;

            if (ImGui.BeginTable($"##PetHotbarTable", tableColumnCount, tableFlags)) {
                ImGui.TableSetupColumn("  ", ImGuiTableColumnFlags.WidthFixed);
                ImGui.TableSetupColumn("Type", ImGuiTableColumnFlags.WidthFixed);
                ImGui.TableSetupColumn("ID", ImGuiTableColumnFlags.WidthFixed);
                ImGui.TableSetupColumn("Icon", ImGuiTableColumnFlags.WidthFixed);
                ImGui.TableSetupColumn("Name", ImGuiTableColumnFlags.WidthStretch);

                for (var slotIndex = 0; slotIndex < RaptureHotbarModule.Instance()->PetHotbar.Slots.Length; slotIndex++) {
                    var slot = RaptureHotbarModule.Instance()->PetHotbar.Slots[slotIndex];

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
                        HotbarManager.ExecutePetHotbarActionByIndex((uint)slotIndex);
                    }
                    ImGuiUtil.ToolTip(Language.ClickToExecute);

                    ImGui.TableNextColumn();
                    ImGui.Text($"{slot.GetDisplayNameForSlot(slot.ApparentSlotType, slot.ApparentActionId)}");

                    ImGui.PopID();
                }
                ImGui.EndTable();
            }
        }


    }
}

