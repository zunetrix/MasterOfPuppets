using Dalamud.Bindings.ImGui;
using Dalamud.Interface.ImGuiNotification;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;

using FFXIVClientStructs.FFXIV.Client.UI.Misc;

using Lumina.Excel.Sheets;

using MasterOfPuppets.Extensions.Dalamud;
using MasterOfPuppets.Resources;
using MasterOfPuppets.Util.ImGuiExt;

namespace MasterOfPuppets.Debug;

public sealed class HotbarDebugWidget : Widget {
    public override string Title => "Hotbar";

    public HotbarDebugWidget(WidgetContext ctx) : base(ctx) {
    }

    public override unsafe void Draw() {
        var raptureHotbarModule = RaptureHotbarModule.Instance();

        for (var i = 0; i < raptureHotbarModule->Hotbars.Length; i++) {
            var hotbar = raptureHotbarModule->Hotbars[i];

            using var titleColor = ImRaii.PushColor(ImGuiCol.Text, Style.Colors.Yellow);
            using var node = ImRaii.TreeNode($"##Hotbar{i}", ImGuiTreeNodeFlags.SpanAvailWidth);

            ImGui.SameLine(ImGui.GetStyle().FramePadding.X * 3f + ImGui.GetFontSize(), 0);
            ImGui.Text($"Hotbar {i}");
            ImGui.SameLine(0, ImGui.GetStyle().FramePadding.X * 3);

            for (var j = 0; j < hotbar.Slots.Length; j++) {
                var slot = hotbar.Slots[j];
                DrawHotbarSlotIcon(slot);
                ImGui.SameLine(0, ImGui.GetStyle().ItemInnerSpacing.X);
            }
            ImGui.NewLine();

            if (!node)
                continue;
            titleColor?.Dispose();

            using var table = ImRaii.Table("RaptureHotbarModuleTable"u8, 6, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.NoSavedSettings);
            if (!table) return;

            ImGui.TableSetupColumn("Slot", ImGuiTableColumnFlags.WidthFixed, 50);
            ImGui.TableSetupColumn("CommandType", ImGuiTableColumnFlags.WidthFixed, 100);
            ImGui.TableSetupColumn("CommandId", ImGuiTableColumnFlags.WidthFixed, 100);
            ImGui.TableSetupColumn("Icon", ImGuiTableColumnFlags.WidthFixed, 50);
            ImGui.TableSetupColumn("Name", ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableSetupColumn("Execute", ImGuiTableColumnFlags.WidthFixed, 100);
            ImGui.TableSetupScrollFreeze(6, 1);
            ImGui.TableHeadersRow();

            for (var j = 0; j < hotbar.Slots.Length; j++) {
                var slot = raptureHotbarModule->GetSlotById((uint)i, (uint)j);

                ImGui.TableNextRow();
                ImGui.TableNextColumn(); // Slot
                ImGui.Text(j.ToString());

                ImGui.TableNextColumn(); // CommandType
                ImGui.Text(slot->CommandType.ToString());

                ImGui.TableNextColumn(); // CommandId
                ImGui.Text(slot->CommandId.ToString());

                ImGui.TableNextColumn(); // Icon
                if (!slot->IsEmpty) {
                    DalamudApi.TextureProvider.DrawIcon(slot->IconId, ImGuiHelpers.ScaledVector2(30, 30));
                    if (ImGui.IsItemClicked()) {
                        ImGui.SetClipboardText($"{slot->IconId}");
                        DalamudApi.ShowNotification(Language.ClipboardCopyMessage, NotificationType.Info, 5000);
                    }
                    ImGuiUtil.ToolTip($"{Language.ClickToCopy}: {slot->IconId}");
                }

                ImGui.TableNextColumn(); // Name
                if (!slot->IsEmpty) {
                    ImGui.Text(GetSlotText(slot));
                }

                ImGui.TableNextColumn(); // Execute
                if (!slot->IsEmpty && ImGui.SmallButton($"Execute##H{i}S{j}Execute")) {
                    raptureHotbarModule->ExecuteSlot(slot);
                }
            }
        }
    }

    private unsafe string GetSlotText(RaptureHotbarModule.HotbarSlot* slot) {
        return slot->CommandType switch {
            RaptureHotbarModule.HotbarSlotType.Action => TextService.GetActionName(slot->CommandId),
            RaptureHotbarModule.HotbarSlotType.Item => TextService.GetItemName(slot->CommandId).ToString(),
            RaptureHotbarModule.HotbarSlotType.EventItem => TextService.GetItemName(slot->CommandId).ToString(),
            RaptureHotbarModule.HotbarSlotType.Emote => TextService.GetEmoteName(slot->CommandId),
            RaptureHotbarModule.HotbarSlotType.Macro => GetMacroName(slot->CommandId),
            RaptureHotbarModule.HotbarSlotType.Marker => ExcelService.TryGetRow<Marker>(slot->CommandId, out var marker) ? marker.Name.ToString() : $"Marker#{slot->CommandId}",
            RaptureHotbarModule.HotbarSlotType.CraftAction => TextService.GetCraftActionName(slot->CommandId),
            RaptureHotbarModule.HotbarSlotType.GeneralAction => TextService.GetGeneralActionName(slot->CommandId),
            RaptureHotbarModule.HotbarSlotType.BuddyAction => TextService.GetBuddyActionName(slot->CommandId),
            RaptureHotbarModule.HotbarSlotType.MainCommand => TextService.GetMainCommandName(slot->CommandId),
            RaptureHotbarModule.HotbarSlotType.Companion => TextService.GetCompanionName(slot->CommandId),
            RaptureHotbarModule.HotbarSlotType.GearSet => GetGearsetName((int)slot->CommandId),
            RaptureHotbarModule.HotbarSlotType.PetAction => TextService.GetPetActionName(slot->CommandId),
            RaptureHotbarModule.HotbarSlotType.Mount => TextService.GetMountName(slot->CommandId),
            RaptureHotbarModule.HotbarSlotType.FieldMarker => ExcelService.TryGetRow<FieldMarker>(slot->CommandId, out var fieldMarker) ? fieldMarker.Name.ToString() : $"FieldMarker#{slot->CommandId}",
            RaptureHotbarModule.HotbarSlotType.Recipe => GetRecipeName(slot),
            RaptureHotbarModule.HotbarSlotType.ChocoboRaceAbility => ExcelService.TryGetRow<ChocoboRaceAbility>(slot->CommandId, out var chocoboRaceAbility) ? chocoboRaceAbility.Name.ToString() : $"ChocoboRaceAbility#{slot->CommandId}",
            RaptureHotbarModule.HotbarSlotType.ChocoboRaceItem => ExcelService.TryGetRow<ChocoboRaceItem>(slot->CommandId, out var chocoboRaceItem) ? chocoboRaceItem.Name.ToString() : $"ChocoboRaceItem#{slot->CommandId}",
            RaptureHotbarModule.HotbarSlotType.ExtraCommand => ExcelService.TryGetRow<ExtraCommand>(slot->CommandId, out var extraCommand) ? extraCommand.Name.ToString() : $"ExtraCommand#{slot->CommandId}",
            RaptureHotbarModule.HotbarSlotType.PvPQuickChat => ExcelService.TryGetRow<QuickChat>(slot->CommandId, out var quickChat) ? quickChat.NameAction.ToString() : $"QuickChat#{slot->CommandId}",
            RaptureHotbarModule.HotbarSlotType.PvPCombo => ExcelService.TryGetRow<ActionComboRoute>(slot->CommandId, out var actionComboRoute) ? actionComboRoute.Name.ToString() : $"ActionComboRoute#{slot->CommandId}",
            RaptureHotbarModule.HotbarSlotType.BgcArmyAction => ExcelService.TryGetRow<BgcArmyAction>(slot->CommandId, out var bgcArmyAction) ? bgcArmyAction.Name.ToString() : $"BgcArmyAction#{slot->CommandId}",
            RaptureHotbarModule.HotbarSlotType.PerformanceInstrument => ExcelService.TryGetRow<Perform>(slot->CommandId, out var perform) ? perform.Instrument.ToString() : $"Perform#{slot->CommandId}",
            RaptureHotbarModule.HotbarSlotType.McGuffin => ExcelService.TryGetRow<McGuffinUIData>(ExcelService.TryGetRow<McGuffin>(slot->CommandId, out var mcGuffin) ? mcGuffin.UIData.RowId : 0, out var mcGuffinUIData) ? mcGuffinUIData.Name.ToString() : $"McGuffin#{slot->CommandId}",
            RaptureHotbarModule.HotbarSlotType.Ornament => TextService.GetOrnamentName(slot->CommandId),
            // LostFindsItem
            RaptureHotbarModule.HotbarSlotType.Glasses => TextService.GetGlassesName(slot->CommandId),
            RaptureHotbarModule.HotbarSlotType.PhantomAction => TextService.GetAddonText(slot->CommandId switch { 1 => 16296, 2 => 16298, _ => 0 }),
            RaptureHotbarModule.HotbarSlotType.QuickPanel => DalamudApi.SeStringEvaluator.EvaluateFromAddon(17215, [slot->CommandId + 1]).ToString(),
            _ => string.Empty
        };
    }

    private unsafe string GetGearsetName(int gearsetId) {
        var gearset = RaptureGearsetModule.Instance()->GetGearset(gearsetId);
        return gearset == null || !gearset->Flags.HasFlag(RaptureGearsetModule.GearsetFlag.Exists) ? string.Empty : gearset->NameString;
    }

    private unsafe string GetMacroName(uint macroId) {
        var set = macroId >= 256 ? 1u : 0;
        var idx = macroId >= 256 ? macroId - 256 : macroId;

        var macro = RaptureMacroModule.Instance()->GetMacro(set, idx);
        return macro == null || !macro->IsNotEmpty() ? string.Empty : macro->Name.ToString();
    }

    private unsafe string GetRecipeName(RaptureHotbarModule.HotbarSlot* slot) {
        if (slot->RecipeValid == 0)
            return TextService.GetAddonText(1449); // Deleted Recipes

        return DalamudApi.SeStringEvaluator.EvaluateFromAddon(1442, [slot->RecipeItemId, slot->RecipeCraftType + 8]).ToString();
    }

    private void DrawHotbarSlotIcon(RaptureHotbarModule.HotbarSlot slot) {
        DalamudApi.TextureProvider.DrawIcon(slot.IconId, ImGuiHelpers.ScaledVector2(ImGui.GetTextLineHeight()));
    }

    // public unsafe void Draw2() {
    //     var hotbars = RaptureHotbarModule.Instance()->Hotbars;
    //     if (hotbars.IsEmpty || hotbars.Length <= 0) {
    //         DalamudApi.PluginLog.Warning($"Invalid Hotbars");
    //         return;
    //     }

    //     // for (var hotbarIndex = 0; hotbarIndex < hotbars.Length; hotbarIndex++)
    //     int hotbarIndex = 0;
    //     foreach (var hotbar in hotbars) {
    //         if (ImGui.CollapsingHeader($"Hotbar [{hotbarIndex}]")) {
    //             if (hotbar.Slots.IsEmpty) {
    //                 DalamudApi.PluginLog.Warning($"hotbar.Slots.IsEmpty");
    //                 return;
    //             }

    //             var tableFlags = ImGuiTableFlags.RowBg | ImGuiTableFlags.PadOuterX |
    //                 ImGuiTableFlags.NoSavedSettings | ImGuiTableFlags.BordersInnerV;
    //             var tableColumnCount = 5;

    //             if (ImGui.BeginTable($"##HotbarTable_{hotbarIndex}", tableColumnCount, tableFlags)) {
    //                 ImGui.TableSetupColumn("  ", ImGuiTableColumnFlags.WidthFixed);
    //                 ImGui.TableSetupColumn("Type", ImGuiTableColumnFlags.WidthFixed);
    //                 ImGui.TableSetupColumn("ID", ImGuiTableColumnFlags.WidthFixed);
    //                 ImGui.TableSetupColumn("Icon", ImGuiTableColumnFlags.WidthFixed);
    //                 ImGui.TableSetupColumn("Name", ImGuiTableColumnFlags.WidthStretch);

    //                 // hotbar.GetHotbarSlot(slotIndex);

    //                 for (var slotIndex = 0; slotIndex < hotbar.Slots.Length; slotIndex++) {
    //                     // if (hotbar.Slots[slotIndex].IsEmpty) return;
    //                     var slot = hotbar.Slots[slotIndex];

    //                     ImGui.PushID(slotIndex);
    //                     ImGui.TableNextRow();
    //                     ImGui.TableNextColumn();
    //                     ImGui.Text($"{slotIndex + 1:000}");

    //                     ImGui.TableNextColumn();
    //                     ImGui.Text($"{slot.CommandType} - ({slot.ApparentSlotType})");

    //                     ImGui.TableNextColumn();
    //                     ImGui.Text($"{slot.CommandId} - ({slot.ApparentActionId})");
    //                     if (ImGui.IsItemClicked()) {
    //                         ImGui.SetClipboardText($"{slot.ApparentActionId}");
    //                         DalamudApi.ShowNotification(Language.ClipboardCopyMessage, NotificationType.Info, 5000);
    //                     }
    //                     ImGuiUtil.ToolTip(Language.ClickToCopy);

    //                     ImGui.TableNextColumn();
    //                     ImGui.Text($"{slot.IconId}");
    //                     DalamudApi.TextureProvider.DrawIcon(slot.IconId, ImGuiHelpers.ScaledVector2(30, 30));
    //                     if (ImGui.IsItemClicked()) {
    //                         HotbarManager.ExecuteHotbarActionByIndex((uint)hotbarIndex, (uint)slotIndex);
    //                     }
    //                     ImGuiUtil.ToolTip(Language.ClickToExecute);

    //                     ImGui.TableNextColumn();
    //                     ImGui.Text($"{slot.GetDisplayNameForSlot(slot.ApparentSlotType, slot.ApparentActionId)}");

    //                     ImGui.PopID();
    //                 }
    //                 ImGui.EndTable();
    //             }
    //         }

    //         hotbarIndex++;
    //     }
    // }
}

